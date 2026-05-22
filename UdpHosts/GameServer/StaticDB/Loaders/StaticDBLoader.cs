namespace GameServer.Data.SDB;

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using FauFau.Formats;
using Records.apt;
using Records.aptfs;
using Records.apttf;
using Records.dbcharacter;
using Records.dbencounterdata;
using Records.dbitems;
using Records.dbphysicsmaterials;
using Records.dbvisualrecords;
using Records.dbzonemetadata;
using Records.vcs;
using Shared.Common;
using static FauFau.Formats.StaticDB;

public class StaticDBLoader : ISDBLoader
{
    private static readonly SnakeCasePropertyNamingPolicy Policy = new SnakeCasePropertyNamingPolicy();
    private static readonly Dictionary<string, string> ManualNameConversions = new()
    {
        { "DamageType", "damageType" }, // InflictDamageCommandDef and HealDamageCommandDef
        { "OrnamentsMapGroupId1", "ornaments_map_group_id_1" }, // dbitems::Weapons
        { "OrnamentsMapGroupId2", "ornaments_map_group_id_2" }, // dbitems::Weapons
        { "FlightFx1stPersonId", "flight_fx_1st_person_id" }, // dbitems::Ammo
    };

    // Chain-critical tables: for these, duplicate handling is strict and logged
    private static readonly HashSet<string> ChainCriticalTables = new()
    {
        "apt::BaseCommandDef",        // Backbone of all ability chains (Next ptr linked list)
        "apt::AbilityData",           // Ability entry point (Chain field)
        "apt::StatusEffectData",      // Status effects (ApplyChain, RemoveChain, UpdateChain, DurationChain)
        "apt::ConditionalBranchCommandDef",      // Branching (IfChain, ThenChain, ElseChain)
        "apt::WhileLoopCommandDef",               // Looping (BodyChain, ConditionChain)
        "apt::LogicOrChainCommandDef",            // Logic (OrChain)
        "apt::LogicOrCommandDef",                 // Logic (AChain, BChain)
        "apt::LogicAndChainCommandDef",           // Logic (AndChain)
        "apt::LogicNegateCommandDef",             // Logic (NegateChain)
        "apt::ImpactToggleEffectCommandDef",      // Effect toggle (PreApplyChain)
        "apt::UpdateWaitAndFireOnceCommandDef",   // Fire command (Chain)
        "apt::RegisterClientProximityCommandDef", // Proximity registration (Chain)
        "aptfs::InteractionTypeCommandDef",       // Interaction handling
        "dbitems::AbilityModule",                 // Item-to-ability bridge (AbilityChainId field)
        "dbitems::ItemSetAbilityEntries",         // Item set ability entries (AbilityChainId field)
        "dbitems::RootItem",                      // Root items (affects client loading)
        "dbitems::Battleframe",                   // Battleframes (affects abilities)
        "dbcharacter::CharCreateLoadout",         // Character loadouts
        "dbcharacter::Deployable",                // Deployables (ability references)
    };

    // Per-table duplicate resolution policy: how to handle duplicate keys
    // "Keep" = use first, "Last" = use last, "Skip" = warn and skip
    private static readonly Dictionary<string, string> DuplicatePolicy = new()
    {
        { "apt::BaseCommandDef", "Keep" },         // Backbone: must be deterministic, usually only one real entry
        { "apt::AbilityData", "Keep" },            // Entry point: first is canonical
        { "apt::StatusEffectData", "Keep" },       // Effect data: first is canonical
        { "dbitems::AttributeRange", "Last" },     // Special case: known duplicates, last is observed in-game (see comment in original)
        { "dbitems::RootItem", "Keep" },           // Items: first instance
    };

    private static StaticDB sdb;
    private static List<string> LoadDiagnostics = new();

    public StaticDBLoader(StaticDB instance)
    {
        sdb = instance;
        LoadDiagnostics.Clear();
    }

    /// <summary>
    /// Get accumulated diagnostics from the load session and clear them.
    /// Call this after all loads are complete to retrieve warnings.
    /// </summary>
    /// <returns>List of diagnostic messages from the load session.</returns>
    public List<string> GetAndClearDiagnostics()
    {
        var result = new List<string>(LoadDiagnostics);
        LoadDiagnostics.Clear();
        return result;
    }

    /// <summary>
    /// Resolve duplicates according to table-specific policy. For chain-critical tables,
    /// logs the decision. Non-critical tables silently use First().
    /// </summary>
    /// <typeparam name="TKey">The type of the grouping key.</typeparam>
    /// <typeparam name="T">The type of the record being grouped.</typeparam>
    /// <param name="tableName">The table name for diagnostics.</param>
    /// <param name="group">The group of duplicate records with the same key.</param>
    /// <returns>The selected record from the group.</returns>
    private static T ResolveDuplicate<TKey, T>(
        string tableName,
        IGrouping<TKey, T> group)
        where T : class
    {
        if (group.Count() <= 1)
        {
            return group.First();
        }

        string policy = "Keep"; // Default behavior
        if (DuplicatePolicy.TryGetValue(tableName, out var tablePolicy))
        {
            policy = tablePolicy;
        }

        T selected = policy == "Last" ? group.Last() : group.First();

        if (ChainCriticalTables.Contains(tableName))
        {
            var keyStr = group.Key?.ToString() ?? "null";
            LoadDiagnostics.Add(
                $"[CHAIN-CRITICAL] Table {tableName} key={keyStr}: found {group.Count()} duplicates, keeping {policy.ToLower()} (ID={GetIdField(selected)})");
        }

        return selected;
    }

    /// <summary>
    /// Extract ID field from any record type for logging.
    /// </summary>
    /// <param name="record">The record to extract the ID from.</param>
    /// <returns>The ID field value as a string, or "?" if not found.</returns>
    private static string GetIdField(object record)
    {
        if (record == null)
        {
            return "null";
        }

        var idProp = record.GetType().GetProperty("Id");
        return idProp?.GetValue(record)?.ToString() ?? "?";
    }

    public Dictionary<uint, CharCreateLoadout> LoadCharCreateLoadout()
    {
        return LoadStaticDB<CharCreateLoadout>("dbcharacter::CharCreateLoadout")
            .GroupBy(row => row.Id)
            .ToDictionary(group => group.Key, group => ResolveDuplicate<uint, CharCreateLoadout>("dbcharacter::CharCreateLoadout", group));
    }

    public Dictionary<uint, Dictionary<byte, CharCreateLoadoutSlots>> LoadCharCreateLoadoutSlots()
    {
        return LoadStaticDB<CharCreateLoadoutSlots>("dbcharacter::CharCreateLoadoutSlots")
        .GroupBy(row => row.LoadoutId)
        .ToDictionary(group => group.Key, group => group.ToDictionary(row => row.SlotType, row => row));
    }

    public Dictionary<uint, Deployable> LoadDeployable()
    {
        return LoadStaticDB<Deployable>("dbcharacter::Deployable")
            .GroupBy(row => row.Id)
            .ToDictionary(group => group.Key, group => ResolveDuplicate<uint, Deployable>("dbcharacter::Deployable", group));
    }

    public Dictionary<uint, DeployableFunction> LoadDeployableFunction()
    {
        return LoadStaticDB<DeployableFunction>("dbcharacter::DeployableFunction")
            .GroupBy(row => row.Id)
            .ToDictionary(group => group.Key, group => group.First());
    }

    public Dictionary<uint, DeployableCategory> LoadDeployableCategory()
    {
        return LoadStaticDB<DeployableCategory>("dbcharacter::DeployableCategory")
            .GroupBy(row => row.Id)
            .ToDictionary(group => group.Key, group => group.First());
    }

    public Dictionary<uint, Faction> LoadFaction()
    {
        return LoadStaticDB<Faction>("dbcharacter::Faction")
            .GroupBy(row => row.Id)
            .ToDictionary(group => group.Key, group => group.First());
    }

    public Dictionary<uint, Monster> LoadMonster()
    {
        return LoadStaticDB<Monster>("dbcharacter::Monster")
            .GroupBy(row => row.Id)
            .ToDictionary(group => group.Key, group => group.First());
    }

    public Dictionary<uint, MonsterVisualOptions> LoadMonsterVisualOptions()
    {
        return LoadStaticDB<MonsterVisualOptions>("dbcharacter::MonsterVisualOptions")
            .GroupBy(row => (uint)row.Id)
            .ToDictionary(group => group.Key, group => group.First());
    }

    public Dictionary<int, List<MonsterVisualOption>> LoadMonsterVisualOption()
    {
        return LoadStaticDB<MonsterVisualOption>("dbcharacter::MonsterVisualOption")
            .GroupBy(row => row.Parent)
            .ToDictionary(group => group.Key, group => group.ToList());
    }

    public Dictionary<uint, Turret> LoadTurret()
    {
        return LoadStaticDB<Turret>("dbcharacter::Turret")
            .GroupBy(row => row.Id)
            .ToDictionary(group => group.Key, group => group.First());
    }

    public Dictionary<uint, GliderParameters> LoadGliderParameters()
    {
        return LoadStaticDB<GliderParameters>("dbcharacter::GliderParameters")
            .GroupBy(row => row.Id)
            .ToDictionary(group => group.Key, group => group.First());
    }

    public Dictionary<ushort, EmoteRecord> LoadEmoteRecord()
    {
        return LoadStaticDB<EmoteRecord>("dbcharacter::EmoteRecord")
            .GroupBy(row => row.Id)
            .ToDictionary(group => group.Key, group => ResolveDuplicate<ushort, EmoteRecord>("dbcharacter::EmoteRecord", group));
    }

    public Dictionary<uint, CharInfo> LoadCharInfo()
    {
        return LoadStaticDB<CharInfo>("dbcharacter::CharInfo")
            .GroupBy(row => row.Id)
            .ToDictionary(group => group.Key, group => group.First());
    }

    public Dictionary<uint, PoseType> LoadPoseType()
    {
        return LoadStaticDB<PoseType>("dbcharacter::PoseType")
            .GroupBy(row => row.PoseId)
            .ToDictionary(group => group.Key, group => group.First());
    }

    public Dictionary<uint, MapMarkerInfo> LoadMapMarkerInfo()
    {
        return LoadStaticDB<MapMarkerInfo>("dbencounterdata::MapMarkerInfo")
            .GroupBy(row => row.Id)
            .ToDictionary(group => group.Key, group => group.First());
    }

    public Dictionary<uint, SinCardTemplate> LoadSinCardTemplate()
    {
        return LoadStaticDB<SinCardTemplate>("dbencounterdata::SinCardTemplate")
            .GroupBy(row => row.Id)
            .ToDictionary(group => group.Key, group => group.First());
    }

    public Dictionary<uint, WarpaintPalette> LoadWarpaintPalettes()
    {
        return LoadStaticDB<WarpaintPalette>("dbvisualrecords::WarpaintPalette")
            .GroupBy(row => row.Id)
            .ToDictionary(group => group.Key, group => group.First());
    }

    public Dictionary<uint, VisualRecord> LoadVisualRecord()
    {
        return LoadStaticDB<VisualRecord>("dbvisualrecords::VisualRecord")
            .GroupBy(row => row.Id)
            .ToDictionary(group => group.Key, group => group.First());
    }

