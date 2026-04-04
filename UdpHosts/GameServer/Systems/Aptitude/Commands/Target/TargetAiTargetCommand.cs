using System;
using GameServer.Data.SDB.Records.customdata;
using GameServer.Entities.Character;

namespace GameServer.Aptitude;

public class TargetAiTargetCommand : Command, ICommand
{
    private TargetAiTargetCommandDef Params;

    public TargetAiTargetCommand(TargetAiTargetCommandDef par)
: base(par)
    {
        Params = par;
    }

    public bool Execute(Context context)
    {
        // Set targets to the AI's current target (threat target)
        // Full AI threat/targeting system not yet implemented.
        // For now: if Self is an NPC, push its owner/initiator as a placeholder target.
        if (context.Self is CharacterEntity { IsPlayerControlled: false } npc)
        {
            var newTargets = new AptitudeTargets();

            if (context.Initiator != null && context.Initiator != context.Self)
            {
                newTargets.Push(context.Initiator);
            }

            context.FormerTargets = context.Targets;
            context.Targets = newTargets;

            Serilog.Log.Information($"[TargetAiTarget] CMD {Id}: AI threat targeting not implemented. Defaulting to initiator.");
        }
        else
        {
            Serilog.Log.Information($"[TargetAiTarget] CMD {Id}: Self is not an NPC, skipping.");
        }

        return true;
    }
}