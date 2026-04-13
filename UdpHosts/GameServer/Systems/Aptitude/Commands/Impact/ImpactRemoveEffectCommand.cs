using System.Linq;
using GameServer.Data.SDB.Records.customdata;

namespace GameServer.Aptitude;

public class ImpactRemoveEffectCommand : Command, ICommand
{
    private ImpactRemoveEffectCommandDef Params;

    public ImpactRemoveEffectCommand(ImpactRemoveEffectCommandDef par)
: base(par)
    {
        Params = par;
    }

    public bool Execute(Context context)
    {
        if (Params.EffectId != null)
        {
            uint effectId = (uint)Params.EffectId;
            if (Params.RemoveFromSelf != null && Params.RemoveFromSelf == true)
            {
                context.Abilities.DoRemoveEffect(context.Self, effectId);
            }
            else
            {
                foreach (IAptitudeTarget target in context.Targets)
                {
                    context.Abilities.DoRemoveEffect(target, effectId);
                }
            }
        }
        else
        {
            var selfEffectIds = string.Join(", ", context.Self.GetActiveEffects().Where(activeEffect => activeEffect?.Effect != null).Select(activeEffect => activeEffect.Effect.Id));
            Logger.Warning("Active Effects (Self): {Message}", selfEffectIds);

            if (context.Targets.Count == 0)
            {
                Logger.Warning("Active Effects (Targets): none (target count is 0)");
            }
            else
            {
                foreach (var target in context.Targets)
                {
                    var targetEffectIds = string.Join(", ", target.GetActiveEffects().Where(activeEffect => activeEffect?.Effect != null).Select(activeEffect => activeEffect.Effect.Id));
                    Logger.Warning("Active Effects (Target {Target}): {Message}", target, targetEffectIds);
                }
            }

            Logger.Warning("Don't know which effect to remove for {Command} {CommandId}", nameof(ImpactRemoveEffectCommand), Params.Id);
        }

        return true;
    }
}