using GameServer.Data.SDB.Records.customdata;

namespace GameServer.Aptitude;

public class TargetTinyObjectCommand : Command, ICommand
{
    private TargetTinyObjectCommandDef Params;

    public TargetTinyObjectCommand(TargetTinyObjectCommandDef par)
: base(par)
    {
        Params = par;
    }

    public bool Execute(Context context)
    {
        // TinyObjects are not tracked as IAptitudeTarget entities; replace the target
        // set with an empty collection so downstream commands see no tiny-object targets.
        context.FormerTargets = context.Targets;
        context.Targets = new AptitudeTargets();
        return true;
    }
}