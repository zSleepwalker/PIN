using System;
using System.Collections.Generic;
using System.Linq;
using AeroMessages.GSS.V66.Character;
using AeroMessages.GSS.V66.Character.Event;
using GameServer.Aptitude;
using GameServer.Data;
using GameServer.Data.SDB;
using GameServer.Enums;

namespace GameServer.Entities.Character;

public sealed partial class CharacterEntity
{
    private static readonly byte[] MainCooldownSlots = { 0, 1, 2, 3 };
    private static readonly byte[] SecondaryCooldownSlots = { 5, 6, 16, 17 };

    private readonly Dictionary<uint, CooldownWindow> _abilityCooldowns = new();
    private readonly Dictionary<uint, CooldownWindow> _categoryCooldowns = new();
    private readonly Dictionary<byte, CooldownWindow> _mainSlotCooldowns = new();

    public uint GlobalCooldownActivatedTime { get; private set; }
    public uint GlobalCooldownReadyAgainTime { get; private set; }
    public bool GlobalCooldownPreventReset { get; private set; }

    public bool ApplyAbilityCooldowns(
        uint abilityId,
        uint itemId,
        uint currentTime,
        uint localPrecoolMs,
        uint localDurationMs,
        uint categoryId,
        uint categoryPrecoolMs,
        uint categoryDurationMs,
        uint globalDurationMs,
        byte mainSlot,
        bool preventReset,
        bool allowFallbackAbilityId)
    {
        PruneExpiredCooldowns(currentTime);

        var slottedAbilities = GetSlottedAbilityIdsBySlot();

        float cooldownModifier = GetEffectiveCooldownModifier();
        localPrecoolMs = ScaleCooldownValue(localPrecoolMs, cooldownModifier);
        localDurationMs = ScaleCooldownValue(localDurationMs, cooldownModifier);
        categoryPrecoolMs = ScaleCooldownValue(categoryPrecoolMs, cooldownModifier);
        categoryDurationMs = ScaleCooldownValue(categoryDurationMs, cooldownModifier);
        globalDurationMs = ScaleCooldownValue(globalDurationMs, cooldownModifier);

        bool changed = false;
        uint resolvedAbilityId = ResolveCooldownAbilityId(abilityId, itemId, allowFallbackAbilityId, slottedAbilities);

        if (resolvedAbilityId != 0)
        {
            changed |= MergeCooldownWindow(_abilityCooldowns, resolvedAbilityId, CreateCooldownWindow(currentTime, localPrecoolMs, localDurationMs, preventReset));
        }

        if (categoryId != 0)
        {
            changed |= MergeCooldownWindow(_categoryCooldowns, categoryId, CreateCooldownWindow(currentTime, categoryPrecoolMs, categoryDurationMs, preventReset));
        }

        if (globalDurationMs != 0)
        {
            changed |= MergeGlobalCooldown(currentTime, globalDurationMs, preventReset);
        }

        if (TryResolveProjectedMainCooldownSlot(resolvedAbilityId, mainSlot, slottedAbilities, out byte projectedMainSlot)
            && TryCreateProjectedSlotCooldownWindow(currentTime, localPrecoolMs, localDurationMs, categoryPrecoolMs, categoryDurationMs, globalDurationMs, preventReset, out var projectedWindow))
        {
            changed |= MergeCooldownWindow(_mainSlotCooldowns, projectedMainSlot, projectedWindow);
        }

        if (changed)
        {
            PublishCooldowns(currentTime);
        }

        return changed;
    }

