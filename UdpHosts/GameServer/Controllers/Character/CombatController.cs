using System;
using System.Linq;
using AeroMessages.GSS.V66.Character;
using AeroMessages.GSS.V66.Character.Command;
using AeroMessages.GSS.V66.Character.Controller;
using AeroMessages.GSS.V66.Character.Event;
using GameServer.Aptitude;
using GameServer.Data.SDB;
using GameServer.Entities.Character;
using GameServer.Enums.GSS.Character;
using GameServer.Extensions;
using GameServer.Packets;
using Serilog;

namespace GameServer.Controllers.Character;

[ControllerID(Enums.GSS.Controllers.Character_CombatController)]
public class CombatController : Base
{
    private ILogger _logger;

    public override void Init(INetworkClient client, IPlayer player, IShard shard, ILogger logger)
    {
        _logger = logger.ForContext<CharacterEntity>();
    }

    [MessageID((byte)Commands.FireInputIgnored)]
    public void FireInputIgnored(INetworkClient client, IPlayer player, ulong entityId, GamePacket packet)
    {
        var query = packet.Unpack<FireInputIgnored>();
        _logger.Verbose("FireInputIgnored Time={Time} Ignored={Ignored}", query?.Time, query?.Ignored);
    }

    [MessageID((byte)Commands.FireBurst)]
    public void FireBurst(INetworkClient client, IPlayer player, ulong entityId, GamePacket packet)
    {
        var query = packet.Unpack<FireBurst>();
        player.CharacterEntity.SetFireBurst(query.Time);
    }

    [MessageID((byte)Commands.FireWeaponProjectile)]
    public void FireWeaponProjectile(INetworkClient client, IPlayer player, ulong entityId, GamePacket packet)
    {
        var fireWeaponProjectile = packet.Unpack<FireWeaponProjectile>();

        player.HandleFireWeaponProjectile(fireWeaponProjectile.Time, fireWeaponProjectile.AimDirection);

        var weaponProjectileFired = new WeaponProjectileFired
        {
            ShortTime = (ushort)fireWeaponProjectile.Time,
            Aim = fireWeaponProjectile.AimDirection,
            HaveShooterVelocity = fireWeaponProjectile.HaveShooterVelocity,
            ShooterVelocity = fireWeaponProjectile.ShooterVelocity
        };

        client.NetChannels[ChannelType.ReliableGss].SendMessage(weaponProjectileFired, player.CharacterEntity.EntityId);
    }

    [MessageID((byte)Commands.FireEnd)]
    public void FireEnd(INetworkClient client, IPlayer player, ulong entityId, GamePacket packet)
    {
        var query = packet.Unpack<FireEnd>();
        player.CharacterEntity.SetFireEnd(query.Time);
    }

    [MessageID((byte)Commands.FireCancel)]
    public void FireCancel(INetworkClient client, IPlayer player, ulong entityId, GamePacket packet)
    {
        var query = packet.Unpack<FireCancel>();
        player.CharacterEntity.SetFireCancel(query.Time);
    }

    [MessageID((byte)Commands.UseScope)]
    public void UseScope(INetworkClient client, IPlayer player, ulong entityId, GamePacket packet)
    {
        var query = packet.Unpack<UseScope>();
        player.CharacterEntity.SetFireMode(1, new FireModeData
        {
            Mode = (byte)query.InScope,
            Time = query.Time,
        });
    }

    [MessageID((byte)Commands.SelectWeapon)]
    public void SelectWeapon(INetworkClient client, IPlayer player, ulong entityId, GamePacket packet)
    {
        var query = packet.Unpack<SelectWeapon>();
        player.CharacterEntity.SetWeaponIndex(new WeaponIndexData
        {
            Index = query.SelectedWeaponIndex,
            Unk1 = query.Unk3,
            Unk2 = 0,
            Time = query.Time,
        });
    }

    [MessageID((byte)Commands.SelectFireMode)]
    public void SelectFireMode(INetworkClient client, IPlayer player, ulong entityId, GamePacket packet)
    {
        var query = packet.Unpack<SelectFireMode>();
        player.CharacterEntity.SetFireMode(0, new FireModeData
        {
            Mode = query.FireMode,
            Time = query.Time,
        });
    }

