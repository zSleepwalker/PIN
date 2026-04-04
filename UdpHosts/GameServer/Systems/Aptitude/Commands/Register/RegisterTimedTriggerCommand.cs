using GameServer.Data.SDB.Records.customdata;

namespace GameServer.Aptitude;

public class RegisterTimedTriggerCommand : Command, ICommand
{
    private RegisterTimedTriggerCommandDef Params;

    private sealed class RegisterTimedTriggerActiveContext : ICommandActiveContext
    {
        public uint Chain { get; init; }
        public uint AbilityId { get; init; }
        public uint IntervalMs { get; init; }
    }

    public RegisterTimedTriggerCommand(RegisterTimedTriggerCommandDef par)
: base(par)
    {
        Params = par;
    }

    public bool Execute(Context context)
    {
        context.Actives[this] = new RegisterTimedTriggerActiveContext
        {
            Chain = Params.Chain,
            AbilityId = Params.AbilityId,
            IntervalMs = Params.IntervalMs,
        };

        Serilog.Log.Information($"[RegisterTimedTrigger] Registered command={Params.Id}, chain={Params.Chain}, ability={Params.AbilityId}, intervalMs={Params.IntervalMs}");
        return true;
    }
}