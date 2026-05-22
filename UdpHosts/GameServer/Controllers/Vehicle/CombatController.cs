using AeroMessages.GSS.V66.Vehicle.Command;
using AeroMessages.GSS.V66.Vehicle.Event;
using GameServer.Aptitude;
using GameServer.Entities.Vehicle;
using GameServer.Enums.GSS.Vehicle;
using GameServer.Extensions;
using GameServer.Packets;
using Serilog;

namespace GameServer.Controllers.Vehicle;

[ControllerID(Enums.GSS.Controllers.Vehicle_CombatController)]
public class CombatController : Base
{
    private ILogger _logger;

    public override void Init(INetworkClient client, IPlayer player, IShard shard, ILogger logger)
    {
        _logger = logger.ForContext<VehicleEntity>();
    }

    [MessageID((byte)Commands.ActivateAbility)]
    public void ActivateAbility(INetworkClient client, IPlayer player, ulong entityId, GamePacket packet)
    {
        var activateAbility = packet.Unpack<ActivateAbility>();

        var vehicle = client.AssignedShard.Entities[entityId & 0xffffffffffffff00] as VehicleEntity;

        var abilityId = vehicle.Abilities[(byte)activateAbility.AbilitySlotIndex];

        var character = player.CharacterEntity;
        var shard = character.Shard;

        if (character.IsPlayerControlled)
        {
            var message = new AbilityActivated() { AbilityId = abilityId, Time = activateAbility.Time };

            character.Player.NetChannels[ChannelType.ReliableGss].SendMessage(message, character.EntityId);
        }

        shard.Abilities.HandleActivateAbility(shard, vehicle, abilityId, activateAbility.Time, new AptitudeTargets(vehicle));
    }

    [MessageID((byte)Commands.DeactivateAbility)]
    public void DeactivateAbility(INetworkClient client, IPlayer player, ulong entityId, GamePacket packet)
    {
        var deactivateAbility = packet.Unpack<DeactivateAbility>();
        if (deactivateAbility == null)
        {
            return;
        }

        if (!client.AssignedShard.Entities.TryGetValue(entityId & 0xffffffffffffff00, out var entity))
        {
            return;
        }

        var vehicle = entity as VehicleEntity;
        if (vehicle == null)
        {
            return;
        }

        if (!vehicle.Abilities.TryGetValue((byte)deactivateAbility.AbilitySlotIndex, out var abilityId) || abilityId == 0)
        {
            _logger.Warning("DeactivateAbility Slot {Slot}: no ability mapped for vehicle 0x{EntityId:X}",
                deactivateAbility.AbilitySlotIndex, entityId);
            return;
        }

        _logger.Information("DeactivateAbility Slot {Slot} AbilityId={AbilityId} on vehicle 0x{EntityId:X}",
            deactivateAbility.AbilitySlotIndex, abilityId, entityId);

        var character = player.CharacterEntity;
        character.EndAbilityActivation(abilityId, deactivateAbility.Time, notifyClient: true,
            sendFailureFallback: false, suppressCooldownOnManualDeactivation: true);
    }
}