using System.Collections.Generic;
using System.Linq;
using AeroMessages.GSS.V66.Character.Event;
using GameServer.Aptitude;
using GameServer.Data.SDB;
using GameServer.Enums;

namespace GameServer.Entities.Character;

public sealed partial class CharacterEntity
{
    private readonly Dictionary<uint, TimedActivationState> _timedActivations = new();
    private readonly Dictionary<uint, HashSet<uint>> _abilityToggleEffects = new();
    private readonly HashSet<uint> _suppressNextRemoveCooldownForAbility = new();

    public bool StartAbilityActivation(uint abilityId, uint activatedTime)
    {
        return SetAbilityActivationState(abilityId, activatedTime, 0, false);
    }

    public bool StartTimedActivation(uint abilityId, uint activatedTime, uint durationMs, bool cancelOnMove)
    {
        return SetAbilityActivationState(abilityId, activatedTime, durationMs, cancelOnMove);
    }

    public bool EndAbilityActivation(uint abilityId, uint currentTime, bool notifyClient, bool sendFailureFallback = false, bool suppressCooldownOnManualDeactivation = false)
    {
        PruneAbilityActivations(currentTime);

        if (suppressCooldownOnManualDeactivation && abilityId != 0)
        {
            _suppressNextRemoveCooldownForAbility.Add(abilityId);
        }

        bool removedActivation = _timedActivations.Remove(abilityId);
        bool removedEffects = RemoveTrackedAbilityToggleEffects(abilityId);
        bool removedAbilityScopedEffects = RemoveAbilityScopedEffects(abilityId);

        if (!removedActivation && !removedEffects && !removedAbilityScopedEffects)
        {
            return false;
        }

        if (notifyClient)
        {
            PublishAbilityActivationEnded(abilityId, currentTime, sendFailureFallback);
        }

        return true;
    }

    public bool EndTimedActivation(uint abilityId, uint currentTime, bool notifyClient, bool sendFailureFallback = false, bool suppressCooldownOnManualDeactivation = false)
    {
        return EndAbilityActivation(abilityId, currentTime, notifyClient, sendFailureFallback, suppressCooldownOnManualDeactivation);
    }

    public bool EndAbilityActivationBySlot(byte slotIndex, uint currentTime, bool notifyClient, bool sendFailureFallback = false, bool suppressCooldownOnManualDeactivation = false)
    {
        uint abilityId = ResolveAbilityIdBySlotIndex(slotIndex);
        return abilityId != 0 && EndAbilityActivation(abilityId, currentTime, notifyClient, sendFailureFallback, suppressCooldownOnManualDeactivation);
    }

    public bool EndTimedActivationBySlot(byte slotIndex, uint currentTime, bool notifyClient, bool sendFailureFallback = false, bool suppressCooldownOnManualDeactivation = false)
    {
        return EndAbilityActivationBySlot(slotIndex, currentTime, notifyClient, sendFailureFallback, suppressCooldownOnManualDeactivation);
    }

    public bool ConsumeManualDeactivationCooldownSuppression(uint abilityId)
    {
        return abilityId != 0 && _suppressNextRemoveCooldownForAbility.Remove(abilityId);
    }

    public bool CancelTimedActivationsOnMove(uint currentTime)
    {
        PruneTimedActivations(currentTime);

        var cancelledAbilityIds = _timedActivations
            .Where(pair => pair.Value.CancelOnMove || HasMovementSensitiveToggleEffect(pair.Key))
            .Select(pair => pair.Key)
            .Concat(GetActiveEffects()
                .Where(activeEffect => activeEffect != null && activeEffect.Context.AbilityId != 0 && EffectCancelsOnMove(activeEffect.Effect.Id))
                .Select(activeEffect => activeEffect.Context.AbilityId))
            .Distinct()
            .ToArray();

        if (cancelledAbilityIds.Length == 0)
        {
            return false;
        }

        foreach (uint abilityId in cancelledAbilityIds)
        {
            EndAbilityActivation(abilityId, currentTime, notifyClient: true, sendFailureFallback: true);
        }

        return true;
    }

    public bool PruneAbilityActivations(uint currentTime)
    {
        var expiredAbilityIds = _timedActivations
            .Where(pair => pair.Value.EndTime != 0 && pair.Value.EndTime <= currentTime)
            .Select(pair => pair.Key)
            .ToArray();

        if (expiredAbilityIds.Length == 0)
        {
            return false;
        }

        foreach (uint abilityId in expiredAbilityIds)
        {
            _timedActivations.Remove(abilityId);
            RemoveTrackedAbilityToggleEffects(abilityId);
            RemoveAbilityScopedEffects(abilityId);
            PublishAbilityActivationEnded(abilityId, currentTime, sendFailureFallback: false);
        }

        return true;
    }

