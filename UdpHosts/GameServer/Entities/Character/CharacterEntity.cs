using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using AeroMessages.Common;
using AeroMessages.GSS.V66;
using AeroMessages.GSS.V66.Character;
using AeroMessages.GSS.V66.Character.Controller;
using AeroMessages.GSS.V66.Character.View;
using BepuUtilities;
using GameServer.Aptitude;
using GameServer.Data;
using GameServer.Data.SDB;
using GameServer.Data.SDB.Records.customdata;
using GameServer.Data.SDB.Records.dbcharacter;
using GameServer.Data.SDB.Records.dbitems;
using GameServer.Data.SDB.Records.dbvisualrecords;
using GameServer.Entities.Deployable;
using GameServer.Enums;
using GameServer.Enums.Visuals;
using GameServer.Systems.Encounters;
using GameServer.Test;
using GrpcGameServerAPIClient;
using CharacterLoadout = GameServer.Data.CharacterLoadout;
using Serilog;
using GibVisuals = AeroMessages.GSS.V66.Character.GibVisuals;
using LoadoutVisualType = AeroMessages.GSS.V66.Character.LoadoutConfig_Visual.LoadoutVisualType;

namespace GameServer.Entities.Character;

/// <summary>
/// Base Character
/// </summary>
public sealed partial class CharacterEntity : BaseAptitudeEntity, IAptitudeTarget
{
    public const byte MaxMapMarkerCount = 64;
    private const uint RecoveryTracePrimaryEffectId = 10531;
    private const uint RecoveryTraceFollowupEffectId = 1375;
    private const uint RecoveryTraceWindowMs = 2000;
    private MapMarkerState[] MapMarkers = new MapMarkerState[MaxMapMarkerCount];
    private readonly List<RegisterMovementEffectCommandActiveContext> RegisteredMovementEffects = new();
    private readonly HashSet<uint> AppliedMovementEffectStatusIds = new();

    public CharacterEntity(IShard shard, ulong eid, CharacterEntity owner = null)
        : base(shard, eid, owner)
    {
        AeroEntityId = new EntityId() { Backing = EntityId, ControllerId = Controller.Character };

        CurrentStatModifiers = new Dictionary<StatModifierIdentifier, Dictionary<uint, ActiveStatModifier>>();
        foreach (StatModifierIdentifier stat in Enum.GetValues(typeof(StatModifierIdentifier)))
        {
            CurrentStatModifiers.Add(stat, new Dictionary<uint, ActiveStatModifier>());

            if (!BaseStatModifiers.ContainsKey(stat))
            {
                BaseStatModifiers[stat] = GetDefaultBaseStatModifier(stat);
            }
        }

        InitFields();
        InitViews();
    }

    public BaseController Character_BaseController { get; set; }
    public CombatController Character_CombatController { get; set; }
    public MissionAndMarkerController Character_MissionAndMarkerController { get; set; }
    public LocalEffectsController Character_LocalEffectsController { get; set; }
    public SpectatorController Character_SpectatorController { get; set; }
    public ObserverView Character_ObserverView { get; set; }
    public EquipmentView Character_EquipmentView { get; set; }
    public CombatView Character_CombatView { get; set; }
    public MovementView Character_MovementView { get; set; }
    public TinyObjectView Character_TinyObjectView { get; set; }

    public new CharacterCollisionComponent Collision { get; set; }
    public INetworkPlayer Player { get; set; }
    public bool IsPlayerControlled => Player != null;
    public Vector3 Velocity { get; set; }
    public Vector3 AimDirection { get; set; }
    public short MovementState { get; set; }
    public ushort MovementShortTime { get; set; }
    public bool Alive { get; set; }
    public short TimeSinceLastJump { get; set; }
    public bool IsAirborne { get; set; }
    public bool IsMoving { get => MovementStateContainer.Sprint || MovementStateContainer.Movement; }

    /// <summary>
    /// Server-side end time (ms) of any active forced movement impulse.
    /// Set by ApplyImpulseCommand; checked by ForcedMovementDurationCommand.
    /// Zero means no active impulse.
    /// </summary>
    public ulong ForcedMovementEndTime { get; set; }
    public ulong RecoveryTraceEndTime { get; set; }
    public bool IsCrouching { get => MovementStateContainer.Crouch; }

    public Dictionary<PermissionFlagsData.CharacterPermissionFlags, bool> CurrentPermissions { get; set; } = new Dictionary<PermissionFlagsData.CharacterPermissionFlags, bool>()
    {
        { PermissionFlagsData.CharacterPermissionFlags.movement, true },
        { PermissionFlagsData.CharacterPermissionFlags.sprint, true },
        { PermissionFlagsData.CharacterPermissionFlags.jump, true },
        { PermissionFlagsData.CharacterPermissionFlags.interact, true },
        { PermissionFlagsData.CharacterPermissionFlags.weapon, true },
        { PermissionFlagsData.CharacterPermissionFlags.melee, true },
        { PermissionFlagsData.CharacterPermissionFlags.abilities, true },
        { PermissionFlagsData.CharacterPermissionFlags.flashlight, true },
        { PermissionFlagsData.CharacterPermissionFlags.unk_8, false },
        { PermissionFlagsData.CharacterPermissionFlags.cheat_jump_midair, false },
        { PermissionFlagsData.CharacterPermissionFlags.glider, false },
        { PermissionFlagsData.CharacterPermissionFlags.unk_11, false },
        { PermissionFlagsData.CharacterPermissionFlags.jetpack, true },
        { PermissionFlagsData.CharacterPermissionFlags.map, true },
        { PermissionFlagsData.CharacterPermissionFlags.unk_14, true },
        { PermissionFlagsData.CharacterPermissionFlags.spectate_input, true },
        { PermissionFlagsData.CharacterPermissionFlags.new_character, false },
        { PermissionFlagsData.CharacterPermissionFlags.glider_hud, false },
        { PermissionFlagsData.CharacterPermissionFlags.crouch, true },
        { PermissionFlagsData.CharacterPermissionFlags.cheat_float, false },
        { PermissionFlagsData.CharacterPermissionFlags.detect_resources, false },
        { PermissionFlagsData.CharacterPermissionFlags.unk_21, true },
        { PermissionFlagsData.CharacterPermissionFlags.calldown_abilities, true },
        { PermissionFlagsData.CharacterPermissionFlags.unk_23, true },
        { PermissionFlagsData.CharacterPermissionFlags.emotes, true },
        { PermissionFlagsData.CharacterPermissionFlags.unk_25, true },
        { PermissionFlagsData.CharacterPermissionFlags.unk_26, true },
        { PermissionFlagsData.CharacterPermissionFlags.self_revive, true },
        { PermissionFlagsData.CharacterPermissionFlags.respawn_input, false },
        { PermissionFlagsData.CharacterPermissionFlags.free_repairs, false },
        { PermissionFlagsData.CharacterPermissionFlags.battleframe_abilities, true },
        { PermissionFlagsData.CharacterPermissionFlags.unk_31, true },
    };

    public ulong CurrentPermissionsValue => GetCurrentPermissionsValue();

    public StaticInfoData StaticInfo { get; set; }
    public byte PvPRank { get; set; }
    public byte EliteLevel { get; set; }
    public byte Level { get; set; }
    public byte EffectiveLevel { get; set; }
    public uint VipLevel { get; set; }
    public ulong ArmyGUID { get; set; }
    public sbyte ArmyIsOfficer { get; set; }

    public CharacterStateData CharacterState { get; set; }
    public int TimePlayed { get; set; }
    public MaxVital MaxShields { get; set; }
    public MaxVital MaxHealth { get; set; }
    public GibVisuals GibVisualsInfo { get; set; }
    public ProcessDelayData ProcessDelay { get; set; }
    public EmoteData Emote { get; set; }
    public DockedParamsData DockedParams { get; set; }
    public CinematicCameraData? CinematicCamera { get; set; } = null;
    public AssetOverridesField AssetOverrides { get; set; }
    public VisualOverridesField VisualOverrides { get; set; }
    public EquipmentData CurrentEquipment { get; set; }
    public CharacterStatsData CharacterStats { get; set; }
    public EnergyParamsData EnergyParams { get; set; }
    public ScopeBubbleInfoData ScopeBubble { get; set; }
    public CharacterSpawnPose SpawnPose { get; set; }
    public byte EffectsFlags { get; set; }
    public WeaponIndexData WeaponIndex { get; set; }
    public FireModeData FireMode_0 { get; set; }
    public FireModeData FireMode_1 { get; set; }
    public PermissionFlagsData PermissionFlags { get; set; }
    public AuthorizedTerminalData AuthorizedTerminal { get; set; } = new AuthorizedTerminalData { TerminalType = 0, TerminalId = 0, TerminalEntityId = 0 };
    public AttachedToData? AttachedTo { get; set; } = null;
    public IEntity AttachedToEntity { get; set; } = null;
    public int SelectedLoadout { get; set; }
    public List<DeployableEntity> OwnedDeployables { get; set; } = new List<DeployableEntity>();
    public uint AmmoOverride { get; set; } = 0;

    public ushort StatusEffectsChangeTime_0 { get; set; }
    public ushort StatusEffectsChangeTime_1 { get; set; }
    public ushort StatusEffectsChangeTime_2 { get; set; }
    public ushort StatusEffectsChangeTime_3 { get; set; }
    public ushort StatusEffectsChangeTime_4 { get; set; }
    public ushort StatusEffectsChangeTime_5 { get; set; }
    public ushort StatusEffectsChangeTime_6 { get; set; }
    public ushort StatusEffectsChangeTime_7 { get; set; }
    public ushort StatusEffectsChangeTime_8 { get; set; }
    public ushort StatusEffectsChangeTime_9 { get; set; }
    public ushort StatusEffectsChangeTime_10 { get; set; }
    public ushort StatusEffectsChangeTime_11 { get; set; }
    public ushort StatusEffectsChangeTime_12 { get; set; }
    public ushort StatusEffectsChangeTime_13 { get; set; }
    public ushort StatusEffectsChangeTime_14 { get; set; }
    public ushort StatusEffectsChangeTime_15 { get; set; }
    public ushort StatusEffectsChangeTime_16 { get; set; }
    public ushort StatusEffectsChangeTime_17 { get; set; }
    public ushort StatusEffectsChangeTime_18 { get; set; }
    public ushort StatusEffectsChangeTime_19 { get; set; }
    public ushort StatusEffectsChangeTime_20 { get; set; }
    public ushort StatusEffectsChangeTime_21 { get; set; }
    public ushort StatusEffectsChangeTime_22 { get; set; }
    public ushort StatusEffectsChangeTime_23 { get; set; }
    public ushort StatusEffectsChangeTime_24 { get; set; }
    public ushort StatusEffectsChangeTime_25 { get; set; }
    public ushort StatusEffectsChangeTime_26 { get; set; }
    public ushort StatusEffectsChangeTime_27 { get; set; }
    public ushort StatusEffectsChangeTime_28 { get; set; }
    public ushort StatusEffectsChangeTime_29 { get; set; }
    public ushort StatusEffectsChangeTime_30 { get; set; }
    public ushort StatusEffectsChangeTime_31 { get; set; }
    public StatusEffectData? StatusEffects_0 { get; set; }
    public StatusEffectData? StatusEffects_1 { get; set; }
    public StatusEffectData? StatusEffects_2 { get; set; }
    public StatusEffectData? StatusEffects_3 { get; set; }
    public StatusEffectData? StatusEffects_4 { get; set; }
    public StatusEffectData? StatusEffects_5 { get; set; }
    public StatusEffectData? StatusEffects_6 { get; set; }
    public StatusEffectData? StatusEffects_7 { get; set; }
    public StatusEffectData? StatusEffects_8 { get; set; }
    public StatusEffectData? StatusEffects_9 { get; set; }
    public StatusEffectData? StatusEffects_10 { get; set; }
    public StatusEffectData? StatusEffects_11 { get; set; }
    public StatusEffectData? StatusEffects_12 { get; set; }
    public StatusEffectData? StatusEffects_13 { get; set; }
    public StatusEffectData? StatusEffects_14 { get; set; }
    public StatusEffectData? StatusEffects_15 { get; set; }
    public StatusEffectData? StatusEffects_16 { get; set; }
    public StatusEffectData? StatusEffects_17 { get; set; }
    public StatusEffectData? StatusEffects_18 { get; set; }
    public StatusEffectData? StatusEffects_19 { get; set; }
    public StatusEffectData? StatusEffects_20 { get; set; }
    public StatusEffectData? StatusEffects_21 { get; set; }
    public StatusEffectData? StatusEffects_22 { get; set; }
    public StatusEffectData? StatusEffects_23 { get; set; }
    public StatusEffectData? StatusEffects_24 { get; set; }
    public StatusEffectData? StatusEffects_25 { get; set; }
    public StatusEffectData? StatusEffects_26 { get; set; }
    public StatusEffectData? StatusEffects_27 { get; set; }
    public StatusEffectData? StatusEffects_28 { get; set; }
    public StatusEffectData? StatusEffects_29 { get; set; }
    public StatusEffectData? StatusEffects_30 { get; set; }
    public StatusEffectData? StatusEffects_31 { get; set; }

