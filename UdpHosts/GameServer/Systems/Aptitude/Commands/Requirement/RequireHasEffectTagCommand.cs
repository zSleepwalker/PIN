using System;
using System.Linq;
using GameServer.Data.SDB;
using GameServer.Data.SDB.Records.aptfs;

namespace GameServer.Aptitude;

public class RequireHasEffectTagCommand : Command, ICommand
{
    private RequireHasEffectTagCommandDef Params;

    public RequireHasEffectTagCommand(RequireHasEffectTagCommandDef par)
: base(par)
    {
        Params = par;
    }

    public bool Execute(Context context)
    {
        Logger.Debug("[{Command} {CommandId}] EffectTag: {TagId}", nameof(RequireHasEffectTagCommand), Params.Id, Params.TagId);
        bool result = false;
        var effectTagEffectIds = SDBInterface.GetStatusEffectsByTag(Params.TagId);

        if (effectTagEffectIds.Count == 0)
        {
            Console.WriteLine($"[RequireHasEffectTag] WARNING: no effects mapped to tag {Params.TagId}");
        }

        if (context.Targets.Count > 0)
        {
            uint matchCounter = 0;
            foreach (IAptitudeTarget target in context.Targets)
            {
                bool targetMatched = false;
                foreach (EffectState active in target.GetActiveEffects())
                {
                    if (active == null)
                    {
                        continue;
                    }

                    if (active.Effect == null)
                    {
                        continue;
                    }

                    var hasTag = effectTagEffectIds.Contains(active.Effect.Id)
                        || SDBInterface.StatusEffectHasTag(active.Effect.Id, Params.TagId);

                    if (hasTag && active.Stacks >= Params.StackCount)
                    {
                        matchCounter++;
                        targetMatched = true;
                        break;
                    }
                }

                if (!targetMatched)
                {
                    var activeIds = string.Join(",", target.GetActiveEffects().Where(e => e?.Effect != null).Select(e => e.Effect.Id));
                    Console.WriteLine($"[RequireHasEffectTag] Target {target} failed tag {Params.TagId}. Active effects: [{activeIds}]");
                }
            }

            if (matchCounter == context.Targets.Count)
            {
                result = true;
            }
        }
        else
        {
            var target = context.Self;
            foreach (EffectState active in target.GetActiveEffects())
            {
                if (active == null)
                {
                    continue;
                }

                if (active.Effect == null)
                {
                    continue;
                }

                var hasTag = effectTagEffectIds.Contains(active.Effect.Id)
                    || SDBInterface.StatusEffectHasTag(active.Effect.Id, Params.TagId);

                if (hasTag && active.Stacks >= Params.StackCount)
                {
                    result = true;
                    break;
                }
            }

            if (!result)
            {
                var activeIds = string.Join(",", target.GetActiveEffects().Where(e => e?.Effect != null).Select(e => e.Effect.Id));
                Console.WriteLine($"[RequireHasEffectTag] Self {target} failed tag {Params.TagId}. Active effects: [{activeIds}]");
            }
        }

        if (Params.Negate == 1)
        {
            result = !result;
        }

        return result;
    }
}