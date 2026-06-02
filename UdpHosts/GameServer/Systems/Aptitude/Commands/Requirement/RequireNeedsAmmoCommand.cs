using System;
using GameServer.Data.SDB.Records.aptfs;
using GameServer.Entities.Character;

namespace GameServer.Aptitude;

public class RequireNeedsAmmoCommand : Command, ICommand
{
    private RequireNeedsAmmoCommandDef Params;

    public RequireNeedsAmmoCommand(RequireNeedsAmmoCommandDef par)
        : base(par)
    {
        Params = par;
    }

    // todo recheck controller props
    public bool Execute(Context context)
    {
        bool result = false;

        // NOTE: Investigate target handling
        var target = context.Self;

        if (target is CharacterEntity character)
        {
            if (Params.CheckPrimary == 1)
            {
                ushort primaryReserve = (ushort)Math.Max(0, character.Character_CombatController.Ammo_0Prop - character.Character_CombatController.Clip_0Prop);
                result = primaryReserve == 0;
            }

            if (Params.CheckSecondary == 1)
            {
                ushort secondaryReserve = (ushort)Math.Max(0, character.Character_CombatController.Ammo_1Prop - character.Character_CombatController.Clip_1Prop);
                result = result || secondaryReserve == 0;
            }
        }

        if (Params.Negate == 1)
        {
            result = !result;
        }

        return result;
    }
}