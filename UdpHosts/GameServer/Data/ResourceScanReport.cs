using System.Numerics;
using AeroMessages.GSS.V66;

namespace GameServer.Data;

public sealed class ResourceScanReport
{
    public uint ScanId { get; init; }

    public Vector3 Position { get; init; }

    public ulong OwnerEntityId { get; init; }

    public uint RadiusMeters { get; init; }

    public ResourceCompositionData[] Composition { get; init; } = [];
}