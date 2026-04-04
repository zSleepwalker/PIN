using System;
using GameServer.Data.SDB.Records.aptfs;
using GameServer.Entities.Character;
using GameServer.Entities.Vehicle;
using GameServer.Enums;

namespace GameServer.Aptitude;

public class StatRequirementCommand : Command, ICommand
{
    private StatRequirementCommandDef Params;

    public StatRequirementCommand(StatRequirementCommandDef par)
: base(par)
    {
        Params = par;
    }

    public bool Execute(Context context)
    {
        float GetStat(IAptitudeTarget target, ushort stat)
        {
            if (target is CharacterEntity character)
            {
                return (AptitudeStat)stat switch
                {
                    AptitudeStat.Health => character.Character_BaseController?.CurrentHealthProp ?? 0,
                    AptitudeStat.MaxHealth => character.MaxHealth.Value,
                    AptitudeStat.Shields => character.Character_BaseController?.CurrentShieldsProp ?? 0,
                    AptitudeStat.MaxShields => character.MaxShields.Value,
                    AptitudeStat.DamageDealt => character.Character_CombatController?.WeaponDamageDealtModProp.Value ?? 0,
                    _ => character.GetItemAttribute(stat),
                };
            }

            if (target is VehicleEntity vehicle)
            {
                return (AptitudeStat)stat switch
                {
                    AptitudeStat.Health => vehicle.CurrentHealth,
                    AptitudeStat.MaxHealth => vehicle.MaxHealth,
                    AptitudeStat.Shields => vehicle.CurrentShields,
                    AptitudeStat.MaxShields => vehicle.MaxShields,
                    _ => 0,
                };
            }

            return 0;
        }

        var target = context.Targets.Count > 0 ? context.Targets.Peek() : context.Self;
        float lhs = GetStat(target, Params.Stat1);

        float rhs = Params.Stat2 != 0
            ? GetStat(target, Params.Stat2)
            : AbilitySystem.RegistryOp(context.Register, Params.Value, (Operand)Params.ValueRegop);

        bool result = true;
        bool anyRule = false;

        if (Params.Greaterthan == 1)
        {
            anyRule = true;
            result &= lhs > rhs;
        }

        if (Params.Lessthan == 1)
        {
            anyRule = true;
            result &= lhs < rhs;
        }

        if (Params.Equalto == 1)
        {
            anyRule = true;
            result &= Math.Abs(lhs - rhs) < 0.001f;
        }

        if (!anyRule)
        {
            result = true;
        }

        if (!result)
        {
            Serilog.Log.Information($"[StatRequirement] Failed command={Params.Id}, stat1={Params.Stat1} value={lhs}, stat2={Params.Stat2} rhs={rhs}, gt={Params.Greaterthan}, lt={Params.Lessthan}, eq={Params.Equalto}");
        }

        return result;
    }
}