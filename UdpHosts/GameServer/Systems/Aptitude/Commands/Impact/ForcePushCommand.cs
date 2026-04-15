using System.Numerics;
using AeroMessages.GSS.V66.Character.Event;
using GameServer.Data.SDB;
using GameServer.Data.SDB.Records.aptfs;
using GameServer.Entities.Character;
using GameServer.Enums;

namespace GameServer.Aptitude;

public class ForcePushCommand : Command, ICommand
{
    private const uint ClientStartLeadMs = 19;
    private const uint DirectGliderAbilityId = 38053;
    private const uint DirectGliderStagingEffectId = 3419;
    private ForcePushCommandDef Params;

    public ForcePushCommand(ForcePushCommandDef par)
: base(par)
    {
        Params = par;
    }

    private static uint ResolveDurationMs(Context context)
    {
        foreach (var activeEffect in context.Self.GetActiveEffects())
        {
            if (activeEffect == null || !ReferenceEquals(activeEffect.Context, context))
            {
                continue;
            }

            var durationChainId = activeEffect.Effect?.Data.DurationChain ?? 0;
            if (durationChainId == 0)
            {
                return 0;
            }

            var durationDef = SDBInterface.GetTimeDurationCommandDef(durationChainId);
            if (durationDef == null)
            {
                return 0;
            }

            var durationMs = AbilitySystem.RegistryOp(context.Register, durationDef.DurationMs, (Operand)durationDef.DurationRegop);
            return durationMs > 0 ? (uint)durationMs : 0;
        }

        return 0;
    }

    private static uint ResolveStatusEffectDurationMs(Context context, uint effectId)
    {
        var effectData = SDBInterface.GetStatusEffectData(effectId);
        if (effectData?.DurationChain == 0)
        {
            return 0;
        }

        var durationDef = SDBInterface.GetTimeDurationCommandDef(effectData.DurationChain);
        if (durationDef == null)
        {
            return 0;
        }

        var durationMs = AbilitySystem.RegistryOp(context.Register, durationDef.DurationMs, (Operand)durationDef.DurationRegop);
        return durationMs > 0 ? (uint)durationMs : 0;
    }

    private static uint ResolveClientStartLeadMs(Context context, CharacterEntity character)
    {
        if (context.AbilityId != DirectGliderAbilityId || character.IsAirborne)
        {
            return ClientStartLeadMs;
        }

        if (character.MovementStateContainer.Movestate is not (Movestate.Standing or Movestate.Walking or Movestate.Running))
        {
            return ClientStartLeadMs;
        }

        var stagingDurationMs = ResolveStatusEffectDurationMs(context, DirectGliderStagingEffectId);
        if (stagingDurationMs == 0)
        {
            return ClientStartLeadMs;
        }

        var pushDurationMs = ResolveDurationMs(context);
        if (pushDurationMs == 0 || pushDurationMs >= stagingDurationMs)
        {
            return ClientStartLeadMs;
        }

        return (stagingDurationMs - pushDurationMs) + ClientStartLeadMs;
    }

    public bool Execute(Context context)
    {
        var durationMs = ResolveDurationMs(context);
        if (durationMs == 0)
        {
            Logger.Warning("{Command} {CommandId}: skipping force push because no SDB duration could be resolved for chain {ChainId}", nameof(ForcePushCommand), Params.Id, context.ChainId);
            return true;
        }

        var pushStrength = AbilitySystem.RegistryOp(context.Register, Params.Strength, (Operand)Params.StrengthRegop);
        if (pushStrength <= 0)
        {
            Logger.Warning("{Command} {CommandId}: skipping force push because resolved strength was {Strength}", nameof(ForcePushCommand), Params.Id, pushStrength);
            return true;
        }

        foreach (IAptitudeTarget target in context.Targets)
        {
            if (target is CharacterEntity character)
            {
                if (!character.IsPlayerControlled)
                {
                    continue;
                }

                var beforeVelocity = new Vector3(character.Velocity.X, character.Velocity.Y, character.Velocity.Z);
                var velocity = beforeVelocity;
                velocity.Z += pushStrength;

                var startLeadMs = ResolveClientStartLeadMs(context, character);

                // Delay the client-side impulse slightly so preceding setup effects in the same chain
                // (notably the 3419 staging effect on direct glider launch) apply before movement begins.
                var startTime = context.Shard.CurrentTime + startLeadMs;
                var endTime = startTime + durationMs;

                Logger.Debug(
                    "{Command} {CommandId}: abilityId={AbilityId}, resolvedStrength={ResolvedStrength}, loft={Loft}, falloff={Falloff}, beforeVelocity={BeforeVelocity}, afterVelocity={AfterVelocity}, startLeadMs={StartLeadMs}, startTime={StartTime}, endTime={EndTime}, movement={MovementDebug}",
                    nameof(ForcePushCommand),
                    Params.Id,
                    context.AbilityId,
                    pushStrength,
                    Params.Loft,
                    Params.Falloff,
                    beforeVelocity,
                    velocity,
                    startLeadMs,
                    startTime,
                    endTime,
                    character.DescribeMovementTransitionDebugState());

                var player = character.Player;
                var message = new ForcedMovement
                {
                    Data = new AeroMessages.GSS.V66.ForcedMovementData
                    {
                        Type = 5,
                        Params5 = new AeroMessages.GSS.V66.ForcedMovementType5Params
                        {
                            Velocity = velocity,
                            Time1 = startTime,
                            Time2 = endTime,
                            Unk2 = 0
                        }
                    },

                    ShortTime = context.Shard.CurrentShortTime,
                };
                player.NetChannels[ChannelType.ReliableGss].SendMessage(message, character.EntityId);
            }
        }

        return true;
    }
}