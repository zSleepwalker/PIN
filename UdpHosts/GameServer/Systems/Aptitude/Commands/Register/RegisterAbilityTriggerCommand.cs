using GameServer.Data.SDB.Records.customdata;

namespace GameServer.Aptitude;

public class RegisterAbilityTriggerCommand : Command, ICommand
{
    private RegisterAbilityTriggerCommandDef Params;

    private sealed class RegisterAbilityTriggerActiveContext : ICommandActiveContext
    {
        public uint Chain { get; init; }
        public uint AbilityId { get; init; }
    }

    public RegisterAbilityTriggerCommand(RegisterAbilityTriggerCommandDef par)
: base(par)
    {
        Params = par;
    }

    public bool Execute(Context context)
    {
        context.Actives[this] = new RegisterAbilityTriggerActiveContext
        {
            Chain = Params.Chain,
            AbilityId = Params.AbilityId,
        };

        Serilog.Log.Information($"[RegisterAbilityTrigger] Registered command={Params.Id}, chain={Params.Chain}, ability={Params.AbilityId}");
        return true;
    }
}