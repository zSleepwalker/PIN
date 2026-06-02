using System;
using AeroMessages.GSS.V66.Character;
using AeroMessages.GSS.V66.Character.Event;
using GameServer.Entities;
using GameServer.Entities.Character;

namespace GameServer.Systems.MovementRelay;

public class MovementRelay
{
    private readonly Shard _shard;

    public MovementRelay(Shard shard)
    {
        _shard = shard;
    }

    public void CharacterMovementInput(INetworkClient client, IEntity entity, AeroMessages.GSS.V66.Character.Command.MovementInput input)
    {
        var character = entity as Entities.Character.CharacterEntity;
        var previousAirborne = character.IsAirborne;
        var previousPosition = character.Position;
        var previousMovementStateValue = character.MovementStateContainer.MovementStateValue;
        var previousMovestate = character.MovementStateContainer.Movestate;

        // Update our data based on the clients input
        var poseData = input.PoseData;
        var posRotState = poseData.PosRotState;
        character.SetPoseData(poseData, input.ShortTime);

        bool sendJumpActioned = poseData.TimeSinceLastJump < character.TimeSinceLastJump; // Compare the old value before updating 
        character.TimeSinceLastJump = poseData.TimeSinceLastJump;

        character.IsAirborne = poseData.GroundTimePositiveAirTimeNegative < 0;

        var movementStateValue = posRotState.MovementState;
        character.MovementStateContainer.MovementStateValue = (ushort)movementStateValue;

        // Update with physics
        _shard.Physics.UpdateEntity(character);
        var rawMovementFlags = (MovementFlags)(movementStateValue & 0x00FF);
        bool moveInputRequested = input.HorizontalInput != 0
            || input.VerticalInput != 0
            || input.InputFlags.HasFlag(MovementInputFlags.Sprinting)
            || input.InputFlags.HasFlag(MovementInputFlags.SprintPressed)
            || rawMovementFlags.HasFlag(MovementFlags.Movement)
            || rawMovementFlags.HasFlag(MovementFlags.Sprint);

        if (character.HasRegisteredMovementEffects())
        {
            Serilog.Log.Information(
                "[MovementRelay] Entity {Entity}, shortTime={ShortTime}, rawState=0x{PreviousMovementState:X4}->0x{CurrentMovementState:X4}, movestate={PreviousMovestate}->{CurrentMovestate}, airborne={PreviousAirborne}->{CurrentAirborne}, groundAirTimer={GroundAirTimer}, velocity={Velocity}, movement={MovementDebug}",
                character,
                input.ShortTime,
                previousMovementStateValue,
                character.MovementStateContainer.MovementStateValue,
                previousMovestate,
                character.MovementStateContainer.Movestate,
                previousAirborne,
                character.IsAirborne,
                poseData.GroundTimePositiveAirTimeNegative,
                poseData.Velocity,
                character.DescribeMovementTransitionDebugState());
        }

        if (previousMovestate != character.MovementStateContainer.Movestate)
        {
            character.SyncMovementEffectStatusEffects($"movement state changed 0x{previousMovementStateValue:X4} ({previousMovestate}) -> 0x{character.MovementStateContainer.MovementStateValue:X4} ({character.MovementStateContainer.Movestate})");
        }

        if (previousMovementStateValue != character.MovementStateContainer.MovementStateValue && character.IsRecoveryTraceActive())
        {
            character.TraceRecoveryState($"movement state changed 0x{previousMovementStateValue:X4} ({previousMovestate}) -> 0x{character.MovementStateContainer.MovementStateValue:X4} ({character.MovementStateContainer.Movestate})");
        }

        if (previousAirborne != character.IsAirborne && character.IsRecoveryTraceActive())
        {
            character.TraceRecoveryState($"airborne changed {previousAirborne} -> {character.IsAirborne}");
        }

        // Apply basic fall damage when transitioning from airborne to grounded.
        if (previousAirborne && !character.IsAirborne && character.Character_BaseController != null)
        {
            var combatFlags = character.Character_CombatController?.CombatFlagsProp.Value ?? 0;
            bool immuneFallDamage = combatFlags.HasFlag(CombatFlagsData.CharacterCombatFlags.immune_falldamage);
            if (!immuneFallDamage)
            {
                float downwardSpeed = Math.Max(0f, Math.Max(-poseData.Velocity.Y, -poseData.Velocity.Z));
                const float minDamageSpeed = 22.0f;
                const float damagePerSpeedUnit = 2.5f;
                if (downwardSpeed > minDamageSpeed)
                {
                    int damage = (int)((downwardSpeed - minDamageSpeed) * damagePerSpeedUnit);
                    if (damage > 0)
                    {
                        int currentHealth = character.Character_BaseController.CurrentHealthProp;
                        int maxHealth = Math.Max(1, character.MaxHealth.Value);
                        int newHealth = Math.Clamp(currentHealth - damage, 0, maxHealth);
                        if (newHealth != currentHealth)
                        {
                            character.Character_BaseController.CurrentHealthProp = newHealth;
                            character.Character_ObserverView.CurrentHealthPctProp = (byte)Math.Clamp((newHealth * 100) / maxHealth, 0, 100);
                            _shard.EntityMan.FlushChanges(character);
                            Serilog.Log.Information(
                                "[FallDamage] Character {Character} took {Damage} ({OldHealth}->{NewHealth}/{MaxHealth}) speed={Speed:F2}",
                                character,
                                damage,
                                currentHealth,
                                newHealth,
                                maxHealth,
                                downwardSpeed);
                        }
                    }
                }
            }
        }

        if (moveInputRequested || (character.IsMoving && (previousMovementStateValue != character.MovementStateContainer.MovementStateValue || previousPosition != character.Position)))
        {
            if (character.CancelTimedActivationsOnMove(_shard.CurrentTime))
            {
                Serilog.Log.Information("[MovementRelay] Cancelled active ability state on move input for {Entity}", character);
            }
        }

        // Confirm the pose with the client
        var confirmedPose = new ConfirmedPoseUpdate
        {
            PoseData = new MovementPoseData
            {
                ShortTime = input.ShortTime,
                MovementType = MovementDataType.PosRotState,
                WaterLevelAndDesc = poseData.WaterLevelAndDesc,
                PosRotState = new MovementPosRotState
                            {
                                Pos = character.Position,
                                Rot = character.Orientation,
                                MovementState = movementStateValue // ToDo: This was ushort previously!
                            },
                Velocity = character.Velocity,
                JetpackEnergy = poseData.JetpackEnergy,
                GroundTimePositiveAirTimeNegative = poseData.GroundTimePositiveAirTimeNegative, // Somehow affects gravity
                TimeSinceLastJump = poseData.TimeSinceLastJump,
                HaveDebugData = 0
            },
            NextShortTime = unchecked((ushort)(input.ShortTime + 90)) // This value has to be in the future, nobody cares why.
        };
        client.NetChannels[ChannelType.UnreliableGss].SendMessage(confirmedPose, character.EntityId);

        if (sendJumpActioned && character.IsRecoveryTraceActive())
        {
            character.TraceRecoveryState("jump actioned");
        }

        // Forward update to remote clients
        var currentPose = new CurrentPoseUpdate
        {
            Data = new AeroMessages.GSS.V66.CurrentPoseUpdateData
            {
                Flags = 0x00,
                ShortTime = character.MovementShortTime,
                UnkAlwaysPresent = 0x79,
                MovementState = (ushort)character.MovementState,
                Position = character.Position,
                Rotation = character.Orientation,
                Aim = character.AimDirection,
            }
        };
        foreach (var remoteClient in _shard.Clients.Values)
        {
            if (remoteClient.Status.Equals(IPlayer.PlayerStatus.Playing))
            {
                if (sendJumpActioned)
                {
                    remoteClient.NetChannels[ChannelType.UnreliableGss].SendMessage(new JumpActioned { ShortTime = input.ShortTime }, character.EntityId);
                }


                remoteClient.NetChannels[ChannelType.UnreliableGss].SendMessage(currentPose, character.EntityId);
            }
        }
    }