    public CharacterLoadout CurrentLoadout { get; set; }

    public Dictionary<StatModifierIdentifier, Dictionary<uint, ActiveStatModifier>> CurrentStatModifiers { get; set; }
    public Dictionary<StatModifierIdentifier, float> BaseStatModifiers { get; set; } = new()
    {
        { StatModifierIdentifier.RunSpeedMult,         1.0f },
        { StatModifierIdentifier.FireRateModifier,     1.0f },
        { StatModifierIdentifier.FwdRunSpeedMult,      1.0f },
        { StatModifierIdentifier.JumpHeightMult,       1.0f },
        { StatModifierIdentifier.AirControlMult,       1.0f },
        { StatModifierIdentifier.ThrustStrengthMult,   1.0f },
        { StatModifierIdentifier.ThrustAirControl,     1.0f },
        { StatModifierIdentifier.Friction,             1.0f },
        { StatModifierIdentifier.AmmoConsumption,      1.0f },
        { StatModifierIdentifier.MaxTurnRate,          0.0f },
        { StatModifierIdentifier.TurnSpeed,            1.0f },
        { StatModifierIdentifier.TimeDilation,         1.0f },
        { StatModifierIdentifier.AccuracyModifier,     1.0f },
        { StatModifierIdentifier.GravityMult,          1.0f },
        { StatModifierIdentifier.AirResistanceMult,    1.0f },
        { StatModifierIdentifier.WeaponChargeupMod,    1.0f },
        { StatModifierIdentifier.WeaponDamageDealtMod, 1.0f },
        { StatModifierIdentifier.EliteRankXpBonus,    1.0f },
        { StatModifierIdentifier.XpBonus,             1.0f },
        { StatModifierIdentifier.ResourceStatusBonus, 1.0f },
    };

    internal MovementStateContainer MovementStateContainer { get; set; } = new();

    public override string ToString()
    {
        return IsPlayerControlled ? StaticInfo.DisplayName : base.ToString();
    }

    public void LoadMonster(uint typeId)
    {
        var monsterInfo = SDBInterface.GetMonster(typeId);
        var chassisWarpaint = SDBUtils.GetChassisWarpaint(monsterInfo.ChassisId, monsterInfo.FullbodyWarpaintPaletteId, monsterInfo.ArmorWarpaintPaletteId, monsterInfo.BodysuitWarpaintPaletteId, monsterInfo.GlowWarpaintPaletteId);

        // TODO: Consider internalizing into the CharacterLoadout instead?
        var loadout = new CharacterLoadout();
        loadout.ChassisID = monsterInfo.ChassisId;
        loadout.BackpackID = monsterInfo.BackpackId;
        loadout.ChassisWarpaint = chassisWarpaint;
        loadout.SlottedItems[LoadoutSlotType.Primary] = monsterInfo.Weapon1Id;
        loadout.SlottedItems[LoadoutSlotType.Secondary] = monsterInfo.Weapon2Id;

        var ornaments = new List<uint>();
        if (monsterInfo.OrnamentsMapGroupId_1 != 0)
        {
            ornaments.Add(monsterInfo.OrnamentsMapGroupId_1);
        }

        if (monsterInfo.OrnamentsMapGroupId_2 != 0)
        {
            ornaments.Add(monsterInfo.OrnamentsMapGroupId_2);
        }

        if (monsterInfo.OrnamentsMapGroupId_3 != 0)
        {
            ornaments.Add(monsterInfo.OrnamentsMapGroupId_3);
        }

        if (monsterInfo.OrnamentsMapGroupId_4 != 0)
        {
            ornaments.Add(monsterInfo.OrnamentsMapGroupId_4);
        }

        var (gradients, cziMaps, morphWeights) = ResolveMonsterVisualOptions(monsterInfo);

        SetStaticInfo(new StaticInfoData()
        {
            DisplayName = "_noname",
            UniqueName = string.Empty,
            Gender = (byte)(monsterInfo.Gender == 'F' ? 1 : 0),
            Race = monsterInfo.Race,
            CharInfoId = monsterInfo.CharinfoId,
            HeadMain = monsterInfo.HeadId,
            Eyes = monsterInfo.EyesId,
            Unk_1 = 0xff,
            TargetFlags = TargetFlags.IsNPC,
            StaffFlags = 0,
            CharacterTypeId = monsterInfo.Id,
            VoiceSet = monsterInfo.VoiceSet,
            TitleId = monsterInfo.Title,
            NameLocalizationId = monsterInfo.LocalizedNameId,
            HeadAccessories = new uint[] { monsterInfo.HeadAcc1Id, monsterInfo.HeadAcc2Id },
            LoadoutVehicle = 0,
            LoadoutGlider = 0,
            Visuals = new VisualsBlock
            {
                Decals = Array.Empty<VisualsDecalsBlock>(),
                Gradients = gradients,
                Colors = new uint[5]
                {
                    monsterInfo.SkinColor,
                    monsterInfo.LipColor,
                    monsterInfo.EyeColor,
                    monsterInfo.HairColor,
                    monsterInfo.FacialHairColor
                },
                Palettes = Array.Empty<VisualsPaletteBlock>(),
                Patterns = Array.Empty<VisualsPatternBlock>(),
                OrnamentGroupIds = ornaments.ToArray(),
                CziMapAssetIds = cziMaps,
                MorphWeights = morphWeights,
                Overlays = Array.Empty<VisualsOverlayBlock>()
            },
            ArmyTag = string.Empty
        });

        SetHostilityInfo(new HostilityInfoData
        {
            Flags = 0 | HostilityInfoData.HostilityFlags.Faction,
            FactionId = (byte)monsterInfo.FactionId
        });

        ApplyLoadout(loadout);

        // Temp hack to equip weapon
        if (monsterInfo.Weapon1Id != 0)
        {
            SetWeaponIndex(new WeaponIndexData()
            {
                Index = 1,
                Unk1 = 1,
                Unk2 = 0,
                Time = Shard.CurrentTime
            });
        }
        else if (monsterInfo.Weapon2Id != 0)
        {
            SetWeaponIndex(new WeaponIndexData()
            {
                Index = 2,
                Unk1 = 1,
                Unk2 = 0,
                Time = Shard.CurrentTime
            });
        }
    }

    private static (uint[] Gradients, uint[] CziMaps, HalfFloat[] MorphWeights) ResolveMonsterVisualOptions(Monster monsterInfo)
    {
        if (monsterInfo.VisualOptionsId == 0)
        {
            return (Array.Empty<uint>(), Array.Empty<uint>(), Array.Empty<HalfFloat>());
        }

        var visualOptions = SDBInterface.GetMonsterVisualOptions(monsterInfo.VisualOptionsId);
        if (visualOptions == null)
        {
            return (Array.Empty<uint>(), Array.Empty<uint>(), Array.Empty<HalfFloat>());
        }

        // Pick the gender-appropriate sub-group parent ID.
        int parentId = monsterInfo.Gender == 'F' ? visualOptions.Female : visualOptions.Male;
        if (parentId == 0)
        {
            return (Array.Empty<uint>(), Array.Empty<uint>(), Array.Empty<HalfFloat>());
        }

        var options = SDBInterface.GetMonsterVisualOptionsByParent(parentId);
        if (options.Count == 0)
        {
            return (Array.Empty<uint>(), Array.Empty<uint>(), Array.Empty<HalfFloat>());
        }

        var gradients = new List<uint>();
        var cziMaps = new List<uint>();
        var morphWeights = new List<HalfFloat>();

        foreach (var opt in options)
        {
            switch (opt.Type)
            {
                case (int)MonsterVisualOptionType.Gradient:
                    gradients.Add((uint)opt.Value);
                    break;
                case (int)MonsterVisualOptionType.CziMap:
                    cziMaps.Add((uint)opt.Value);
                    break;
                case (int)MonsterVisualOptionType.MorphWeight:
                    morphWeights.Add((HalfFloat)BitConverter.Int32BitsToSingle((int)opt.Value));
                    break;
                default:
                    Log.Debug("Monster {TypeId} VisualOptions {VisualOptionsId}: unrecognised MonsterVisualOption type {OptionType}, value {Value}", monsterInfo.Id, monsterInfo.VisualOptionsId, opt.Type, opt.Value);
                    break;
            }
        }

        return (gradients.ToArray(), cziMaps.ToArray(), morphWeights.ToArray());
    }

    public void LoadRemote(CharacterAndBattleframeVisuals remoteData)
    {
        var headAccessories = BuildHeadAccessories(remoteData.CharacterVisuals);
        uint[] currentColors = StaticInfo.Visuals.Colors ?? Array.Empty<uint>();

        uint skinColor = ResolveRemoteColor(remoteData.CharacterVisuals.SkinColor, currentColors.Length > 0 ? currentColors[0] : 0u);
        uint lipColor = ResolveRemoteColor(remoteData.CharacterVisuals.LipColor, currentColors.Length > 1 ? currentColors[1] : 0u);
        uint eyeColor = ResolveRemoteColor(remoteData.CharacterVisuals.EyeColor, currentColors.Length > 2 ? currentColors[2] : 0u);
        uint hairColor = ResolveRemoteColor(remoteData.CharacterVisuals.HairColor, currentColors.Length > 3 ? currentColors[3] : 0u);
        uint facialHairColor = ResolveRemoteColor(remoteData.CharacterVisuals.FacialHairColor, currentColors.Length > 4 ? currentColors[4] : 0u);

        Shard.Logger.Debug(
            "HairVisualsIn entity=0x{EntityId:X} name={Name} hairId={HairId} facialHairId={FacialHairId} headAcc={HeadAccessories} hairColor=0x{HairColor:X8} facialHairColor=0x{FacialHairColor:X8}",
            EntityId,
            remoteData.CharacterInfo.Name,
            remoteData.CharacterVisuals.Hair?.Id ?? 0,
            remoteData.CharacterVisuals.FacialHair?.Id ?? 0,
            string.Join(",", headAccessories),
            hairColor,
            facialHairColor);

        Load(new BasicCharacterData()
        {
            CharacterInfo = new Data.BasicCharacterInfo()
            {
                Name = remoteData.CharacterInfo.Name,
                Gender = (byte)remoteData.CharacterInfo.Gender,
                Race = (byte)remoteData.CharacterInfo.Race,
                TitleId = (ushort)remoteData.CharacterInfo.TitleId,
                CurrentBattleframeSDBId = remoteData.CharacterInfo.CurrentBattleframeSDBId,
                ArmyGuid = remoteData.CharacterInfo.ArmyGuid,
                ArmyTag = remoteData.CharacterInfo.ArmyTag,
                ArmyIsOfficer = remoteData.CharacterInfo.ArmyIsOfficer,
                TimePlayed = (int)remoteData.CharacterInfo.TimePlayed,
                PvPRank = remoteData.CharacterInfo.PvPRank,
                EliteLevel = remoteData.CharacterInfo.EliteLevel,
                StaffFlags = remoteData.CharacterInfo.StaffFlags,
                Level = remoteData.CharacterInfo.Level,
                EffectiveLevel = remoteData.CharacterInfo.EffectiveLevel,
                VipLevel = remoteData.CharacterInfo.VipLevel,
            },
            CharacterVisuals = new Data.BasicCharacterVisuals()
            {
                Vehicle = ResolveRemoteId(remoteData.CharacterVisuals.Vehicle, StaticInfo.LoadoutVehicle),
                Glider = ResolveRemoteId(remoteData.CharacterVisuals.Glider, StaticInfo.LoadoutGlider),

                Head = ResolveRemoteId(remoteData.CharacterVisuals.Head, StaticInfo.HeadMain),
                Eyes = ResolveRemoteId(remoteData.CharacterVisuals.Eyes, StaticInfo.Eyes),
                VoiceSet = ResolveRemoteId(remoteData.CharacterVisuals.VoiceSet, StaticInfo.VoiceSet),

                HeadAccessories = headAccessories,
                Ornaments = ResolveRemoteOrnaments(remoteData.CharacterVisuals.Ornaments, StaticInfo.Visuals.OrnamentGroupIds),

                SkinColor = skinColor,
                LipColor = lipColor,
                EyeColor = eyeColor,
                HairColor = hairColor,
                FacialHairColor = facialHairColor
            }
        });

        ApplyRemoteBattleframeVisuals(remoteData.BattleframeVisuals);

        Shard.Logger.Debug(
            "HairVisualsOut entity=0x{EntityId:X} headMain={HeadMain} headAcc={HeadAccessories} colors=[0x{Skin:X8},0x{Lip:X8},0x{Eye:X8},0x{Hair:X8},0x{FacialHair:X8}]",
            EntityId,
            StaticInfo.HeadMain,
            string.Join(",", StaticInfo.HeadAccessories ?? Array.Empty<uint>()),
            StaticInfo.Visuals.Colors.Length > 0 ? StaticInfo.Visuals.Colors[0] : 0u,
            StaticInfo.Visuals.Colors.Length > 1 ? StaticInfo.Visuals.Colors[1] : 0u,
            StaticInfo.Visuals.Colors.Length > 2 ? StaticInfo.Visuals.Colors[2] : 0u,
            StaticInfo.Visuals.Colors.Length > 3 ? StaticInfo.Visuals.Colors[3] : 0u,
            StaticInfo.Visuals.Colors.Length > 4 ? StaticInfo.Visuals.Colors[4] : 0u);
    }