    public Dictionary<uint, AttributeCategory> LoadAttributeCategory()
    {
        return LoadStaticDB<AttributeCategory>("dbitems::AttributeCategory")
            .GroupBy(row => row.Id)
            .ToDictionary(group => group.Key, group => group.First());
    }

    public Dictionary<uint, AttributeDefinition> LoadAttributeDefinition()
    {
        return LoadStaticDB<AttributeDefinition>("dbitems::AttributeDefinition")
            .GroupBy(row => row.Id)
            .ToDictionary(group => group.Key, group => group.First());
    }

    public Dictionary<KeyValuePair<uint, ushort>, AttributeRange> LoadAttributeRange()
    {
        // There are duplicates, like item 78084 which has the range attribute twice. Ingame, it seems too use only one result for that one, so chosing to do the same here.
        return LoadStaticDB<AttributeRange>("dbitems::AttributeRange")
        .GroupBy(row => new KeyValuePair<uint, ushort>(row.ItemId, row.AttributeId))
        .ToDictionary(group => group.Key, group => group.Last());
    }

    public Dictionary<KeyValuePair<uint, ushort>, ItemModuleScalars> LoadItemModuleScalars()
    {
        return LoadStaticDB<ItemModuleScalars>("dbitems::ItemModuleScalars")
        .GroupBy(row => new KeyValuePair<uint, ushort>(row.ItemId, row.AttributeCategory))
        .ToDictionary(group => group.Key, group => group.First());
    }

    public Dictionary<KeyValuePair<uint, ushort>, ItemCharacterScalars> LoadItemCharacterScalars()
    {
        return LoadStaticDB<ItemCharacterScalars>("dbitems::ItemCharacterScalars")
        .GroupBy(row => new KeyValuePair<uint, ushort>(row.ItemId, row.AttributeCategory))
        .ToDictionary(group => group.Key, group => group.First());
    }

    public Dictionary<uint, RootItem> LoadRootItem()
    {
        return LoadStaticDB<RootItem>("dbitems::RootItem")
            .GroupBy(row => row.SdbId)
            .ToDictionary(group => group.Key, group => ResolveDuplicate<uint, RootItem>("dbitems::RootItem", group));
    }

    public Dictionary<uint, Battleframe> LoadBattleframe()
    {
        return LoadStaticDB<Battleframe>("dbitems::Battleframe")
            .GroupBy(row => row.Id)
            .ToDictionary(group => group.Key, group => ResolveDuplicate<uint, Battleframe>("dbitems::Battleframe", group));
    }

    public Dictionary<uint, AbilityModule> LoadAbilityModule()
    {
        return LoadStaticDB<AbilityModule>("dbitems::AbilityModule")
            .GroupBy(row => row.Id)
            .ToDictionary(group => group.Key, group => ResolveDuplicate<uint, AbilityModule>("dbitems::AbilityModule", group));
    }

    public Dictionary<uint, CarryableObject> LoadCarryableObject()
    {
        return LoadStaticDB<CarryableObject>("dbitems::CarryableObject")
            .GroupBy(row => row.Id)
            .ToDictionary(group => group.Key, group => group.First());
    }

    public Dictionary<uint, BaseCommandDef> LoadBaseCommandDef()
    {
        return LoadStaticDB<BaseCommandDef>("apt::BaseCommandDef")
        .GroupBy(row => row.Id).ToDictionary(group => group.Key, group => ResolveDuplicate<uint, BaseCommandDef>("apt::BaseCommandDef", group));
    }

    public Dictionary<uint, CommandType> LoadCommandType()
    {
        return LoadStaticDB<CommandType>("apt::CommandType")
        .GroupBy(row => row.Id).ToDictionary(group => group.Key, group => group.First());
    }

    public Dictionary<uint, AbilityData> LoadAbilityData()
    {
        return LoadStaticDB<AbilityData>("apt::AbilityData")
        .GroupBy(row => row.Id).ToDictionary(group => group.Key, group => ResolveDuplicate<uint, AbilityData>("apt::AbilityData", group));
    }

    public Dictionary<uint, ActiveInitiationCommandDef> LoadActiveInitiationTypeCommandDef()
    {
        return LoadStaticDB<ActiveInitiationCommandDef>("apt::ActiveInitiationCommandDef")
        .GroupBy(row => row.Id).ToDictionary(group => group.Key, group => group.First());
    }

    public Dictionary<uint, ImpactApplyEffectCommandDef> LoadImpactApplyEffectCommandDef()
    {
        return LoadStaticDB<ImpactApplyEffectCommandDef>("apt::ImpactApplyEffectCommandDef")
        .GroupBy(row => row.Id).ToDictionary(group => group.Key, group => group.First());
    }

    public Dictionary<uint, ImpactRemoveEffectCommandDef> LoadImpactRemoveEffectCommandDef()
    {
        return LoadStaticDB<ImpactRemoveEffectCommandDef>("apt::ImpactRemoveEffectCommandDef")
        .GroupBy(row => row.Id).ToDictionary(group => group.Key, group => group.First());
    }

    public Dictionary<uint, ImpactToggleEffectCommandDef> LoadImpactToggleEffectCommandDef()
    {
        return LoadStaticDB<ImpactToggleEffectCommandDef>("apt::ImpactToggleEffectCommandDef")
            .GroupBy(row => row.Id).ToDictionary(group => group.Key, group => ResolveDuplicate<uint, ImpactToggleEffectCommandDef>("apt::ImpactToggleEffectCommandDef", group));
    }

    public Dictionary<uint, ConditionalBranchCommandDef> LoadConditionalBranchCommandDef()
    {
        return LoadStaticDB<ConditionalBranchCommandDef>("apt::ConditionalBranchCommandDef")
        .GroupBy(row => row.Id).ToDictionary(group => group.Key, group => ResolveDuplicate<uint, ConditionalBranchCommandDef>("apt::ConditionalBranchCommandDef", group));
    }

    public Dictionary<uint, WhileLoopCommandDef> LoadWhileLoopCommandDef()
    {
        return LoadStaticDB<WhileLoopCommandDef>("apt::WhileLoopCommandDef")
        .GroupBy(row => row.Id).ToDictionary(group => group.Key, group => ResolveDuplicate<uint, WhileLoopCommandDef>("apt::WhileLoopCommandDef", group));
    }

    public Dictionary<uint, LogicNegateCommandDef> LoadLogicNegateCommandDef()
    {
        return LoadStaticDB<LogicNegateCommandDef>("apt::LogicNegateCommandDef")
        .GroupBy(row => row.Id).ToDictionary(group => group.Key, group => ResolveDuplicate<uint, LogicNegateCommandDef>("apt::LogicNegateCommandDef", group));
    }

    public Dictionary<uint, LogicOrCommandDef> LoadLogicOrCommandDef()
    {
        return LoadStaticDB<LogicOrCommandDef>("apt::LogicOrCommandDef")
        .GroupBy(row => row.Id).ToDictionary(group => group.Key, group => ResolveDuplicate<uint, LogicOrCommandDef>("apt::LogicOrCommandDef", group));
    }

    public Dictionary<uint, LogicOrChainCommandDef> LoadLogicOrChainCommandDef()
    {
        return LoadStaticDB<LogicOrChainCommandDef>("apt::LogicOrChainCommandDef")
        .GroupBy(row => row.Id).ToDictionary(group => group.Key, group => ResolveDuplicate<uint, LogicOrChainCommandDef>("apt::LogicOrChainCommandDef", group));
    }

    public Dictionary<uint, LogicAndChainCommandDef> LoadLogicAndChainCommandDef()
    {
        return LoadStaticDB<LogicAndChainCommandDef>("apt::LogicAndChainCommandDef")
        .GroupBy(row => row.Id).ToDictionary(group => group.Key, group => ResolveDuplicate<uint, LogicAndChainCommandDef>("apt::LogicAndChainCommandDef", group));
    }

    public Dictionary<uint, CallCommandDef> LoadCallCommandDef()
    {
        return LoadStaticDB<CallCommandDef>("apt::CallCommandDef")
        .GroupBy(row => row.Id).ToDictionary(group => group.Key, group => group.First());
    }

    public Dictionary<uint, InstantActivationCommandDef> LoadInstantActivationCommandDef()
    {
        return LoadStaticDB<InstantActivationCommandDef>("apt::InstantActivationCommandDef")
        .GroupBy(row => row.Id).ToDictionary(group => group.Key, group => group.First());
    }

    public Dictionary<uint, StagedActivationCommandDef> LoadStagedActivationCommandDef()
    {
        return LoadStaticDB<StagedActivationCommandDef>("apt::StagedActivationCommandDef")
            .GroupBy(row => row.Id).ToDictionary(group => group.Key, group => group.First());
    }

    public Dictionary<uint, StatusEffectData> LoadStatusEffectData()
    {
        return LoadStaticDB<StatusEffectData>("apt::StatusEffectData")
        .GroupBy(row => row.Id).ToDictionary(group => group.Key, group => ResolveDuplicate<uint, StatusEffectData>("apt::StatusEffectData", group));
    }

    public Dictionary<uint, HashSet<uint>> LoadStatusEffectTags()
    {
        return LoadStaticDB<StatusEffectTags>("apt::StatusEffectTags")
               .GroupBy(row => row.StatusfxId)
            .ToDictionary(group => group.Key, group => group.Select(item => item.TagtypeId).ToHashSet());
    }

    public Dictionary<uint, TargetPBAECommandDef> LoadTargetPBAECommandDef()
    {
        return LoadStaticDB<TargetPBAECommandDef>("apt::TargetPBAECommandDef")
        .GroupBy(row => row.Id).ToDictionary(group => group.Key, group => group.First());
    }

    public Dictionary<uint, TargetConeAECommandDef> LoadTargetConeAECommandDef()
    {
        return LoadStaticDB<TargetConeAECommandDef>("apt::TargetConeAECommandDef")
        .GroupBy(row => row.Id).ToDictionary(group => group.Key, group => group.First());
    }

    public Dictionary<uint, TargetClearCommandDef> LoadTargetClearCommandDef()
    {
        return LoadStaticDB<TargetClearCommandDef>("apt::TargetClearCommandDef")
        .GroupBy(row => row.Id).ToDictionary(group => group.Key, group => group.First());
    }

    public Dictionary<uint, TargetSelfCommandDef> LoadTargetSelfCommandDef()
    {
        return LoadStaticDB<TargetSelfCommandDef>("apt::TargetSelfCommandDef")
        .GroupBy(row => row.Id).ToDictionary(group => group.Key, group => group.First());
    }

    public Dictionary<uint, TargetInitiatorCommandDef> LoadTargetInitiatorCommandDef()
    {
        return LoadStaticDB<TargetInitiatorCommandDef>("apt::TargetInitiatorCommandDef")
        .GroupBy(row => row.Id).ToDictionary(group => group.Key, group => group.First());
    }

    public Dictionary<uint, TargetSwapCommandDef> LoadTargetSwapCommandDef()
    {
        return LoadStaticDB<TargetSwapCommandDef>("apt::TargetSwapCommandDef")
            .GroupBy(row => row.Id).ToDictionary(group => group.Key, group => group.First());
    }

