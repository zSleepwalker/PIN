using GameServer.Data.SDB.Records.customdata;

namespace GameServer.Aptitude;

public class ReduceCooldownsCommand : Command, ICommand
{
    private ReduceCooldownsCommandDef Params;

    public ReduceCooldownsCommand(ReduceCooldownsCommandDef par)
    : base(par)
    {
        Params = par;
    }

    public bool Execute(Context context)
    {
        uint currentTime = CooldownCommandSupport.ResolveCommandTime(context);
        uint reductionMs = CooldownCommandSupport.ClampDuration(context.Register);
        if (reductionMs == 0)
        {
            return true;
        }

        foreach (var character in CooldownCommandSupport.ResolveCooldownTargets(context))
        {
            character.ReduceAbilityCooldowns(reductionMs, currentTime);
        }

        return true;
    }
}