    public bool ReduceAbilityCooldowns(uint reductionMs, uint currentTime)
    {
        PruneExpiredCooldowns(currentTime);

        if (reductionMs == 0)
        {
            return false;
        }

        bool changed = ReduceCooldownDictionary(_abilityCooldowns, reductionMs, currentTime)
            | ReduceCooldownDictionary(_categoryCooldowns, reductionMs, currentTime)
            | ReduceCooldownDictionary(_mainSlotCooldowns, reductionMs, currentTime);

        if (GlobalCooldownReadyAgainTime > currentTime)
        {
            uint newReadyAgainTime = Math.Max(currentTime, SaturatingSubtract(GlobalCooldownReadyAgainTime, reductionMs));
            if (newReadyAgainTime != GlobalCooldownReadyAgainTime)
            {
                GlobalCooldownReadyAgainTime = newReadyAgainTime;
                if (GlobalCooldownReadyAgainTime <= currentTime)
                {
                    GlobalCooldownActivatedTime = currentTime;
                }

                changed = true;
            }
        }

        if (changed)
        {
            PublishCooldowns(currentTime);
        }

        return changed;
    }

    public bool ResetAbilityCooldowns(uint currentTime)
    {
        PruneExpiredCooldowns(currentTime);

        bool changed = false;
        changed |= ResetCooldownDictionary(_abilityCooldowns);
        changed |= ResetCooldownDictionary(_categoryCooldowns);
        changed |= ResetCooldownDictionary(_mainSlotCooldowns);

        if (!GlobalCooldownPreventReset && GlobalCooldownReadyAgainTime > currentTime)
        {
            GlobalCooldownActivatedTime = currentTime;
            GlobalCooldownReadyAgainTime = currentTime;
            changed = true;
        }

        if (changed)
        {
            PublishCooldowns(currentTime);
        }

        return changed;
    }

    public AbilityCooldownsData GetAbilityCooldownsData(uint currentTime)
    {
        PruneExpiredCooldowns(currentTime);

        var (group1, group2, mainSlotReadyTimes) = BuildActiveCooldownGroups(currentTime);
        RefreshMainSlotCooldownProps(mainSlotReadyTimes, currentTime);

        return new AbilityCooldownsData
        {
            ActiveCooldowns_Group1 = group1,
            ActiveCooldowns_Group2 = group2,
            GlobalCooldown_Activated_Time = GlobalCooldownReadyAgainTime > currentTime ? GlobalCooldownActivatedTime : currentTime,
            GlobalCooldown_ReadyAgain_Time = GlobalCooldownReadyAgainTime > currentTime ? GlobalCooldownReadyAgainTime : currentTime,
            Unk = 0,
        };
    }

    public uint GetAbilityCooldownReadyAgainTime(uint abilityId, uint currentTime)
    {
        return TryBuildCooldownWindow(abilityId, currentTime, out var window) ? window.ReadyAgainTime : currentTime;
    }

    public uint GetCategoryCooldownReadyAgainTime(uint categoryId, uint currentTime)
    {
        return _categoryCooldowns.TryGetValue(categoryId, out var window) && window.ReadyAgainTime > currentTime
            ? window.ReadyAgainTime
            : currentTime;
    }

    public uint GetResolvedCategoryCooldownReadyAgainTime(uint abilityId, uint categoryId, uint currentTime)
    {
        uint resolvedCategoryId = categoryId;
        if (resolvedCategoryId == 0)
        {
            TryResolveCooldownCategory(abilityId, out resolvedCategoryId);
        }

        return resolvedCategoryId != 0
            ? GetCategoryCooldownReadyAgainTime(resolvedCategoryId, currentTime)
            : currentTime;
    }

    public uint GetGlobalCooldownReadyAgainTime(uint currentTime)
    {
        return GlobalCooldownReadyAgainTime > currentTime ? GlobalCooldownReadyAgainTime : currentTime;
    }

    private static CooldownWindow CreateCooldownWindow(uint currentTime, uint precoolMs, uint durationMs, bool preventReset)
    {
        if (durationMs == 0)
        {
            return default;
        }

        uint activatedTime = SaturatingSubtract(currentTime, precoolMs);
        uint readyAgainTime = SaturatingAdd(activatedTime, durationMs);
        return new CooldownWindow(activatedTime, readyAgainTime, preventReset);
    }