    public bool PruneTimedActivations(uint currentTime)
    {
        return PruneAbilityActivations(currentTime);
    }

    public bool IsAbilityActivationActive(uint abilityId, uint currentTime)
    {
        PruneAbilityActivations(currentTime);
        return abilityId != 0 && _timedActivations.ContainsKey(abilityId);
    }

    public bool IsTimedActivationActive(uint abilityId, uint currentTime)
    {
        return IsAbilityActivationActive(abilityId, currentTime);
    }

    public bool HasTrackedAbilityToggleEffects(uint abilityId)
    {
        return abilityId != 0
            && _abilityToggleEffects.TryGetValue(abilityId, out var effectIds)
            && effectIds.Count != 0;
    }

    public bool HasAbilityScopedEffects(uint abilityId)
    {
        if (abilityId == 0)
        {
            return false;
        }

        foreach (var activeEffect in GetActiveEffects())
        {
            if (activeEffect != null && activeEffect.Context.AbilityId == abilityId)
            {
                return true;
            }
        }

        return false;
    }

    public void TrackAbilityToggleEffect(uint abilityId, uint effectId)
    {
        if (abilityId == 0 || effectId == 0)
        {
            return;
        }

        if (!_abilityToggleEffects.TryGetValue(abilityId, out var effectIds))
        {
            effectIds = new HashSet<uint>();
            _abilityToggleEffects[abilityId] = effectIds;
        }

        effectIds.Add(effectId);
    }

    public bool UntrackAbilityToggleEffect(uint abilityId, uint effectId)
    {
        if (abilityId == 0 || effectId == 0)
        {
            return false;
        }

        if (!_abilityToggleEffects.TryGetValue(abilityId, out var effectIds))
        {
            return false;
        }

        if (!effectIds.Remove(effectId))
        {
            return false;
        }

        if (effectIds.Count == 0)
        {
            _abilityToggleEffects.Remove(abilityId);
        }

        return true;
    }

    public void HandleTrackedAbilityEffectRemoved(uint abilityId, uint effectId, uint currentTime)
    {
        if (!UntrackAbilityToggleEffect(abilityId, effectId))
        {
            return;
        }

        if (IsAbilityActivationActive(abilityId, currentTime) && !HasTrackedAbilityToggleEffects(abilityId))
        {
            EndAbilityActivation(abilityId, currentTime, notifyClient: true, sendFailureFallback: false);
        }
    }

    internal uint ResolveAbilityIdBySlotIndex(byte abilitySlot)
    {
        if (CurrentLoadout != null)
        {
            uint moduleId = CurrentLoadout.GetAbilityModuleIdBySlotIndex(abilitySlot);
            if (moduleId != 0)
            {
                var abilityModule = SDBInterface.GetAbilityModule(moduleId);
                if (abilityModule?.AbilityChainId > 0)
                {
                    return abilityModule.AbilityChainId;
                }
            }
        }

        return abilitySlot switch
        {
            4 => 187,
            13 => 43,
            _ => 0,
        };
    }

    private bool SetAbilityActivationState(uint abilityId, uint activatedTime, uint durationMs, bool cancelOnMove)
    {
        if (abilityId == 0)
        {
            return false;
        }

        PruneAbilityActivations(activatedTime);
        _timedActivations[abilityId] = new TimedActivationState(
            activatedTime,
            durationMs == 0 ? 0 : SaturatingAdd(activatedTime, durationMs),
            cancelOnMove);
        return true;
    }

    private bool RemoveTrackedAbilityToggleEffects(uint abilityId)
    {
        if (!_abilityToggleEffects.Remove(abilityId, out var effectIds) || effectIds.Count == 0)
        {
            return false;
        }

        foreach (uint effectId in effectIds)
        {
            Shard.Abilities.DoRemoveEffect(this, effectId);
        }

        return true;
    }

    private bool RemoveAbilityScopedEffects(uint abilityId)
    {
        if (abilityId == 0)
        {
            return false;
        }

        var scopedEffects = GetActiveEffects()
            .Where(activeEffect => activeEffect != null && activeEffect.Context.AbilityId == abilityId)
            .ToArray();

        if (scopedEffects.Length == 0)
        {
            return false;
        }

        foreach (var scopedEffect in scopedEffects)
        {
            Shard.Abilities.DoRemoveEffect(scopedEffect);
        }

        return true;
    }

