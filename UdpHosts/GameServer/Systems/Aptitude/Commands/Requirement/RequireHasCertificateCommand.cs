using GameServer.Data.SDB.Records.aptfs;
using GameServer.Entities.Character;

namespace GameServer.Aptitude;

public class RequireHasCertificateCommand : Command, ICommand
{
    private RequireHasCertificateCommandDef Params;

    public RequireHasCertificateCommand(RequireHasCertificateCommandDef par)
: base(par)
    {
        Params = par;
    }

    public bool Execute(Context context)
    {
        if (context.Self is not CharacterEntity { IsPlayerControlled: true } character)
        {
            return false;
        }

        // Pass the current battleframe ID so frame-specific certs are only valid on their frame
        uint currentFrameId = character.CurrentLoadout?.ChassisID ?? 0;
        bool hasCert = character.Player.Inventory.Unlocks.HasCertificate(Params.CertificateId, currentFrameId);
        return Params.Negate == 1 ? !hasCert : hasCert;
    }
}