    private static uint SaturatingAdd(uint left, uint right)
    {
        ulong result = (ulong)left + right;
        return result > uint.MaxValue ? uint.MaxValue : (uint)result;
    }

    private static uint SaturatingSubtract(uint left, uint right)
    {
        return right >= left ? 0 : left - right;
    }

    private static uint ScaleCooldownValue(uint value, float multiplier)
    {
        if (value == 0)
        {
            return 0;
        }

        if (!float.IsFinite(multiplier) || multiplier <= 0.0f)
        {
            return 0;
        }

        double scaled = value * multiplier;
        if (scaled >= uint.MaxValue)
        {
            return uint.MaxValue;
        }

        return (uint)Math.Round(scaled, MidpointRounding.AwayFromZero);
    }

    private float GetEffectiveCooldownModifier()
    {
        float modifier = GetCurrentStatModifierValue(StatModifierIdentifier.CooldownModifier);
        return modifier > 0.0f ? modifier : 1.0f;
    }

    private bool MergeCooldownWindow<TKey>(Dictionary<TKey, CooldownWindow> windows, TKey key, CooldownWindow window)
        where TKey : notnull
    {
        if (!window.IsActive)
        {
            return false;
        }

        if (!windows.TryGetValue(key, out var existing))
        {
            windows[key] = window;
            return true;
        }

        bool preventReset = existing.PreventReset || window.PreventReset;
        if (window.ReadyAgainTime > existing.ReadyAgainTime)
        {
            windows[key] = new CooldownWindow(window.ActivatedTime, window.ReadyAgainTime, preventReset);
            return true;
        }

        if (preventReset != existing.PreventReset)
        {
            windows[key] = new CooldownWindow(existing.ActivatedTime, existing.ReadyAgainTime, preventReset);
            return true;
        }

        return false;
    }

    private bool MergeGlobalCooldown(uint currentTime, uint durationMs, bool preventReset)
    {
        uint readyAgainTime = SaturatingAdd(currentTime, durationMs);
        bool newPreventReset = GlobalCooldownPreventReset || preventReset;

        if (readyAgainTime > GlobalCooldownReadyAgainTime)
        {
            GlobalCooldownActivatedTime = currentTime;
            GlobalCooldownReadyAgainTime = readyAgainTime;
            GlobalCooldownPreventReset = newPreventReset;
            return true;
        }

        if (newPreventReset != GlobalCooldownPreventReset)
        {
            GlobalCooldownPreventReset = newPreventReset;
            return true;
        }

        return false;
    }

    private static bool ReduceCooldownDictionary<TKey>(Dictionary<TKey, CooldownWindow> windows, uint reductionMs, uint currentTime)
        where TKey : notnull
    {
        bool changed = false;
        foreach (var key in windows.Keys.ToArray())
        {
            var window = windows[key];
            if (window.ReadyAgainTime <= currentTime)
            {
                windows.Remove(key);
                changed = true;
                continue;
            }

            uint newReadyAgainTime = Math.Max(currentTime, SaturatingSubtract(window.ReadyAgainTime, reductionMs));
            if (newReadyAgainTime <= currentTime)
            {
                windows.Remove(key);
                changed = true;
                continue;
            }

            if (newReadyAgainTime != window.ReadyAgainTime)
            {
                windows[key] = new CooldownWindow(window.ActivatedTime, newReadyAgainTime, window.PreventReset);
                changed = true;
            }
        }

        return changed;
    }

    private static bool ResetCooldownDictionary<TKey>(Dictionary<TKey, CooldownWindow> windows)
        where TKey : notnull
    {
        bool changed = false;
        foreach (var key in windows.Keys.ToArray())
        {
            if (!windows[key].PreventReset)
            {
                windows.Remove(key);
                changed = true;
            }
        }

        return changed;
    }

