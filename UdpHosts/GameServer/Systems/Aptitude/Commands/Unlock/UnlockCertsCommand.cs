using GameServer.Data.SDB.Records.customdata;
using GameServer.Entities.Character;

namespace GameServer.Aptitude;

public class UnlockCertsCommand : Command, ICommand
{
    private UnlockCertsCommandDef Params;

    public UnlockCertsCommand(UnlockCertsCommandDef par)
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

        bool changed = character.Player.Inventory.Unlocks.UnlockCertificate(Params.Id, frameId: null, source: "apt_unlock_certs");
        if (changed)
        {
            character.Player.Inventory.SendCertificateUnlocksUpdate();
        }

        return true;
    }
}