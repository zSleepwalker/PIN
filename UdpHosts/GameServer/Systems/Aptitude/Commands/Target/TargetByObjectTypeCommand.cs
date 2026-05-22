using GameServer.Data.SDB.Records.aptfs;
using GameServer.Entities.Character;
using GameServer.Entities.Deployable;
using GameServer.Entities.Vehicle;

namespace GameServer.Aptitude;

public class TargetByObjectTypeCommand : Command, ICommand
{
    private TargetByObjectTypeCommandDef Params;

    public TargetByObjectTypeCommand(TargetByObjectTypeCommandDef par)
: base(par)
    {
        Params = par;
    }

    // Projectile and Tinyobject entity types have no IAptitudeTarget implementation yet;
    // when those flags are set the filter naturally produces no matches until the
    // corresponding entity classes are introduced.
    public bool Execute(Context context)
    {
        var previousTargets = context.Targets;
        var newTargets = new AptitudeTargets();
        foreach (IAptitudeTarget target in previousTargets)
        {
            if (Params.Character == 1 && target is CharacterEntity)
            {
                newTargets.Push(target);
            }
            else if (Params.Deployable == 1 && target is DeployableEntity)
            {
                newTargets.Push(target);
            }
            else if (Params.Vehicle == 1 && target is VehicleEntity)
            {
                newTargets.Push(target);
            }

            // Params.Tinyobject == 1 and Params.Projectile == 1 intentionally have no
            // matching branch: neither type exists as a tracked IAptitudeTarget entity.
        }

        context.FormerTargets = previousTargets;
        context.Targets = newTargets;

        if (Params.FailNoTargets == 1 && context.Targets.Count == 0)
        {
            return false;
        }
        else
        {
            return true;
        }
    }
}