    public Dictionary<uint, TargetStackEmptyCommandDef> LoadTargetStackEmptyCommandDef()
    {
        return LoadStaticDB<TargetStackEmptyCommandDef>("apt::TargetStackEmptyCommandDef")
            .GroupBy(row => row.Id).ToDictionary(group => group.Key, group => group.First());
    }

    public Dictionary<uint, PeekTargetsCommandDef> LoadPeekTargetsCommandDef()
    {
        return LoadStaticDB<PeekTargetsCommandDef>("apt::PeekTargetsCommandDef")
            .GroupBy(row => row.Id).ToDictionary(group => group.Key, group => group.First());
    }

    public Dictionary<uint, PopTargetsCommandDef> LoadPopTargetsCommandDef()
    {
        return LoadStaticDB<PopTargetsCommandDef>("apt::PopTargetsCommandDef")
            .GroupBy(row => row.Id).ToDictionary(group => group.Key, group => group.First());
    }

    public Dictionary<uint, PushTargetsCommandDef> LoadPushTargetsCommandDef()
    {
        return LoadStaticDB<PushTargetsCommandDef>("apt::PushTargetsCommandDef")
            .GroupBy(row => row.Id).ToDictionary(group => group.Key, group => group.First());
    }

    public Dictionary<uint, TargetFriendliesCommandDef> LoadTargetFriendliesCommandDef()
    {
        return LoadStaticDB<TargetFriendliesCommandDef>("aptfs::TargetFriendliesCommandDef")
        .GroupBy(row => row.Id).ToDictionary(group => group.Key, group => group.First());
    }

    public Dictionary<uint, TargetByEffectCommandDef> LoadTargetByEffectCommandDef()
    {
        return LoadStaticDB<TargetByEffectCommandDef>("aptfs::TargetByEffectCommandDef")
        .GroupBy(row => row.Id).ToDictionary(group => group.Key, group => group.First());
    }

    public Dictionary<uint, TargetByEffectTagCommandDef> LoadTargetByEffectTagCommandDef()
    {
        return LoadStaticDB<TargetByEffectTagCommandDef>("aptfs::TargetByEffectTagCommandDef")
            .GroupBy(row => row.Id).ToDictionary(group => group.Key, group => group.First());
    }

    public Dictionary<uint, TargetOwnerCommandDef> LoadTargetOwnerCommandDef()
    {
        return LoadStaticDB<TargetOwnerCommandDef>("aptfs::TargetOwnerCommandDef")
        .GroupBy(row => row.Id).ToDictionary(group => group.Key, group => group.First());
    }

    public Dictionary<uint, TargetByObjectTypeCommandDef> LoadTargetByObjectTypeCommandDef()
    {
        return LoadStaticDB<TargetByObjectTypeCommandDef>("aptfs::TargetByObjectTypeCommandDef")
        .GroupBy(row => row.Id).ToDictionary(group => group.Key, group => group.First());
    }

    public Dictionary<uint, TargetHostilesCommandDef> LoadTargetHostilesCommandDef()
    {
        return LoadStaticDB<TargetHostilesCommandDef>("aptfs::TargetHostilesCommandDef")
        .GroupBy(row => row.Id).ToDictionary(group => group.Key, group => group.First());
    }

    public Dictionary<uint, TargetByCharacterStateCommandDef> LoadTargetByCharacterStateCommandDef()
    {
        return LoadStaticDB<TargetByCharacterStateCommandDef>("aptfs::TargetByCharacterStateCommandDef")
        .GroupBy(row => row.Id).ToDictionary(group => group.Key, group => group.First());
    }

    public Dictionary<uint, InflictDamageCommandDef> LoadInflictDamageCommandDef()
    {
        return LoadStaticDB<InflictDamageCommandDef>("aptfs::InflictDamageCommandDef")
        .GroupBy(row => row.Id).ToDictionary(group => group.Key, group => group.First());
    }

    public Dictionary<uint, ForcePushCommandDef> LoadForcePushCommandDef()
    {
        return LoadStaticDB<ForcePushCommandDef>("aptfs::ForcePushCommandDef")
        .GroupBy(row => row.Id).ToDictionary(group => group.Key, group => group.First());
    }

    public Dictionary<uint, RequestBattleFrameListCommandDef> LoadRequestBattleFrameListCommandDef()
    {
        return LoadStaticDB<RequestBattleFrameListCommandDef>("aptfs::RequestBattleFrameListCommandDef")
            .GroupBy(row => row.Id).ToDictionary(group => group.Key, group => group.First());
    }

    public Dictionary<uint, ApplyImpulseCommandDef> LoadApplyImpulseCommandDef()
    {
        return LoadStaticDB<ApplyImpulseCommandDef>("aptfs::ApplyImpulseCommandDef")
        .GroupBy(row => row.Id).ToDictionary(group => group.Key, group => group.First());
    }

    public Dictionary<uint, DeployableCalldownCommandDef> LoadDeployableCalldownCommandDef()
    {
        return LoadStaticDB<DeployableCalldownCommandDef>("aptfs::DeployableCalldownCommandDef")
        .GroupBy(row => row.Id).ToDictionary(group => group.Key, group => group.First());
    }

    public Dictionary<uint, VehicleCalldownCommandDef> LoadVehicleCalldownCommandDef()
    {
        return LoadStaticDB<VehicleCalldownCommandDef>("aptfs::VehicleCalldownCommandDef")
        .GroupBy(row => row.Id).ToDictionary(group => group.Key, group => group.First());
    }

    public Dictionary<uint, FireProjectileCommandDef> LoadFireProjectileCommandDef()
    {
        return LoadStaticDB<FireProjectileCommandDef>("aptfs::FireProjectileCommandDef")
        .GroupBy(row => row.Id).ToDictionary(group => group.Key, group => group.First());
    }

    public Dictionary<uint, ResourceNodeBeaconCalldownCommandDef> LoadResourceNodeBeaconCalldownCommandDef()
    {
        return LoadStaticDB<ResourceNodeBeaconCalldownCommandDef>("aptfs::ResourceNodeBeaconCalldownCommandDef")
        .GroupBy(row => row.Id).ToDictionary(group => group.Key, group => group.First());
    }

    public Dictionary<uint, AttemptToCalldownVehicleCommandDef> LoadAttemptToCalldownVehicleCommandDef()
    {
        return LoadStaticDB<AttemptToCalldownVehicleCommandDef>("aptfs::AttemptToCalldownVehicleCommandDef")
        .GroupBy(row => row.Id).ToDictionary(group => group.Key, group => group.First());
    }

    public Dictionary<uint, RegisterClientProximityCommandDef> LoadRegisterClientProximityCommandDef()
    {
        return LoadStaticDB<RegisterClientProximityCommandDef>("aptfs::RegisterClientProximityCommandDef")
        .GroupBy(row => row.Id).ToDictionary(group => group.Key, group => ResolveDuplicate<uint, RegisterClientProximityCommandDef>("aptfs::RegisterClientProximityCommandDef", group));
    }

    public Dictionary<uint, CombatFlagsCommandDef> LoadCombatFlagsCommandDef()
    {
        return LoadStaticDB<CombatFlagsCommandDef>("aptfs::CombatFlagsCommandDef")
        .GroupBy(row => row.Id).ToDictionary(group => group.Key, group => group.First());
    }

    public Dictionary<uint, ApplyFreezeCommandDef> LoadApplyFreezeCommandDef()
    {
        return LoadStaticDB<ApplyFreezeCommandDef>("aptfs::ApplyFreezeCommandDef")
        .GroupBy(row => row.Id).ToDictionary(group => group.Key, group => group.First());
    }

    public Dictionary<uint, OrientationLockCommandDef> LoadOrientationLockCommandDef()
    {
        return LoadStaticDB<OrientationLockCommandDef>("aptfs::OrientationLockCommandDef")
        .GroupBy(row => row.Id).ToDictionary(group => group.Key, group => group.First());
    }

    public Dictionary<uint, StatModifierCommandDef> LoadStatModifierCommandDef()
    {
        return LoadStaticDB<StatModifierCommandDef>("aptfs::StatModifierCommandDef")
        .GroupBy(row => row.Id).ToDictionary(group => group.Key, group => group.First());
    }

    public Dictionary<uint, RequireAimModeCommandDef> LoadRequireAimModeCommandDef()
    {
        return LoadStaticDB<RequireAimModeCommandDef>("aptfs::RequireAimModeCommandDef")
        .GroupBy(row => row.Id).ToDictionary(group => group.Key, group => group.First());
    }

    public Dictionary<uint, RequireArmyCommandDef> LoadRequireArmyCommandDef()
    {
        return LoadStaticDB<RequireArmyCommandDef>("aptfs::RequireArmyCommandDef")
        .GroupBy(row => row.Id).ToDictionary(group => group.Key, group => group.First());
    }

    public Dictionary<uint, RequireBackstabCommandDef> LoadRequireBackstabCommandDef()
    {
        return LoadStaticDB<RequireBackstabCommandDef>("aptfs::RequireBackstabCommandDef")
        .GroupBy(row => row.Id).ToDictionary(group => group.Key, group => group.First());
    }

    public Dictionary<uint, RequireBulletHitCommandDef> LoadRequireBulletHitCommandDef()
    {
        return LoadStaticDB<RequireBulletHitCommandDef>("aptfs::RequireBulletHitCommandDef")
        .GroupBy(row => row.Id).ToDictionary(group => group.Key, group => group.First());
    }

    public Dictionary<uint, RequireCAISStateCommandDef> LoadRequireCAISStateCommandDef()
    {
        return LoadStaticDB<RequireCAISStateCommandDef>("aptfs::RequireCAISStateCommandDef")
        .GroupBy(row => row.Id).ToDictionary(group => group.Key, group => group.First());
    }

    public Dictionary<uint, RequireCStateCommandDef> LoadRequireCStateCommandDef()
    {
        return LoadStaticDB<RequireCStateCommandDef>("aptfs::RequireCStateCommandDef")
        .GroupBy(row => row.Id).ToDictionary(group => group.Key, group => group.First());
    }

    public Dictionary<uint, RequireDamageResponseCommandDef> LoadRequireDamageResponseCommandDef()
    {
        return LoadStaticDB<RequireDamageResponseCommandDef>("aptfs::RequireDamageResponseCommandDef")
        .GroupBy(row => row.Id).ToDictionary(group => group.Key, group => group.First());
    }

    public Dictionary<uint, RequireEliteLevelCommandDef> LoadRequireEliteLevelCommandDef()
    {
        return LoadStaticDB<RequireEliteLevelCommandDef>("aptfs::RequireEliteLevelCommandDef")
        .GroupBy(row => row.Id).ToDictionary(group => group.Key, group => group.First());
    }

    public Dictionary<uint, RequireEnergyByRangeCommandDef> LoadRequireEnergyByRangeCommandDef()
    {
        return LoadStaticDB<RequireEnergyByRangeCommandDef>("aptfs::RequireEnergyByRangeCommandDef")
        .GroupBy(row => row.Id).ToDictionary(group => group.Key, group => group.First());
    }

    public Dictionary<uint, RequireEnergyCommandDef> LoadRequireEnergyCommandDef()
    {
        return LoadStaticDB<RequireEnergyCommandDef>("aptfs::RequireEnergyCommandDef")
        .GroupBy(row => row.Id).ToDictionary(group => group.Key, group => group.First());
    }