    private static uint[] BuildHeadAccessories(GrpcGameServerAPIClient.CharacterVisuals visuals)
    {
        var accessories = new List<uint>(2);
        bool hasExplicitHairFields = visuals.Hair != null || visuals.FacialHair != null;

        // Prefer explicit hair/facial_hair fields, since those are authoritative in New You updates.
        if (visuals.Hair != null && visuals.Hair.Id > 0)
        {
            accessories.Add((uint)visuals.Hair.Id);
        }

        if (visuals.FacialHair != null && visuals.FacialHair.Id > 0)
        {
            accessories.Add((uint)visuals.FacialHair.Id);
        }

        if (hasExplicitHairFields)
        {
            return accessories.ToArray();
        }

        // Fall back to head_accessories for legacy characters that do not have hair fields populated.
        // Some update flows append into this list without pruning old entries, so keep only the latest
        // valid slots to avoid layered/stuck hairstyles.
        var fallback = visuals.HeadAccessories
            .ToList<WebIdValueColor>()
            .Select(item => (uint)item.Id)
            .Where(id => id > 0)
            .ToList();

        if (fallback.Count <= 2)
        {
            return fallback.ToArray();
        }

        return fallback.Skip(fallback.Count - 2).ToArray();
    }

    private static uint ResolveRemoteId(WebId value, uint fallback)
    {
        if (value == null || value.Id <= 0)
        {
            return fallback;
        }

        return (uint)value.Id;
    }

    private static uint ResolveRemoteColor(WebIdValueColor value, uint fallback)
    {
        uint remote = value?.Value?.Color ?? 0u;
        return remote != 0 ? remote : fallback;
    }

    private static uint[] ResolveRemoteOrnaments(global::Google.Protobuf.Collections.RepeatedField<WebId> ornaments, uint[] fallback)
    {
        if (ornaments == null || ornaments.Count == 0)
        {
            return fallback ?? Array.Empty<uint>();
        }

        return ornaments
            .ToList()
            .Select(item => (uint)item.Id)
            .Where(id => id > 0)
            .ToArray();
    }

    private void ApplyRemoteBattleframeVisuals(PlayerBattleframeVisuals battleframeVisuals)
    {
        if (battleframeVisuals == null)
        {
            return;
        }

        var equipment = CurrentEquipment;
        var chassis = equipment.Chassis;
        chassis.Visuals = BuildChassisVisualsFromRemoteBattleframe(chassis.Visuals, battleframeVisuals);
        equipment.Chassis = chassis;
        SetCurrentEquipment(equipment);

        SetVisualOverrides(new VisualOverridesField
        {
            Data = BuildVisualOverridesFromRemoteBattleframe(battleframeVisuals),
        });
    }

    public void ReapplyRemoteBattleframeVisuals(PlayerBattleframeVisuals battleframeVisuals)
    {
        ApplyRemoteBattleframeVisuals(battleframeVisuals);
    }

    private static VisualsBlock BuildChassisVisualsFromRemoteBattleframe(VisualsBlock existing, PlayerBattleframeVisuals battleframeVisuals)
    {
        var colors = existing.Colors?.Length == 7
            ? existing.Colors.ToArray()
            : new uint[7];

        var warpaintPalette = battleframeVisuals.WarpaintId > 0
            ? SDBInterface.GetWarpaintPalette((uint)battleframeVisuals.WarpaintId)
            : null;

        if (warpaintPalette != null)
        {
            var paletteColors = new uint[7]
            {
                FColor.CombineLightDark(warpaintPalette.Color1Highlight, warpaintPalette.Color1Shadow),
                FColor.CombineLightDark(warpaintPalette.Color2Highlight, warpaintPalette.Color2Shadow),
                FColor.CombineLightDark(warpaintPalette.Color3Highlight, warpaintPalette.Color3Shadow),
                FColor.CombineLightDark(warpaintPalette.Color4Highlight, warpaintPalette.Color4Shadow),
                FColor.CombineLightDark(warpaintPalette.Color5Highlight, warpaintPalette.Color5Shadow),
                FColor.CombineLightDark(warpaintPalette.Color6Highlight, warpaintPalette.Color6Shadow),
                FColor.CombineLightDark(warpaintPalette.Color7Highlight, warpaintPalette.Color7Shadow),
            };

            if ((warpaintPalette.TypeFlags & (uint)Math.Pow(2, 4)) != 0)
            {
                colors[0] = paletteColors[0];
                colors[1] = paletteColors[1];
                colors[2] = paletteColors[2];
                colors[3] = paletteColors[3];
                colors[4] = paletteColors[4];
                colors[5] = paletteColors[5];
                colors[6] = paletteColors[6];
            }

            if ((warpaintPalette.TypeFlags & (uint)Math.Pow(2, 0)) != 0)
            {
                colors[0] = paletteColors[0];
                colors[1] = paletteColors[1];
                colors[2] = paletteColors[2];
            }

            if ((warpaintPalette.TypeFlags & (uint)Math.Pow(2, 1)) != 0)
            {
                colors[3] = paletteColors[3];
                colors[4] = paletteColors[4];
            }

            if ((warpaintPalette.TypeFlags & (uint)Math.Pow(2, 3)) != 0)
            {
                colors[5] = paletteColors[5];
                colors[6] = paletteColors[6];
            }
        }
        else if (battleframeVisuals.Warpaint.Count == 7)
        {
            colors = battleframeVisuals.Warpaint.ToArray();
        }

        var palettes = battleframeVisuals.WarpaintId > 0
            && warpaintPalette != null
            && SDBUtils.TryMapWarpaintTypeFlagsToPaletteType(warpaintPalette.TypeFlags, out var paletteType)
            ? new[]
            {
                new VisualsPaletteBlock
                {
                    PaletteType = paletteType,
                    PaletteId = (uint)battleframeVisuals.WarpaintId,
                },
            }
            : Array.Empty<VisualsPaletteBlock>();

        var patternData = battleframeVisuals.WarpaintPatternData;
        var patterns = (patternData != null && patternData.Count > 0
                ? patternData
                    .Where(pattern => pattern != null && pattern.SdbId > 0)
                    .Select(pattern => new VisualsPatternBlock
                    {
                        PatternId = (uint)pattern.SdbId,
                        TransformValues = BuildPatternTransform(pattern.Transform),
                        Usage = (byte)Math.Clamp(pattern.Usage, 0, 3),
                    })
                : battleframeVisuals.WarpaintPatterns
                    .Where(patternId => patternId > 0)
                    .Select((patternId, index) => new VisualsPatternBlock
                    {
                        PatternId = (uint)patternId,
                        TransformValues = (HalfVector4)Vector4.Zero,
                        Usage = (byte)Math.Clamp(index, 0, 3),
                    }))
            .ToArray();

        var decals = battleframeVisuals.Decals
            .Where(decal => decal != null && decal.SdbId > 0)
            .Select(decal => new VisualsDecalsBlock
            {
                DecalId = (uint)decal.SdbId,
                Color = unchecked((uint)decal.Color),
                Transform = BuildDecalTransforms(decal.Transform),
                Usage = 0,
            })
            .ToArray();

        var gradients = battleframeVisuals.Decalgradients
            .Where(gradient => gradient > 0)
            .Select(gradient => (uint)gradient)
            .ToArray();

        if (gradients.Length == 0 && warpaintPalette?.TextureGradientId > 0)
        {
            gradients = [warpaintPalette.TextureGradientId];
        }

        if (gradients.Length == 0)
        {
            gradients = existing.Gradients ?? Array.Empty<uint>();
        }

        return new VisualsBlock
        {
            Decals = decals,
            Gradients = gradients,
            Colors = colors,
            Palettes = palettes,
            Patterns = patterns,
            OrnamentGroupIds = existing.OrnamentGroupIds ?? Array.Empty<uint>(),
            CziMapAssetIds = existing.CziMapAssetIds ?? Array.Empty<uint>(),
            MorphWeights = existing.MorphWeights ?? Array.Empty<HalfFloat>(),
            Overlays = existing.Overlays ?? Array.Empty<VisualsOverlayBlock>(),
        };
    }

    private static HalfVector4[] BuildDecalTransforms(global::Google.Protobuf.Collections.RepeatedField<float> rawTransform)
    {
        var transforms = new HalfVector4[3];
        if (rawTransform == null || rawTransform.Count == 0)
        {
            return transforms;
        }

        for (int i = 0; i < transforms.Length; i++)
        {
            int baseIndex = i * 4;

            var source = new Vector4(
                baseIndex < rawTransform.Count ? rawTransform[baseIndex] : 0f,
                baseIndex + 1 < rawTransform.Count ? rawTransform[baseIndex + 1] : 0f,
                baseIndex + 2 < rawTransform.Count ? rawTransform[baseIndex + 2] : 0f,
                baseIndex + 3 < rawTransform.Count ? rawTransform[baseIndex + 3] : 0f);

            transforms[i] = (HalfVector4)source;
        }

        return transforms;
    }

    private static HalfVector4 BuildPatternTransform(global::Google.Protobuf.Collections.RepeatedField<float> rawTransform)
    {
        if (rawTransform == null || rawTransform.Count == 0)
        {
            return (HalfVector4)Vector4.Zero;
        }

        return (HalfVector4)new Vector4(
            rawTransform.Count > 0 ? rawTransform[0] : 0f,
            rawTransform.Count > 1 ? rawTransform[1] : 0f,
            rawTransform.Count > 2 ? rawTransform[2] : 0f,
            rawTransform.Count > 3 ? rawTransform[3] : 0f);
    }

    private static VisualOverridesData[] BuildVisualOverridesFromRemoteBattleframe(PlayerBattleframeVisuals battleframeVisuals)
    {
        return battleframeVisuals.VisualOverrides
            .Where(visualGroupId => visualGroupId > 0)
            .Distinct()
            .Select(visualGroupId => new VisualOverridesData
            {
                SlotType = 0,
                VisualsGroupId = (uint)visualGroupId,
            })
            .ToArray();
    }

    public void Load(BasicCharacterData data)
    {
        var info = data.CharacterInfo;
        var visuals = data.CharacterVisuals;

        SetStaticInfo(new StaticInfoData
        {
            DisplayName = info.Name,
            UniqueName = info.Name,
            Gender = (byte)info.Gender,
            Race = (byte)info.Race,
            TitleId = info.TitleId,

            CharInfoId = ResolveCharInfoId((byte)info.Gender, StaticInfo.CharInfoId != 0 ? StaticInfo.CharInfoId : 1),
            Unk_1 = 0xff,
            TargetFlags = 0,
            StaffFlags = (byte)info.StaffFlags,
            CharacterTypeId = 0,
            NameLocalizationId = 0,

            HeadMain = visuals.Head,
            Eyes = visuals.Eyes,
            VoiceSet = visuals.VoiceSet,
            HeadAccessories = visuals.HeadAccessories,
            LoadoutVehicle = visuals.Vehicle,
            LoadoutGlider = visuals.Glider,
            Visuals = new VisualsBlock
            {
                Decals = Array.Empty<VisualsDecalsBlock>(),
                Gradients = Array.Empty<uint>(),
                Colors = new uint[5]
                {
                    visuals.SkinColor,
                    visuals.LipColor,
                    visuals.EyeColor,
                    visuals.HairColor,
                    visuals.FacialHairColor
                },
                Palettes = Array.Empty<VisualsPaletteBlock>(),
                Patterns = Array.Empty<VisualsPatternBlock>(),
                OrnamentGroupIds = visuals.Ornaments,
                CziMapAssetIds = Array.Empty<uint>(),
                MorphWeights = Array.Empty<HalfFloat>(),
                Overlays = Array.Empty<VisualsOverlayBlock>()
            },
            ArmyTag = DataUtils.FormatArmyTag(info.ArmyTag),
        });

        PvPRank = (byte)info.PvPRank;
        EliteLevel = (byte)info.EliteLevel;
        Level = (byte)info.Level;
        EffectiveLevel = (byte)info.EffectiveLevel;
        VipLevel = info.VipLevel;
        ArmyGUID = info.ArmyGuid;
        ArmyIsOfficer = (sbyte)(info.ArmyIsOfficer ? 1 : 0);

        SetTimePlayed(info.TimePlayed);
        SetArmyGUID(info.ArmyGuid);
        SetArmyIsOfficer((sbyte)(info.ArmyIsOfficer ? 1 : 0));

        // Add setters for the new dynamic fields
        SetPvPRank((byte)info.PvPRank);
        SetEliteLevel((byte)info.EliteLevel);
        SetStaffFlags((byte)info.StaffFlags);
        SetLevel((byte)info.Level);
        SetEffectiveLevel((byte)info.EffectiveLevel);
        SetVipLevel(info.VipLevel);
    }

