using GameServer.Data.SDB.Records.apt;
using GameServer.Entities.Character;

namespace GameServer.Aptitude;

public class InstantActivationCommand : Command, ICommand
{
    private InstantActivationCommandDef Params;

    public InstantActivationCommand(InstantActivationCommandDef par)
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
        }

        return true;
    }
}