using System;
using AeroMessages.GSS.V66.Vehicle.Command;
using GameServer.Entities;
using GameServer.Enums.GSS.Vehicle;
using GameServer.Extensions;
using GameServer.Packets;
using Serilog;

namespace GameServer.Controllers.Vehicle;

[ControllerID(Enums.GSS.Controllers.Vehicle_BaseController)]
public class BaseController : Base
{
    public override void Init(INetworkClient client, IPlayer player, IShard shard, ILogger logger)
    {
    }

    [MessageID((byte)Commands.MovementInput)]
    public void MovementInput(INetworkClient client, IPlayer player, ulong entityId, GamePacket packet)
    {
        var movementInput = packet.Unpack<MovementInput>();
        client.AssignedShard.Entities.TryGetValue(entityId & 0xffffffffffffff00, out IEntity entity);
        if (entity == null)
        {
            return;
        }

        var vehicle = entity as Entities.Vehicle.VehicleEntity;
        if (vehicle.ControllingPlayer == player)
        {
            client.AssignedShard.Movement.VehicleMovementInput(client, vehicle, movementInput);
        }
    }

    [MessageID((byte)Commands.SetWaterLevelAndDesc)]
    public void SetWaterLevelAndDesc(INetworkClient client, IPlayer player, ulong entityId, GamePacket packet)
    {
        var query = packet.Unpack<SetWaterLevelAndDesc>();
        client.AssignedShard.Entities.TryGetValue(entityId & 0xffffffffffffff00, out IEntity entity);
        if (entity == null)
        {
            return;
        }

        var vehicle = entity as Entities.Vehicle.VehicleEntity;
        vehicle.SetWaterLevelAndDesc(query.Value);
    }

    [MessageID((byte)Commands.SetEffectsFlag)]
    public void SetEffectsFlag(INetworkClient client, IPlayer player, ulong entityId, GamePacket packet)
    {
        var query = packet.Unpack<SetEffectsFlag>();
        client.AssignedShard.Entities.TryGetValue(entityId & 0xffffffffffffff00, out IEntity entity);
        if (entity == null)
        {
            return;
        }

        var vehicle = entity as Entities.Vehicle.VehicleEntity;
        vehicle.SetEffectsFlags(query.UnkByte2_HeadlightEnabled);
    }

    [MessageID((byte)Commands.ReceiveCollisionDamage)]
    public void ReceiveCollisionDamage(INetworkClient client, IPlayer player, ulong entityId, GamePacket packet)
    {
        var query = packet.Unpack<ReceiveCollisionDamage>();
        client.AssignedShard.Entities.TryGetValue(entityId & 0xffffffffffffff00, out IEntity entity);
        if (entity == null)
        {
            return;
        }

        var vehicle = entity as Entities.Vehicle.VehicleEntity;
        if (vehicle == null || vehicle.ControllingPlayer != player)
        {
            return;
        }

        // Approximate collision damage from current movement speed. The client packet does
        // not include a damage scalar, so the server derives it from authoritative motion.
        float speed = vehicle.Velocity.Length();
        const float minDamageSpeed = 18.0f;
        const float damagePerSpeedUnit = 4.0f;

        if (speed >= minDamageSpeed)
        {
            int damageAmount = (int)((speed - minDamageSpeed) * damagePerSpeedUnit);
            if (damageAmount > 0)
            {
                uint currentHealth = vehicle.CurrentHealth;
                uint maxHealth = Math.Max(1u, vehicle.MaxHealth);
                uint appliedDamage = (uint)Math.Min(damageAmount, int.MaxValue);
                uint newHealth = currentHealth > appliedDamage ? currentHealth - appliedDamage : 0;

                if (newHealth != currentHealth)
                {
                    vehicle.CurrentHealth = newHealth;
                    if (vehicle.Vehicle_BaseController != null)
                    {
                        vehicle.Vehicle_BaseController.CurrentHealthProp = newHealth;
                        vehicle.Vehicle_BaseController.MaxHealthProp = maxHealth;
                    }

                    if (vehicle.Vehicle_ObserverView != null)
                    {
                        vehicle.Vehicle_ObserverView.CurrentHealthProp = newHealth;
                        vehicle.Vehicle_ObserverView.MaxHealthProp = maxHealth;
                    }

                    client.AssignedShard.EntityMan.FlushChanges(vehicle);

                    Log.Information("Vehicle collision damage applied: vehicle=0x{VehicleId:X}, speed={Speed:F2}, damage={Damage}, health={OldHealth}->{NewHealth}",
                        entityId,
                        speed,
                        damageAmount,
                        currentHealth,
                        newHealth);
                }
            }
        }

        if (query.HaveEntity == 1)
        {
            Log.Information($"Vehicle collision reported by {player.PlayerId}: vehicle=0x{entityId:X}, collidedWith=0x{query.CollidedWithEntity.Backing:X}, shortTime={query.ShortTime}");
        }
    }
}