using System;
using System.Collections.Generic;
using System.Threading;
using AeroMessages.GSS.V66.Character;
using AeroMessages.GSS.V66.Character.Command;
using AeroMessages.GSS.V66.Character.Event;
using GameServer.Data.SDB;
using GameServer.Entities.Character;
using GameServer.Enums;

namespace GameServer.Aptitude;

public class AbilitySystem
{
    private readonly Shard _shard;
    private readonly ulong _updateIntervalMs = 20;
    private readonly Dictionary<ulong, VehicleCalldownRequest> _playerVehicleCalldownRequests;
    private readonly Dictionary<ulong, DeployableCalldownRequest> _playerDeployableCalldownRequests;
    private readonly Dictionary<ulong, ResourceNodeBeaconCalldownRequest> _playerThumperCalldownRequests;
    private ulong _lastUpdate = 0;

    public AbilitySystem(Shard shard)
    {
        _shard = shard;
        Factory = new Factory(shard);
        _playerVehicleCalldownRequests = [];
        _playerDeployableCalldownRequests = [];
        _playerThumperCalldownRequests = [];
    }

    public Factory Factory { get; }

    public static float RegistryOp(float first, float second, Operand op)
    {
        if (float.IsNaN(first))
        {
            return second;
        }

        switch (op)
        {
            case Operand.ASSIGN:
                return second;
            case Operand.ADD:
            case Operand.ADD_ALT:
                return second + first;
            case Operand.MULTIPLY:
            case Operand.MULTIPLY_ALT:
                return second * first;
            case Operand.EXPONENTIATE:
                Serilog.Log.Information($"Uncertain RegistryOp {op}. {second} ^ {first} = {(float)Math.Pow(second, first)}");
                return (float)Math.Pow(second, first);
            case Operand.SUBTRACT:
                Serilog.Log.Information($"Uncertain RegistryOp {op}. {second} - {first} = {second - first}");
                return second - first;
            case Operand.DIVIDE:
                Serilog.Log.Information($"Uncertain RegistryOp {op}. {second} / {first} = {second / first}");
                return second / first;
            case Operand.MINIMUM:
                Serilog.Log.Information($"Uncertain RegistryOp {op}. Min({second}, {first}) = {((first <= second) ? first : second)}");
                return (first <= second) ? first : second;
            case Operand.MAXIMUM:
                Serilog.Log.Information($"Uncertain RegistryOp {op}. Max({second}, {first}) = {((first >= second) ? first : second)}");
                return (first >= second) ? first : second;
            default:
                Serilog.Log.Information($"Unknown RegistryOp {op}");
                return second;
        }
    }

    public void Tick(double deltaTime, ulong currentTime, CancellationToken ct)
    {
        if (currentTime > _lastUpdate + _updateIntervalMs)
        {
            _lastUpdate = currentTime;
            foreach (var entity in _shard.Entities.Values)
            {
                if (entity is IAptitudeTarget target)
                {
                    ProcessTarget(target, currentTime);
                }
            }
        }
    }

    public void ProcessTarget(IAptitudeTarget entity, ulong currentTime)
    {
        if (entity is Entities.Character.CharacterEntity character)
        {
            character.PruneAbilityActivations((uint)currentTime);
        }

        var activeEffects = entity.GetActiveEffects();
        foreach (var activeEffect in activeEffects)
        {
            if (activeEffect == null)
            {
                continue;
            }

            // Duration is checked every tick regardless of UpdateFrequency.
            // UpdateFrequency only controls how often the UpdateChain (periodic tick) runs.
            if (activeEffect.Effect.DurationChain != null)
            {
                activeEffect.Context.ExecutionHint = ExecutionHint.DurationEffect;
                bool durationResult = activeEffect.Effect.DurationChain.Execute(activeEffect.Context);

                if (!durationResult)
                {
                    DoRemoveEffect(activeEffect);
                    continue;
                }
            }

            // Run periodic update at the configured frequency
            if (activeEffect.Effect.UpdateChain != null
                && currentTime > activeEffect.LastUpdateTime + activeEffect.Effect.UpdateFrequency)
            {
                activeEffect.LastUpdateTime = currentTime;
                activeEffect.Context.ExecutionHint = ExecutionHint.UpdateEffect;
                activeEffect.Effect.UpdateChain.Execute(activeEffect.Context);
            }
        }
    }

