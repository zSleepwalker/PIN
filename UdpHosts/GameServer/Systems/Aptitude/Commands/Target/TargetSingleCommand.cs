using GameServer.Data.SDB.Records.apt;

namespace GameServer.Aptitude;

public class TargetSingleCommand : Command, ICommand
{
    private TargetSingleCommandDef Params;

    public TargetSingleCommand(TargetSingleCommandDef par)
: base(par)
    {
        Params = par;
    }

    public bool Execute(Context context)
    {
        // TargetSingle: keep only the first target (or the closest target within range)
        // This effectively limits the target set to 1 entity
        var prevTargets = context.Targets;

        if (prevTargets.Count == 0)
        {
            return Params.UseInitPos == 1; // Return success if using init pos fallback
        }

        var newTargets = new AptitudeTargets();

        // Keep only the first / primary target
        if (prevTargets.TryPeek(out var first))
        {
            newTargets.Push(first);
        }

        context.FormerTargets = prevTargets;
        context.Targets = newTargets;

        return true;
    }
}