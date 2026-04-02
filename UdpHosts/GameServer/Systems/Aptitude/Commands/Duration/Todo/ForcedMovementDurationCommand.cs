using GameServer.Data.SDB.Records.aptfs;
using GameServer.Entities.Character;

namespace GameServer.Aptitude;

public class ForcedMovementDurationCommand : Command, ICommand
{
    private ForcedMovementDurationCommandDef Params;

    public ForcedMovementDurationCommand(ForcedMovementDurationCommandDef par)
: base(par)
    {
        Params = par;
    }

    public bool Execute(Context context)
    {
        bool active = false;

        if (context.Self is CharacterEntity character)
        {
            // True while the impulse end time is in the future
            active = character.ForcedMovementEndTime > context.Shard.CurrentTime;
        }

        return Params.Negate == 1 ? !active : active;
    }
}