using System;
using GameServer.Data.SDB.Records.apt;
using GameServer.Entities.Character;
using GameServer.Entities.Vehicle;
using GameServer.Enums;

namespace GameServer.Aptitude;

public class LoadRegisterFromStatCommand : Command, ICommand
{
    private LoadRegisterFromStatCommandDef Params;

    public LoadRegisterFromStatCommand(LoadRegisterFromStatCommandDef par)
: base(par)
    {
        Params = par;
    }

    public bool Execute(Context context)
    {
        float statValue = 0.0f;

        if (context.Self is CharacterEntity character)
        {
            statValue = (AptitudeStat)Params.Stat switch
            {
                AptitudeStat.Health => character.Character_BaseController?.CurrentHealthProp ?? 0,
                AptitudeStat.MaxHealth => character.MaxHealth.Value,
                AptitudeStat.Shields => character.Character_BaseController?.CurrentShieldsProp ?? 0,
                AptitudeStat.MaxShields => character.MaxShields.Value,
                AptitudeStat.DamageDealt => character.Character_CombatController?.WeaponDamageDealtModProp.Value ?? 0,

                // HealingReceivedMult is a runtime stat modifier, not an item attribute; route it correctly.
                AptitudeStat.HealingReceivedMult => character.GetCurrentStatModifierValue(StatModifierIdentifier.HealingReceivedMult),

                _ => character.GetItemAttribute(Params.Stat),
            };
        }
        else if (context.Self is VehicleEntity vehicle)
        {
            statValue = (AptitudeStat)Params.Stat switch
            {
                AptitudeStat.Health => vehicle.CurrentHealth,
                AptitudeStat.MaxHealth => vehicle.MaxHealth,
                AptitudeStat.Shields => vehicle.CurrentShields,
                AptitudeStat.MaxShields => vehicle.MaxShields,
                _ => 0,
            };
        }
        else
        {
            Serilog.Log.Information($"LoadRegisterFromStatCommand unsupported self target type {context.Self?.GetType().Name}");
            return true;
        }

        context.Register = AbilitySystem.RegistryOp(context.Register, statValue, (Operand)Params.Regop);
        return true;
    }
}