    private bool HasMovementSensitiveToggleEffect(uint abilityId)
    {
        if (!_abilityToggleEffects.TryGetValue(abilityId, out var effectIds) || effectIds.Count == 0)
        {
            return false;
        }

        foreach (uint effectId in effectIds)
        {
            if (EffectCancelsOnMove(effectId))
            {
                return true;
            }
        }

        return false;
    }

    private static bool EffectCancelsOnMove(uint effectId)
    {
        var effectData = SDBInterface.GetStatusEffectData(effectId);
        if (effectData?.DurationChain == 0)
        {
            return false;
        }

        return ChainContainsMoveCancelBattleFrameDuration(effectData.DurationChain, new HashSet<uint>());
    }

    private static bool ChainContainsMoveCancelBattleFrameDuration(uint chainId, HashSet<uint> visited)
    {
        while (chainId != 0)
        {
            if (!visited.Add(chainId))
            {
                return false;
            }

            var baseDef = SDBInterface.GetBaseCommandDef(chainId);
            if (baseDef == null)
            {
                return false;
            }

            var commandType = (CommandType)baseDef.Subtype;
            switch (commandType)
            {
                case CommandType.BattleFrameDuration:
                    var durationDef = SDBInterface.GetBattleFrameDurationCommandDef(baseDef.Id);
                    if (durationDef?.Notchanged == 1)
                    {
                        return true;
                    }

                    break;
                case CommandType.Call:
                    var callDef = SDBInterface.GetCallCommandDef(baseDef.Id);
                    if (callDef != null)
                    {
                        var calledAbility = SDBInterface.GetAbilityData(callDef.AbilityId);
                        if (calledAbility?.Chain != 0 && ChainContainsMoveCancelBattleFrameDuration(calledAbility.Chain, visited))
                        {
                            return true;
                        }
                    }

                    break;
                case CommandType.ConditionalBranch:
                    var branchDef = SDBInterface.GetConditionalBranchCommandDef(baseDef.Id);
                    if (branchDef != null)
                    {
                        if ((branchDef.ThenChain != 0 && ChainContainsMoveCancelBattleFrameDuration(branchDef.ThenChain, visited))
                            || (branchDef.ElseChain != 0 && ChainContainsMoveCancelBattleFrameDuration(branchDef.ElseChain, visited)))
                        {
                            return true;
                        }
                    }

                    break;
                case CommandType.LogicAndChain:
                    var andDef = SDBInterface.GetLogicAndChainCommandDef(baseDef.Id);
                    if (andDef?.AndChain != 0 && ChainContainsMoveCancelBattleFrameDuration(andDef.AndChain, visited))
                    {
                        return true;
                    }

                    break;
                case CommandType.LogicOrChain:
                    var orDef = SDBInterface.GetLogicOrChainCommandDef(baseDef.Id);
                    if (orDef?.OrChain != 0 && ChainContainsMoveCancelBattleFrameDuration(orDef.OrChain, visited))
                    {
                        return true;
                    }

                    break;
            }

            chainId = baseDef.Next;
        }

        return false;
    }

    private void PublishAbilityActivationEnded(uint abilityId, uint currentTime, bool sendFailureFallback)
    {
        if (!IsPlayerControlled)
        {
            return;
        }

        if (sendFailureFallback && abilityId != 0)
        {
            Player.NetChannels[ChannelType.ReliableGss].SendMessage(new AbilityFailed
            {
                FailedAbilityId = abilityId,
                Unk2 = 0,
                AbilityCooldownsData = GetAbilityCooldownsData(currentTime),
            }, EntityId);
            return;
        }

        Player.NetChannels[ChannelType.ReliableGss].SendMessage(new AbilityCooldowns
        {
            Data = GetAbilityCooldownsData(currentTime),
        }, EntityId);
    }

    private readonly struct TimedActivationState
    {
        public TimedActivationState(uint activatedTime, uint endTime, bool cancelOnMove)
        {
            ActivatedTime = activatedTime;
            EndTime = endTime;
            CancelOnMove = cancelOnMove;
        }

        public uint ActivatedTime { get; }
        public uint EndTime { get; }
        public bool CancelOnMove { get; }
    }
}