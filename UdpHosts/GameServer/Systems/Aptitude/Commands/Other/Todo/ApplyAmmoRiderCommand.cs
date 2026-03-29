using GameServer.Data.SDB.Records.aptfs;
using GameServer.Entities.Character;

namespace GameServer.Aptitude;

public class ApplyAmmoRiderCommand : Command, ICommand
{
    private ApplyAmmoRiderCommandDef Params;

    public ApplyAmmoRiderCommand(ApplyAmmoRiderCommandDef par)
: base(par)
    {
        Params = par;
    }

    public bool Execute(Context context)
    {
        foreach (var target in context.Targets)
        {
            if (target is CharacterEntity character)
            {
                character.AmmoOverride = Params.AmmoId;
            }
        }

        return true;
    }
}