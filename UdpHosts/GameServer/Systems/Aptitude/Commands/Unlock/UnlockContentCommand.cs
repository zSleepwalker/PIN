using GameServer.Data.SDB.Records.customdata;
using GameServer.Entities.Character;

namespace GameServer.Aptitude;

public class UnlockContentCommand : Command, ICommand
{
    private UnlockContentCommandDef Params;

    public UnlockContentCommand(UnlockContentCommandDef par)
: base(par)
    {
        Params = par;
    }

    public bool Execute(Context context)
    {
        if (context.Self is not CharacterEntity { IsPlayerControlled: true } character)
        {
            return true;
        }

        bool changed = character.Player.Inventory.Unlocks.UnlockContent(Params.Id, "apt_unlock_content");
        if (changed)
        {
            character.Player.Inventory.SendCertificateUnlocksUpdate();
        }

        return true;
    }
}