    private static uint ResolveCharInfoId(byte gender, uint fallbackCharInfoId)
    {
        // Player body mesh selection depends on CharInfoId. In retail data, 1/2 map to female/male.
        return gender switch
        {
            1 => 1u,
            0 => 2u,
            _ => fallbackCharInfoId,
        };
    }

    public void ApplyLoadout(CharacterLoadout loadout)
    {
        CurrentLoadout = loadout;

        SetStaticInfo(StaticInfo with
        {
            LoadoutVehicle = loadout.VehicleID,
            LoadoutGlider = loadout.GliderID,
        });

        var emptyVisuals = new VisualsBlock
        {
            Decals = Array.Empty<VisualsDecalsBlock>(),
            Gradients = Array.Empty<uint>(),
            Colors = Array.Empty<uint>(),
            Palettes = Array.Empty<VisualsPaletteBlock>(),
            Patterns = Array.Empty<VisualsPatternBlock>(),
            OrnamentGroupIds = Array.Empty<uint>(),
            CziMapAssetIds = Array.Empty<uint>(),
            MorphWeights = Array.Empty<HalfFloat>(),
            Overlays = Array.Empty<VisualsOverlayBlock>()
        };

        var chassis = new SlottedItem
        {
            SdbId = loadout.ChassisID,
            SlotIndex = 255,
            Flags = 0,
            Unk2 = 0,
            Modules = loadout.GetChassisModules(),
            Visuals = loadout.GetChassisVisuals()
        };
        var backpack = new SlottedItem
        {
            SdbId = loadout.BackpackID,
            SlotIndex = 255,
            Flags = 0,
            Unk2 = 0,
            Modules = loadout.GetBackpackModules(),
            Visuals = emptyVisuals
        };
        var primary = new SlottedWeapon
        {
            Item = new SlottedItem
            {
                SdbId = loadout.SlottedItems.GetValueOrDefault(LoadoutSlotType.Primary),
                SlotIndex = 255,
                Flags = 0,
                Unk2 = 0,
                Modules = Array.Empty<SlottedModule>(),
                Visuals = new VisualsBlock
                {
                    Decals = Array.Empty<VisualsDecalsBlock>(),
                    Gradients = Array.Empty<uint>(),
                    Colors = new uint[] { 0x322c0000, 0x543110a2, 0x65b42104 },
                    Palettes = Array.Empty<VisualsPaletteBlock>(),
                    Patterns = Array.Empty<VisualsPatternBlock>(),
                    OrnamentGroupIds = Array.Empty<uint>(),
                    CziMapAssetIds = Array.Empty<uint>(),
                    MorphWeights = Array.Empty<HalfFloat>(),
                    Overlays = Array.Empty<VisualsOverlayBlock>()
                }
            },
            Unk1 = 0,
            Unk2 = 0
        };
        var secondary = new SlottedWeapon
        {
            Item = new SlottedItem
            {
                SdbId = loadout.SlottedItems.GetValueOrDefault(LoadoutSlotType.Secondary, 0u),
                SlotIndex = 255,
                Flags = 0,
                Unk2 = 0,
                Modules = Array.Empty<SlottedModule>(),
                Visuals = new VisualsBlock
                {
                    Decals = Array.Empty<VisualsDecalsBlock>(),
                    Gradients = Array.Empty<uint>(),
                    Colors = new uint[] { 0x322c0000, 0x543110a2, 0x65b42104 },
                    Palettes = Array.Empty<VisualsPaletteBlock>(),
                    Patterns = Array.Empty<VisualsPatternBlock>(),
                    OrnamentGroupIds = Array.Empty<uint>(),
                    CziMapAssetIds = Array.Empty<uint>(),
                    MorphWeights = Array.Empty<HalfFloat>(),
                    Overlays = Array.Empty<VisualsOverlayBlock>()
                }
            },
            Unk1 = 0,
            Unk2 = 0
        };

        SetCurrentEquipment(new EquipmentData
        {
            Chassis = chassis,
            Backpack = backpack,
            PrimaryWeapon = primary,
            SecondaryWeapon = secondary,
            EndUnk1 = 0,
            EndUnk2 = 0
        });


        SetCharacterStats(new CharacterStatsData
        {
            ItemAttributes = loadout.GetItemAttributes(),
            Unk1 = 0,
            WeaponA = loadout.GetPrimaryWeaponAttributes(),
            Unk2 = 0,
            WeaponB = loadout.GetSecondaryWeaponAttributes(),
            Unk3 = 0,
            AttributeCategories1 = loadout.GetItemModuleScalars(), // TODO: Compare with capture
            AttributeCategories2 = loadout.GetItemCharacterScalars()
        });

        SelectedLoadout = loadout.LoadoutID;
        if (Character_BaseController != null)
        {
            Character_BaseController.SelectedLoadoutProp = SelectedLoadout;
        }

        StatusEffectsChangeTime_0 = loadout.ChassisChangeTime != 0 ? (ushort)loadout.ChassisChangeTime : (ushort)0;

        RefreshStats();

        if (chassis.SdbId != 0)
        {
            var gender = StaticInfo.Gender;
            var race = StaticInfo.Race;
            var charInfoId = StaticInfo.CharInfoId;

            CharInfo charInfo;
            Battleframe battleframeRecord;
            PoseType poseTypeRecord;
            List<BattleframeVisuals> battleframeVisualGroupRecords;
            BattleframeVisuals battleframeVisualGroupRecord = null;
            VisualRecord battleframeVisualRecord = null;

            try
            {
                charInfo = SDBInterface.GetCharInfo(charInfoId);
                battleframeRecord = SDBInterface.GetBattleframe(chassis.SdbId);
                poseTypeRecord = SDBInterface.GetPoseType(battleframeRecord.PosetypeId);
                battleframeVisualGroupRecords = SDBInterface.GetBattleframeVisuals(battleframeRecord.VisualGroup);

                // Find the appropriate visual record
                byte retries = 3;
                do
                {
                    foreach (var record in battleframeVisualGroupRecords)
                    {
                        bool matchesRace = record.Race == race;
                        bool matchesAnyRace = record.Race == 255;
                        bool matchesGender = (record.Gender == 'F' && gender == 1) || (record.Gender == 'M' && gender == 0);
                        bool matchesAnyGender = record.Gender == 'X';

                        bool valid = true;
                        switch (retries)
                        {
                            case 3:
                                // Pick exact match if found
                                valid = matchesRace && matchesGender;
                                break;
                            case 2:
                                // Otherwise, pick fallback if found
                                valid = matchesAnyRace && matchesAnyGender;
                                break;
                            case 1:
                                // Try to pick something reasonable
                                valid = matchesRace || matchesGender;
                                break;
                            case 0:
                                // Pick first result
                                valid = true;
                                break;
                        }

                        if (valid)
                        {
                            if (retries < 2)
                            {
                                Log.Warning("Picking uncertain Battleframe VisualRecord {recordId} of group {visualGroup} for chassi {chassiId}.", record.VisualrecId, battleframeRecord.VisualGroup, chassis.SdbId);
                            }

                            Log.Debug("Selected Battleframe VisualRecord {recordId} of group {visualGroup} for chassi {chassiId} (Had Gender {genderChar}, Race {raceId} ({raceStr}))", record.VisualrecId, battleframeRecord.VisualGroup, chassis.SdbId, gender == 1 ? "F" : "M", race, (CharacterRace)race);

                            battleframeVisualGroupRecord = record;
                            break;
                        }
                    }

                    retries--;
                }
                while (battleframeVisualRecord == null && retries > 0);

                battleframeVisualRecord = SDBInterface.GetVisualRecord(battleframeVisualGroupRecord.VisualrecId);
            }
            catch
            {
                Log.Error("Failed to get pose or visualrecord for chassi {chassiId}", chassis.SdbId);
                throw;
            }

            // We should have the data now since we survived
            Log.Debug(
                "ApplyLoadout Collision Debug | CharInfo: {id} ({name}) | RequiresRagdoll: {requiresRagdoll} | ChassisId: {chassisId} | PoseType: {poseId} | Physics: (R={radius}, H={height}, M={mass}) | VisualGroup: {visualGroup} | VisualRecord: {visualRecord} | StandingCollisionId: {standingCollisionId} | HitboxCollisionId: {hitboxCollisionId} | RagdollCollisionId: {ragdollCollisionId}",
                charInfo.Id,
                charInfo.Name,
                charInfo.RequiresRagdoll,
                chassis.SdbId,
                poseTypeRecord.PoseId,
                poseTypeRecord.PhysicsRadius,
                poseTypeRecord.PhysicsHeight,
                poseTypeRecord.PhysicsMass,
                battleframeRecord.VisualGroup,
                battleframeVisualRecord.Id,
                poseTypeRecord.StandingCollisionid,
                battleframeVisualRecord.HitboxCollisionId,
                battleframeVisualRecord.RagdollCollisionId);

            // Scale
            // max_rand_scale, min_rand_scale
            if (battleframeRecord.MinRandScale != battleframeRecord.MaxRandScale)
            {
                Log.Warning("Wtf battleframe {battleframe} has random scale: min: {min}, max: {max}", battleframeRecord.Id, battleframeRecord.MinRandScale, battleframeRecord.MaxRandScale);
            }

            Collision = new CharacterCollisionComponent
            {
                RequiresRagdoll = charInfo.RequiresRagdoll == 1,
                PoseTypeRecord = poseTypeRecord,
                RagdollCollisionId = battleframeVisualRecord.RagdollCollisionId,
                HitboxCollisionId = battleframeVisualRecord.HitboxCollisionId,
                Scale = battleframeRecord.MinRandScale,
            };
        }
    }

    public float GetItemAttribute(ushort id) => CurrentLoadout.ItemAttributes.GetValueOrDefault(id);

    public void AddStatModifier(uint reference, ActiveStatModifier mod)
    {
        CurrentStatModifiers[mod.Stat][reference] = mod;
        RefreshStatModifier(mod.Stat);
    }

    public void RemoveStatModifier(uint reference, StatModifierIdentifier stat)
    {
        if (CurrentStatModifiers[stat].ContainsKey(reference))
        {
            CurrentStatModifiers[stat].Remove(reference);
            RefreshStatModifier(stat);
        }
    }

    public void RefreshStatModifier(StatModifierIdentifier stat)
    {
        if (Character_CombatController != null)
        {
            StatMultiplierData value = new()
            {
                Value = GetCurrentStatModifierValue(stat),
                Time = Shard.CurrentTime,
            };

            Serilog.Log.Information($"StatModifier {stat} set to {value.Value}");

            switch (stat)
            {
                case StatModifierIdentifier.RunSpeedMult:
                    Character_CombatController.RunSpeedMultProp = value;
                    break;
                case StatModifierIdentifier.FireRateModifier:
                    Character_CombatController.FireRateModifierProp = value;
                    break;
                case StatModifierIdentifier.FwdRunSpeedMult:
                    Character_CombatController.FwdRunSpeedMultProp = value;
                    break;
                case StatModifierIdentifier.JumpHeightMult:
                    Character_CombatController.JumpHeightMultProp = value;
                    break;
                case StatModifierIdentifier.AirControlMult:
                    Character_CombatController.AirControlMultProp = value;
                    break;
                case StatModifierIdentifier.ThrustStrengthMult:
                    Character_CombatController.ThrustStrengthMultProp = value;
                    break;
                case StatModifierIdentifier.ThrustAirControl:
                    Character_CombatController.ThrustAirControlProp = value;
                    break;
                case StatModifierIdentifier.Friction:
                    Character_CombatController.FrictionProp = value;
                    break;
                case StatModifierIdentifier.AmmoConsumption:
                    Character_CombatController.AmmoConsumptionProp = value;
                    break;
                case StatModifierIdentifier.MaxTurnRate:
                    Character_CombatController.MaxTurnRateProp = value;
                    break;
                case StatModifierIdentifier.TurnSpeed:
                    Character_CombatController.TurnSpeedProp = value;
                    break;
                case StatModifierIdentifier.TimeDilation:
                    Character_CombatController.TimeDilationProp = value;
                    break;
                case StatModifierIdentifier.AccuracyModifier:
                    Character_CombatController.AccuracyModifierProp = value;
                    break;
                case StatModifierIdentifier.GravityMult:
                    Character_CombatController.GravityMultProp = value;
                    break;
                case StatModifierIdentifier.AirResistanceMult:
                    Character_CombatController.AirResistanceMultProp = value;
                    break;
                case StatModifierIdentifier.WeaponChargeupMod:
                    Character_CombatController.WeaponChargeupModProp = value;
                    break;
                case StatModifierIdentifier.WeaponDamageDealtMod:
                    Character_CombatController.WeaponDamageDealtModProp = value;
                    break;
            }
        }
    }

