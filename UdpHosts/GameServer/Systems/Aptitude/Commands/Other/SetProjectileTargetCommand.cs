using GameServer.Data.SDB.Records.aptfs;

namespace GameServer.Aptitude;

public class SetProjectileTargetCommand : Command, ICommand
{
    private SetProjectileTargetCommandDef Params;

    public SetProjectileTargetCommand(SetProjectileTargetCommandDef par)
: base(par)
    {
        Params = par;
    }

    public bool Execute(Context context)
    {
        // Resolve which entity should be used as the homing target.
        // TargetingThis == 1: use the ability's Self entity (e.g. a turret targeting its own
        //   host or a self-aimed ability).
        // Otherwise: use the top of the Targets stack (the entity currently being engaged).
        IAptitudeTarget homingTarget;
        if (Params.TargetingThis == 1)
        {
            homingTarget = context.Self;
        }
        else if (!context.Targets.TryPeek(out homingTarget))
        {
            Logger.Debug("SetProjectileTargetCommand {Id}: no target in context, HomingTarget unchanged", Id);
            return true;
        }

        context.HomingTarget = homingTarget;
        return true;
    }
}