    public Dictionary<uint, RequireEnergyFromTargetCommandDef> LoadRequireEnergyFromTargetCommandDef()
    {
        return LoadStaticDB<RequireEnergyFromTargetCommandDef>("aptfs::RequireEnergyFromTargetCommandDef")
        .GroupBy(row => row.Id).ToDictionary(group => group.Key, group => group.First());
    }

    public Dictionary<uint, RequireEquippedItemCommandDef> LoadRequireEquippedItemCommandDef()
    {
        return LoadStaticDB<RequireEquippedItemCommandDef>("aptfs::RequireEquippedItemCommandDef")
        .GroupBy(row => row.Id).ToDictionary(group => group.Key, group => group.First());
    }

    public Dictionary<uint, RequireFriendsCommandDef> LoadRequireFriendsCommandDef()
    {
        return LoadStaticDB<RequireFriendsCommandDef>("aptfs::RequireFriendsCommandDef")
        .GroupBy(row => row.Id).ToDictionary(group => group.Key, group => group.First());
    }

    public Dictionary<uint, RequireHasCertificateCommandDef> LoadRequireHasCertificateCommandDef()
    {
        return LoadStaticDB<RequireHasCertificateCommandDef>("aptfs::RequireHasCertificateCommandDef")
        .GroupBy(row => row.Id).ToDictionary(group => group.Key, group => group.First());
    }

    public Dictionary<uint, RequireHasEffectCommandDef> LoadRequireHasEffectCommandDef()
    {
        return LoadStaticDB<RequireHasEffectCommandDef>("aptfs::RequireHasEffectCommandDef")
        .GroupBy(row => row.Id).ToDictionary(group => group.Key, group => group.First());
    }

    public Dictionary<uint, RequireHasEffectTagCommandDef> LoadRequireHasEffectTagCommandDef()
    {
        return LoadStaticDB<RequireHasEffectTagCommandDef>("aptfs::RequireHasEffectTagCommandDef")
        .GroupBy(row => row.Id).ToDictionary(group => group.Key, group => group.First());
    }

    public Dictionary<uint, RequireHasItemCommandDef> LoadRequireHasItemCommandDef()
    {
        return LoadStaticDB<RequireHasItemCommandDef>("aptfs::RequireHasItemCommandDef")
        .GroupBy(row => row.Id).ToDictionary(group => group.Key, group => group.First());
    }

    public Dictionary<uint, RequireHasUnlockCommandDef> LoadRequireHasUnlockCommandDef()
    {
        return LoadStaticDB<RequireHasUnlockCommandDef>("aptfs::RequireHasUnlockCommandDef")
        .GroupBy(row => row.Id).ToDictionary(group => group.Key, group => group.First());
    }

    public Dictionary<uint, RequireHeadshotCommandDef> LoadRequireHeadshotCommandDef()
    {
        return LoadStaticDB<RequireHeadshotCommandDef>("aptfs::RequireHeadshotCommandDef")
        .GroupBy(row => row.Id).ToDictionary(group => group.Key, group => group.First());
    }

    public Dictionary<uint, RequireInCombatCommandDef> LoadRequireInCombatCommandDef()
    {
        return LoadStaticDB<RequireInCombatCommandDef>("aptfs::RequireInCombatCommandDef")
        .GroupBy(row => row.Id).ToDictionary(group => group.Key, group => group.First());
    }

    public Dictionary<uint, RequireInRangeCommandDef> LoadRequireInRangeCommandDef()
    {
        return LoadStaticDB<RequireInRangeCommandDef>("aptfs::RequireInRangeCommandDef")
        .GroupBy(row => row.Id).ToDictionary(group => group.Key, group => group.First());
    }

    public Dictionary<uint, RequireInVehicleCommandDef> LoadRequireInVehicleCommandDef()
    {
        return LoadStaticDB<RequireInVehicleCommandDef>("aptfs::RequireInVehicleCommandDef")
        .GroupBy(row => row.Id).ToDictionary(group => group.Key, group => group.First());
    }

    public Dictionary<uint, RequireIsNPCCommandDef> LoadRequireIsNPCCommandDef()
    {
        return LoadStaticDB<RequireIsNPCCommandDef>("aptfs::RequireIsNPCCommandDef")
        .GroupBy(row => row.Id).ToDictionary(group => group.Key, group => group.First());
    }

    public Dictionary<uint, RequireItemAttributeCommandDef> LoadRequireItemAttributeCommandDef()
    {
        return LoadStaticDB<RequireItemAttributeCommandDef>("aptfs::RequireItemAttributeCommandDef")
        .GroupBy(row => row.Id).ToDictionary(group => group.Key, group => group.First());
    }

    public Dictionary<uint, RequireItemDurabilityCommandDef> LoadRequireItemDurabilityCommandDef()
    {
        return LoadStaticDB<RequireItemDurabilityCommandDef>("aptfs::RequireItemDurabilityCommandDef")
        .GroupBy(row => row.Id).ToDictionary(group => group.Key, group => group.First());
    }

    public Dictionary<uint, RequireJumpedCommandDef> LoadRequireJumpedCommandDef()
    {
        return LoadStaticDB<RequireJumpedCommandDef>("aptfs::RequireJumpedCommandDef")
        .GroupBy(row => row.Id).ToDictionary(group => group.Key, group => group.First());
    }

    public Dictionary<uint, RequireLevelCommandDef> LoadRequireLevelCommandDef()
    {
        return LoadStaticDB<RequireLevelCommandDef>("aptfs::RequireLevelCommandDef")
        .GroupBy(row => row.Id).ToDictionary(group => group.Key, group => group.First());
    }

    public Dictionary<uint, RequireLineOfSightCommandDef> LoadRequireLineOfSightCommandDef()
    {
        return LoadStaticDB<RequireLineOfSightCommandDef>("aptfs::RequireLineOfSightCommandDef")
        .GroupBy(row => row.Id).ToDictionary(group => group.Key, group => group.First());
    }

    public Dictionary<uint, RequirementServerCommandDef> LoadRequirementServerCommandDef()
    {
        return LoadStaticDB<RequirementServerCommandDef>("aptfs::RequirementServerCommandDef")
        .GroupBy(row => row.Id).ToDictionary(group => group.Key, group => group.First());
    }

    public Dictionary<uint, RequireMovementFlagsCommandDef> LoadRequireMovementFlagsCommandDef()
    {
        return LoadStaticDB<RequireMovementFlagsCommandDef>("aptfs::RequireMovementFlagsCommandDef")
        .GroupBy(row => row.Id).ToDictionary(group => group.Key, group => group.First());
    }

    public Dictionary<uint, RequireMovestateCommandDef> LoadRequireMovestateCommandDef()
    {
        return LoadStaticDB<RequireMovestateCommandDef>("aptfs::RequireMovestateCommandDef")
        .GroupBy(row => row.Id).ToDictionary(group => group.Key, group => group.First());
    }

    public Dictionary<uint, RequireMovingCommandDef> LoadRequireMovingCommandDef()
    {
        return LoadStaticDB<RequireMovingCommandDef>("aptfs::RequireMovingCommandDef")
        .GroupBy(row => row.Id).ToDictionary(group => group.Key, group => group.First());
    }

    public Dictionary<uint, RequireNeedsAmmoCommandDef> LoadRequireNeedsAmmoCommandDef()
    {
        return LoadStaticDB<RequireNeedsAmmoCommandDef>("aptfs::RequireNeedsAmmoCommandDef")
        .GroupBy(row => row.Id).ToDictionary(group => group.Key, group => group.First());
    }

    public Dictionary<uint, RequireNotRespawnedCommandDef> LoadRequireNotRespawnedCommandDef()
    {
        return LoadStaticDB<RequireNotRespawnedCommandDef>("aptfs::RequireNotRespawnedCommandDef")
        .GroupBy(row => row.Id).ToDictionary(group => group.Key, group => group.First());
    }

    public Dictionary<uint, RequirePermissionCommandDef> LoadRequirePermissionCommandDef()
    {
        return LoadStaticDB<RequirePermissionCommandDef>("aptfs::RequirePermissionCommandDef")
        .GroupBy(row => row.Id).ToDictionary(group => group.Key, group => group.First());
    }

    public Dictionary<uint, RequireProjectileSlopeCommandDef> LoadRequireProjectileSlopeCommandDef()
    {
        return LoadStaticDB<RequireProjectileSlopeCommandDef>("aptfs::RequireProjectileSlopeCommandDef")
        .GroupBy(row => row.Id).ToDictionary(group => group.Key, group => group.First());
    }

    public Dictionary<uint, RequireReloadCommandDef> LoadRequireReloadCommandDef()
    {
        return LoadStaticDB<RequireReloadCommandDef>("aptfs::RequireReloadCommandDef")
        .GroupBy(row => row.Id).ToDictionary(group => group.Key, group => group.First());
    }

    public Dictionary<uint, RequireResourceCommandDef> LoadRequireResourceCommandDef()
    {
        return LoadStaticDB<RequireResourceCommandDef>("aptfs::RequireResourceCommandDef")
        .GroupBy(row => row.Id).ToDictionary(group => group.Key, group => group.First());
    }

    public Dictionary<uint, RequireResourceFromTargetCommandDef> LoadRequireResourceFromTargetCommandDef()
    {
        return LoadStaticDB<RequireResourceFromTargetCommandDef>("aptfs::RequireResourceFromTargetCommandDef")
        .GroupBy(row => row.Id).ToDictionary(group => group.Key, group => group.First());
    }

    public Dictionary<uint, RequireSinAcquiredCommandDef> LoadRequireSinAcquiredCommandDef()
    {
        return LoadStaticDB<RequireSinAcquiredCommandDef>("aptfs::RequireSinAcquiredCommandDef")
        .GroupBy(row => row.Id).ToDictionary(group => group.Key, group => group.First());
    }

    public Dictionary<uint, RequireSprintModifierCommandDef> LoadRequireSprintModifierCommandDef()
    {
        return LoadStaticDB<RequireSprintModifierCommandDef>("aptfs::RequireSprintModifierCommandDef")
        .GroupBy(row => row.Id).ToDictionary(group => group.Key, group => group.First());
    }

    public Dictionary<uint, RequireSquadLeaderCommandDef> LoadRequireSquadLeaderCommandDef()
    {
        return LoadStaticDB<RequireSquadLeaderCommandDef>("aptfs::RequireSquadLeaderCommandDef")
        .GroupBy(row => row.Id).ToDictionary(group => group.Key, group => group.First());
    }

    public Dictionary<uint, RequireSuperChargeCommandDef> LoadRequireSuperChargeCommandDef()
    {
        return LoadStaticDB<RequireSuperChargeCommandDef>("aptfs::RequireSuperChargeCommandDef")
        .GroupBy(row => row.Id).ToDictionary(group => group.Key, group => group.First());
    }

    public Dictionary<uint, RequireTookDamageCommandDef> LoadRequireTookDamageCommandDef()
    {
        return LoadStaticDB<RequireTookDamageCommandDef>("aptfs::RequireTookDamageCommandDef")
        .GroupBy(row => row.Id).ToDictionary(group => group.Key, group => group.First());
    }

