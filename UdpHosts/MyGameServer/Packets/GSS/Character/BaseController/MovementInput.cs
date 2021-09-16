﻿using MyGameServer.Enums.GSS.Character;
using MyGameServer.Packets.Common;
using Shared.Udp;

namespace MyGameServer.Packets.GSS.Character.BaseController
{
    [GSSMessage(Enums.GSS.Controllers.Character_BaseController, (byte)Commands.MovementInput)]
    public class MovementInput
    {
        [Field] public Vector AimDirection;

        [Field] public ushort LastJumpTimer;

        [Field] public Vector Position;

        [Field] public Quaternion Rotation;

        [Field] public ushort ShortTime;

        [Field] public ushort State;

        [Field] public ushort Time;

        [Field] public byte UnkByte1;

        [Field] public byte UnkByte2;

        [Field] public byte UnkByte3;

        [Field] public byte UnkByte4;

        [Field] public byte UnkByte5;

        [Field] public int UnkInt1;

        [Field] public ushort UnkUShort1;

        [Field] public ushort UnkUShort2;

        [Field] public ushort UnkUShort3;

        [Field] public ushort UnkUShort4;

        [Field] public Vector Velocity;
    }
}