    [MessageID((byte)Commands.ReloadWeapon)]
    public void ReloadWeapon(INetworkClient client, IPlayer player, ulong entityId, GamePacket packet)
    {
        var query = packet.Unpack<ReloadWeapon>();

        var character = player.CharacterEntity;
        if (character.TryGetActiveWeaponAmmoState(out ushort clip, out ushort reserve, out ushort maxClip, out _))
        {
            int missingInClip = Math.Max(0, maxClip - clip);
            int reloadAmount = Math.Min(missingInClip, reserve);
            if (reloadAmount > 0)
            {
                ushort updatedClip = (ushort)Math.Min(maxClip, clip + reloadAmount);
                ushort updatedReserve = (ushort)Math.Max(0, reserve - reloadAmount);
                character.SetActiveWeaponAmmoState(updatedClip, updatedReserve);
            }
        }

        player.CharacterEntity.SetWeaponReloaded(query.Time);
    }

    [MessageID((byte)Commands.CancelReload)]
    public void CancelReload(INetworkClient client, IPlayer player, ulong entityId, GamePacket packet)
    {
        var query = packet.Unpack<CancelReload>();
        player.CharacterEntity.SetWeaponReloadCancelled(query.Time);
    }

    [MessageID((byte)Commands.ActivateConsumable)]
    public void ActivateConsumable(INetworkClient client, IPlayer player, ulong entityId, GamePacket packet)
    {
        var query = packet.Unpack<ActivateConsumable>();
        _logger.Information("ActivateConsumable {ItemSdbId}", query?.ItemSdbId);
        if (query == null)
        {
            return;
        }

        var abilityModule = SDBInterface.GetAbilityModule(query.ItemSdbId);
        if (abilityModule == null)
        {
            return;
        }

        uint abilityId = abilityModule.AbilityChainId;
        if (abilityId != 0)
        {
            var character = player.CharacterEntity;
            var activationTime = query.Time;
            var initiator = character as IAptitudeTarget;
            var shard = player.CharacterEntity.Shard;
            var targets = new AptitudeTargets();
            shard.Abilities.HandleActivateAbility(shard, initiator, abilityId, activationTime, targets, query.ItemSdbId);
        }
    }

    [MessageID((byte)Commands.ActivateAbility)]
    public void ActivateAbility(INetworkClient client, IPlayer player, ulong entityId, GamePacket packet)
    {
        var activateAbility = packet.Unpack<ActivateAbility>();
        _logger.Information("ActivateAbility Slot {AbilitySlotIndex}", activateAbility?.AbilitySlotIndex);
        if (activateAbility == null)
        {
            return;
        }

        // Get the ability id based on the slotted ability
        var abilitySlot = activateAbility.AbilitySlotIndex;
        var character = player.CharacterEntity;
        uint abilityId = character.ResolveAbilityIdBySlotIndex(abilitySlot);

        if (abilityId != 0)
        {
            var activationTime = activateAbility.Time;

            if (character.IsAbilityActivationActive(abilityId, activationTime)
                || character.HasTrackedAbilityToggleEffects(abilityId)
                || character.HasAbilityScopedEffects(abilityId))
            {
                _logger.Information("ActivateAbility toggling off {AbilityId} from slot {AbilitySlotIndex}", abilityId, abilitySlot);
                if (!character.EndAbilityActivation(abilityId, activationTime, notifyClient: true, sendFailureFallback: false, suppressCooldownOnManualDeactivation: true))
                {
                    SendAbilityCooldowns(character);
                }

                return;
            }

            var initiator = character as IAptitudeTarget;
            var shard = player.CharacterEntity.Shard;
            var targets = activateAbility.Targets
            .Where(entityId =>
            {
                try
                {
                    return shard.Entities[entityId.Backing & 0xffffffffffffff00] != null;
                }
                catch
                {
                    return false;
                }
            })
            .Select(entityId => (IAptitudeTarget)shard.Entities[entityId.Backing & 0xffffffffffffff00])
            .ToArray();

            shard.Abilities.HandleActivateAbility(shard, initiator, abilityId, activationTime, new AptitudeTargets(targets));
        }
    }

    [MessageID((byte)Commands.DeactivateAbility)]
    public void DeactivateAbility(INetworkClient client, IPlayer player, ulong entityId, GamePacket packet)
    {
        var deactivateAbility = packet.Unpack<DeactivateAbility>();
        if (deactivateAbility == null)
        {
            return;
        }

        var character = player.CharacterEntity;
        var deactivationTime = deactivateAbility.Time;

        _logger.Information("DeactivateAbility Slot {AbilitySlotIndex}", deactivateAbility.AbilitySlotIndex);

        if (!character.EndAbilityActivationBySlot(deactivateAbility.AbilitySlotIndex, deactivationTime, notifyClient: true, sendFailureFallback: false, suppressCooldownOnManualDeactivation: true))
        {
            SendAbilityCooldowns(character);
        }
    }