    public float GetCurrentStatModifierValue(StatModifierIdentifier stat)
    {
        float value = BaseStatModifiers.GetValueOrDefault(stat, GetDefaultBaseStatModifier(stat));

        foreach (ActiveStatModifier mod in CurrentStatModifiers[stat].Values)
        {
            if (mod.Op == 1) // ADD
            {
                value += mod.Value;
            }
            else if (mod.Op == 2) // MULTIPLY
            {
                value = (value * mod.Value) / 100;
            }
            else if (mod.Op == 0) // ASSIGN
            {
                value = mod.Value;
            }
            else if (mod.Op == 4) // SUBTRACT
            {
                value -= mod.Value;
            }
            else if (mod.Op == 5) // DIVIDE
            {
                value = value / mod.Value;
            }
            else
            {
                Serilog.Log.Information($"GetCurrentStatModifierValue Unknown Op {mod.Op}");
            }
        }

        return value;
    }

    private static float GetDefaultBaseStatModifier(StatModifierIdentifier stat)
    {
        // Most networked stat modifiers behave as multipliers and default to 1.0.
        // MaxTurnRate is treated as additive and historically defaults to 0.0.
        return stat == StatModifierIdentifier.MaxTurnRate ? 0.0f : 1.0f;
    }

    public void SetCharacterStats(CharacterStatsData value)
    {
        CharacterStats = value;
        Character_EquipmentView.CharacterStatsProp = value;
        if (Character_BaseController != null)
        {
            Character_BaseController.CharacterStatsProp = value;
        }
    }

    public void SetStaticInfo(StaticInfoData value)
    {
        StaticInfo = value;
        Character_ObserverView.StaticInfoProp = StaticInfo;
        if (Character_BaseController != null)
        {
            Character_BaseController.StaticInfoProp = StaticInfo;
        }
    }

    public void SetTimePlayed(int value)
    {
        TimePlayed = value;
        if (Character_BaseController != null)
        {
            Character_BaseController.TimePlayedProp = TimePlayed;
        }
    }

    public void SetArmyGUID(ulong value)
    {
        ArmyGUID = value;
        Character_ObserverView.ArmyGUIDProp = ArmyGUID;
        if (Character_BaseController != null)
        {
            Character_BaseController.ArmyGUIDProp = ArmyGUID;
        }
    }

    public void SetArmyIsOfficer(sbyte value)
    {
        ArmyIsOfficer = value;
        if (Character_BaseController != null)
        {
            Character_BaseController.ArmyIsOfficerProp = ArmyIsOfficer;
        }
    }

    public void SetPvPRank(byte value)
    {
        PvPRank = value;
        if (Character_BaseController != null)
        {
            Character_BaseController.PvPRankProp = value;
        }

        if (Character_EquipmentView != null)
        {
            Character_EquipmentView.PvPRankProp = value;
        }
    }

    public void SetEliteLevel(byte value)
    {
        EliteLevel = value;
        if (Character_BaseController != null)
        {
            Character_BaseController.EliteLevelProp = value;
        }

        if (Character_EquipmentView != null)
        {
            Character_EquipmentView.EliteLevelProp = value;
        }
    }

    public void SetLevel(byte value)
    {
        Level = value;
        if (Character_BaseController != null)
        {
            Character_BaseController.LevelProp = value;
        }

        if (Character_EquipmentView != null)
        {
            Character_EquipmentView.LevelProp = value;
        }

        RefreshStats();

        if (IsPlayerControlled && Player?.Inventory != null)
        {
            Player.Inventory.SendCertificateUnlocksUpdate();
        }
    }

    public void SetEffectiveLevel(byte value)
    {
        EffectiveLevel = value;
        if (Character_BaseController != null)
        {
            Character_BaseController.EffectiveLevelProp = value;
        }
    }

    public void SetVipLevel(uint value)
    {
        VipLevel = value;
        if (Character_BaseController != null)
        {
            var loyalty = Character_BaseController.LoyaltyProp;
            loyalty.Tier = value;
            Character_BaseController.LoyaltyProp = loyalty;
        }
    }

    public void SetStaffFlags(byte value)
    {
        var staticInfo = StaticInfo;
        staticInfo.StaffFlags = value;
        SetStaticInfo(staticInfo);
    }

    public void SetCurrentEquipment(EquipmentData value)
    {
        CurrentEquipment = value;
        Character_EquipmentView.CurrentEquipmentProp = CurrentEquipment;
        if (Character_BaseController != null)
        {
            Character_BaseController.CurrentEquipmentProp = CurrentEquipment;
        }
    }

    public void SetVisualOverrides(VisualOverridesField value)
    {
        VisualOverrides = value;
        Character_EquipmentView.VisualOverridesProp = value;
        if (Character_BaseController != null)
        {
            Character_BaseController.VisualOverridesProp = value;
        }
    }

    public void SetAimDirection(Vector3 newDirection)
    {
        AimDirection = newDirection;
        RefreshMovementView();
    }

    public void SetCharacterState(CharacterStateData.CharacterStatus characterStatus, uint time)
    {
        var previousCharacterState = CharacterState.State;

        CharacterState = new CharacterStateData
        {
            State = characterStatus,
            Time = time
        };
        Character_ObserverView.CharacterStateProp = CharacterState;
        if (Character_BaseController != null)
        {
            Character_BaseController.CharacterStateProp = CharacterState;
        }

        if (previousCharacterState != characterStatus && IsRecoveryTraceActive())
        {
            TraceRecoveryState($"character state changed {previousCharacterState} -> {characterStatus}");
        }
    }

    public void SetControllingPlayer(INetworkPlayer player)
    {
        Player = player;
        InitControllers();
    }

    public void SetEffectsFlags(byte value)
    {
        EffectsFlags = value;
        Character_ObserverView.EffectsFlagsProp = EffectsFlags;
    }

    public void SetEmote(EmoteData value)
    {
        Emote = value;
        Character_ObserverView.EmoteIDProp = value;
        if (Character_BaseController != null)
        {
            Character_BaseController.EmoteIDProp = value;
        }
    }

    public void SetCinematicCamera(CinematicCameraData? value)
    {
        CinematicCamera = value;
        if (Character_BaseController != null)
        {
            Character_BaseController.CinematicCameraProp = value;
        }
    }

    public void SetFireBurst(uint time)
    {
        Character_CombatView.WeaponBurstFiredProp = time;
    }

    public void SetFireCancel(uint time)
    {
        Character_CombatView.WeaponBurstCancelledProp = time;
    }

    public void SetFireEnd(uint time)
    {
        Character_CombatView.WeaponBurstEndedProp = time;
    }

    public void SetFireMode(byte index, FireModeData value)
    {
        switch (index)
        {
            case 0:
                FireMode_0 = value;
                Character_CombatView.FireMode_0Prop = FireMode_0;
                if (Character_CombatController != null)
                {
                    Character_CombatController.FireMode_0Prop = FireMode_0;
                }

                break;
            case 1:
                FireMode_1 = value;
                Character_CombatView.FireMode_1Prop = FireMode_1;
                if (Character_CombatController != null)
                {
                    Character_CombatController.FireMode_1Prop = FireMode_1;
                }

                break;
        }
    }

    public void SetPoseData(MovementPoseData poseData, ushort shortTime)
    {
        Position = poseData.PosRotState.Pos;
        Orientation = poseData.PosRotState.Rot;
        MovementState = poseData.PosRotState.MovementState;
        Velocity = poseData.Velocity;
        AimDirection = poseData.Aim;
        MovementShortTime = shortTime;
        RefreshMovementView();
    }

    public void SetPosition(Vector3 newPosition)
    {
        Position = newPosition;
        RefreshMovementView();
    }

    public void SetWeaponReloaded(uint time)
    {
        Character_CombatView.WeaponReloadedProp = time;
    }

    public void SetWeaponReloadCancelled(uint time)
    {
        Character_CombatView.WeaponReloadCancelledProp = time;
    }

    public void SetOrientation(Quaternion newOrientation)
    {
        Orientation = newOrientation;
        RefreshMovementView();
    }

    public void PositionAtSpawnPoint(SpawnPoint spawnPoint)
    {
        Position = spawnPoint.Position;
        Orientation = spawnPoint.Orientation;
        AimDirection = spawnPoint.AimDirection;
        RefreshMovementView();
    }

    public void SetSpawnPose()
    {
        SpawnPose = new CharacterSpawnPose
        {
            Time = Shard.CurrentTime,
            Position = Position,
            Rotation = Orientation,
            AimDirection = AimDirection,
            Velocity = Velocity,
            MovementState = 0x1000,
            Unk1 = 0,
            Unk2 = 0,
            JetpackEnergy = 0x639c,
            AirGroundTimer = 0,
            JumpTimer = 0,
            HaveDebugData = 0
        };
        Character_ObserverView.SpawnTimeProp = Shard.CurrentTime;
        if (Character_BaseController != null)
        {
            Character_BaseController.SpawnPoseProp = SpawnPose;
            Character_BaseController.SpawnTimeProp = Shard.CurrentTime;
        }
    }

    public void SetSpawnTime(uint time)
    {
        Character_ObserverView.SpawnTimeProp = time;
        if (Character_BaseController != null)
        {
            Character_BaseController.SpawnTimeProp = time;
        }
    }

    public void SetNpcType(ushort npcType)
    {
        Character_ObserverView.NPCTypeProp = npcType;
    }

    public void SetWeaponIndex(WeaponIndexData value)
    {
        WeaponIndex = value;
        Character_CombatView.WeaponIndexProp = value;

        if (Character_CombatController != null)
        {
            Character_CombatController.WeaponIndexProp = value;
        }
    }

    public void SetPermissionFlag(PermissionFlagsData.CharacterPermissionFlags flag, bool value)
    {
        var previousValue = CurrentPermissions.GetValueOrDefault(flag);
        CurrentPermissions[flag] = value;
        PermissionFlags = new PermissionFlagsData
        {
            Time = Shard.CurrentTime,
            Value = (PermissionFlagsData.CharacterPermissionFlags)GetCurrentPermissionsValue(),
        };

        if (Character_CombatController != null)
        {
            Character_CombatController.PermissionFlagsProp = PermissionFlags;
        }

        if (previousValue != value && IsRecoveryTraceActive())
        {
            TraceRecoveryState($"permission {flag} changed {previousValue} -> {value}");
        }
    }

    public void SetGliderProfileId(uint profileId)
    {
        if (Character_CombatController != null)
        {
            Character_CombatController.GliderProfileIdProp = profileId;
        }
    }

    public void SetHoverProfileId(uint profileId)
    {
        if (Character_CombatController != null)
        {
            Character_CombatController.HoverProfileIdProp = profileId;
        }
    }

    public bool HasRegisteredMovementEffects()
    {
        return RegisteredMovementEffects.Count > 0;
    }

    public string DescribeMovementTransitionDebugState()
    {
        return string.Join(", ",
            $"rawState=0x{MovementStateContainer.MovementStateValue:X4}",
            $"movestate={MovementStateContainer.Movestate}",
            $"airborne={IsAirborne}",
            $"forcedMoveEnd={ForcedMovementEndTime}",
            $"perm.glider={CurrentPermissions.GetValueOrDefault(PermissionFlagsData.CharacterPermissionFlags.glider)}",
            $"perm.gliderHud={CurrentPermissions.GetValueOrDefault(PermissionFlagsData.CharacterPermissionFlags.glider_hud)}",
            $"perm.jetpack={CurrentPermissions.GetValueOrDefault(PermissionFlagsData.CharacterPermissionFlags.jetpack)}",
            $"registered={FormatRegisteredMovementEffects()}",
            $"applied={FormatStatusEffectIds(AppliedMovementEffectStatusIds)}");
    }

