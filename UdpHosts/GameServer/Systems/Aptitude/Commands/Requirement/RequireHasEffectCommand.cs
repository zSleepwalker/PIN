using GameServer.Data.SDB.Records.aptfs;

namespace GameServer.Aptitude;

public class RequireHasEffectCommand : Command, ICommand
{
    private RequireHasEffectCommandDef Params;

    public RequireHasEffectCommand(RequireHasEffectCommandDef par)
: base(par)
    {
        Params = par;
    }

    public bool Execute(Context context)
    {
        bool result = false;

        if (context.Targets.Count > 0)
        {
            uint matchCounter = 0;
            foreach (IAptitudeTarget target in context.Targets)
            {
                if (!TargetHasMatchingEffect(target, context))
                {
                    result = false;
                    break;
                }

                matchCounter++;
            }

            if (matchCounter == context.Targets.Count)
            {
                result = true;
            }
        }
        else
        {
            result = TargetHasMatchingEffect(context.Self, context);
        }

        if (Params.Negate == 1)
        {
            result = !result;
        }

        return result;
    }

    private bool TargetHasMatchingEffect(IAptitudeTarget target, Context context)
    {
        foreach (EffectState active in target.GetActiveEffects())
        {
            if (active?.Effect == null)
            {
                continue;
            }

            if (active.Effect.Id != Params.EffectId || active.Stacks < Params.StackCount)
            {
                continue;
            }

            if (Params.SameInitiator == 1 && context.Initiator != active.Context.Initiator)
            {
                continue;
            }

            return true;
        }

        return false;
    }
}