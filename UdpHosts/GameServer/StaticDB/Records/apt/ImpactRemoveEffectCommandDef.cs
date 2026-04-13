namespace GameServer.Data.SDB.Records.apt;
public record class ImpactRemoveEffectCommandDef : ICommandDef
{
    public uint EffectId { get; set; }
    public uint Id { get; set; }
    public byte RemoveFromSelf { get; set; }
}
