﻿using MyGameServer.Enums.GSS.Generic;
using MyGameServer.Packets;

namespace MyGameServer.Controllers
{
    [ControllerID(Enums.GSS.Controllers.Generic2)]
    public class Generic2 : Base
    {
        public override void Init(INetworkClient client, IPlayer player, IShard shard)
        {
        }

        [MessageID((byte)Commands.RequestLogout)]
        public void RequestLogout(INetworkClient client, IPlayer player, ulong EntityID, GamePacket packet)
        {
        }
    }
}