    public void VehicleMovementInput(INetworkClient client, IEntity entity, AeroMessages.GSS.V66.Vehicle.Command.MovementInput input)
    {
        var vehicle = entity as Entities.Vehicle.VehicleEntity;
        vehicle.SetPoseData(input);

        // Update with physics
        _shard.Physics.UpdateEntity(vehicle);

        if (vehicle.ControllingPlayer?.CharacterEntity != null)
        {
            var character = vehicle.ControllingPlayer.CharacterEntity;
            character.SetPosition(input.Position);
            CharacterMovementInput(client, character, new AeroMessages.GSS.V66.Character.Command.MovementInput()
            {
                ShortTime = client.AssignedShard.CurrentShortTime,
                PoseData = new MovementPoseData()
                {
                    ShortTime = client.AssignedShard.CurrentShortTime,
                    MovementType = MovementDataType.PosRotState,
                    WaterLevelAndDesc = 0,
                    PosRotState = new MovementPosRotState()
                    {
                        Pos = input.Position,
                        Rot = character.Orientation,
                        MovementState = unchecked((short)0xd000)
                    },
                    Velocity = character.Velocity,
                    JetpackEnergy = 0x639c,
                    GroundTimePositiveAirTimeNegative = 0,
                    TimeSinceLastJump = character.TimeSinceLastJump,
                    HaveDebugData = 0
                }
            });
        }
    }
}