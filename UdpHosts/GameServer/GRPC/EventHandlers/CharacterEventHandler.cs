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

        // Re-assert local combat permissions after a live visuals refresh.
        // This keeps weapon usage enabled if the client enters a stale local control state.
        character.SetPermissionFlag(AeroMessages.GSS.V66.Character.Controller.PermissionFlagsData.CharacterPermissionFlags.weapon, true);
        character.SetPermissionFlag(AeroMessages.GSS.V66.Character.Controller.PermissionFlagsData.CharacterPermissionFlags.abilities, true);

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
            // Force a full owner-side reload so body/gender swaps are rebuilt exactly
            // as if the character re-entered scope after a visuals update.
            character.Shard.EntityMan.ScopeOut(client, character);
            character.Shard.EntityMan.ScopeIn(client, character);
        }

        if (character.Character_ObserverView != null)
        {
            channel.SendViewScopeOut(character.Character_ObserverView, entityId);
            channel.SendViewKeyframe(character.Character_ObserverView, entityId);
        }

        if (character.Character_EquipmentView != null)
        {
            channel.SendViewScopeOut(character.Character_EquipmentView, entityId);
            channel.SendViewKeyframe(character.Character_EquipmentView, entityId);
        }

        if (character.Character_CombatView != null)
        {
            channel.SendViewScopeOut(character.Character_CombatView, entityId);
            channel.SendViewKeyframe(character.Character_CombatView, entityId);
        }

        if (character.Character_MovementView != null)
        {
            channel.SendViewScopeOut(character.Character_MovementView, entityId);
            channel.SendViewKeyframe(character.Character_MovementView, entityId);
        }

        if (character.Character_TinyObjectView != null)
        {
            channel.SendViewScopeOut(character.Character_TinyObjectView, entityId);
            channel.SendViewKeyframe(character.Character_TinyObjectView, entityId);
        }
    }
}