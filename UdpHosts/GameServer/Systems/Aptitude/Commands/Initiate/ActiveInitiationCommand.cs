using GameServer.Data.SDB.Records.apt;
using GameServer.Entities.Character;

namespace GameServer.Aptitude;

public class ActiveInitiationCommand : Command, ICommand
{
    private ActiveInitiationCommandDef Params;

    public ActiveInitiationCommand(ActiveInitiationCommandDef par)
    : base(par)
    {
        Params = par;
    }

    public bool Execute(Context context)
    {
        if (context.Self is CharacterEntity character)
        {
            context.PendingActivationCharacter = character;
            context.PendingActivationAcknowledgement = true;
            context.PendingActivationStateRequested = true;
            context.PendingTimedActivation = false;
            context.PendingActivationDurationMs = 0;
            context.PendingActivationCancelOnMove = false;
        }

        return true;
    }
}