    public void DoApplyEffect(uint effectId, IAptitudeTarget target, Context context)
    {
        if (effectId == 0)
        {
            return;
        }

        var applyContext = Context.CopyContext(context);
        applyContext.Self = target;
        applyContext.ExecutionHint = ExecutionHint.ApplyEffect;
        applyContext.SourceContext = context.SourceEffect != 0 ? context.SourceEffect : context.SourceContext;
        applyContext.SourceEffect = effectId;

        // InitTime must reflect when THIS EFFECT was applied (server time), not the original
        // client activation time. TimeDurationCommand compares Shard.CurrentTime against
        // InitTime, so using a client-side timestamp causes immediate expiry and effects end
        // after exactly one UpdateFrequency interval instead of their correct DurationMs.
        applyContext.InitTime = context.Shard.CurrentTime;

        var effect = Factory.LoadEffect(effectId);

        // TODO: Decouple effect storage from fields so that hidden effects can be added without using a network field
        /*
        if (effect.Data.Hidden == 0)
        {

        }
        */
        var effectState = target.AddEffect(effect, applyContext);

        if (effectState.MaxStacksExceeded)
        {
            return;
        }

        effect.ApplyChain?.Execute(applyContext);

        foreach (var pair in applyContext.Actives)
        {
            ICommand activeCommand = pair.Key;
            activeCommand.OnApply(applyContext, pair.Value);
        }
    }

    public void DoRemoveEffect(EffectState activeEffect)
    {
        activeEffect.Context.ExecutionHint = ExecutionHint.RemoveEffect;
        activeEffect.Context.SourceEffect = activeEffect.Effect.Id;
        activeEffect.Context.Self.ClearEffect(activeEffect);
        activeEffect.Effect.RemoveChain?.Execute(activeEffect.Context);

        foreach (var pair in activeEffect.Context.Actives)
        {
            ICommand activeCommand = pair.Key;
            activeCommand.OnRemove(activeEffect.Context, pair.Value);
        }

        if (activeEffect.Context.Self is CharacterEntity character)
        {
            character.HandleTrackedAbilityEffectRemoved(
                activeEffect.Context.AbilityId,
                activeEffect.Effect.Id,
                (uint)activeEffect.Context.Shard.CurrentTime);
        }
    }

    public void DoRemoveEffect(IAptitudeTarget entity, uint effectId)
    {
        var activeEffects = entity.GetActiveEffects();
        foreach (var activeEffect in activeEffects)
        {
            if (activeEffect?.Effect.Id != null)
            {
                if (activeEffect.Effect.Id == effectId)
                {
                    DoRemoveEffect(activeEffect);
                    break;
                }
            }
        }
    }

    public VehicleCalldownRequest TryConsumeVehicleCalldownRequest(ulong entityId)
    {
        return _playerVehicleCalldownRequests.Remove(entityId, out var result) ? result : null;
    }

    public DeployableCalldownRequest TryConsumeDeployableCalldownRequest(ulong entityId)
    {
        return _playerDeployableCalldownRequests.Remove(entityId, out var result) ? result : null;
    }

    public ResourceNodeBeaconCalldownRequest TryConsumeResourceNodeBeaconCalldownRequest(ulong entityId)
    {
        return _playerThumperCalldownRequests.Remove(entityId, out var result) ? result : null;
    }

    public void HandleVehicleCalldownRequest(ulong entityId, VehicleCalldownRequest request)
    {
        if (_playerVehicleCalldownRequests.ContainsKey(entityId))
        {
            Serilog.Log.Information($"Discarded an unconsumed vehicle calldown request");
            _playerVehicleCalldownRequests.Remove(entityId);
        }

        _playerVehicleCalldownRequests.Add(entityId, request);
    }

    public void HandleDeployableCalldownRequest(ulong entityId, DeployableCalldownRequest request)
    {
        if (_playerDeployableCalldownRequests.ContainsKey(entityId))
        {
            Serilog.Log.Information($"Discarded an unconsumed deployable calldown request");
            _playerDeployableCalldownRequests.Remove(entityId);
        }

        _playerDeployableCalldownRequests.Add(entityId, request);
    }

    public void HandleResourceNodeBeaconCalldownRequest(ulong entityId, ResourceNodeBeaconCalldownRequest request)
    {
        if (_playerThumperCalldownRequests.ContainsKey(entityId))
        {
            Serilog.Log.Information($"Discarded an unconsumed thumper calldown request");
            _playerThumperCalldownRequests.Remove(entityId);
        }

        _playerThumperCalldownRequests.Add(entityId, request);
    }

