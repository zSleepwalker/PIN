namespace GameServer.Data.SDB.Records.dbcharacter;

public record class MonsterVisualOption
{
    public uint Parent { get; set; }
    public long Value { get; set; }
    public uint Type { get; set; }
}