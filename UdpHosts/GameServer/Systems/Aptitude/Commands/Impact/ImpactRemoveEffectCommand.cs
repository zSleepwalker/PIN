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
            foreach (var target in ResolveTargets(context, fallbackToSelfWhenNoTargets: false))
            {
                context.Abilities.DoRemoveEffect(target, effectId);
            }

            return true;
        }

        if (context.SourceEffect != 0)
        {
            foreach (var target in ResolveTargets(context, fallbackToSelfWhenNoTargets: true))
            {
                var candidates = target.GetActiveEffects()
                    .Where(activeEffect => activeEffect != null)
                    .Where(activeEffect => activeEffect.Effect.Id != context.SourceEffect)
                    .Where(activeEffect => activeEffect.Context.SourceContext == context.SourceEffect
                        || activeEffect.Context.SourceEffect == context.SourceEffect)
                    .ToList();

                foreach (var candidate in candidates)
                {
                    context.Abilities.DoRemoveEffect(candidate);
                }
            }

            return true;
        }

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

        return true;
    }

    private IAptitudeTarget[] ResolveTargets(Context context, bool fallbackToSelfWhenNoTargets)
    {
        if (Params.RemoveFromSelf == true)
        {
            return new IAptitudeTarget[] { context.Self };
        }

        if (context.Targets.Count == 0)
        {
            return fallbackToSelfWhenNoTargets ? new IAptitudeTarget[] { context.Self } : System.Array.Empty<IAptitudeTarget>();
        }

        return context.Targets.ToArray();
    }
}