using GameServer.Data.SDB.Records.customdata;
using GameServer.Entities.Character;

namespace GameServer.Aptitude;

public class RequireAppliedUnlockCommand : Command, ICommand
{
    private RequireAppliedUnlockCommandDef Params;

    public RequireAppliedUnlockCommand(RequireAppliedUnlockCommandDef par)
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

        return character.Player.Inventory.Unlocks.HasAppliedUnlock(Params.Id);
    }
}