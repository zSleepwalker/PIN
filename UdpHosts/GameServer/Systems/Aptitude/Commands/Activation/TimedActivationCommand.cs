using GameServer.Data.SDB.Records.apt;
using GameServer.Entities.Character;

namespace GameServer.Aptitude;

public class TimedActivationCommand : Command, ICommand
{
    private TimedActivationCommandDef Params;

    public TimedActivationCommand(TimedActivationCommandDef par)
: base(par)
    {
        Params = par;
    }

    public bool Execute(Context context)
    {
        if (context.Self is not CharacterEntity character)
        {
            return true;
        }

        context.PendingActivationCharacter = character;
        context.PendingActivationAcknowledgement = true;
        context.PendingActivationStateRequested = true;
        context.PendingTimedActivation = true;
        context.PendingActivationDurationMs = Params.Duration;
        context.PendingActivationCancelOnMove = Params.CancelOnMove != 0;
        return true;
    }
}