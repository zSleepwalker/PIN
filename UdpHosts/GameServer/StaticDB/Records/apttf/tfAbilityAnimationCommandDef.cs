namespace GameServer.Data.SDB.Records.apttf;
public record class tfAbilityAnimationCommandDef : ICommandDef
{
    public uint SubStateIndex { get; set; }
    public uint QueueTimeOffset { get; set; }
    public uint AbilityAnimIndex { get; set; }
    public uint BackpackState { get; set; }
    public uint Id { get; set; }
    public uint Cancel { get; set; }
    public uint AllowReloads { get; set; }
    public uint Outro { get; set; }
    public uint Combo { get; set; }
    public uint AllowAiming { get; set; }
    public uint MovementTime { get; set; }
    public uint FullBody { get; set; }
}