﻿using System.Runtime.InteropServices;
using GameServer.Enums;

namespace GameServer.Packets.Control;

[ControlMessage(ControlPacketType.TimeSyncRequest)]
[StructLayout(LayoutKind.Sequential, Pack = 1)]
public readonly struct TimeSyncRequest
{
    public readonly ulong ClientTime;

    public TimeSyncRequest(ulong clientTime)
    {
        ClientTime = clientTime;
    }
}