    public void SetAuthorizedTerminal(AuthorizedTerminalData value)
    {
        AuthorizedTerminal = value;

        if (Character_BaseController != null)
        {
            Character_BaseController.AuthorizedTerminalProp = AuthorizedTerminal;
        }
    }

    public override void SetStatusEffect(byte index, ushort time, StatusEffectData data)
    {
        Serilog.Log.Information($"Character.SetStatusEffect Index {index}, Time {time}, Id {data.Id}");

        // Member
        GetType().GetProperty($"StatusEffectsChangeTime_{index}").SetValue(this, time, null);
        GetType().GetProperty($"StatusEffects_{index}").SetValue(this, data, null);

        // CombatController
        if (Character_CombatController != null)
        {
            Character_CombatController.GetType().GetProperty($"StatusEffectsChangeTime_{index}Prop").SetValue(Character_CombatController, time, null);
            Character_CombatController.GetType().GetProperty($"StatusEffects_{index}Prop").SetValue(Character_CombatController, data, null);
        }

        // CombatView
        Character_CombatView.GetType().GetProperty($"StatusEffectsChangeTime_{index}Prop").SetValue(Character_CombatView, time, null);
        Character_CombatView.GetType().GetProperty($"StatusEffects_{index}Prop").SetValue(Character_CombatView, data, null);

        TraceRecoveryEffect("set", index, time, data.Id);
    }

    public override void ClearStatusEffect(byte index, ushort time, uint debugEffectId)
    {
        Serilog.Log.Information($"Character.ClearStatusEffect Index {index}, Time {time}, Id {debugEffectId}");

        // Member
        GetType().GetProperty($"StatusEffectsChangeTime_{index}").SetValue(this, time, null);
        GetType().GetProperty($"StatusEffects_{index}").SetValue(this, null, null);

        // CombatController
        if (Character_CombatController != null)
        {
            Character_CombatController.GetType().GetProperty($"StatusEffectsChangeTime_{index}Prop").SetValue(Character_CombatController, time, null);
            Character_CombatController.GetType().GetProperty($"StatusEffects_{index}Prop").SetValue(Character_CombatController, null, null);
        }

        // CombatView
        Character_CombatView.GetType().GetProperty($"StatusEffectsChangeTime_{index}Prop").SetValue(Character_CombatView, time, null);
        Character_CombatView.GetType().GetProperty($"StatusEffects_{index}Prop").SetValue(Character_CombatView, null, null);

        TraceRecoveryEffect("clear", index, time, debugEffectId);
    }

    public void RegisterMovementEffect(RegisterMovementEffectCommandActiveContext registration)
    {
        RegisteredMovementEffects.RemoveAll(existing => ReferenceEquals(existing, registration));
        RegisteredMovementEffects.Add(registration);
        SyncMovementEffectStatusEffects($"register command={registration.CommandId} statusfx={registration.StatusEffectId} movestate={registration.Movestate}");
    }

    public void UnregisterMovementEffect(RegisterMovementEffectCommandActiveContext registration)
    {
        RegisteredMovementEffects.RemoveAll(existing => ReferenceEquals(existing, registration));
        SyncMovementEffectStatusEffects($"unregister command={registration.CommandId} statusfx={registration.StatusEffectId} movestate={registration.Movestate}");
    }

    public void SyncMovementEffectStatusEffects(string reason = null)
    {
        var currentMovestate = MovementStateContainer.Movestate;
        var desiredStatusIds = RegisteredMovementEffects
            .Where(registration => registration.Movestate == currentMovestate)
            .Select(registration => registration.StatusEffectId)
            .Distinct()
            .ToHashSet();

        Serilog.Log.Debug(
            "[MovementEffectSync] Entity {Entity}, reason={Reason}, currentMovestate={CurrentMovestate}, registered={Registered}, desired={Desired}, applied={Applied}",
            this,
            reason ?? "unspecified",
            currentMovestate,
            FormatRegisteredMovementEffects(),
            FormatStatusEffectIds(desiredStatusIds),
            FormatStatusEffectIds(AppliedMovementEffectStatusIds));

        foreach (var effectId in AppliedMovementEffectStatusIds.ToList())
        {
            if (!desiredStatusIds.Contains(effectId))
            {
                Serilog.Log.Debug("[MovementEffectSync] Entity {Entity} removing statusfx {StatusEffectId} because movestate {CurrentMovestate} no longer requires it", this, effectId, currentMovestate);
                Shard.Abilities.DoRemoveEffect(this, effectId);
                AppliedMovementEffectStatusIds.Remove(effectId);
            }
        }

        foreach (var effectId in desiredStatusIds)
        {
            if (HasActiveEffect(effectId))
            {
                Serilog.Log.Debug("[MovementEffectSync] Entity {Entity} keeping statusfx {StatusEffectId} active for movestate {CurrentMovestate}", this, effectId, currentMovestate);
                continue;
            }

            var registration = RegisteredMovementEffects.FirstOrDefault(active => active.Movestate == currentMovestate && active.StatusEffectId == effectId);
            if (registration == null)
            {
                Serilog.Log.Warning("[MovementEffectSync] Entity {Entity} missing registration context for desired statusfx {StatusEffectId} at movestate {CurrentMovestate}", this, effectId, currentMovestate);
                continue;
            }

            Serilog.Log.Debug("[MovementEffectSync] Entity {Entity} applying statusfx {StatusEffectId} for movestate {CurrentMovestate} via command {CommandId}", this, effectId, currentMovestate, registration.CommandId);
            Shard.Abilities.DoApplyEffect(effectId, this, registration.TemplateContext);
            AppliedMovementEffectStatusIds.Add(effectId);
        }

        foreach (var effectId in AppliedMovementEffectStatusIds.ToList())
        {
            if (!HasActiveEffect(effectId))
            {
                Serilog.Log.Debug("[MovementEffectSync] Entity {Entity} observed statusfx {StatusEffectId} is no longer active after sync", this, effectId);
                AppliedMovementEffectStatusIds.Remove(effectId);
            }
        }
    }

    public bool IsRecoveryTraceActive()
    {
        return Shard.CurrentTime <= RecoveryTraceEndTime;
    }

    public void TraceRecoveryState(string reason)
    {
        if (!IsRecoveryTraceActive())
        {
            return;
        }

        Serilog.Log.Debug($"[RECOVERY] Time {Shard.CurrentTime}, Entity {this}, Move {MovementStateContainer.Movestate}, CState {CharacterState.State}, Airborne {IsAirborne}, ForcedMoveEnd {ForcedMovementEndTime}, Perms movement={CurrentPermissions[PermissionFlagsData.CharacterPermissionFlags.movement]}, abilities={CurrentPermissions[PermissionFlagsData.CharacterPermissionFlags.abilities]}, jump={CurrentPermissions[PermissionFlagsData.CharacterPermissionFlags.jump]}, sprint={CurrentPermissions[PermissionFlagsData.CharacterPermissionFlags.sprint]} :: {reason}");
    }

    private static bool IsRecoveryTraceEffect(uint effectId)
    {
        return effectId == RecoveryTracePrimaryEffectId || effectId == RecoveryTraceFollowupEffectId;
    }

    private void ExtendRecoveryTraceWindow(uint durationMs)
    {
        ulong nextEndTime = Shard.CurrentTime + durationMs;
        if (nextEndTime > RecoveryTraceEndTime)
        {
            RecoveryTraceEndTime = nextEndTime;
        }
    }

    private void TraceRecoveryEffect(string action, byte index, ushort time, uint effectId)
    {
        if (!IsRecoveryTraceEffect(effectId))
        {
            return;
        }

        ExtendRecoveryTraceWindow(effectId == RecoveryTracePrimaryEffectId ? 12000u : RecoveryTraceWindowMs);
        TraceRecoveryState($"{action} status effect {effectId} at index {index}, shortTime {time}");
    }

    private bool HasActiveEffect(uint effectId)
    {
        return GetActiveEffects().Any(activeEffect => activeEffect?.Effect?.Id == effectId);
    }

    private string FormatRegisteredMovementEffects()
    {
        if (RegisteredMovementEffects.Count == 0)
        {
            return "none";
        }

        return string.Join(", ",
            RegisteredMovementEffects.Select(registration => $"cmd={registration.CommandId}:statusfx={registration.StatusEffectId}@{registration.Movestate}"));
    }

    private static string FormatStatusEffectIds(IEnumerable<uint> effectIds)
    {
        var ids = effectIds.Distinct().OrderBy(id => id).ToArray();
        return ids.Length == 0 ? "none" : string.Join(",", ids);
    }

    public void SetAttachedTo(AttachedToData newValue, IEntity entity, uint pose, Vector3 poseOffset)
    {
        AttachedToEntity = entity;
        AttachedTo = newValue;
        Collision.AttachmentPoseId = pose;
        Collision.AttachmentPoseOffset = poseOffset;
        Character_ObserverView.AttachedToProp = AttachedTo;
        if (Character_BaseController != null)
        {
            Character_BaseController.AttachedToProp = AttachedTo;
        }
    }

    public void ClearAttachedTo()
    {
        AttachedToEntity = null;
        AttachedTo = null;
        Collision.AttachmentPoseId = 0;
        Collision.AttachmentPoseOffset = Vector3.Zero;
        Character_ObserverView.AttachedToProp = AttachedTo;
        Character_ObserverView.SnapMountProp = 0;
        if (Character_BaseController != null)
        {
            Character_BaseController.AttachedToProp = AttachedTo;
            Character_BaseController.SnapMountProp = 0;
        }
    }

    public void HackClearAllStatusEffects()
    {
        var time = Shard.CurrentShortTime;
        for (int index = 0; index < 32; index++)
        {
            Serilog.Log.Debug($"Character.ClearStatusEffect Index {index}, Time {time}");

            // Member
            GetType().GetProperty($"StatusEffectsChangeTime_{index}").SetValue(this, time, null);
            GetType().GetProperty($"StatusEffects_{index}").SetValue(this, null, null);

            // CombatController
            if (Character_CombatController != null)
            {
                Character_CombatController.GetType().GetProperty($"StatusEffectsChangeTime_{index}Prop").SetValue(Character_CombatController, time, null);
                Character_CombatController.GetType().GetProperty($"StatusEffects_{index}Prop").SetValue(Character_CombatController, null, null);
            }

            // CombatView
            Character_CombatView.GetType().GetProperty($"StatusEffectsChangeTime_{index}Prop").SetValue(Character_CombatView, time, null);
            Character_CombatView.GetType().GetProperty($"StatusEffects_{index}Prop").SetValue(Character_CombatView, null, null);
        }

        Shard.EntityMan.FlushChanges(this);
    }

    public void AddMapMarker(ulong encounterId, PersonalMapMarkerData data)
    {
        byte firstFreeIndex = InvalidIndex;
        for (byte i = 0; i < MaxMapMarkerCount; i++)
        {
            if (MapMarkers[i] == null)
            {
                firstFreeIndex = i;
                break;
            }
        }

        if (firstFreeIndex == InvalidIndex)
        {
            Serilog.Log.Information("AddMapMarkers but there are too many active map markers!");
            firstFreeIndex = MaxMapMarkerCount - 1; // Lets not crash
        }

        var state = new MapMarkerState
        {
            EncounterId = data.EncounterId,
            EncounterMarkerId = data.EncounterMarkerId,
        };

        MapMarkers[firstFreeIndex] = state;

        SetMapMarker(firstFreeIndex, data);
    }

    public void RemoveEncounterMapMarkers(ulong encounterId)
    {
        for (byte i = 0; i < MapMarkers.Length; i++)
        {
            if (MapMarkers[i] == null)
            {
                continue;
            }

            if (MapMarkers[i].EncounterId.Backing == encounterId)
            {
                SetMapMarker(i, null);
            }
        }
    }

    public void SetCombatFlags(CombatFlagsData value)
    {
        Serilog.Log.Debug("Character {Character} SetCombatFlags value={Flags} time={Time}", this, value.Value, value.Time);
        Character_CombatController.CombatFlagsProp = value;
        Character_CombatView.CombatFlagsProp = value;
    }

    public void EquipItemByGUID(int loadoutId, LoadoutSlotType slot, ulong guid)
    {
        Player.Inventory.EquipItemByGUID(loadoutId, slot, guid);
        RefreshAppliedLoadoutFromInventory(loadoutId);
    }

    public void EquipVisualBySdbId(int loadoutId, LoadoutVisualType visualSlot, LoadoutSlotType slot, uint sdb_id)
    {
        Player.Inventory.EquipVisualBySdbId(loadoutId, visualSlot, slot, sdb_id);
        RefreshAppliedLoadoutFromInventory(loadoutId);
    }

