using Serilog;

namespace GameServer.Controllers.Character;

/// <summary>
/// Handles local effects applied only on the owning client.
/// This controller (ID 6) carries no inbound command messages as defined in AeroMessages.
/// </summary>
[ControllerID(Enums.GSS.Controllers.Character_LocalEffectsController)]
public class LocalEffectsController : Base
{
    public override void Init(INetworkClient client, IPlayer player, IShard shard, ILogger logger)
    {
    }
}