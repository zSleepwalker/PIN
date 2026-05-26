using System;
using GameServer.Data;
using GameServer.Data.SDB;
using GameServer.Data.SDB.Records.customdata;
using GameServer.Entities.Character;

namespace GameServer.Aptitude;

public class RestockAmmoCommand : Command, ICommand
{
    private RestockAmmoCommandDef Params;

    public RestockAmmoCommand(RestockAmmoCommandDef par)
: base(par)
    {
        Params = par;
    }

    public bool Execute(Context context)
    {
        int percent = Params.Percent.GetValueOrDefault(100);
        if (percent <= 0)
        {
            return true;
        }

        bool hasTargets = context.Targets.Count > 0;
        if (hasTargets)
        {
            foreach (var target in context.Targets)
            {
                if (target is CharacterEntity character)
                {
                    Restock(character, percent);
                }
            }
        }
        else if (context.Self is CharacterEntity selfCharacter)
        {
            Restock(selfCharacter, percent);
        }

        return true;
    }

    private void Restock(CharacterEntity character, int percent)
    {
        if (character.Character_CombatController == null || character.Character_CombatView == null)
        {
            return;
        }

        var loadout = character.CurrentLoadout;
        if (loadout == null)
        {
            return;
        }

        uint primaryWeaponId = loadout.SlottedItems.TryGetValue(LoadoutSlotType.Primary, out var primary) ? primary : 0;
        uint secondaryWeaponId = loadout.SlottedItems.TryGetValue(LoadoutSlotType.Secondary, out var secondary) ? secondary : 0;

        RestockMainReserveForSlot(character, primaryWeaponId, slotIndex: 0, percent);
        RestockMainReserveForSlot(character, secondaryWeaponId, slotIndex: 1, percent);

        RestockAltReserveForSlot(character, primaryWeaponId, slotIndex: 0, percent);
        RestockAltReserveForSlot(character, secondaryWeaponId, slotIndex: 1, percent);
    }

    private void RestockMainReserveForSlot(CharacterEntity character, uint weaponSdbId, int slotIndex, int percent)
    {
        if (weaponSdbId == 0)
        {
            return;
        }

        var detailed = SDBUtils.GetDetailedWeaponInfo(weaponSdbId);
        if (detailed?.Main == null)
        {
            return;
        }

        ushort maxReserve = detailed.Main.MaxAmmo;
        ushort current = slotIndex == 0
            ? character.Character_CombatController.Ammo_0Prop
            : character.Character_CombatController.Ammo_1Prop;
        ushort next = ComputeRestockedAmmo(current, maxReserve, percent);

        if (slotIndex == 0)
        {
            character.Character_CombatController.Ammo_0Prop = next;
        }
        else
        {
            character.Character_CombatController.Ammo_1Prop = next;
        }
    }

    private void RestockAltReserveForSlot(CharacterEntity character, uint weaponSdbId, int slotIndex, int percent)
    {
        if (weaponSdbId == 0)
        {
            return;
        }

        var mainDetailed = SDBUtils.GetDetailedWeaponInfo(weaponSdbId);
        var altTemplate = mainDetailed?.Alt;
        if (altTemplate == null)
        {
            return;
        }

        ushort maxReserve = altTemplate.MaxAmmo;
        ushort current = slotIndex == 0
            ? character.Character_CombatController.AltAmmo_0Prop
            : character.Character_CombatController.AltAmmo_1Prop;
        ushort next = ComputeRestockedAmmo(current, maxReserve, percent);

        if (slotIndex == 0)
        {
            character.Character_CombatController.AltAmmo_0Prop = next;
        }
        else
        {
            character.Character_CombatController.AltAmmo_1Prop = next;
        }
    }

    private static ushort ComputeRestockedAmmo(ushort currentAmmo, ushort maxAmmo, int percent)
    {
        if (maxAmmo == 0)
        {
            return currentAmmo;
        }

        if (percent >= 100)
        {
            return maxAmmo;
        }

        int toAdd = (int)Math.Ceiling(maxAmmo * (percent / 100.0));
        int next = Math.Min(maxAmmo, currentAmmo + toAdd);
        return (ushort)next;
    }
}