    private void RefreshAppliedLoadoutFromInventory(int loadoutId)
    {
        if ((CurrentLoadout?.LoadoutID ?? 0) != loadoutId && SelectedLoadout != loadoutId)
        {
            return;
        }

        var refData = Player?.Inventory?.GetLoadoutReferenceData(loadoutId);
        if (refData == null)
        {
            return;
        }

        ApplyLoadout(new CharacterLoadout(refData));
    }

#nullable enable

    // TODO: cache this
    public ActiveWeaponDetails? GetActiveWeaponDetails()
    {
        // Weapon
        uint weaponId;
        StatsData[] weaponAttributes;
        switch (WeaponIndex.Index)
        {
            case 2:
                weaponId = CurrentLoadout.SlottedItems.GetValueOrDefault(LoadoutSlotType.Secondary);
                weaponAttributes = CurrentLoadout.GetSecondaryWeaponAttributes();
                break;
            case 1:
                weaponId = CurrentLoadout.SlottedItems.GetValueOrDefault(LoadoutSlotType.Primary);
                weaponAttributes = CurrentLoadout.GetPrimaryWeaponAttributes();
                break;
            case 0:
            default:
                // Serilog.Log.Information($"GetActiveWeaponDetails fails because invalid selected weapon index {WeaponIndex.Index}");
                return null;
        }

        if (weaponId == 0)
        {
            Serilog.Log.Information($"GetActiveWeaponDetails failed to get selected weapon id from loadout");
            return null;
        }

        var weaponDetails = SDBUtils.GetDetailedWeaponInfo(weaponId);
        var weapon = weaponDetails.Main;
        if (weaponDetails.Alt != null && (FireMode_0.Mode != 0 || FireMode_1.Mode != 0))
        {
            weapon = weaponDetails.Alt;
        }

        var weaponAttributesDict = weaponAttributes.ToDictionary((StatsData p) => p.Id);

        float weaponAttributeSpread = 1f;
        float weaponAttributeRateOfFire = 1f;

        if (weaponAttributesDict.TryGetValue((ushort)ItemAttributeId.WeaponSpread, out var spreadAttr))
        {
            weaponAttributeSpread = spreadAttr.Value;
        }

        if (weaponAttributesDict.TryGetValue((ushort)ItemAttributeId.RateOfFire, out var rofAttr))
        {
            weaponAttributeRateOfFire = rofAttr.Value;
        }

        // Calculate spread factor using Main even for Underbarrel, based on testing in-game.
        // Bio Crossbow - Max spread 0, min spread 0.75, attribute spread 1, expected spread 0.75 => Ignore max spread if 0 and use attribute spread
        float spreadFactor = weaponDetails.Main.MaxSpread > 0f ? weaponAttributeSpread / weaponDetails.Main.MaxSpread : weaponAttributeSpread;

        return new ActiveWeaponDetails()
        {
            Weapon = weapon,
            WeaponId = weaponId,
            AmmoId = AmmoOverride != 0 ? AmmoOverride : weapon.AmmoId,
            Spread = spreadFactor,
            RateOfFire = weaponAttributeRateOfFire,
        };
    }
#nullable disable

    public Vector3 GetProjectileOrigin()
    {
        return GetProjectileOrigin(AimDirection);
    }

    public Vector3 GetProjectileOrigin(Vector3 aimDirection)
    {
        var muzzleBase = new Vector3(0.2f, 0.0f, 1.62f); // TODO: Should probably vary by character
        if (IsCrouching)
        {
            muzzleBase.Z = 1.08f;
        }

        var muzzleBaseWorld = QuaternionEx.Transform(muzzleBase, QuaternionEx.Inverse(Orientation)); // Match the characters orientation
        var muzzleOffset = new Vector3(aimDirection.X, aimDirection.Y, aimDirection.Z) * 0.1f; // Offset like a sphere based on aim
        var muzzleOffsetWorld = muzzleBaseWorld + muzzleOffset; // Apply offset to base in world
        var origin = Position + muzzleOffsetWorld; // Translate to character
        return origin;
    }

    public void SetHostilityInfo(HostilityInfoData newValue)
    {
        HostilityInfo = newValue;
        Character_ObserverView?.HostilityInfoProp = HostilityInfo;
        Character_BaseController?.HostilityInfoProp = HostilityInfo;
    }

    private void InitFields()
    {
        Position = new Vector3();
        Orientation = Quaternion.Identity;
        Velocity = new Vector3();
        AimDirection = new Vector3(0.70707911253f, 0.707134246826f, 0.000504541851114f); // Look kinda forward instead of up
        MovementState = 0x1000;
        MovementShortTime = Shard.CurrentShortTime;

        Alive = false;
        TimeSinceLastJump = 0;
        IsAirborne = false;

        StaticInfo = new StaticInfoData();
        CharacterState = new CharacterStateData { State = CharacterStateData.CharacterStatus.Living, Time = Shard.CurrentTime };
        HostilityInfo = new HostilityInfoData { Flags = 0 | HostilityInfoData.HostilityFlags.Faction, FactionId = 1 };
        MaxShields = new MaxVital { Value = 0, Time = Shard.CurrentTime };
        MaxHealth = new MaxVital { Value = 0, Time = Shard.CurrentTime };
        GibVisualsInfo = new GibVisuals { Id = 0, Time = Shard.CurrentTime };
        ProcessDelay = new ProcessDelayData { Unk1 = 30721, Unk2 = 236 };
        Emote = new EmoteData { Id = 0, Time = 0 };
        DockedParams = new DockedParamsData { Unk1 = new EntityId { Backing = 0 }, Unk2 = Vector3.Zero, Unk3 = 0 };
        AssetOverrides = new AssetOverridesField { Ids = Array.Empty<uint>() };
        VisualOverrides = new VisualOverridesField { Data = Array.Empty<VisualOverridesData>() };
        CurrentEquipment = new EquipmentData { };
        CharacterStats = new CharacterStatsData
        {
            ItemAttributes = Array.Empty<StatsData>(),
            Unk1 = 0,
            WeaponA = Array.Empty<StatsData>(),
            Unk2 = 0,
            WeaponB = Array.Empty<StatsData>(),
            Unk3 = 0,
            AttributeCategories1 = Array.Empty<StatsData>(),
            AttributeCategories2 = Array.Empty<StatsData>()
        };

        EnergyParams = new EnergyParamsData { Max = 1000.0f, Delay = 500, Recharge = 156.0f, Time = Shard.CurrentTime };
        ScopeBubble = new ScopeBubbleInfoData { Layer = 0, Unk2 = 0 };
        SpawnPose = new CharacterSpawnPose
        {
            Time = Shard.CurrentTime,
            Position = Position,
            Rotation = Orientation,
            AimDirection = AimDirection,
            Velocity = Velocity,
            MovementState = 0x1000,
            Unk1 = 0,
            Unk2 = 0,
            JetpackEnergy = 0x639c,
            AirGroundTimer = 0,
            JumpTimer = 0,
            HaveDebugData = 0
        };

        EffectsFlags = 0;
        FireMode_0 = new FireModeData { Mode = 0, Time = Shard.CurrentTime };
        FireMode_1 = new FireModeData { Mode = 0, Time = Shard.CurrentTime };
        WeaponIndex = new WeaponIndexData { Index = 1, Unk1 = 1, Unk2 = 0, Time = Shard.CurrentTime };

        PermissionFlags = new PermissionFlagsData
        {
            Time = Shard.CurrentTime,
            Value = (PermissionFlagsData.CharacterPermissionFlags)GetCurrentPermissionsValue(),
        };

        Level = 1;
        EffectiveLevel = 1;
        VipLevel = 0;
        PvPRank = 0;
        EliteLevel = 0;
        ArmyGUID = 0;
        ArmyIsOfficer = 0;
    }

    private void InitControllers()
    {
        Character_BaseController = new BaseController
        {
            TimePlayedProp = TimePlayed,
            CurrentWeightProp = 0,
            EncumberedWeightProp = 255,
            AuthorizedTerminalProp = AuthorizedTerminal,
            PingTimeProp = 0, // Shard.CurrentTime,
            StaticInfoProp = StaticInfo,
            SpawnTimeProp = Shard.CurrentTime,
            VisualOverridesProp = VisualOverrides,
            CurrentEquipmentProp = CurrentEquipment,
            SelectedLoadoutProp = SelectedLoadout,
            SelectedLoadoutIsPvPProp = 0,
            GibVisualsIdProp = GibVisualsInfo,
            SpawnPoseProp = SpawnPose,
            ProcessDelayProp = ProcessDelay,
            SpectatorModeProp = 0,
            CinematicCameraProp = CinematicCamera,
            CharacterStateProp = CharacterState,
            HostilityInfoProp = HostilityInfo,
            PersonalFactionStanceProp = null,
            CurrentHealthProp = 0,
            CurrentShieldsProp = 0,
            MaxShieldsProp = MaxShields,
            MaxHealthProp = MaxHealth,
            CurrentDurabilityPctProp = 100,
            EnergyParamsProp = EnergyParams,
            CharacterStatsProp = CharacterStats,
            EmoteIDProp = Emote,
            AttachedToProp = null,
            SnapMountProp = 0,
            SinFlagsProp = 0,
            SinFlagsPrivateProp = 0,
            SinFactionsAcquiredByProp = null,
            SinTeamsAcquiredByProp = null,
            ArmyGUIDProp = ArmyGUID,
            ArmyIsOfficerProp = ArmyIsOfficer,
            EncounterPartyTupleProp = null,
            DockedParamsProp = DockedParams,
            LookAtTargetProp = null,
            ZoneUnlocksProp = 0,
            RegionUnlocksProp = 0,
            ChatPartyLeaderIdProp = new EntityId { Backing = 0 },
            ScopeBubbleInfoProp = ScopeBubble,
            CarryableObjects_0Prop = null,
            CarryableObjects_1Prop = null,
            CarryableObjects_2Prop = null,
            CachedAssetsProp = null,
            RespawnTimesProp = null,
            ProgressionXpProp = 0,
            PermanentStatusEffectsProp = new PermanentStatusEffectsData { Effects = Array.Empty<PermanentStatusEffectsInnerData>() },
            XpBoostModifierProp = new StatModifierData { ModifierId = 0, StatValue = 0.0f },
            XpPermanentModifierProp = new StatModifierData { ModifierId = 0, StatValue = 0.0f },
            XpZoneModifierProp = new StatModifierData { ModifierId = 0, StatValue = 0.0f },
            XpVipModifierProp = new StatModifierData { ModifierId = 0, StatValue = 0.0f },
            XpEventModifierProp = new StatModifierData { ModifierId = 0, StatValue = 0.0f },
            ResourceBoostModifierProp = new StatModifierData { ModifierId = 0, StatValue = 0.0f },
            ResourcePermanentModifierProp = new StatModifierData { ModifierId = 0, StatValue = 0.0f },
            ResourceZoneModifierProp = new StatModifierData { ModifierId = 0, StatValue = 0.0f },
            ResourceVipModifierProp = new StatModifierData { ModifierId = 0, StatValue = 0.0f },
            ResourceEventModifierProp = new StatModifierData { ModifierId = 0, StatValue = 0.0f },
            MoneyBoostModifierProp = new StatModifierData { ModifierId = 0, StatValue = 0.0f },
            MoneyPermanentModifierProp = new StatModifierData { ModifierId = 0, StatValue = 0.0f },
            MoneyZoneModifierProp = new StatModifierData { ModifierId = 0, StatValue = 0.0f },
            MoneyVipModifierProp = new StatModifierData { ModifierId = 0, StatValue = 0.0f },
            MoneyEventModifierProp = new StatModifierData { ModifierId = 0, StatValue = 0.0f },
            ReputationBoostModifierProp = new StatModifierData { ModifierId = 0, StatValue = 0.0f },
            ReputationPermanentModifierProp = new StatModifierData { ModifierId = 0, StatValue = 0.0f },
            ReputationZoneModifierProp = new StatModifierData { ModifierId = 0, StatValue = 0.0f },
            ReputationVipModifierProp = new StatModifierData { ModifierId = 0, StatValue = 0.0f },
            ReputationEventModifierProp = new StatModifierData { ModifierId = 0, StatValue = 0.0f },
            WalletProp = new WalletData { Beans = 999, Epoch = 1462889864 },
            LoyaltyProp = new LoyaltyData { Current = 0, Lifetime = 0, Tier = VipLevel },
            LevelProp = Level,
            EffectiveLevelProp = EffectiveLevel,

            LevelResetCountProp = 0,
            OldestDeployablesProp = new OldestDeployablesField { Data = Array.Empty<OldestDeployablesData>() },
            PerkRespecsProp = 0,
            ArcStatusProp = null,
            LeaveZoneTimeProp = null,
            ChatMuteStatusProp = 0,
            TimedDailyRewardProp = new TimedDailyRewardData
            {
                Stage = 0,
                State = 0,
                RollNumber = 0,
                MaxRolls = 0,
                CountdownToTime = 0
            },
            TimedDailyRewardResultProp = null,
            SinCardTypeProp = 0,
            SinCardFields_0Prop = null,
            SinCardFields_1Prop = null,
            SinCardFields_2Prop = null,
            SinCardFields_3Prop = null,
            SinCardFields_4Prop = null,
            SinCardFields_5Prop = null,
            SinCardFields_6Prop = null,
            SinCardFields_7Prop = null,
            SinCardFields_8Prop = null,
            SinCardFields_9Prop = null,
            SinCardFields_10Prop = null,
            SinCardFields_11Prop = null,
            SinCardFields_12Prop = null,
            SinCardFields_13Prop = null,
            SinCardFields_14Prop = null,
            SinCardFields_15Prop = null,
            SinCardFields_16Prop = null,
            SinCardFields_17Prop = null,
            SinCardFields_18Prop = null,
            SinCardFields_19Prop = null,
            SinCardFields_20Prop = null,
            SinCardFields_21Prop = null,
            SinCardFields_22Prop = null,
            AssetOverridesProp = AssetOverrides,
            FriendCountProp = 0, // :'(
            CAISStatusProp = new CAISStatusData { State = CAISStatusData.CAISState.None, Elapsed = 0 },
            ScalingLevelProp = Level,
            PvPRankProp = PvPRank,
            PvPRankPointsProp = 0,
            PvPTokensProp = 0,
            BountyPointsLastClaimedProp = 0,
            EliteLevelProp = EliteLevel
        };

        Character_CombatController = new CombatController
        {
            RunSpeedMultProp = new StatMultiplierData { Value = 1.0f, Time = Shard.CurrentTime },
            FwdRunSpeedMultProp = new StatMultiplierData { Value = 1.0f, Time = Shard.CurrentTime },
            JumpHeightMultProp = new StatMultiplierData { Value = 1.0f, Time = Shard.CurrentTime },
            AirControlMultProp = new StatMultiplierData { Value = 1.0f, Time = Shard.CurrentTime },
            ThrustStrengthMultProp = new StatMultiplierData { Value = 1.0f, Time = Shard.CurrentTime },
            ThrustAirControlProp = new StatMultiplierData { Value = 1.0f, Time = Shard.CurrentTime },
            FrictionProp = new StatMultiplierData { Value = 1.0f, Time = Shard.CurrentTime },
            AmmoConsumptionProp = new StatMultiplierData { Value = 1.0f, Time = Shard.CurrentTime },
            MaxTurnRateProp = new StatMultiplierData { Value = 0f, Time = Shard.CurrentTime },
            TurnSpeedProp = new StatMultiplierData { Value = 1.0f, Time = Shard.CurrentTime },
            TimeDilationProp = new StatMultiplierData { Value = 1.0f, Time = Shard.CurrentTime },
            FireRateModifierProp = new StatMultiplierData { Value = 1.0f, Time = Shard.CurrentTime },
            AccuracyModifierProp = new StatMultiplierData { Value = 1.0f, Time = Shard.CurrentTime },
            GravityMultProp = new StatMultiplierData { Value = 1.0f, Time = Shard.CurrentTime },
            AirResistanceMultProp = new StatMultiplierData { Value = 1.0f, Time = Shard.CurrentTime },
            WeaponChargeupModProp = new StatMultiplierData { Value = 1.0f, Time = Shard.CurrentTime },
            WeaponDamageDealtModProp = new StatMultiplierData { Value = 1.0f, Time = Shard.CurrentTime },
            FireMode_0Prop = FireMode_0,
            FireMode_1Prop = FireMode_1,
            WeaponIndexProp = WeaponIndex,
            WeaponFireBaseTimeProp = new WeaponFireBaseTimeData { ChangeTime = 0, Unk = 0 },
            WeaponAgilityModProp = 1.0f,
            CombatFlagsProp = new CombatFlagsData { Value = 0, Time = Shard.CurrentTime },
            PermissionFlagsProp = PermissionFlags,
            NemesesProp = new NemesesData { Values = Array.Empty<ulong>() },
            SuperChargeProp = new SuperChargeData { Value = 100, Op = 0 }
        };
        Character_MissionAndMarkerController = new MissionAndMarkerController();
        Character_LocalEffectsController = new LocalEffectsController();
    }

