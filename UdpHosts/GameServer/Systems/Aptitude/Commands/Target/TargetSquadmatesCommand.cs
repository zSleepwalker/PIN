using System.Linq;
using GameServer.Data.SDB.Records.aptfs;
using GameServer.Entities.Character;

namespace GameServer.Aptitude;

public class TargetSquadmatesCommand : Command, ICommand
{
    private TargetSquadmatesCommandDef Params;

    public TargetSquadmatesCommand(TargetSquadmatesCommandDef par)
: base(par)
    {
        Params = par;
    }

    public bool Execute(Context context)
    {
        if (Params.Filter == 1)
        {
            // Filter mode: remove non-squadmates from current targets
            var prevTargets = context.Targets;
            var newTargets = new AptitudeTargets();

            foreach (IAptitudeTarget target in prevTargets)
            {
                if (IsSquadmate(context.Self, target))
                {
                    newTargets.Push(target);
                }
            }

            context.FormerTargets = prevTargets;
            context.Targets = newTargets;
        }
        else
        {
            // Gather mode: add all squadmates from the shard
            var newTargets = new AptitudeTargets();

            foreach (var entity in context.Shard.Entities.Values)
            {
                if (entity is IAptitudeTarget target && IsSquadmate(context.Self, target))
                {
                    newTargets.Push(target);
                }
            }

            context.FormerTargets = context.Targets;
            context.Targets = newTargets;
        }

        if (Params.FailNone == 1 && context.Targets.Count == 0)
        {
            return false;
        }

        return true;
    }

    private static bool IsSquadmate(IAptitudeTarget self, IAptitudeTarget other)
    {
        if (self == other)
        {
            return false;
        }

        if (self is CharacterEntity selfChar && other is CharacterEntity otherChar)
        {
            // In the absence of a full squad system, use army membership as a proxy
            if (selfChar.ArmyGUID != 0 && selfChar.ArmyGUID == otherChar.ArmyGUID)
            {
                return true;
            }

            // Both player-controlled and no army = treat as squadmates
            if (selfChar.IsPlayerControlled && otherChar.IsPlayerControlled && selfChar.ArmyGUID == 0)
            {
                return true;
            }
        }

        return false;
    }
}