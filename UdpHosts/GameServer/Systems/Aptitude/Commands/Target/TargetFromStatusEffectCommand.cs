using GameServer.Data.SDB.Records.aptfs;
using GameServer.Entities;

namespace GameServer.Aptitude;

public class TargetFromStatusEffectCommand : Command, ICommand
{
    private TargetFromStatusEffectCommandDef Params;

    public TargetFromStatusEffectCommand(TargetFromStatusEffectCommandDef par)
: base(par)
    {
        Params = par;
    }

    public bool Execute(Context context)
    {
        var newTargets = new AptitudeTargets();

        // Gather all targets that have the specified status effect active
        foreach (IAptitudeTarget target in context.Shard.Entities.Values)
        {
            if (target is not BaseAptitudeEntity aptTarget)
            {
                continue;
            }

            foreach (var effect in aptTarget.GetActiveEffects())
            {
                if (effect != null && effect.Effect.Id == Params.StatusfxId)
                {
                    newTargets.Push(aptTarget);
                    break;
                }
            }
        }

        if (Params.AlsoInitiator == 1 && context.Initiator != null)
        {
            newTargets.Push(context.Initiator);
        }

        context.FormerTargets = context.Targets;
        context.Targets = newTargets;

        return true;
    }
}