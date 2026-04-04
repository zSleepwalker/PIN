using GameServer.Data.SDB;
using GameServer.Data.SDB.Records.aptfs;

namespace GameServer.Aptitude;

public class ApplyClientStatusEffectCommand : Command, ICommand
{
    private ApplyClientStatusEffectCommandDef Params;

    public ApplyClientStatusEffectCommand(ApplyClientStatusEffectCommandDef par)
: base(par)
    {
        Params = par;
    }

    public bool Execute(Context context)
    {
        if (Params.StatusEffectId == 0)
        {
            return true;
        }

        // Client-only status effects are still represented by apt::StatusEffectData IDs.
        // If we cannot resolve the effect record, skip instead of throwing downstream.
        if (SDBInterface.GetStatusEffectData(Params.StatusEffectId) == null)
        {
            Serilog.Log.Information($"ApplyClientStatusEffectCommand {Id} skipped unknown StatusEffectId {Params.StatusEffectId}");
            return true;
        }

        bool applied = false;

        if (Params.ApplyToSelf == 1)
        {
            context.Abilities.DoApplyEffect(Params.StatusEffectId, context.Self, context);
            applied = true;
        }

        // UseTargetClients maps best to current selected targets in server aptitude context.
        if (Params.UseTargetClients == 1)
        {
            foreach (var target in context.Targets)
            {
                context.Abilities.DoApplyEffect(Params.StatusEffectId, target, context);
                applied = true;
            }
        }

        // Fallback observed in many commands: when neither flag routes anywhere, apply to self.
        if (!applied)
        {
            context.Abilities.DoApplyEffect(Params.StatusEffectId, context.Self, context);
        }

        return true;
    }
}