    public void HandleLocalProximityAbilitySuccess(IShard shard, IAptitudeTarget source, uint commandId, uint time, AptitudeTargets targets)
    {
        Serilog.Log.Information($"HandleLocalProximityAbilitySuccess Source {source}, Command {commandId}, Time {time}, Targets {string.Join(Environment.NewLine, targets)} ({targets.Count})");

        var commandDef = SDBInterface.GetRegisterClientProximityCommandDef(commandId);
        if (commandDef == null)
        {
            Serilog.Log.Information($"[Proximity] Missing RegisterClientProximityCommandDef for commandId={commandId}");
            return;
        }

        Serilog.Log.Information($"[Proximity] commandId={commandId}, abilityId={commandDef.AbilityId}, chain={commandDef.Chain}, radius={commandDef.Radius}, maxTargets={commandDef.MaxTargets}, retryMs={commandDef.RetryInterval}");

        if (commandDef.AbilityId != 0)
        {
            HandleActivateAbility(shard, source, commandDef.AbilityId, time, targets);
        }

        if (commandDef.Chain != 0)
        {
            var chain = Factory.LoadChain(commandDef.Chain);
            chain.Execute(new Context(shard, source)
            {
                ChainId = commandDef.Chain,
                Targets = targets,
                InitTime = time,
                ExecutionHint = ExecutionHint.Proximity
            });
        }
    }

    public void HandleActivateAbility(IShard shard, IAptitudeTarget initiator, uint abilityId, uint activationTime, AptitudeTargets targets, uint itemId = 0, bool activationAcknowledged = false)
    {
        var chainId = SDBInterface.GetAbilityData(abilityId).Chain;
        if (chainId == 0)
        {
            return;
        }

        var chain = Factory.LoadChain(chainId);
        var context = new Context(shard, initiator)
        {
            ChainId = chainId,
            AbilityId = abilityId,
            Targets = targets,
            InitTime = activationTime,
            ExecutionHint = ExecutionHint.Ability,
            ItemId = itemId,
            ActivationAcknowledged = activationAcknowledged,
        };

        bool success = chain.Execute(context);
        CharacterEntity activationCharacter = context.PendingActivationCharacter;
        CharacterEntity feedbackCharacter = activationCharacter
            ?? initiator as CharacterEntity
            ?? context.Self as CharacterEntity;

        if (activationCharacter == null)
        {
            if (!success)
            {
                PublishAbilityFailed(feedbackCharacter, abilityId, activationTime);
            }

            return;
        }

        if (!success)
        {
            activationCharacter.EndAbilityActivation(abilityId, activationTime, notifyClient: false);
            PublishAbilityFailed(feedbackCharacter, abilityId, activationTime);
            return;
        }

        if (context.PendingActivationStateRequested)
        {
            if (context.PendingTimedActivation)
            {
                activationCharacter.StartTimedActivation(abilityId, activationTime, context.PendingActivationDurationMs, context.PendingActivationCancelOnMove);
            }
            else
            {
                activationCharacter.StartAbilityActivation(abilityId, activationTime);
            }
        }

        if (!context.PendingActivationAcknowledgement || context.ActivationAcknowledged || !activationCharacter.IsPlayerControlled)
        {
            return;
        }

        uint currentTime = (uint)activationCharacter.Shard.CurrentTime;

        var message = new AbilityActivated
        {
            ActivatedAbilityId = abilityId,
            ActivatedTime = activationTime,
            AbilityCooldownsData = activationCharacter.GetAbilityCooldownsData(currentTime),
        };

        Serilog.Log.Information("ActivateAbility {ActivatedAbilityId} at {ActivatedTime}", message.ActivatedAbilityId, message.ActivatedTime);
        activationCharacter.Player.NetChannels[ChannelType.ReliableGss].SendMessage(message, activationCharacter.EntityId);
    }

    private static void PublishAbilityFailed(CharacterEntity character, uint abilityId, uint activationTime)
    {
        if (character is not { IsPlayerControlled: true })
        {
            return;
        }

        uint currentTime = (uint)character.Shard.CurrentTime;

        character.Player.NetChannels[ChannelType.ReliableGss].SendMessage(new AbilityFailed
        {
            FailedAbilityId = abilityId,
            Unk2 = 0,
            AbilityCooldownsData = character.GetAbilityCooldownsData(currentTime),
        }, character.EntityId);
    }

    public void HandleActivateAbility(IShard shard, IAptitudeTarget initiator, uint abilityId)
    {
        HandleActivateAbility(shard, initiator, abilityId, _shard.CurrentTime, new AptitudeTargets());
    }

    public void HandleTargetAbility()
    {
        throw new NotImplementedException();
    }

    public void HandleDeactivateAbility()
    {
        throw new NotImplementedException();
    }

    public void HandleActivateConsumable()
    {
        throw new NotImplementedException();
    }
}