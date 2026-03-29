using GameServer.Data.SDB.Records.aptfs;
using GameServer.Entities.Character;

namespace GameServer.Aptitude;

public class TargetHostilesCommand : Command, ICommand
{
    private TargetHostilesCommandDef Params;

    public TargetHostilesCommand(TargetHostilesCommandDef par)
: base(par)
    {
        Params = par;
    }

    public bool Execute(Context context)
    {
        var previousTargets = context.Targets;
        var newTargets = new AptitudeTargets();

        foreach (IAptitudeTarget target in previousTargets)
        {
            if (target == context.Self && Params.IncludeSelf == 0)
            {
                continue;
            }

            if (target == context.Initiator && Params.IncludeInitiator == 0)
            {
                continue;
            }

            if (target == context.Self?.Owner && Params.IncludeOwner == 0)
            {
                continue;
            }

            // Filter: keep only hostiles
            if (target is CharacterEntity targetChar)
            {
                var selfChar = context.Self as CharacterEntity;
                bool isHostile = false;

                // NPC vs player = hostile
                if (selfChar != null && targetChar.IsPlayerControlled != selfChar.IsPlayerControlled)
                {
                    isHostile = true;
                }

                // Different armies (both non-zero) = hostile
                if (selfChar != null && selfChar.ArmyGUID != 0 && targetChar.ArmyGUID != 0 && selfChar.ArmyGUID != targetChar.ArmyGUID)
                {
                    isHostile = true;
                }

                if (isHostile)
                {
                    newTargets.Push(target);
                }
            }
        }

        context.FormerTargets = previousTargets;
        context.Targets = newTargets;

        if (Params.FailNoTargets == 1 && context.Targets.Count == 0)
        {
            return false;
        }

        return true;
    }
}