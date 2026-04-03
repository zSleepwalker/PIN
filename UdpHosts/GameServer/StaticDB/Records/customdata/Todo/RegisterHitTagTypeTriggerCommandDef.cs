namespace GameServer.Data.SDB.Records.customdata;

public record RegisterHitTagTypeTriggerCommandDef : ICommandDef
{
    public uint Id { get; set; }
    public uint HitTagTypeId { get; set; }
    public uint AbilityId { get; set; }
    public uint Chain { get; set; }
    public string Comment { get; set; }
}