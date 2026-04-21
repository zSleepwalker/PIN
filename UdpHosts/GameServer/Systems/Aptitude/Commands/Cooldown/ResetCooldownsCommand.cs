using GameServer.Data.SDB.Records.customdata;

namespace GameServer.Aptitude;

public class ResetCooldownsCommand : Command, ICommand
{
    private ResetCooldownsCommandDef Params;

    public ResetCooldownsCommand(ResetCooldownsCommandDef par)
    : base(par)
    {
        Params = par;
    }

    public bool Execute(Context context)
    {
        uint currentTime = CooldownCommandSupport.ResolveCommandTime(context);
        foreach (var character in CooldownCommandSupport.ResolveCooldownTargets(context))
        {
            character.ResetAbilityCooldowns(currentTime);
        }

        return true;
    }
}