    private void PublishCooldowns(uint currentTime)
    {
        var data = GetAbilityCooldownsData(currentTime);
        if (IsPlayerControlled)
        {
            var message = new AbilityCooldowns
            {
                Data = data,
            };
            Player.NetChannels[ChannelType.ReliableGss].SendMessage(message, EntityId);
        }
    }

    private void PruneExpiredCooldowns(uint currentTime)
    {
        RemoveExpiredCooldowns(_abilityCooldowns, currentTime);
        RemoveExpiredCooldowns(_categoryCooldowns, currentTime);
        RemoveExpiredCooldowns(_mainSlotCooldowns, currentTime);

        if (GlobalCooldownReadyAgainTime <= currentTime)
        {
            GlobalCooldownActivatedTime = currentTime;
            GlobalCooldownReadyAgainTime = currentTime;
            GlobalCooldownPreventReset = false;
        }
    }

    private static void RemoveExpiredCooldowns<TKey>(Dictionary<TKey, CooldownWindow> windows, uint currentTime)
        where TKey : notnull
    {
        foreach (var key in windows.Keys.ToArray())
        {
            if (windows[key].ReadyAgainTime <= currentTime)
            {
                windows.Remove(key);
            }
        }
    }

    private (ActiveCooldown[] Group1, ActiveCooldown[] Group2, uint[] MainSlotReadyTimes) BuildActiveCooldownGroups(uint currentTime)
    {
        var slottedAbilities = GetSlottedAbilityIdsBySlot();
        var activeByAbility = new Dictionary<uint, ActiveCooldown>();
        var mainSlotReadyTimes = new uint[] { currentTime, currentTime, currentTime, currentTime };

        foreach (var abilityId in slottedAbilities.Values.Distinct())
        {
            if (TryBuildCooldownWindow(abilityId, currentTime, out var window))
            {
                activeByAbility[abilityId] = ToActiveCooldown(abilityId, window);
            }
        }

        foreach (var abilityId in _abilityCooldowns.Keys.ToArray())
        {
            if (activeByAbility.ContainsKey(abilityId))
            {
                continue;
            }

            if (TryBuildCooldownWindow(abilityId, currentTime, out var window))
            {
                activeByAbility[abilityId] = ToActiveCooldown(abilityId, window);
            }
        }

        var group1 = new List<ActiveCooldown>();
        foreach (byte slot in MainCooldownSlots)
        {
            if (!slottedAbilities.TryGetValue(slot, out var abilityId))
            {
                continue;
            }

            bool hasProjectedMainSlotCooldown = TryGetCooldownWindow(_mainSlotCooldowns, slot, currentTime, out var projectedMainSlotCooldown);
            if (hasProjectedMainSlotCooldown)
            {
                mainSlotReadyTimes[slot] = projectedMainSlotCooldown.ReadyAgainTime;
            }

            activeByAbility.Remove(abilityId);

            if (hasProjectedMainSlotCooldown)
            {
                // Authored main-slot cooldowns should drive the slot timer without
                // keeping the ability in the published active-cooldown list.
                if (TryGetCooldownWindow(_abilityCooldowns, abilityId, currentTime, out var localCooldown))
                {
                    group1.Add(ToActiveCooldown(abilityId, localCooldown));
                }

                continue;
            }

            if (TryBuildCooldownWindow(abilityId, currentTime, out var cooldown))
            {
                group1.Add(ToActiveCooldown(abilityId, cooldown));
                mainSlotReadyTimes[slot] = cooldown.ReadyAgainTime;
            }
        }

        var group2 = new List<ActiveCooldown>();
        foreach (byte slot in SecondaryCooldownSlots)
        {
            if (!slottedAbilities.TryGetValue(slot, out var abilityId))
            {
                continue;
            }

            if (activeByAbility.Remove(abilityId, out var cooldown))
            {
                group2.Add(cooldown);
            }
        }

        foreach (var cooldown in activeByAbility.Values.OrderBy(cooldown => cooldown.AbilityId))
        {
            group2.Add(cooldown);
        }

        return (group1.ToArray(), group2.ToArray(), mainSlotReadyTimes);
    }

