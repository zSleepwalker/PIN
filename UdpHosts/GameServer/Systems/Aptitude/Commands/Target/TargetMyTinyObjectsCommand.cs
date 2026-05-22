using GameServer.Data.SDB.Records.customdata;

namespace GameServer.Aptitude;

public class TargetMyTinyObjectsCommand : Command, ICommand
{
    private TargetMyTinyObjectsCommandDef Params;

    public TargetMyTinyObjectsCommand(TargetMyTinyObjectsCommandDef par)
: base(par)
    {
        Params = par;
    }

    public bool Execute(Context context)
    {
        // TinyObjects are not tracked as IAptitudeTarget entities; produce an empty
        // target set so downstream targeting/filtering commands behave consistently.
        context.FormerTargets = context.Targets;
        context.Targets = new AptitudeTargets();
        return true;
    }
}