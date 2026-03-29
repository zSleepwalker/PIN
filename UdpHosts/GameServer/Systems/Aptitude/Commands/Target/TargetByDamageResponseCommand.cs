using GameServer.Data.SDB.Records.aptfs;
using GameServer.Entities.Character;

namespace GameServer.Aptitude;

public class TargetByDamageResponseCommand : Command, ICommand
{
    private TargetByDamageResponseCommandDef Params;

    public TargetByDamageResponseCommand(TargetByDamageResponseCommandDef par)
: base(par)
    {
        Params = par;
    }

    public bool Execute(Context context)
    {
        // DamageResponse filtering requires a full damage type/vulnerability system.
        // For now: filter out targets that are marked as invulnerable if NotInvulnerable=1.
        var prevTargets = context.Targets;
        var newTargets = new AptitudeTargets();

        foreach (IAptitudeTarget target in prevTargets)
        {
            bool include = true;

            if (Params.NotInvulnerable == 1)
            {
                // If target has the invulnerable state, exclude it
                // Currently no invulnerability system; include all
                include = true;
            }

            bool condition = include;

            if (Params.Negate == 1)
            {
                condition = !condition;
            }

            if (condition)
            {
                newTargets.Push(target);
            }
        }

        context.FormerTargets = prevTargets;
        context.Targets = newTargets;

        return true;
    }
}