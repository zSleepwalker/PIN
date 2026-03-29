using GameServer.Data.SDB.Records.aptfs;
using GameServer.Entities.Character;

namespace GameServer.Aptitude;

public class TargetFriendliesCommand : Command, ICommand
{
    private TargetFriendliesCommandDef Params;

    public TargetFriendliesCommand(TargetFriendliesCommandDef par)
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

            // Filter: keep only friendlies (player-controlled characters or same army)
            if (target is CharacterEntity targetChar)
            {
                var selfChar = context.Self as CharacterEntity;
                bool isFriendly = false;

                // Both player-controlled = friendly in PvE world
                if (selfChar != null && targetChar.IsPlayerControlled && selfChar.IsPlayerControlled)
                {
                    isFriendly = true;
                }

                // Same army = friendly
                if (selfChar != null && selfChar.ArmyGUID != 0 && selfChar.ArmyGUID == targetChar.ArmyGUID)
                {
                    isFriendly = true;
                }

                if (isFriendly)
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