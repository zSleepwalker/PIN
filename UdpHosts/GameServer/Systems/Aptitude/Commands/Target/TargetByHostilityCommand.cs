using GameServer.Data.SDB.Records.aptfs;
using GameServer.Entities;
using GameServer.Entities.Character;

namespace GameServer.Aptitude;

public class TargetByHostilityCommand : Command, ICommand
{
    private TargetByHostilityCommandDef Params;

    public TargetByHostilityCommand(TargetByHostilityCommandDef par)
: base(par)
    {
        Params = par;
    }

    public bool Execute(Context context)
    {
        // FilterType: 0 = hostiles, 1 = friendlies (based on Params.ExcludeMode=1 means exclude instead of include)
        var prevTargets = context.Targets;
        var newTargets = new AptitudeTargets();

        var reference = Params.CompareFromInitiator == 1 ? context.Initiator : context.Self;

        foreach (IAptitudeTarget target in prevTargets)
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

            bool isHostile = IsHostile(reference, target);

            // FilterType: 0 = keep hostiles, 1 = keep friendlies
            bool keep = Params.FilterType == 0 ? isHostile : !isHostile;

            if (Params.ExcludeMode == 1)
            {
                keep = !keep;
            }

            if (keep)
            {
                newTargets.Push(target);
            }
        }

        context.FormerTargets = prevTargets;
        context.Targets = newTargets;

        if (Params.FailNoTargets == 1 && context.Targets.Count == 0)
        {
            return false;
        }

        return true;
    }

    private static bool IsHostile(IAptitudeTarget self, IAptitudeTarget other)
    {
        if (ReferenceEquals(self, other))
        {
            return false;
        }

        // Prefer broad faction hostility when both targets are entities.
        if (self is IEntity selfEntity && other is IEntity otherEntity)
        {
            byte selfFaction = selfEntity.HostilityInfo.FactionId;
            byte otherFaction = otherEntity.HostilityInfo.FactionId;
            if (selfFaction != 0 && otherFaction != 0)
            {
                return selfFaction != otherFaction;
            }
        }

        if (self is CharacterEntity selfChar
            && other is CharacterEntity otherChar)
        {
            // NPC vs player = hostile
            if (selfChar.IsPlayerControlled != otherChar.IsPlayerControlled)
            {
                return true;
            }

            // Different armies = hostile
            if (selfChar.ArmyGUID != 0 && otherChar.ArmyGUID != 0 && selfChar.ArmyGUID != otherChar.ArmyGUID)
            {
                return true;
            }
        }

        return false;
    }
}