    public Dictionary<uint, RequireWeaponArmedCommandDef> LoadRequireWeaponArmedCommandDef()
    {
        return LoadStaticDB<RequireWeaponArmedCommandDef>("aptfs::RequireWeaponArmedCommandDef")
        .GroupBy(row => row.Id).ToDictionary(group => group.Key, group => group.First());
    }

    public Dictionary<uint, RequireWeaponTemplateCommandDef> LoadRequireWeaponTemplateCommandDef()
    {
        return LoadStaticDB<RequireWeaponTemplateCommandDef>("aptfs::RequireWeaponTemplateCommandDef")
        .GroupBy(row => row.Id).ToDictionary(group => group.Key, group => group.First());
    }

    public Dictionary<uint, RequireZoneTypeCommandDef> LoadRequireZoneTypeCommandDef()
    {
        return LoadStaticDB<RequireZoneTypeCommandDef>("aptfs::RequireZoneTypeCommandDef")
        .GroupBy(row => row.Id).ToDictionary(group => group.Key, group => group.First());
    }

    public Dictionary<uint, RequireDamageTypeCommandDef> LoadRequireDamageTypeCommandDef()
    {
        return LoadStaticDB<RequireDamageTypeCommandDef>("apt::RequireDamageTypeCommandDef")
        .GroupBy(row => row.Id).ToDictionary(group => group.Key, group => group.First());
    }

    public Dictionary<uint, TimeDurationCommandDef> LoadTimeDurationCommandDef()
    {
        return LoadStaticDB<TimeDurationCommandDef>("apt::TimeDurationCommandDef")
        .GroupBy(row => row.Id).ToDictionary(group => group.Key, group => group.First());
    }

    public Dictionary<uint, ReturnCommandDef> LoadReturnCommandDef()
    {
        return LoadStaticDB<ReturnCommandDef>("apt::ReturnCommandDef")
        .GroupBy(row => row.Id).ToDictionary(group => group.Key, group => group.First());
    }

    public Dictionary<uint, LoadRegisterFromItemStatCommandDef> LoadLoadRegisterFromItemStatCommandDef()
    {
        return LoadStaticDB<LoadRegisterFromItemStatCommandDef>("apt::LoadRegisterFromItemStatCommandDef")
        .GroupBy(row => row.Id).ToDictionary(group => group.Key, group => group.First());
    }

    public Dictionary<uint, LoadRegisterFromBonusCommandDef> LoadLoadRegisterFromBonusCommandDef()
    {
        return LoadStaticDB<LoadRegisterFromBonusCommandDef>("apt::LoadRegisterFromBonusCommandDef")
        .GroupBy(row => row.Id).ToDictionary(group => group.Key, group => group.First());
    }

    public Dictionary<uint, LoadRegisterFromDamageCommandDef> LoadLoadRegisterFromDamageCommandDef()
    {
        return LoadStaticDB<LoadRegisterFromDamageCommandDef>("apt::LoadRegisterFromDamageCommandDef")
        .GroupBy(row => row.Id).ToDictionary(group => group.Key, group => group.First());
    }

    public Dictionary<uint, LoadRegisterFromLevelCommandDef> LoadLoadRegisterFromLevelCommandDef()
    {
        return LoadStaticDB<LoadRegisterFromLevelCommandDef>("apt::LoadRegisterFromLevelCommandDef")
        .GroupBy(row => row.Id).ToDictionary(group => group.Key, group => group.First());
    }

    public Dictionary<uint, LoadRegisterFromModulePowerCommandDef> LoadLoadRegisterFromModulePowerCommandDef()
    {
        return LoadStaticDB<LoadRegisterFromModulePowerCommandDef>("apt::LoadRegisterFromModulePowerCommandDef")
        .GroupBy(row => row.Id).ToDictionary(group => group.Key, group => group.First());
    }

    public Dictionary<uint, LoadRegisterFromNamedVarCommandDef> LoadLoadRegisterFromNamedVarCommandDef()
    {
        return LoadStaticDB<LoadRegisterFromNamedVarCommandDef>("apt::LoadRegisterFromNamedVarCommandDef")
        .GroupBy(row => row.Id).ToDictionary(group => group.Key, group => group.First());
    }

    public Dictionary<uint, LoadRegisterFromResourceCommandDef> LoadLoadRegisterFromResourceCommandDef()
    {
        return LoadStaticDB<LoadRegisterFromResourceCommandDef>("apt::LoadRegisterFromResourceCommandDef")
        .GroupBy(row => row.Id).ToDictionary(group => group.Key, group => group.First());
    }

    public Dictionary<uint, LoadRegisterFromStatCommandDef> LoadLoadRegisterFromStatCommandDef()
    {
        return LoadStaticDB<LoadRegisterFromStatCommandDef>("apt::LoadRegisterFromStatCommandDef")
        .GroupBy(row => row.Id).ToDictionary(group => group.Key, group => group.First());
    }

    public Dictionary<uint, RegisterComparisonCommandDef> LoadRegisterComparisonCommandDef()
    {
        return LoadStaticDB<RegisterComparisonCommandDef>("apt::RegisterComparisonCommandDef")
        .GroupBy(row => row.Id).ToDictionary(group => group.Key, group => group.First());
    }

    public Dictionary<uint, RegisterRandomCommandDef> LoadRegisterRandomCommandDef()
    {
        return LoadStaticDB<RegisterRandomCommandDef>("apt::RegisterRandomCommandDef")
        .GroupBy(row => row.Id).ToDictionary(group => group.Key, group => group.First());
    }

    public Dictionary<uint, SetRegisterCommandDef> LoadSetRegisterCommandDef()
    {
        return LoadStaticDB<SetRegisterCommandDef>("apt::SetRegisterCommandDef")
        .GroupBy(row => row.Id).ToDictionary(group => group.Key, group => group.First());
    }

    public Dictionary<uint, NamedVariableAssignCommandDef> LoadNamedVariableAssignCommandDef()
    {
        return LoadStaticDB<NamedVariableAssignCommandDef>("apt::NamedVariableAssignCommandDef")
        .GroupBy(row => row.Id).ToDictionary(group => group.Key, group => group.First());
    }

    public Dictionary<uint, InflictCooldownCommandDef> LoadInflictCooldownCommandDef()
    {
        return LoadStaticDB<InflictCooldownCommandDef>("apt::InflictCooldownCommandDef")
        .GroupBy(row => row.Id).ToDictionary(group => group.Key, group => group.First());
    }

    public Dictionary<uint, InteractionTypeCommandDef> LoadInteractionTypeCommandDef()
    {
        return LoadStaticDB<InteractionTypeCommandDef>("aptfs::InteractionTypeCommandDef")
        .GroupBy(row => row.Id).ToDictionary(group => group.Key, group => group.First());
    }

    public Dictionary<byte, VehicleClass> LoadVehicleClass()
    {
        return LoadStaticDB<VehicleClass>("vcs::VehicleClass")
        .GroupBy(row => row.Id).ToDictionary(group => group.Key, group => group.First());
    }

    public Dictionary<ushort, VehicleInfo> LoadVehicleInfo()
    {
        return LoadStaticDB<VehicleInfo>("vcs::VehicleInfo")
        .GroupBy(row => row.Id).ToDictionary(group => group.Key, group => group.First());
    }

    public Dictionary<ushort, Dictionary<uint, BaseComponentDef>> LoadBaseComponentDef()
    {
        return LoadStaticDB<BaseComponentDef>("vcs::BaseComponentDef")
        .GroupBy(row => row.VehicleId)
        .ToDictionary(group => group.Key, group => group.ToDictionary(row => row.Id, row => row));
    }

    public Dictionary<uint, ScopingComponentDef> LoadScopingComponentDef()
    {
        return LoadStaticDB<ScopingComponentDef>("vcs::ScopingComponentDef")
        .GroupBy(row => row.Id).ToDictionary(group => group.Key, group => group.First());
    }

    public Dictionary<uint, DriverComponentDef> LoadDriverComponentDef()
    {
        return LoadStaticDB<DriverComponentDef>("vcs::DriverComponentDef")
        .GroupBy(row => row.Id).ToDictionary(group => group.Key, group => group.First());
    }

    public Dictionary<uint, PassengerComponentDef> LoadPassengerComponentDef()
    {
        return LoadStaticDB<PassengerComponentDef>("vcs::PassengerComponentDef")
        .GroupBy(row => row.Id).ToDictionary(group => group.Key, group => group.First());
    }

    public Dictionary<uint, HullSegmentDef> LoadHullSegmentDef()
    {
        return LoadStaticDB<HullSegmentDef>("vcs::HullSegmentDef")
        .GroupBy(row => row.Id).ToDictionary(group => group.Key, group => group.First());
    }

    public Dictionary<uint, AbilityComponentDef> LoadAbilityComponentDef()
    {
        return LoadStaticDB<AbilityComponentDef>("vcs::AbilityComponentDef")
        .GroupBy(row => row.Id).ToDictionary(group => group.Key, group => group.First());
    }

    public Dictionary<uint, DamageComponentDef> LoadDamageComponentDef()
    {
        return LoadStaticDB<DamageComponentDef>("vcs::DamageComponentDef")
        .GroupBy(row => row.Id).ToDictionary(group => group.Key, group => group.First());
    }

    public Dictionary<uint, StatusEffectComponentDef> LoadStatusEffectComponentDef()
    {
        return LoadStaticDB<StatusEffectComponentDef>("vcs::StatusEffectComponentDef")
        .GroupBy(row => row.Id).ToDictionary(group => group.Key, group => group.First());
    }

    public Dictionary<uint, TurretComponentDef> LoadTurretComponentDef()
    {
        return LoadStaticDB<TurretComponentDef>("vcs::TurretComponentDef")
        .GroupBy(row => row.Id).ToDictionary(group => group.Key, group => group.First());
    }

    public Dictionary<uint, DeployableComponentDef> LoadDeployableComponentDef()
    {
        return LoadStaticDB<DeployableComponentDef>("vcs::DeployableComponentDef")
        .GroupBy(row => row.Id).ToDictionary(group => group.Key, group => group.First());
    }

    public Dictionary<uint, SpawnPointComponentDef> LoadSpawnPointComponentDef()
    {
        return LoadStaticDB<SpawnPointComponentDef>("vcs::SpawnPointComponentDef")
        .GroupBy(row => row.Id).ToDictionary(group => group.Key, group => group.First());
    }

    public Dictionary<uint, TargetSingleCommandDef> LoadTargetSingleCommandDef()
    {
        return LoadStaticDB<TargetSingleCommandDef>("apt::TargetSingleCommandDef")
            .GroupBy(row => row.Id).ToDictionary(group => group.Key, group => group.First());
    }

    public Dictionary<uint, TimeCooldownCommandDef> LoadTimeCooldownCommandDef()
    {
        return LoadStaticDB<TimeCooldownCommandDef>("apt::TimeCooldownCommandDef")
            .GroupBy(row => row.Id).ToDictionary(group => group.Key, group => group.First());
    }

    public Dictionary<uint, TimedActivationCommandDef> LoadTimedActivationCommandDef()
    {
        return LoadStaticDB<TimedActivationCommandDef>("apt::TimedActivationCommandDef")
            .GroupBy(row => row.Id).ToDictionary(group => group.Key, group => group.First());
    }