    private void InitViews()
    {
        Character_ObserverView = new ObserverView
        {
            StaticInfoProp = StaticInfo,
            SpawnTimeProp = Shard.CurrentTime,
            EffectsFlagsProp = EffectsFlags,
            GibVisualsIDProp = GibVisualsInfo,
            ProcessDelayProp = ProcessDelay,
            CharacterStateProp = CharacterState,
            HostilityInfoProp = HostilityInfo,
            PersonalFactionStanceProp = null,
            CurrentHealthPctProp = 100,
            MaxHealthProp = MaxHealth,
            EmoteIDProp = Emote,
            AttachedToProp = null,
            SnapMountProp = 0,
            SinFlagsProp = 0,
            SinFactionsAcquiredByProp = null,
            SinTeamsAcquiredByProp = null,
            ArmyGUIDProp = 0,
            OwnerIdProp = Owner?.EntityId ?? 0,
            NPCTypeProp = 0,
            DockedParamsProp = DockedParams,
            LookAtTargetProp = null,
            WaterLevelAndDescProp = 0,
            CarryableObjects_0Prop = null,
            CarryableObjects_1Prop = null,
            CarryableObjects_2Prop = null,
            RespawnTimesProp = null,
            SinCardTypeProp = 0,
            SinCardFields_0Prop = null,
            SinCardFields_1Prop = null,
            SinCardFields_2Prop = null,
            SinCardFields_3Prop = null,
            SinCardFields_4Prop = null,
            SinCardFields_5Prop = null,
            SinCardFields_6Prop = null,
            SinCardFields_7Prop = null,
            SinCardFields_8Prop = null,
            SinCardFields_9Prop = null,
            SinCardFields_10Prop = null,
            SinCardFields_11Prop = null,
            SinCardFields_12Prop = null,
            SinCardFields_13Prop = null,
            SinCardFields_14Prop = null,
            SinCardFields_15Prop = null,
            SinCardFields_16Prop = null,
            SinCardFields_17Prop = null,
            SinCardFields_18Prop = null,
            SinCardFields_19Prop = null,
            SinCardFields_20Prop = null,
            SinCardFields_21Prop = null,
            SinCardFields_22Prop = null,
            AssetOverridesProp = AssetOverrides
        };
        Character_EquipmentView = new EquipmentView
        {
            VisualOverridesProp = VisualOverrides,
            CurrentEquipmentProp = CurrentEquipment,
            CurrentDurabilityPctProp = 100,
            CharacterStatsProp = CharacterStats,
            ScalingLevelProp = Level,
            PvPRankProp = PvPRank,
            EliteLevelProp = EliteLevel,
            LevelProp = Level
        };

        Character_CombatView = new CombatView
        {
            FireMode_0Prop = FireMode_0,
            FireMode_1Prop = FireMode_1,
            WeaponIndexProp = WeaponIndex,
            WeaponAgilityModProp = 1.0f,
            CombatFlagsProp = new CombatFlagsData { Value = 0, Time = Shard.CurrentTime },
            MimicParentProp = new EntityId { Backing = 0 },
            MimicOffsetProp = Vector3.Zero,

            ClipEmptyBeginProp = Shard.CurrentTime,
            ClipEmptyEndProp = Shard.CurrentTime,
            WeaponBurstFiredProp = Shard.CurrentTime,
            WeaponBurstEndedProp = Shard.CurrentTime,
            WeaponBurstCancelledProp = Shard.CurrentTime,
            WeaponReloadedProp = Shard.CurrentTime,
            WeaponReloadCancelledProp = Shard.CurrentTime,
            AbilityCooldownEndMs_0Prop = Shard.CurrentTime,
            AbilityCooldownEndMs_1Prop = Shard.CurrentTime,
            AbilityCooldownEndMs_2Prop = Shard.CurrentTime,
            AbilityCooldownEndMs_3Prop = Shard.CurrentTime,
            EquipmentLoadTimeProp = Shard.CurrentTime,
            Ammo_0Prop = 88,
            Ammo_1Prop = 88,
            AltAmmo_0Prop = 52,
            AltAmmo_1Prop = 52,
        };
        Character_MovementView = new MovementView
        {
            MovementProp = new AeroMessages.GSS.V66.Character.MovementData
            {
                Position = Position,
                Rotation = Orientation,
                Aim = AimDirection,
                MovementState = (ushort)MovementState,
                Time = Shard.CurrentTime
            }
        };
    }

    private void RefreshMovementView()
    {
        Character_MovementView.MovementProp = new MovementData
        {
            Position = Position,
            Rotation = Orientation,
            Aim = AimDirection,
            MovementState = (ushort)MovementState,
            Time = Shard.CurrentTime
        };
    }

    private void RefreshAllStatusEffects()
    {
        for (int i = 0; i < 32; i++)
        {
            var sourceTime = GetType().GetProperty($"StatusEffectsChangeTime_{i}").GetValue(this);
            var sourceData = GetType().GetProperty($"StatusEffects_{i}").GetValue(this);

            Character_CombatView.GetType().GetProperty($"StatusEffectsChangeTime_{i}Prop").SetValue(Character_CombatView, sourceTime, null);
            Character_CombatView.GetType().GetProperty($"StatusEffects_{i}Prop").SetValue(Character_CombatView, sourceData, null);

            if (Character_CombatController != null)
            {
                Character_CombatController.GetType().GetProperty($"StatusEffectsChangeTime_{i}Prop").SetValue(Character_CombatController, sourceTime, null);
                Character_CombatController.GetType().GetProperty($"StatusEffects_{i}Prop").SetValue(Character_CombatController, sourceData, null);
            }
        }
    }

    private ulong GetCurrentPermissionsValue()
    {
        ulong result = 0ul;
        foreach (var pair in CurrentPermissions)
        {
            if (pair.Value)
            {
                result += (ulong)pair.Key;
            }
        }

        return result;
    }

    private void SetMapMarker(byte index, PersonalMapMarkerData? data)
    {
        Character_MissionAndMarkerController?.GetType().GetProperty($"PersonalMapMarkers_{index}Prop")
                                            ?.SetValue(Character_MissionAndMarkerController, data);
    }

    public void RefreshStats()
    {
        if (CurrentLoadout == null)
        {
            return;
        }

        CurrentLoadout.Level = Level;
        CurrentLoadout.CalculateItemAttributes();

        if (CurrentLoadout.ItemAttributes.TryGetValue(6, out var maxHealth))
        {
            MaxHealth = new MaxVital { Value = (int)maxHealth, Time = Shard.CurrentTime };
            if (Character_BaseController != null)
            {
                Character_BaseController.MaxHealthProp = MaxHealth;

                // Clamp current health down when the new max is lower (e.g. after removing health-boosting items).
                int currentHealth = Character_BaseController.CurrentHealthProp;
                if (MaxHealth.Value > 0 && currentHealth > MaxHealth.Value)
                {
                    Character_BaseController.CurrentHealthProp = MaxHealth.Value;
                    Character_ObserverView.CurrentHealthPctProp = 100;
                    Shard.EntityMan.FlushChanges(this);
                }
            }
        }

        // Attribute 7 is Max Shields
        if (CurrentLoadout.ItemAttributes.TryGetValue(7, out var maxShields))
        {
            MaxShields = new MaxVital { Value = (int)maxShields, Time = Shard.CurrentTime };
            if (Character_BaseController != null)
            {
                Character_BaseController.MaxShieldsProp = MaxShields;
            }
        }

        // Load jetpack energy params from battleframe SDB
        var battleframe = SDBInterface.GetBattleframe(CurrentLoadout.ChassisID);
        if (battleframe != null && battleframe.BaseEnergy > 0)
        {
            EnergyParams = new EnergyParamsData
            {
                Max = battleframe.BaseEnergy,
                Delay = battleframe.EnergyRechargeDelayMs,
                Recharge = battleframe.EnergyRechargePerSec,
                Time = Shard.CurrentTime,
            };
            if (Character_BaseController != null)
            {
                Character_BaseController.EnergyParamsProp = EnergyParams;
            }
        }
    }

    public class ActiveStatModifier
    {
        public StatModifierIdentifier Stat { get; set; }
        public byte Op { get; set; }
        public float Value { get; set; }
    }

    public class ActiveWeaponDetails
    {
        public WeaponTemplateResult Weapon;
        public uint WeaponId;
        public uint AmmoId;
        public float Spread;
        public float RateOfFire;
    }
}