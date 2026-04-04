using GameServer.Data.SDB.Records.aptfs;
using GameServer.Entities.Character;

namespace GameServer.Aptitude;

public class RequireHasUnlockCommand : Command, ICommand
{
    private RequireHasUnlockCommandDef Params;

    public RequireHasUnlockCommand(RequireHasUnlockCommandDef par)
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

        bool hasUnlock = character.Player.Inventory.Unlocks.HasUnlock(Params.UnlockType, Params.UnlockId);
        return Params.Negate == 1 ? !hasUnlock : hasUnlock;
    }
}