    public Dictionary<uint, PassiveInitiationCommandDef> LoadPassiveInitiationCommandDef()
    {
        return LoadStaticDB<PassiveInitiationCommandDef>("apt::PassiveInitiationCommandDef")
            .GroupBy(row => row.Id).ToDictionary(group => group.Key, group => group.First());
    }

    public Dictionary<uint, TargetInteractivesCommandDef> LoadTargetInteractivesCommandDef()
    {
        return LoadStaticDB<TargetInteractivesCommandDef>("aptfs::TargetInteractivesCommandDef")
            .GroupBy(row => row.Id).ToDictionary(group => group.Key, group => group.First());
    }

    public Dictionary<uint, ImpactMarkInteractivesCommandDef> LoadImpactMarkInteractivesCommandDef()
    {
        return LoadStaticDB<ImpactMarkInteractivesCommandDef>("aptfs::ImpactMarkInteractivesCommandDef")
            .GroupBy(row => row.Id).ToDictionary(group => group.Key, group => group.First());
    }

    public Dictionary<uint, TargetPreviousCommandDef> LoadTargetPreviousCommandDef()
    {
        return LoadStaticDB<TargetPreviousCommandDef>("apt::TargetPreviousCommandDef")
            .GroupBy(row => row.Id).ToDictionary(group => group.Key, group => group.First());
    }

    public Dictionary<uint, HasTargetsDurationCommandDef> LoadHasTargetsDurationCommandDef()
    {
        return LoadStaticDB<HasTargetsDurationCommandDef>("aptfs::HasTargetsDurationCommandDef")
            .GroupBy(row => row.Id).ToDictionary(group => group.Key, group => group.First());
    }

    public Dictionary<uint, ActivationDurationCommandDef> LoadActivationDurationCommandDef()
    {
        return LoadStaticDB<ActivationDurationCommandDef>("aptfs::ActivationDurationCommandDef")
            .GroupBy(row => row.Id).ToDictionary(group => group.Key, group => group.First());
    }

    public Dictionary<uint, UpdateYieldCommandDef> LoadUpdateYieldCommandDef()
    {
        return LoadStaticDB<UpdateYieldCommandDef>("apt::UpdateYieldCommandDef")
            .GroupBy(row => row.Id).ToDictionary(group => group.Key, group => group.First());
    }

    public Dictionary<uint, RopePullCommandDef> LoadRopePullCommandDef()
    {
        return LoadStaticDB<RopePullCommandDef>("aptfs::RopePullCommandDef")
            .GroupBy(row => row.Id).ToDictionary(group => group.Key, group => group.First());
    }

    public Dictionary<uint, SetTargetOffsetCommandDef> LoadSetTargetOffsetCommandDef()
    {
        return LoadStaticDB<SetTargetOffsetCommandDef>("aptfs::SetTargetOffsetCommandDef")
            .GroupBy(row => row.Id).ToDictionary(group => group.Key, group => group.First());
    }

    public Dictionary<uint, HealDamageCommandDef> LoadHealDamageCommandDef()
    {
        return LoadStaticDB<HealDamageCommandDef>("aptfs::HealDamageCommandDef")
            .GroupBy(row => row.Id).ToDictionary(group => group.Key, group => group.First());
    }

    public Dictionary<uint, BullrushCommandDef> LoadBullrushCommandDef()
    {
        return LoadStaticDB<BullrushCommandDef>("aptfs::BullrushCommandDef")
            .GroupBy(row => row.Id).ToDictionary(group => group.Key, group => group.First());
    }

    public Dictionary<uint, EnergyToDamageCommandDef> LoadEnergyToDamageCommandDef()
    {
        return LoadStaticDB<EnergyToDamageCommandDef>("aptfs::EnergyToDamageCommandDef")
            .GroupBy(row => row.Id).ToDictionary(group => group.Key, group => group.First());
    }

    public Dictionary<uint, AirborneDurationCommandDef> LoadAirborneDurationCommandDef()
    {
        return LoadStaticDB<AirborneDurationCommandDef>("aptfs::AirborneDurationCommandDef")
            .GroupBy(row => row.Id).ToDictionary(group => group.Key, group => group.First());
    }

    public Dictionary<uint, BattleFrameDurationCommandDef> LoadBattleFrameDurationCommandDef()
    {
        return LoadStaticDB<BattleFrameDurationCommandDef>("aptfs::BattleFrameDurationCommandDef")
            .GroupBy(row => row.Id).ToDictionary(group => group.Key, group => group.First());
    }

    public Dictionary<uint, ShootingDurationCommandDef> LoadShootingDurationCommandDef()
    {
        return LoadStaticDB<ShootingDurationCommandDef>("aptfs::ShootingDurationCommandDef")
            .GroupBy(row => row.Id).ToDictionary(group => group.Key, group => group.First());
    }

    public Dictionary<uint, SwitchWeaponCommandDef> LoadSwitchWeaponCommandDef()
    {
        return LoadStaticDB<SwitchWeaponCommandDef>("aptfs::SwitchWeaponCommandDef")
            .GroupBy(row => row.Id).ToDictionary(group => group.Key, group => group.First());
    }

    public Dictionary<uint, StatRequirementCommandDef> LoadStatRequirementCommandDef()
    {
        return LoadStaticDB<StatRequirementCommandDef>("aptfs::StatRequirementCommandDef")
            .GroupBy(row => row.Id).ToDictionary(group => group.Key, group => group.First());
    }

    public Dictionary<uint, ConsumeEnergyCommandDef> LoadConsumeEnergyCommandDef()
    {
        return LoadStaticDB<ConsumeEnergyCommandDef>("aptfs::ConsumeEnergyCommandDef")
            .GroupBy(row => row.Id).ToDictionary(group => group.Key, group => group.First());
    }

    public Dictionary<uint, TargetClassTypeCommandDef> LoadTargetClassTypeCommandDef()
    {
        return LoadStaticDB<TargetClassTypeCommandDef>("aptfs::TargetClassTypeCommandDef")
            .GroupBy(row => row.Id).ToDictionary(group => group.Key, group => group.First());
    }

    public Dictionary<uint, TargetDifferenceCommandDef> LoadTargetDifferenceCommandDef()
    {
        return LoadStaticDB<TargetDifferenceCommandDef>("apt::TargetDifferenceCommandDef")
            .GroupBy(row => row.Id).ToDictionary(group => group.Key, group => group.First());
    }

    public Dictionary<uint, ClimbLedgeCommandDef> LoadClimbLedgeCommandDef()
    {
        return LoadStaticDB<ClimbLedgeCommandDef>("aptfs::ClimbLedgeCommandDef")
            .GroupBy(row => row.Id).ToDictionary(group => group.Key, group => group.First());
    }

    public Dictionary<uint, AimRangeDurationCommandDef> LoadAimRangeDurationCommandDef()
    {
        return LoadStaticDB<AimRangeDurationCommandDef>("apt::AimRangeDurationCommandDef")
            .GroupBy(row => row.Id).ToDictionary(group => group.Key, group => group.First());
    }

    public Dictionary<uint, CopyInitiationPositionCommandDef> LoadCopyInitiationPositionCommandDef()
    {
        return LoadStaticDB<CopyInitiationPositionCommandDef>("aptfs::CopyInitiationPositionCommandDef")
            .GroupBy(row => row.Id).ToDictionary(group => group.Key, group => group.First());
    }

    public Dictionary<uint, SlotAmmoCommandDef> LoadSlotAmmoCommandDef()
    {
        return LoadStaticDB<SlotAmmoCommandDef>("aptfs::SlotAmmoCommandDef")
            .GroupBy(row => row.Id).ToDictionary(group => group.Key, group => group.First());
    }

    public Dictionary<uint, AddPhysicsCommandDef> LoadAddPhysicsCommandDef()
    {
        return LoadStaticDB<AddPhysicsCommandDef>("aptfs::AddPhysicsCommandDef")
            .GroupBy(row => row.Id).ToDictionary(group => group.Key, group => group.First());
    }

    public Dictionary<uint, TargetCurrentVehicleCommandDef> LoadTargetCurrentVehicleCommandDef()
    {
        return LoadStaticDB<TargetCurrentVehicleCommandDef>("aptfs::TargetCurrentVehicleCommandDef")
            .GroupBy(row => row.Id).ToDictionary(group => group.Key, group => group.First());
    }

    public Dictionary<uint, TargetPassengersCommandDef> LoadTargetPassengersCommandDef()
    {
        return LoadStaticDB<TargetPassengersCommandDef>("aptfs::TargetPassengersCommandDef")
            .GroupBy(row => row.Id).ToDictionary(group => group.Key, group => group.First());
    }

    public Dictionary<uint, TargetSquadmatesCommandDef> LoadTargetSquadmatesCommandDef()
    {
        return LoadStaticDB<TargetSquadmatesCommandDef>("aptfs::TargetSquadmatesCommandDef")
            .GroupBy(row => row.Id).ToDictionary(group => group.Key, group => group.First());
    }

    public Dictionary<uint, TargetTrimCommandDef> LoadTargetTrimCommandDef()
    {
        return LoadStaticDB<TargetTrimCommandDef>("apt::TargetTrimCommandDef")
            .GroupBy(row => row.Id).ToDictionary(group => group.Key, group => group.First());
    }

    public Dictionary<uint, SetWeaponDamageCommandDef> LoadSetWeaponDamageCommandDef()
    {
        return LoadStaticDB<SetWeaponDamageCommandDef>("aptfs::SetWeaponDamageCommandDef")
            .GroupBy(row => row.Id).ToDictionary(group => group.Key, group => group.First());
    }

    public Dictionary<uint, ConsumeEnergyOverTimeCommandDef> LoadConsumeEnergyOverTimeCommandDef()
    {
        return LoadStaticDB<ConsumeEnergyOverTimeCommandDef>("aptfs::ConsumeEnergyOverTimeCommandDef")
            .GroupBy(row => row.Id).ToDictionary(group => group.Key, group => group.First());
    }

    public Dictionary<uint, RequestAbilitySelectionCommandDef> LoadRequestAbilitySelectionCommandDef()
    {
        return LoadStaticDB<RequestAbilitySelectionCommandDef>("aptfs::RequestAbilitySelectionCommandDef")
            .GroupBy(row => row.Id).ToDictionary(group => group.Key, group => group.First());
    }

    public Dictionary<uint, BonusGreaterThanCommandDef> LoadBonusGreaterThanCommandDef()
    {
        return LoadStaticDB<BonusGreaterThanCommandDef>("apt::BonusGreaterThanCommandDef")
            .GroupBy(row => row.Id).ToDictionary(group => group.Key, group => group.First());
    }

    public Dictionary<uint, BombardmentCommandDef> LoadBombardmentCommandDef()
    {
        return LoadStaticDB<BombardmentCommandDef>("aptfs::BombardmentCommandDef")
            .GroupBy(row => row.Id).ToDictionary(group => group.Key, group => group.First());
    }

