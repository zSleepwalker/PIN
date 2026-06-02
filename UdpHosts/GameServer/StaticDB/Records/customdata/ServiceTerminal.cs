using System.Numerics;

namespace GameServer.Data.SDB.Records.customdata;

public record class ServiceTerminal
{
    public uint Id { get; set; }
    public uint ZoneId { get; set; }

    public uint DeployableType { get; set; }
    public Vector3 Position { get; set; }
    public Quaternion Orientation { get; set; } = Quaternion.Identity;

    public int TerminalType { get; set; }
    public int TerminalId { get; set; }

    // Optional metadata for authoring and debugging.
    public string Name { get; set; } = string.Empty;
    public string ServiceType { get; set; } = string.Empty;
    public float Radius { get; set; }
}
