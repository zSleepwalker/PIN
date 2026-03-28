using GameServer.Enums.GSS.Character;
using GameServer.Packets;
using Serilog;

namespace GameServer.Controllers.Character;

/// <summary>
/// Controller for NPC-specific state (controller ID 3).
/// AeroMessages defines no inbound command messages for this controller;
/// all NPC-related commands (NPCApplyEffect, NPCInteractWithTarget, etc.)
/// are routed via BaseController (controller 2) per their AeroMessageId attributes.
/// </summary>
[ControllerID(Enums.GSS.Controllers.Character_NPCController)]
public class NPCController : Base
{
    public override void Init(INetworkClient client, IPlayer player, IShard shard, ILogger logger)
    {
    }
}
