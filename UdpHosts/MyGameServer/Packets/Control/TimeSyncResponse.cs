﻿using MyGameServer.Enums;
using System.Runtime.InteropServices;

namespace MyGameServer.Packets.Control
{
    [ControlMessage(ControlPacketType.TimeSyncResponse)]
    [StructLayout(LayoutKind.Sequential, Pack = 0)]
    public readonly struct TimeSyncResponse
    {
        public readonly ulong ClientTime;
        public readonly ulong ServerTime;

        public TimeSyncResponse(ulong clientTime, ulong serverTime)
        {
            ClientTime = clientTime;
            ServerTime = serverTime;
        }
    }
}