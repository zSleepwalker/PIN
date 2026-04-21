using FauFau.Util.CommmonDataTypes;

namespace GameServer.Data.SDB.Records.apttf;
public record class tfCustomPlayerCameraCommandDef : ICommandDef
{
    public Vector3 LookOffset { get; set; }
    public Vector3 RelativeOffset { get; set; }
    public float FieldOfView { get; set; }
    public float LookChangeTime { get; set; }
    public int DownAimClampDegrees { get; set; }
    public int UpAimClampDegrees { get; set; }
    public float PositionChangeTime { get; set; }
    public float ExitChangeTime { get; set; }
    public uint Id { get; set; }
    public byte UseAimOrientation { get; set; }
}