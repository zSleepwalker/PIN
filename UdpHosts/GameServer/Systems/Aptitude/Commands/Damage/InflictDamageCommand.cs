using System;
using System.Collections.Generic;
using AeroMessages.Common;
using AeroMessages.GSS.V66;
using AeroMessages.GSS.V66.Character.Event;
using GameServer.Data.SDB.Records.aptfs;
using GameServer.Entities.Character;
using GameServer.Entities.Vehicle;
using GameServer.Enums;

namespace GameServer.Aptitude;

public class InflictDamageCommand : Command, ICommand
{
    private InflictDamageCommandDef Params;

    public InflictDamageCommand(InflictDamageCommandDef par)
: base(par)
    {
        Params = par;
    }

    public bool Execute(Context context)
    {
        float baseDamage = AbilitySystem.RegistryOp(context.Register, Params.Damagepoints, (Operand)Params.DamagepointsRegop);

        if (Params.Weapondamage == 1)
        {
            baseDamage = Math.Max(baseDamage, context.Register);
        }

        if (baseDamage <= 0)
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

        var initiatorEntity = context.Initiator as CharacterEntity;
        var initiatorEntityId = initiatorEntity != null
            ? new EntityId { Backing = initiatorEntity.EntityId }
            : (EntityId?)null;

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

                float damageTakenMult = character.GetCurrentStatModifierValue(StatModifierIdentifier.DamageTaken);
                int damageAmount = (int)MathF.Ceiling(baseDamage * damageTakenMult);

                if (damageAmount <= 0)
                {
                    continue;
                }

                int newHealth = Math.Clamp(currentHealth - damageAmount, 0, maxHealth);

                if (newHealth != currentHealth)
                {
                    character.Character_BaseController.CurrentHealthProp = newHealth;
                    character.Character_ObserverView.CurrentHealthPctProp = (byte)Math.Clamp((newHealth * 100) / maxHealth, 0, 100);
                    context.Shard.EntityMan.FlushChanges(character);
                }

                Serilog.Log.Information(
                    $"[InflictDamage] Character {character} took {damageAmount} dmg ({currentHealth}->{newHealth}/{maxHealth}) command={Params.Id}");

                // Send TookHit to victim's own client
                if (character.IsPlayerControlled && character.Player.Status == IPlayer.PlayerStatus.Playing)
                {
                    var tookHit = new TookHit
                    {
                        HaveDamage = 1,
                        DamageData = new DamageHitStruct
                        {
                            Target = new EntityId { Backing = character.EntityId },
                            HaveDealer = (byte)(initiatorEntityId.HasValue ? 1 : 0),
                            Dealer = initiatorEntityId ?? default,
                            DamageValue = -damageAmount,
                            DamageType = Params.DamageType,
                        },
                        RepeatHitIdx = 0,
                        DamageFlags = DamageResponseFlags.Effective,
                        ShortTime = context.Shard.CurrentShortTime,
                        Unk2 = 0,
                    };
                    character.Player.NetChannels[ChannelType.ReliableGss].SendMessage(tookHit, character.EntityId);
                }

                // Send DealtHit to initiator's client (if different from victim)
                if (initiatorEntity != null
                    && initiatorEntity.IsPlayerControlled
                    && initiatorEntity.Player.Status == IPlayer.PlayerStatus.Playing
                    && !ReferenceEquals(initiatorEntity, character))
                {
                    var dealtHit = new DealtHit
                    {
                        HaveDamage = 1,
                        DamageData = new DamageHitStruct
                        {
                            Target = new EntityId { Backing = character.EntityId },
                            HaveDealer = 1,
                            Dealer = initiatorEntityId!.Value,
                            DamageValue = -damageAmount,
                            DamageType = Params.DamageType,
                        },
                        RepeatHitIdx = 0,
                        DamageFlags = DamageResponseFlags.Effective,
                    };
                    initiatorEntity.Player.NetChannels[ChannelType.ReliableGss].SendMessage(dealtHit, character.EntityId);
                }

                // Handle death
                if (newHealth <= 0)
                {
                    character.SetCharacterState(
                        AeroMessages.GSS.V66.Character.CharacterStateData.CharacterStatus.Dead,
                        context.Shard.CurrentTime);
                    context.Shard.EntityMan.FlushChanges(character);

                    var killed = new Killed
                    {
                        ShortTime = context.Shard.CurrentShortTime,
                        Killer = initiatorEntityId ?? new EntityId { Backing = character.EntityId },
                        Unk1 = 0,
                        Unk2 = 0,
                        Unk3 = 0,
                    };

                    // Broadcast Killed to all players in shard (owner via Reliable, observers via Unreliable)
                    foreach (var client in context.Shard.Clients.Values)
                    {
                        if (client.Status != IPlayer.PlayerStatus.Playing)
                        {
                            continue;
                        }

                        bool isOwner = character.IsPlayerControlled && ReferenceEquals(client, character.Player);
                        if (isOwner)
                        {
                            client.NetChannels[ChannelType.ReliableGss].SendMessage(killed, character.EntityId);
                        }
                        else
                        {
                            client.NetChannels[ChannelType.UnreliableGss].SendMessage(killed, character.EntityId);
                        }
                    }

                    Serilog.Log.Information($"[InflictDamage] Character {character} killed by {context.Initiator} command={Params.Id}");
                }

                continue;
            }

            if (target is VehicleEntity vehicle)
            {
                uint maxHealth = Math.Max(1u, vehicle.MaxHealth);
                uint currentHealth = vehicle.CurrentHealth;
                int damageAmount = (int)MathF.Ceiling(baseDamage);
                if (damageAmount <= 0)
                {
                    continue;
                }

                uint clampedDamage = (uint)Math.Min(damageAmount, int.MaxValue);
                uint newHealth = currentHealth > clampedDamage ? currentHealth - clampedDamage : 0;

                if (newHealth != currentHealth)
                {
                    vehicle.CurrentHealth = newHealth;
                    if (vehicle.Vehicle_BaseController != null)
                    {
                        vehicle.Vehicle_BaseController.CurrentHealthProp = newHealth;
                        vehicle.Vehicle_BaseController.MaxHealthProp = maxHealth;
                    }

                    if (vehicle.Vehicle_ObserverView != null)
                    {
                        vehicle.Vehicle_ObserverView.CurrentHealthProp = newHealth;
                        vehicle.Vehicle_ObserverView.MaxHealthProp = maxHealth;
                    }

                    context.Shard.EntityMan.FlushChanges(vehicle);
                }

                Serilog.Log.Information(
                    $"[InflictDamage] Vehicle {vehicle} took {damageAmount} dmg ({currentHealth}->{newHealth}/{maxHealth}) command={Params.Id}");
            }
        }

        return true;
    }
}