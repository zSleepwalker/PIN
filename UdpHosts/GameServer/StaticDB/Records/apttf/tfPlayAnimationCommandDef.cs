namespace GameServer.Data.SDB.Records.apttf;
public record class tfPlayAnimationCommandDef : ICommandDef
{
    public string AnimationName { get; set; }
    public uint Id { get; set; }
    public byte Param2 { get; set; }
    public byte Param3 { get; set; }
}