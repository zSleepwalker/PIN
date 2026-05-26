using System.Collections.Generic;
using GameServer.Data.SDB.Records.apt;

namespace GameServer.Aptitude;

public class TargetDifferenceCommand : Command, ICommand
{
    private TargetDifferenceCommandDef Params;

    public TargetDifferenceCommand(TargetDifferenceCommandDef par)
: base(par)
    {
        Params = par;
    }

    public bool Execute(Context context)
    {
        var previousTargets = context.Targets;
        var previousFormerTargets = context.FormerTargets;

        var formerLookup = new HashSet<IAptitudeTarget>(previousFormerTargets);
        var newTargets = new AptitudeTargets();
        foreach (var target in previousTargets)
        {
            if (!formerLookup.Contains(target))
            {
                newTargets.Push(target);
            }
        }

        context.Targets = newTargets;

        if (Params.ReplaceFormer == 1)
        {
            context.FormerTargets = new AptitudeTargets(previousTargets);
        }

        if (Params.SwapCurrentFormer == 1)
        {
            (context.Targets, context.FormerTargets) = (context.FormerTargets, context.Targets);
        }

        return true;
    }
}