    private static void SendAbilityCooldowns(CharacterEntity character)
    {
        uint currentTime = (uint)character.Shard.CurrentTime;
        character.Player.NetChannels[ChannelType.ReliableGss].SendMessage(new AbilityCooldowns
        {
            Data = character.GetAbilityCooldownsData(currentTime)
        }, character.EntityId);
    }

    [MessageID((byte)Commands.ReportProjectileHit)]
    public void ReportProjectileHit(INetworkClient client, IPlayer player, ulong entityId, GamePacket packet)
    {
        var report = packet.Unpack<ReportProjectileHit>();
        if (report == null)
        {
            return;
        }

        var pendingHit = player.CharacterEntity.Shard.ProjectileSim.ResolveHit(report.TraceRef);
        if (pendingHit == null || pendingHit.HitEntityId == 0)
        {
            _logger.Verbose("ReportProjectileHit TraceRef={TraceRef} — no server-side hit, rejected", report.TraceRef);
            return;
        }

        var shard = player.CharacterEntity.Shard;
        var shooter = player.CharacterEntity;
        var weaponDetails = shooter.GetActiveWeaponDetails();
        uint attackAbilityId = weaponDetails?.Weapon?.AttackAbility ?? 0;

        if (attackAbilityId != 0 && shard.Entities.TryGetValue(pendingHit.HitEntityId, out var hitEntity))
        {
            var targets = new AptitudeTargets(new[] { (IAptitudeTarget)hitEntity });
            shard.Abilities.HandleActivateAbility(shard, shooter, attackAbilityId, report.ShortTime, targets);
        }

        client.NetChannels[ChannelType.ReliableGss].SendMessage(new ProjectileHitReported
        {
            TraceRef = report.TraceRef,
            ShortTime = report.ShortTime,
            Unk2 = 0,
            Unk3 = 0,
        }, entityId);

        _logger.Verbose("ReportProjectileHit TraceRef={TraceRef} hit entity {HitEntityId:x16} ability={AbilityId}",
            report.TraceRef, pendingHit.HitEntityId, weaponDetails?.Weapon?.AttackAbility ?? 0);
    }

    [MessageID((byte)Commands.AcquireWeaponTarget)]
    public void AcquireWeaponTarget(INetworkClient client, IPlayer player, ulong entityId, GamePacket packet)
    {
        var query = packet.Unpack<AcquireWeaponTarget>();
        if (query == null)
        {
            return;
        }

        _logger.Verbose("AcquireWeaponTarget Time={Time} TargetEntityId=0x{TargetId:X} Unk3={Unk3} Unk4={Unk4}",
            query.Unk1, query.Unk2, query.Unk3, query.Unk4);

        var targetBaseId = query.Unk2 & 0xffffffffffffff00UL;
        if (targetBaseId != 0 && client.AssignedShard.Entities.TryGetValue(targetBaseId, out var targetEntity))
        {
            var targetCharacter = targetEntity as CharacterEntity;
            if (targetCharacter?.IsPlayerControlled == true)
            {
                targetCharacter.Player.NetChannels[ChannelType.ReliableGss].SendMessage(
                    new WarnLockTargeted(), targetCharacter.EntityId);
            }
        }
    }

    [MessageID((byte)Commands.LoseWeaponTarget)]
    public void LoseWeaponTarget(INetworkClient client, IPlayer player, ulong entityId, GamePacket packet)
    {
        var query = packet.Unpack<LoseWeaponTarget>();
        _logger.Verbose("LoseWeaponTarget Time={Time} TargetEntityId=0x{TargetId:X}", query?.Unk1, query?.Unk2);
    }

    [MessageID((byte)Commands.RequestSelfRevive)]
    public void RequestSelfRevive(INetworkClient client, IPlayer player, ulong entityId, GamePacket packet)
    {
        var character = player.CharacterEntity;
        bool hasSelfRevivePermission = character.CurrentPermissions.TryGetValue(
            PermissionFlagsData.CharacterPermissionFlags.self_revive,
            out bool selfRevivePermission)
            && selfRevivePermission;
        bool canSelfRevive = !character.Alive && hasSelfRevivePermission;

        _logger.Information("RequestSelfRevive Alive={Alive} Permission={Permission} Granted={Granted}",
            character.Alive,
            hasSelfRevivePermission,
            canSelfRevive);

        var response = new SelfReviveResponse
        {
            Unk1 = canSelfRevive ? (sbyte)0 : (sbyte)-1,
            Unk2 = 0,
            Unk3 = 0,
        };
        client.NetChannels[ChannelType.ReliableGss].SendMessage(response, entityId);

        if (canSelfRevive)
        {
            player.Respawn();
        }
    }
}