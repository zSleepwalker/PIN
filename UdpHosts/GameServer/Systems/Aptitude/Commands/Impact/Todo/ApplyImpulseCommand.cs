using System;
using System.Numerics;
using AeroMessages.GSS.V66;
using AeroMessages.GSS.V66.Character.Event;
using GameServer.Data.SDB.Records.aptfs;
using GameServer.Entities.Character;

namespace GameServer.Aptitude;

public class ApplyImpulseCommand : Command, ICommand
{
    private ApplyImpulseCommandDef Params;

    public ApplyImpulseCommand(ApplyImpulseCommandDef par)
: base(par)
    {
        Params = par;
    }

    public bool Execute(Context context)
    {
        if (context.Self is not CharacterEntity character)
        {
            return true;
        }

        var duration = Params.Duration > 0 ? Params.Duration : 1000u;
        character.ForcedMovementEndTime = context.Shard.CurrentTime + duration;

        context.Actives[this] = new ApplyImpulseCommandActiveContext
        {
            EndTime = character.ForcedMovementEndTime,
        };

        if (character.IsPlayerControlled)
        {
            var speed = AbilitySystem.RegistryOp(context.Register, Params.Speed, (Enums.Operand)Params.SpeedRegop);
            var yaw = Params.Yawangle;
            var loft = Params.Loftangle;

            // Build velocity from character aim direction modified by loft and yaw angles
            var forward = character.AimDirection;
            var velocity = forward * speed;

            // Tilt upward by loft angle (positive loft = upward component)
            var horizontalSpeed = (float)(speed * Math.Cos(loft));
            var verticalSpeed = (float)(speed * Math.Sin(loft));
            velocity = new Vector3(forward.X * horizontalSpeed, forward.Y * horizontalSpeed, verticalSpeed);

            var startTime = (uint)context.Shard.CurrentTime;
            var endTime = startTime + duration;

            character.Player.NetChannels[ChannelType.ReliableGss].SendMessage(
                new ForcedMovement
                {
                    Data = new AeroMessages.GSS.V66.ForcedMovementData
                    {
                        Type = 0x05,
                        Unk1 = Params.Id,
                        HaveUnk2 = 0,
                        Params5 = new AeroMessages.GSS.V66.ForcedMovementType5Params
                        {
                            Velocity = velocity,
                            Time1 = startTime,
                            Time2 = endTime,
                            Unk2 = Params.AllowPrediction,
                        },
                    },
                    ShortTime = context.Shard.CurrentShortTime,
                },
                character.EntityId);
        }

        return true;
    }

    public void OnRemove(Context context, ICommandActiveContext activeCommandContext)
    {
        if (context.Self is not CharacterEntity character)
        {
            return;
        }

        if (activeCommandContext is not ApplyImpulseCommandActiveContext impulseCtx)
        {
            return;
        }

        // Only the active/owning impulse instance may clear/cancel movement.
        // This avoids stale remove callbacks from cancelling a newer impulse.
        bool ownsCurrentImpulse = character.ForcedMovementEndTime == impulseCtx.EndTime;
        if (!ownsCurrentImpulse)
        {
            return;
        }

        bool endedNaturally = context.Shard.CurrentTime >= impulseCtx.EndTime;
        character.ForcedMovementEndTime = 0;

        // Send cancel only when removing before natural end time.
        if (!endedNaturally && character.IsPlayerControlled)
        {
            Serilog.Log.Information($"ApplyImpulseCommand Sending ForcedMovementCancelled {Params.Id}");
            character.Player.NetChannels[ChannelType.ReliableGss].SendMessage(
                new ForcedMovementCancelled
                {
                    CommandId = Params.Id,
                    ShortTime = context.Shard.CurrentShortTime,
                },
                character.EntityId);
        }
    }
}

public class ApplyImpulseCommandActiveContext : ICommandActiveContext
{
    public ulong EndTime { get; set; }
}