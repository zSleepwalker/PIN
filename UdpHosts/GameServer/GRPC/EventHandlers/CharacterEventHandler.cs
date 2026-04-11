using System.Collections.Generic;
using System.Linq;
using GrpcGameServerAPIClient;
using CharacterEntity = GameServer.Entities.Character.CharacterEntity;

namespace GameServer.GRPC.EventHandlers;

public static class CharacterEventHandler
{
    public static void HandleEvent(CharacterVisualsUpdated e, IDictionary<uint, INetworkPlayer> clients)
    {
        var player = clients.Values.FirstOrDefault(p => p.CharacterId + 0xFE == e.CharacterGuid);
        var character = player?.CharacterEntity;
        if (character == null)
        {
            return;
        }

        character.LoadRemote(e.CharacterAndBattleframeVisuals);

        // Push updated static info immediately so body/gender swaps don't wait for relog.
        character.Shard.EntityMan.FlushChanges(character);

        // Refresh just the views so body/gender visuals rebuild without removing self controllers.
        foreach (var client in clients.Values)
        {
            RefreshCharacterViews(client, character);
        }
    }

    private static void RefreshCharacterViews(INetworkPlayer client, CharacterEntity character)
    {
        if (!client.CanReceiveGSS)
        {
            return;
        }

        var channel = client.NetChannels[ChannelType.ReliableGss];
        var entityId = character.EntityId;

        if (character.IsPlayerControlled && character.Player == client)
        {
            var baseController = character.Character_BaseController;
            if (baseController != null)
            {
                channel.SendControllerKeyframe(baseController, entityId, client.PlayerId);
            }
        }

        if (character.Character_ObserverView != null)
        {
            channel.SendViewScopeOut(character.Character_ObserverView, entityId);
            channel.SendViewKeyframe(character.Character_ObserverView, entityId);
        }

        if (character.Character_EquipmentView != null)
        {
            channel.SendViewKeyframe(character.Character_EquipmentView, entityId);
        }
    }
}