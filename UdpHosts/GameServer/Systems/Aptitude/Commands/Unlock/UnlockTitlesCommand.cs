using GameServer.Data.SDB.Records.customdata;
using GameServer.Entities.Character;

namespace GameServer.Aptitude;

public class UnlockTitlesCommand : Command, ICommand
{
    private UnlockTitlesCommandDef Params;

    public UnlockTitlesCommand(UnlockTitlesCommandDef par)
: base(par)
    {
        Params = par;
    }

    public bool Execute(Context context)
    {
        if (Params.TitleId == 0)
        {
            return true;
        }

        if (context.Self is not CharacterEntity { IsPlayerControlled: true } character)
        {
            return true;
        }

        bool changed = character.Player.Inventory.Unlocks.UnlockByType("title", Params.TitleId, "apt_unlock_titles");
        if (changed)
        {
            character.Player.Inventory.SendCertificateUnlocksUpdate();
        }

        return true;
    }
}
