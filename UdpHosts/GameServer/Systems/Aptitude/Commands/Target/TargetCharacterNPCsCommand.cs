using GameServer.Data.SDB.Records.customdata;
using GameServer.Entities.Character;

namespace GameServer.Aptitude;

public class TargetCharacterNPCsCommand : Command, ICommand
{
    private TargetCharacterNPCsCommandDef Params;

    public TargetCharacterNPCsCommand(TargetCharacterNPCsCommandDef par)
: base(par)
    {
        Params = par;
    }

    public bool Execute(Context context)
    {
        if (context.Self is not CharacterEntity { IsPlayerControlled: true } player)
        {
            return false;
        }

        var newTargets = new AptitudeTargets();
        context.FormerTargets = new AptitudeTargets(context.Targets);

        // Target all NPCs that belong to this player
        // OwnedNPCs not yet implemented — use owned deployables as a placeholder
        foreach (var deployable in player.OwnedDeployables)
        {
            newTargets.Push(deployable);
        }

        // TODO: Add OwnedNPCs targeting when NPC ownership system is implemented
        Serilog.Log.Information($"[TargetCharacterNPCs] CMD {Id}: NPC ownership system not fully implemented. Using OwnedDeployables as proxy ({newTargets.Count} targets).");

        context.Targets = newTargets;
        return true;
    }
}