    public Dictionary<uint, SetProjectileTargetCommandDef> LoadSetProjectileTargetCommandDef()
    {
        return LoadStaticDB<SetProjectileTargetCommandDef>("aptfs::SetProjectileTargetCommandDef")
            .GroupBy(row => row.Id).ToDictionary(group => group.Key, group => group.First());
    }

    public Dictionary<uint, UpdateWaitCommandDef> LoadUpdateWaitCommandDef()
    {
        return LoadStaticDB<UpdateWaitCommandDef>("apt::UpdateWaitCommandDef")
            .GroupBy(row => row.Id).ToDictionary(group => group.Key, group => group.First());
    }

    public Dictionary<uint, PushRegisterCommandDef> LoadPushRegisterCommandDef()
    {
        return LoadStaticDB<PushRegisterCommandDef>("apt::PushRegisterCommandDef")
            .GroupBy(row => row.Id).ToDictionary(group => group.Key, group => group.First());
    }

    public Dictionary<uint, PopRegisterCommandDef> LoadPopRegisterCommandDef()
    {
        return LoadStaticDB<PopRegisterCommandDef>("apt::PopRegisterCommandDef")
            .GroupBy(row => row.Id).ToDictionary(group => group.Key, group => group.First());
    }

    public Dictionary<uint, PeekRegisterCommandDef> LoadPeekRegisterCommandDef()
    {
        return LoadStaticDB<PeekRegisterCommandDef>("apt::PeekRegisterCommandDef")
            .GroupBy(row => row.Id).ToDictionary(group => group.Key, group => group.First());
    }

    public Dictionary<uint, MovementSlideCommandDef> LoadMovementSlideCommandDef()
    {
        return LoadStaticDB<MovementSlideCommandDef>("aptfs::MovementSlideCommandDef")
            .GroupBy(row => row.Id).ToDictionary(group => group.Key, group => group.First());
    }

    public Dictionary<uint, TargetFromStatusEffectCommandDef> LoadTargetFromStatusEffectCommandDef()
    {
        return LoadStaticDB<TargetFromStatusEffectCommandDef>("aptfs::TargetFromStatusEffectCommandDef")
            .GroupBy(row => row.Id).ToDictionary(group => group.Key, group => group.First());
    }

    public Dictionary<uint, TargetByDamageResponseCommandDef> LoadTargetByDamageResponseCommandDef()
    {
        return LoadStaticDB<TargetByDamageResponseCommandDef>("aptfs::TargetByDamageResponseCommandDef")
            .GroupBy(row => row.Id).ToDictionary(group => group.Key, group => group.First());
    }

    public Dictionary<uint, ForcedMovementDurationCommandDef> LoadForcedMovementDurationCommandDef()
    {
        return LoadStaticDB<ForcedMovementDurationCommandDef>("aptfs::ForcedMovementDurationCommandDef")
            .GroupBy(row => row.Id).ToDictionary(group => group.Key, group => group.First());
    }

    public Dictionary<uint, FireUiEventCommandDef> LoadFireUiEventCommandDef()
    {
        return LoadStaticDB<FireUiEventCommandDef>("aptfs::FireUiEventCommandDef")
            .GroupBy(row => row.Id).ToDictionary(group => group.Key, group => group.First());
    }

    public Dictionary<uint, UiNamedVariableCommandDef> LoadUiNamedVariableCommandDef()
    {
        return LoadStaticDB<UiNamedVariableCommandDef>("aptfs::UiNamedVariableCommandDef")
            .GroupBy(row => row.Id).ToDictionary(group => group.Key, group => group.First());
    }

    public Dictionary<uint, DetonateProjectilesCommandDef> LoadDetonateProjectilesCommandDef()
    {
        return LoadStaticDB<DetonateProjectilesCommandDef>("aptfs::DetonateProjectilesCommandDef")
            .GroupBy(row => row.Id).ToDictionary(group => group.Key, group => group.First());
    }

    public Dictionary<uint, SetWeaponDamageTypeCommandDef> LoadSetWeaponDamageTypeCommandDef()
    {
        return LoadStaticDB<SetWeaponDamageTypeCommandDef>("aptfs::SetWeaponDamageTypeCommandDef")
            .GroupBy(row => row.Id).ToDictionary(group => group.Key, group => group.First());
    }

    public Dictionary<uint, TargetFilterMovestateCommandDef> LoadTargetFilterMovestateCommandDef()
    {
        return LoadStaticDB<TargetFilterMovestateCommandDef>("aptfs::TargetFilterMovestateCommandDef")
            .GroupBy(row => row.Id).ToDictionary(group => group.Key, group => group.First());
    }

    public Dictionary<uint, TargetByHostilityCommandDef> LoadTargetByHostilityCommandDef()
    {
        return LoadStaticDB<TargetByHostilityCommandDef>("aptfs::TargetByHostilityCommandDef")
            .GroupBy(row => row.Id).ToDictionary(group => group.Key, group => group.First());
    }

    public Dictionary<uint, ConsumeSuperChargeCommandDef> LoadConsumeSuperChargeCommandDef()
    {
        return LoadStaticDB<ConsumeSuperChargeCommandDef>("aptfs::ConsumeSuperChargeCommandDef")
            .GroupBy(row => row.Id).ToDictionary(group => group.Key, group => group.First());
    }

    public Dictionary<uint, TargetByHealthCommandDef> LoadTargetByHealthCommandDef()
    {
        return LoadStaticDB<TargetByHealthCommandDef>("aptfs::TargetByHealthCommandDef")
            .GroupBy(row => row.Id).ToDictionary(group => group.Key, group => group.First());
    }

    public Dictionary<uint, RegisterMovementEffectCommandDef> LoadRegisterMovementEffectCommandDef()
    {
        return LoadStaticDB<RegisterMovementEffectCommandDef>("aptfs::RegisterMovementEffectCommandDef")
            .GroupBy(row => row.Id).ToDictionary(group => group.Key, group => group.First());
    }

    public Dictionary<uint, UpdateWaitAndFireOnceCommandDef> LoadUpdateWaitAndFireOnceCommandDef()
    {
        return LoadStaticDB<UpdateWaitAndFireOnceCommandDef>("apt::UpdateWaitAndFireOnceCommandDef")
            .GroupBy(row => row.Id).ToDictionary(group => group.Key, group => ResolveDuplicate<uint, UpdateWaitAndFireOnceCommandDef>("apt::UpdateWaitAndFireOnceCommandDef", group));
    }

    public Dictionary<uint, ApplyAmmoRiderCommandDef> LoadApplyAmmoRiderCommandDef()
    {
        return LoadStaticDB<ApplyAmmoRiderCommandDef>("aptfs::ApplyAmmoRiderCommandDef")
            .GroupBy(row => row.Id).ToDictionary(group => group.Key, group => group.First());
    }

    public Dictionary<uint, TargetFilterByRangeCommandDef> LoadTargetFilterByRangeCommandDef()
    {
        return LoadStaticDB<TargetFilterByRangeCommandDef>("aptfs::TargetFilterByRangeCommandDef")
            .GroupBy(row => row.Id).ToDictionary(group => group.Key, group => group.First());
    }

    public Dictionary<uint, OverrideCollisionCommandDef> LoadOverrideCollisionCommandDef()
    {
        return LoadStaticDB<OverrideCollisionCommandDef>("aptfs::OverrideCollisionCommandDef")
            .GroupBy(row => row.Id)
            .ToDictionary(group => group.Key, group => group.First());
    }

    public Dictionary<uint, RegisterLoadScaleCommandDef> LoadRegisterLoadScaleCommandDef()
    {
        return LoadStaticDB<RegisterLoadScaleCommandDef>("apt::RegisterLoadScaleCommandDef")
            .GroupBy(row => row.Id)
            .ToDictionary(group => group.Key, group => group.First());
    }

    public Dictionary<uint, MovementFacingCommandDef> LoadMovementFacingCommandDef()
    {
        return LoadStaticDB<MovementFacingCommandDef>("aptfs::MovementFacingCommandDef")
            .GroupBy(row => row.Id)
            .ToDictionary(group => group.Key, group => group.First());
    }

    public Dictionary<uint, TargetFilterBySinAcquiredCommandDef> LoadTargetFilterBySinAcquiredCommandDef()
    {
        return LoadStaticDB<TargetFilterBySinAcquiredCommandDef>("aptfs::TargetFilterBySinAcquiredCommandDef")
            .GroupBy(row => row.Id)
            .ToDictionary(group => group.Key, group => group.First());
    }

    public Dictionary<uint, MovementTetherCommandDef> LoadMovementTetherCommandDef()
    {
        return LoadStaticDB<MovementTetherCommandDef>("aptfs::MovementTetherCommandDef")
            .GroupBy(row => row.Id)
            .ToDictionary(group => group.Key, group => group.First());
    }

    public Dictionary<uint, RegisterLoadFromWeaponCommandDef> LoadRegisterLoadFromWeaponCommandDef()
    {
        return LoadStaticDB<RegisterLoadFromWeaponCommandDef>("aptfs::RegisterLoadFromWeaponCommandDef")
            .GroupBy(row => row.Id)
            .ToDictionary(group => group.Key, group => group.First());
    }

    public Dictionary<uint, ApplyClientStatusEffectCommandDef> LoadApplyClientStatusEffectCommandDef()
    {
        return LoadStaticDB<ApplyClientStatusEffectCommandDef>("aptfs::ApplyClientStatusEffectCommandDef")
            .GroupBy(row => row.Id)
            .ToDictionary(group => group.Key, group => group.First());
    }

    public Dictionary<uint, RemoveClientStatusEffectCommandDef> LoadRemoveClientStatusEffectCommandDef()
    {
        return LoadStaticDB<RemoveClientStatusEffectCommandDef>("aptfs::RemoveClientStatusEffectCommandDef")
            .GroupBy(row => row.Id)
            .ToDictionary(group => group.Key, group => group.First());
    }

    public Dictionary<uint, DisableChatBubbleCommandDef> LoadDisableChatBubbleCommandDef()
    {
        return LoadStaticDB<DisableChatBubbleCommandDef>("aptfs::DisableChatBubbleCommandDef")
            .GroupBy(row => row.Id)
            .ToDictionary(group => group.Key, group => group.First());
    }

    public Dictionary<uint, DisableHealthAndIconCommandDef> LoadDisableHealthAndIconCommandDef()
    {
        return LoadStaticDB<DisableHealthAndIconCommandDef>("aptfs::DisableHealthAndIconCommandDef")
            .GroupBy(row => row.Id)
            .ToDictionary(group => group.Key, group => group.First());
    }

    public Dictionary<uint, tfAbilityAnimationCommandDef> LoadAbilityAnimationCommandDef()
    {
        return LoadRawTable("apttf::tfAbilityAnimationCommandDef", row => new tfAbilityAnimationCommandDef
        {
            SubStateIndex = ToUInt(row[0]),
            QueueTimeOffset = ToUInt(row[1]),
            AbilityAnimIndex = ToUInt(row[2]),
            BackpackState = ToUInt(row[3]),
            Id = ToUInt(row[4]),
            Cancel = ToUInt(row[5]),
            AllowReloads = ToUInt(row[6]),
            Outro = ToUInt(row[7]),
            Combo = ToUInt(row[8]),
            AllowAiming = ToUInt(row[9]),
            MovementTime = ToUInt(row[10]),
            FullBody = ToUInt(row[11]),
        })
            .GroupBy(row => row.Id)
            .ToDictionary(group => group.Key, group => ResolveDuplicate<uint, tfAbilityAnimationCommandDef>("apttf::tfAbilityAnimationCommandDef", group));
    }

