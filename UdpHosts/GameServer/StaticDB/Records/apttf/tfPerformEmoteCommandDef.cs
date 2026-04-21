namespace GameServer.Data.SDB.Records.apttf;
public record class tfPerformEmoteCommandDef : ICommandDef
{
    public string EmoteName { get; set; }
    public uint Id { get; set; }
}