    private void RefreshMainSlotCooldownProps(uint[] mainSlotReadyTimes, uint currentTime)
    {
        Character_CombatView.AbilityCooldownEndMs_0Prop = mainSlotReadyTimes.ElementAtOrDefault(0);
        Character_CombatView.AbilityCooldownEndMs_1Prop = mainSlotReadyTimes.ElementAtOrDefault(1);
        Character_CombatView.AbilityCooldownEndMs_2Prop = mainSlotReadyTimes.ElementAtOrDefault(2);
        Character_CombatView.AbilityCooldownEndMs_3Prop = mainSlotReadyTimes.ElementAtOrDefault(3);

        if (mainSlotReadyTimes.Length < 4)
        {
            Character_CombatView.AbilityCooldownEndMs_0Prop = currentTime;
            Character_CombatView.AbilityCooldownEndMs_1Prop = currentTime;
            Character_CombatView.AbilityCooldownEndMs_2Prop = currentTime;
            Character_CombatView.AbilityCooldownEndMs_3Prop = currentTime;
        }
    }

    private Dictionary<byte, uint> GetSlottedAbilityIdsBySlot()
    {
        var result = new Dictionary<byte, uint>();
        if (CurrentLoadout == null)
        {
            return result;
        }

        foreach (var pair in CharacterLoadout.LoadoutToAbilitySlotMap)
        {
            uint itemId = CurrentLoadout.SlottedItems.GetValueOrDefault(pair.Key);
            if (itemId == 0)
            {
                continue;
            }

            var abilityModule = SDBInterface.GetAbilityModule(itemId);
            if (abilityModule?.AbilityChainId > 0)
            {
                result[(byte)pair.Value] = abilityModule.AbilityChainId;
            }
        }

        return result;
    }

    private uint ResolveCooldownAbilityId(uint abilityId, uint itemId, bool allowFallbackAbilityId, Dictionary<byte, uint> slottedAbilities)
    {
        if (itemId != 0)
        {
            var abilityModule = SDBInterface.GetAbilityModule(itemId);
            if (abilityModule?.AbilityChainId > 0)
            {
                return abilityModule.AbilityChainId;
            }
        }

        if (abilityId == 0)
        {
            return 0;
        }

        if (slottedAbilities.Count != 0)
        {
            bool hasSlottedMatch = slottedAbilities.Values.Any(slottedAbilityId => slottedAbilityId == abilityId);
            if (hasSlottedMatch)
            {
                return abilityId;
            }
        }

        return allowFallbackAbilityId ? abilityId : 0;
    }

    private static bool TryResolveProjectedMainCooldownSlot(uint resolvedAbilityId, byte mainSlot, Dictionary<byte, uint> slottedAbilities, out byte projectedMainSlot)
    {
        projectedMainSlot = 0;

        if (mainSlot == 0)
        {
            return false;
        }

        if (resolvedAbilityId != 0)
        {
            foreach (var slottedAbility in slottedAbilities)
            {
                if (slottedAbility.Value == resolvedAbilityId && slottedAbility.Key < MainCooldownSlots.Length)
                {
                    projectedMainSlot = slottedAbility.Key;
                    return true;
                }
            }
        }

        if (mainSlot > MainCooldownSlots.Length)
        {
            return false;
        }

        projectedMainSlot = MainCooldownSlots[mainSlot - 1];
        return true;
    }