    public Dictionary<uint, tfPlayAnimationCommandDef> LoadPlayAnimationCommandDef()
    {
        return LoadRawTable("apttf::tfPlayAnimationCommandDef", row => new tfPlayAnimationCommandDef
        {
            AnimationName = Convert.ToString(row[0], CultureInfo.InvariantCulture) ?? string.Empty,
            Id = ToUInt(row[1]),
            Param2 = ToByte(row[2]),
            Param3 = ToByte(row[3]),
        })
            .GroupBy(row => row.Id)
            .ToDictionary(group => group.Key, group => ResolveDuplicate<uint, tfPlayAnimationCommandDef>("apttf::tfPlayAnimationCommandDef", group));
    }

    public Dictionary<uint, tfPerformEmoteCommandDef> LoadPerformEmoteCommandDef()
    {
        return LoadRawTable("apttf::tfPerformEmoteCommandDef", row => new tfPerformEmoteCommandDef
        {
            EmoteName = Convert.ToString(row[0], CultureInfo.InvariantCulture) ?? string.Empty,
            Id = ToUInt(row[1]),
        })
            .GroupBy(row => row.Id)
            .ToDictionary(group => group.Key, group => ResolveDuplicate<uint, tfPerformEmoteCommandDef>("apttf::tfPerformEmoteCommandDef", group));
    }

    public Dictionary<uint, tfCustomPlayerCameraCommandDef> LoadCustomPlayerCameraCommandDef()
    {
        return LoadRawTable("apttf::tfCustomPlayerCameraCommandDef", row => new tfCustomPlayerCameraCommandDef
        {
            LookOffset = (FauFau.Util.CommmonDataTypes.Vector3)row[0],
            RelativeOffset = (FauFau.Util.CommmonDataTypes.Vector3)row[1],
            FieldOfView = ToFloat(row[2]),
            LookChangeTime = ToFloat(row[3]),
            DownAimClampDegrees = Convert.ToInt32(row[4], CultureInfo.InvariantCulture),
            UpAimClampDegrees = Convert.ToInt32(row[5], CultureInfo.InvariantCulture),
            PositionChangeTime = ToFloat(row[6]),
            ExitChangeTime = ToFloat(row[7]),
            Id = ToUInt(row[8]),
            UseAimOrientation = ToByte(row[9]),
        })
            .GroupBy(row => row.Id)
            .ToDictionary(group => group.Key, group => ResolveDuplicate<uint, tfCustomPlayerCameraCommandDef>("apttf::tfCustomPlayerCameraCommandDef", group));
    }

    public Dictionary<uint, Weapons> LoadWeapons()
    {
        return LoadStaticDB<Weapons>("dbitems::Weapons")
            .GroupBy(row => row.Id)
            .ToDictionary(group => group.Key, group => group.First());
    }

    public Dictionary<uint, WeaponTemplates> LoadWeaponTemplates()
    {
        return LoadStaticDB<WeaponTemplates>("dbitems::WeaponTemplates")
            .GroupBy(row => row.Id)
            .ToDictionary(group => group.Key, group => group.First());
    }

    public Dictionary<uint, WeaponTemplateModifiers> LoadWeaponTemplateModifiers()
    {
        return LoadStaticDB<WeaponTemplateModifiers>("dbitems::WeaponTemplateModifiers")
            .GroupBy(row => row.WeaponId)
            .ToDictionary(group => group.Key, group => group.First());
    }

    public Dictionary<uint, WeaponScope> LoadWeaponScope()
    {
        return LoadStaticDB<WeaponScope>("dbitems::WeaponScope")
            .GroupBy(row => row.Id)
            .ToDictionary(group => group.Key, group => group.First());
    }

    public Dictionary<uint, WeaponUnderbarrel> LoadWeaponUnderbarrel()
    {
        return LoadStaticDB<WeaponUnderbarrel>("dbitems::WeaponUnderbarrel")
            .GroupBy(row => row.Id)
            .ToDictionary(group => group.Key, group => group.First());
    }

    public Dictionary<uint, Ammo> LoadAmmo()
    {
        return LoadStaticDB<Ammo>("dbitems::Ammo")
            .GroupBy(row => row.Id)
            .ToDictionary(group => group.Key, group => group.First());
    }

    public Dictionary<uint, LevelBand> LoadLevelBand()
    {
        return LoadStaticDB<LevelBand>("dbitems::LevelBand")
            .GroupBy(row => row.Id)
            .ToDictionary(group => group.Key, group => group.First());
    }

    public Dictionary<uint, List<BattleframeVisuals>> LoadBattleframeVisuals()
    {
        return LoadStaticDB<BattleframeVisuals>("dbitems::BattleframeVisuals")
            .GroupBy(row => row.VisualGroup)
            .ToDictionary(group => group.Key, group => group.ToList());
    }

    public Dictionary<uint, PhysicsMaterial> LoadPhysicsMaterial()
    {
        return LoadStaticDB<PhysicsMaterial>("dbphysicsmaterials::PhysicsMaterial")
            .GroupBy(row => row.Id)
            .ToDictionary(group => group.Key, group => group.First());
    }

    public Dictionary<uint, ZoneRecord> LoadZoneRecord()
    {
        return LoadStaticDB<ZoneRecord>("dbzonemetadata::ZoneRecord")
            .GroupBy(row => row.Id)
            .ToDictionary(group => group.Key, group => group.First());
    }

    public Dictionary<uint, ResourceNodeType> LoadResourceNodeType()
    {
        return LoadStaticDB<ResourceNodeType>("dbzonemetadata::ResourceNodeType")
            .GroupBy(row => row.Id)
            .ToDictionary(group => group.Key, group => group.First());
    }

    public Dictionary<uint, List<ResourceNodeTypeResource>> LoadResourceNodeTypeResource()
    {
        return LoadStaticDB<ResourceNodeTypeResource>("dbzonemetadata::ResourceNodeTypeResource")
            .GroupBy(row => row.NodeTypeId)
            .ToDictionary(group => group.Key, group => group.ToList());
    }

    public Dictionary<uint, ResourceNodeBeacon> LoadResourceNodeBeacon()
    {
        return LoadStaticDB<ResourceNodeBeacon>("dbitems::ResourceNodeBeacon")
            .GroupBy(row => row.Id)
            .ToDictionary(group => group.Key, group => group.First());
    }

    public Dictionary<KeyValuePair<uint, uint>, LevelCategoryScalars> LoadLevelCategoryScalars()
    {
        return LoadStaticDB<LevelCategoryScalars>("dbitems::LevelCategoryScalars")
            .GroupBy(row => new KeyValuePair<uint, uint>(row.AttributeCategory, row.Level))
            .ToDictionary(group => group.Key, group => group.First());
    }

    public Dictionary<uint, FrameProgressionLevel> LoadFrameProgressionLevel()
    {
        var all = LoadStaticDB<FrameProgressionLevel>("dbitems::FrameProgressionLevel");
        var grouped = all.GroupBy(row => row.Level);
        var dict = grouped.ToDictionary(group => group.Key, group => group.First());
        return dict;
    }

    public Dictionary<uint, Blueprints> LoadBlueprints()
    {
        return LoadStaticDB<Blueprints>("dbitems::Blueprints")
               .GroupBy(row => row.Id)
               .ToDictionary(group => group.Key, group => group.First());
    }

    public Dictionary<uint, List<Blueprint_Items>> LoadBlueprintItems()
    {
        return LoadStaticDB<Blueprint_Items>("dbitems::Blueprint_Items")
        .GroupBy(row => row.BlueprintId)
        .ToDictionary(group => group.Key, group => group.ToList());
    }

    private static T[] LoadRawTable<T>(string tableName, Func<Row, T> rowMapper)
        where T : class
    {
        var table = TryGetTable(tableName);
        if (table == null)
        {
            return Array.Empty<T>();
        }

        Serilog.Log.Information($"Loading table {tableName} ({table.Rows.Count} rows)");

        var list = new List<T>(table.Rows.Count);
        for (int i = 0; i < table.Rows.Count; i++)
        {
            list.Add(rowMapper(table.Rows[i]));
        }

        return list.ToArray();
    }

    private static Table TryGetTable(string tableName)
    {
        try
        {
            var table = sdb.GetTableByName(tableName);
            if (table == null)
            {
                Serilog.Log.Information($"Warning: Table {tableName} not found in SDB. Skipping load.");
            }

            return table;
        }
        catch (ArgumentOutOfRangeException)
        {
            Serilog.Log.Information($"Warning: Table {tableName} not found in SDB. Skipping load.");
            return null;
        }
    }

    private static uint ToUInt(object value)
    {
        return Convert.ToUInt32(value, CultureInfo.InvariantCulture);
    }

    private static byte ToByte(object value)
    {
        return Convert.ToByte(value, CultureInfo.InvariantCulture);
    }

    private static float ToFloat(object value)
    {
        return Convert.ToSingle(value, CultureInfo.InvariantCulture);
    }

    private static T[] LoadStaticDB<T>(string tableName)
    where T : class, new()
    {
        HashSet<string> warningsSet = new HashSet<string>();

        Table table = TryGetTable(tableName);
        if (table == null)
        {
            return Array.Empty<T>();
        }

        Serilog.Log.Information($"Loading table {tableName} ({table.Rows.Count} rows)");

        var list = new List<T>();
        var properties = typeof(T).GetProperties()
            .Select(propInfo =>
                    {
                        var convertedName = Policy.ConvertName(propInfo.Name);
                        var index = table.GetColumnIndexByName(convertedName);

                        if (index == -1 && ManualNameConversions.TryGetValue(propInfo.Name, out var value))
                        {
                            index = table.GetColumnIndexByName(value);
                        }

                        if (index == -1)
                        {
                            index = table.GetColumnIndexByName(propInfo.Name);
                        }

                        return new { PropInfo = propInfo, ConvertedName = convertedName, Index = index, };
                    }).ToList();

        try
        {
            for (int i = 0; i < table.Rows.Count; i++)
            {
                Row row = table.Rows[i];
                T entry = new T();
                int fieldCount = row.Fields.Count;
                foreach (var prop in properties)
                {
                    try
                    {
                        if (prop.Index != -1)
                        {
                            if (prop.Index < fieldCount)
                            {
                                prop.PropInfo.SetValue(entry, row[prop.Index], null);
                            }
                            else
                            {
                                warningsSet.Add($"Index {prop.Index} out of range for row (Count: {fieldCount}) in {tableName}, column {prop.PropInfo.Name}");
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Serilog.Log.Information($"Exception field-mapping {tableName} row {i}, column {prop.PropInfo.Name}: {ex.Message}");
                    }
                }

                list.Add(entry);
            }
        }
        catch (Exception ex)
        {
            Serilog.Log.Information($"Fatal exception while iterating rows in {tableName}: {ex.Message}");
        }

        foreach (string text in warningsSet)
        {
            Serilog.Log.Information(text);
        }

        return list.ToArray();
    }
}