namespace GameServer.Data.SDB.Records.customdata;

public record ApplyPermanentEffectCommandDef : ICommandDef
{
    public uint Id { get; set; }
    public uint EffectId { get; set; }
    public uint DurationSeconds { get; set; }
    public float ExperienceBoost { get; set; }
    public float ResourceBoost { get; set; }
    public float ReputationBoost { get; set; }
    public byte Permanent { get; set; }
}