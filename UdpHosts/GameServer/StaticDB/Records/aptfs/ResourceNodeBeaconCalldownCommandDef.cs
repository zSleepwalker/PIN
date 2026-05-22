namespace GameServer.Data.SDB.Records.aptfs;
public record class ResourceNodeBeaconCalldownCommandDef : ICommandDef
{
    public float MaxSlope { get; set; }
    public uint LandedAbility { get; set; }
    public uint CompletedAbility { get; set; }
    public uint Visualrecord { get; set; }
    public uint Calldownfx { get; set; }
    public float MaxRange { get; set; }
    public uint DeathAbility { get; set; }
    public float OutdoorsHeight { get; set; }
    public float Health { get; set; }
    public uint CalldownTimeMs { get; set; }
    public float Radius { get; set; }
    public uint ResourceNodeBeaconId { get; set; }
    public uint Id { get; set; }
    public byte Tier { get; set; }
    public byte GroundAlignment { get; set; }

    /// <summary>
    /// Optional override for the resource node type spawned by this thumper.
    /// Not currently present in the SDB (will be 0 unless the SDB is extended).
    /// When 0, the command falls back to <see cref="SDBInterface.GetDefaultThumperNodeTypeId"/>.
    /// </summary>
    public uint NodeTypeId { get; set; }
}