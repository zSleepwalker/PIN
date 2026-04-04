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

        // The packet only reports collision context (time/entity), not a damage value.
        // Consume it so it is not treated as an unknown command; damage handling can be
        // implemented later once authoritative collision-damage rules are defined.
        if (query.HaveEntity == 1)
        {
            Log.Information($"Vehicle collision reported by {player.PlayerId}: vehicle=0x{entityId:X}, collidedWith=0x{query.CollidedWithEntity.Backing:X}, shortTime={query.ShortTime}");
        }
    }
}