    private static bool TryCreateProjectedSlotCooldownWindow(
        uint currentTime,
        uint localPrecoolMs,
        uint localDurationMs,
        uint categoryPrecoolMs,
        uint categoryDurationMs,
        uint globalDurationMs,
        bool preventReset,
        out CooldownWindow window)
    {
        window = default;

        PreferProjectedSlotCooldown(ref window, CreateCooldownWindow(currentTime, localPrecoolMs, localDurationMs, preventReset));
        PreferProjectedSlotCooldown(ref window, CreateCooldownWindow(currentTime, categoryPrecoolMs, categoryDurationMs, preventReset));
        PreferProjectedSlotCooldown(ref window, CreateCooldownWindow(currentTime, 0, globalDurationMs, preventReset));

        return window.IsActive;
    }

    private static void PreferProjectedSlotCooldown(ref CooldownWindow currentWindow, CooldownWindow candidateWindow)
    {
        if (!candidateWindow.IsActive)
        {
            return;
        }

        if (!currentWindow.IsActive || candidateWindow.ReadyAgainTime > currentWindow.ReadyAgainTime)
        {
            currentWindow = candidateWindow;
        }
    }

    private static bool TryGetCooldownWindow<TKey>(Dictionary<TKey, CooldownWindow> windows, TKey key, uint currentTime, out CooldownWindow window)
        where TKey : notnull
    {
        if (windows.TryGetValue(key, out window) && window.ReadyAgainTime > currentTime)
        {
            return true;
        }

        window = default;
        return false;
    }

    private bool TryBuildCooldownWindow(uint abilityId, uint currentTime, out CooldownWindow window)
    {
        bool hasLocal = _abilityCooldowns.TryGetValue(abilityId, out var localWindow) && localWindow.ReadyAgainTime > currentTime;
        bool hasCategory = false;
        CooldownWindow categoryWindow = default;

        if (TryResolveCooldownCategory(abilityId, out var categoryId)
            && categoryId != 0
            && _categoryCooldowns.TryGetValue(categoryId, out categoryWindow)
            && categoryWindow.ReadyAgainTime > currentTime)
        {
            hasCategory = true;
        }

        if (!hasLocal && !hasCategory)
        {
            window = default;
            return false;
        }

        window = hasLocal ? localWindow : categoryWindow;
        if (hasCategory && (!hasLocal || categoryWindow.ReadyAgainTime > localWindow.ReadyAgainTime))
        {
            window = categoryWindow;
        }

        return true;
    }

    private static ActiveCooldown ToActiveCooldown(uint abilityId, CooldownWindow window)
    {
        return new ActiveCooldown
        {
            AbilityId = abilityId,
            Activated_Time = window.ActivatedTime,
            ReadyAgain_Time = window.ReadyAgainTime,
            Unk = new byte[5],
        };
    }

    internal static bool TryResolveCooldownCategory(uint abilityId, out uint categoryId)
    {
        categoryId = 0;

        var ability = SDBInterface.GetAbilityData(abilityId);
        if (ability == null || ability.Chain == 0)
        {
            return false;
        }

        return TryResolveCooldownCategoryFromChain(ability.Chain, new HashSet<uint>(), new HashSet<uint>(), out categoryId);
    }

