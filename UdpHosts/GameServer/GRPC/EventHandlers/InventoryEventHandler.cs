using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using GrpcGameServerAPIClient;

namespace GameServer.GRPC.EventHandlers;

public static class InventoryEventHandler
{
    public static async Task HandleEvent(InventoryUpdated e, IDictionary<uint, INetworkPlayer> clients)
    {
        // Find the player with the matching character GUID
        var player = clients.Values.FirstOrDefault(p => p.CharacterId + 0xFE == e.CharacterGuid);
        
        if (player != null)
        {
            // Trigger an inventory refresh
            await player.Inventory.RefreshFromDatabase();
        }
    }
}
