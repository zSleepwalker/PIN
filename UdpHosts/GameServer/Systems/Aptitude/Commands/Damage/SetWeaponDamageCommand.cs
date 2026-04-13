using System;
using GameServer.Data.SDB.Records.aptfs;
using GameServer.Enums;

namespace GameServer.Aptitude;

public class SetWeaponDamageCommand : Command, ICommand
{
    private SetWeaponDamageCommandDef Params;

    public SetWeaponDamageCommand(SetWeaponDamageCommandDef par)
: base(par)
    {
        Params = par;
    }

    public bool Execute(Context context)
    {
        float input = context.Register;

        // Approximate the client behavior: derive a lerp factor from register and map to damage range.
        float lerpMin = Params.Lerpminvalue;
        float lerpMax = Params.Lerpmaxvalue;
        float t = input;

        if (Params.Clamplerp == 1)
        {
            t = Math.Clamp(t, lerpMin, lerpMax);
        }

        float normalized = 0;
        float lerpRange = lerpMax - lerpMin;
        if (Math.Abs(lerpRange) > 0.0001f)
        {
            normalized = Math.Clamp((t - lerpMin) / lerpRange, 0, 1);
        }

        float damageValue = Params.Dmgminvalue + ((Params.Dmgmaxvalue - Params.Dmgminvalue) * normalized);

        if (Params.Multiply == 1)
        {
            damageValue *= input;
        }

        context.Register = AbilitySystem.RegistryOp(context.Register, damageValue, (Operand)Params.DamageRegop);

        if (Params.Set == 1)
        {
            context.FormerRegister = context.Register;
        }

        return true;
    }
}