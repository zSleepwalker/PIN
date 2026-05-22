namespace GameServer.Data.SDB.Records.dbcharacter;

public record class MonsterVisualOptions
{
    public uint Id { get; set; }
    public uint Female { get; set; }
    public uint Male { get; set; }
}