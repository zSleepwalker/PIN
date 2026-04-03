namespace GameServer.Data.SDB.Records.customdata;

public record RegisterEffectTagTriggerCommandDef : ICommandDef
{
    public uint Id { get; set; }
    public uint TagId { get; set; }
    public uint StackCount { get; set; }
    public uint AbilityId { get; set; }
    public uint Chain { get; set; }
    public byte Negate { get; set; }
    public string Comment { get; set; }
}