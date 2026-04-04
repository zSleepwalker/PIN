using System;
using System.Collections.Generic;
using GameServer.Data.SDB.Records.aptfs;
using GameServer.Entities.Character;
using GameServer.Entities.Deployable;
using GameServer.Entities.Vehicle;
using GameServer.Enums;

namespace GameServer.Aptitude;

public class HealDamageCommand : Command, ICommand
{
    private HealDamageCommandDef Params;

    public HealDamageCommand(HealDamageCommandDef par)
: base(par)
    {
        Params = par;
    }

    public bool Execute(Context context)
    {
        float baseHealValue = AbilitySystem.RegistryOp(context.Register, Params.Healpoints, (Operand)Params.HealpointsRegop);

        // Keep weapon-driven heals meaningful even when Healpoints is configured as a small baseline.
        if (Params.Weapondamage == 1)
        {
            baseHealValue = Math.Max(baseHealValue, context.Register);
        }

        // Note: deployable-sourced heals bypass the base-value early-out (they use 50% max health instead).
        bool isDeployableSource = context.Initiator is DeployableEntity;
        if (baseHealValue <= 0 && !isDeployableSource)
        {
            return true;
        }

        var targets = new List<IAptitudeTarget>();
        if (context.Targets.Count > 0)
        {
            targets.AddRange(context.Targets);
        }
        else
        {
            targets.Add(context.Self);
        }

        foreach (var target in targets)
        {
            if (target is CharacterEntity character)
            {
                if (character.Character_BaseController == null)
                {
                    continue;
                }

                int maxHealth = Math.Max(1, character.MaxHealth.Value);
                int currentHealth = character.Character_BaseController.CurrentHealthProp;

                int healAmount;
                if (isDeployableSource)
                {
                    // Deployable health pads restore 50% of the target's max health per heal tick.
                    healAmount = (int)MathF.Ceiling(maxHealth * 0.5f);
                }
                else
                {
                    // Scale by HealingReceivedMult stat modifier ("Health Increased By" item/ability perk).
                    float healMult = character.GetCurrentStatModifierValue(StatModifierIdentifier.HealingReceivedMult);

                    // Add HealthRegen item attribute as a flat bonus per heal application.
                    float regenBonus = character.GetItemAttribute((ushort)ItemAttributeId.HealthRegen);

                    // Use Ceiling to avoid periodic low-value ticks (e.g. 0.5) silently rounding to 0.
                    healAmount = (int)MathF.Ceiling((baseHealValue + regenBonus) * healMult);
                }

                if (healAmount <= 0)
                {
                    continue;
                }

                int newHealth = Math.Clamp(currentHealth + healAmount, 0, maxHealth);

                if (newHealth != currentHealth)
                {
                    character.Character_BaseController.CurrentHealthProp = newHealth;
                    character.Character_ObserverView.CurrentHealthPctProp = (byte)Math.Clamp((newHealth * 100) / maxHealth, 0, 100);
                    context.Shard.EntityMan.FlushChanges(character);
                }

                Serilog.Log.Information($"[HealDamage] Character {character} healed {newHealth - currentHealth} ({currentHealth}->{newHealth}/{maxHealth}) deployable={isDeployableSource} command={Params.Id}");
                continue;
            }

            if (target is VehicleEntity vehicle)
            {
                int healAmount = (int)MathF.Ceiling(baseHealValue);
                if (healAmount <= 0)
                {
                    continue;
                }

                uint currentHealth = vehicle.CurrentHealth;
                uint maxHealth = Math.Max(1u, vehicle.MaxHealth);
                uint newHealth = Math.Min(maxHealth, currentHealth + (uint)healAmount);
                if (newHealth != currentHealth)
                {
                    vehicle.CurrentHealth = newHealth;
                    if (vehicle.Vehicle_BaseController != null)
                    {
                        vehicle.Vehicle_BaseController.CurrentHealthProp = newHealth;
                    }

                    if (vehicle.Vehicle_ObserverView != null)
                    {
                        vehicle.Vehicle_ObserverView.CurrentHealthProp = newHealth;
                        vehicle.Vehicle_ObserverView.MaxHealthProp = maxHealth;
                    }

                    context.Shard.EntityMan.FlushChanges(vehicle);
                }

                Serilog.Log.Information($"[HealDamage] Vehicle {vehicle} healed {newHealth - currentHealth} ({currentHealth}->{newHealth}) command={Params.Id}");
            }
        }

        return true;
    }
}