    private static bool TryResolveCooldownCategoryFromChain(uint chainId, HashSet<uint> visitedChains, HashSet<uint> visitedAbilities, out uint categoryId)
    {
        categoryId = 0;
        if (chainId == 0 || !visitedChains.Add(chainId))
        {
            return false;
        }

        uint next = chainId;
        while (next != 0)
        {
            var baseDef = SDBInterface.GetBaseCommandDef(next);
            if (baseDef == null)
            {
                break;
            }

            switch ((CommandType)baseDef.Subtype)
            {
                case CommandType.InflictCooldown:
                {
                    var commandDef = SDBInterface.GetInflictCooldownCommandDef(baseDef.Id);
                    if (commandDef != null && commandDef.Category != 0)
                    {
                        categoryId = commandDef.Category;
                        return true;
                    }

                    break;
                }

                case CommandType.InstantActivation:
                {
                    var commandDef = SDBInterface.GetInstantActivationCommandDef(baseDef.Id);
                    if (commandDef != null && commandDef.Category != 0)
                    {
                        categoryId = commandDef.Category;
                        return true;
                    }

                    break;
                }

                case CommandType.Call:
                {
                    var commandDef = SDBInterface.GetCallCommandDef(baseDef.Id);
                    if (commandDef != null && commandDef.AbilityId != 0 && visitedAbilities.Add(commandDef.AbilityId))
                    {
                        var calledAbility = SDBInterface.GetAbilityData(commandDef.AbilityId);
                        if (calledAbility != null && TryResolveCooldownCategoryFromChain(calledAbility.Chain, visitedChains, visitedAbilities, out categoryId))
                        {
                            return true;
                        }
                    }

                    break;
                }

                case CommandType.ConditionalBranch:
                {
                    var commandDef = SDBInterface.GetConditionalBranchCommandDef(baseDef.Id);
                    if (commandDef != null)
                    {
                        if (TryResolveCooldownCategoryFromChain(commandDef.IfChain, visitedChains, visitedAbilities, out categoryId)
                            || TryResolveCooldownCategoryFromChain(commandDef.ThenChain, visitedChains, visitedAbilities, out categoryId)
                            || TryResolveCooldownCategoryFromChain(commandDef.ElseChain, visitedChains, visitedAbilities, out categoryId))
                        {
                            return true;
                        }
                    }

                    break;
                }

                case CommandType.WhileLoop:
                {
                    var commandDef = SDBInterface.GetWhileLoopCommandDef(baseDef.Id);
                    if (commandDef != null)
                    {
                        if (TryResolveCooldownCategoryFromChain(commandDef.ConditionChain, visitedChains, visitedAbilities, out categoryId)
                            || TryResolveCooldownCategoryFromChain(commandDef.BodyChain, visitedChains, visitedAbilities, out categoryId))
                        {
                            return true;
                        }
                    }

                    break;
                }

                case CommandType.LogicAndChain:
                {
                    var commandDef = SDBInterface.GetLogicAndChainCommandDef(baseDef.Id);
                    if (commandDef != null && TryResolveCooldownCategoryFromChain(commandDef.AndChain, visitedChains, visitedAbilities, out categoryId))
                    {
                        return true;
                    }

                    break;
                }

                case CommandType.LogicOrChain:
                {
                    var commandDef = SDBInterface.GetLogicOrChainCommandDef(baseDef.Id);
                    if (commandDef != null && TryResolveCooldownCategoryFromChain(commandDef.OrChain, visitedChains, visitedAbilities, out categoryId))
                    {
                        return true;
                    }

                    break;
                }

                case CommandType.LogicNegate:
                {
                    var commandDef = SDBInterface.GetLogicNegateCommandDef(baseDef.Id);
                    if (commandDef != null && TryResolveCooldownCategoryFromChain(commandDef.NegateChain, visitedChains, visitedAbilities, out categoryId))
                    {
                        return true;
                    }

                    break;
                }

                case CommandType.LogicOr:
                {
                    var commandDef = SDBInterface.GetLogicOrCommandDef(baseDef.Id);
                    if (commandDef != null)
                    {
                        if (TryResolveCooldownCategoryFromChain(commandDef.AChain, visitedChains, visitedAbilities, out categoryId)
                            || TryResolveCooldownCategoryFromChain(commandDef.BChain, visitedChains, visitedAbilities, out categoryId))
                        {
                            return true;
                        }
                    }

                    break;
                }
            }

            next = baseDef.Next;
        }

        return false;
    }

    private readonly struct CooldownWindow
    {
        public CooldownWindow(uint activatedTime, uint readyAgainTime, bool preventReset)
        {
            ActivatedTime = activatedTime;
            ReadyAgainTime = readyAgainTime;
            PreventReset = preventReset;
        }

        public uint ActivatedTime { get; }
        public uint ReadyAgainTime { get; }
        public bool PreventReset { get; }
        public bool IsActive => ReadyAgainTime > ActivatedTime;
    }
}