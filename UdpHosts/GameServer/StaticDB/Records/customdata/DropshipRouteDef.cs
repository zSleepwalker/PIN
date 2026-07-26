using System.Collections.Generic;
using System.Numerics;

namespace GameServer.Data.SDB.Records.customdata;

public record class DropshipRouteDef
{
    public uint Id { get; set; }
    public uint ZoneId { get; set; }
    public string Name { get; set; } = string.Empty;
    public uint ShipType { get; set; } = 2693;
    public byte ShipCount { get; set; } = 1;
    public float SpeedUnitsPerSecond { get; set; } = 45.0f;
    public float LandingSpeedUnitsPerSecond { get; set; } = 10.0f;
    public float StopDurationSeconds { get; set; } = 10.0f;
    public float FlightAltitudeOffset { get; set; } = 300.0f;
    public float HeadingOffsetDegrees { get; set; } = 180.0f;
    public bool GlobalScope { get; set; } = true;
    public float ScopeRange { get; set; } = 5000.0f;
    public List<DropshipRouteStopDef> Stops { get; set; } = new();
}

public record class DropshipRouteStopDef
{
    public string Name { get; set; } = string.Empty;
    public Vector3 Position { get; set; }
}
