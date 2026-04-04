using System;
using System.Collections.Generic;
using System.Linq;
using AeroMessages.GSS.V66.Character.Event;
using GameServer.Aptitude;
using GameServer.Data.SDB;
using GameServer.Entities.Character;
using GameServer.GRPC;
using GrpcGameServerAPIClient;
using NetworkPlayer = GameServer.NetworkPlayer;

namespace GameServer.Data;

public class CharacterUnlocks
{
    private readonly IShard _shard;
    private readonly INetworkClient _player;
    private readonly CharacterEntity _character;

    private readonly HashSet<uint> _autoGlobalCertificates = new();
    private readonly HashSet<(uint CertId, uint FrameId)> _autoFrameCertificates = new();
    private readonly HashSet<uint> _autoBlueprints = new();

    private readonly HashSet<uint> _manualGlobalCertificates = new();
    private readonly HashSet<(uint CertId, uint FrameId)> _manualFrameCertificates = new();
    private readonly HashSet<uint> _manualBlueprints = new();

    private readonly Dictionary<string, HashSet<uint>> _manualUnlocksByType = new(StringComparer.OrdinalIgnoreCase);

    public CharacterUnlocks(IShard shard, INetworkClient player, CharacterEntity character)
    {
        _shard = shard;
        _player = player;
        _character = character;
    }

    public void RebuildAutoUnlocks(IEnumerable<Loadout> loadouts, IEnumerable<uint> ownedItemSdbIds, byte characterLevel)
    {
        _autoGlobalCertificates.Clear();
        _autoFrameCertificates.Clear();
        _autoBlueprints.Clear();

        _shard?.Logger?.Warning("[REBUILD-DEBUG] RebuildAutoUnlocks: Starting rebuild");

        foreach (var loadout in loadouts)
        {
            if (loadout.ChassisID == 0)
            {
                _shard?.Logger?.Debug("[REBUILD-DEBUG] Skipping loadout with chassis ID 0");
                continue;
            }

            _shard?.Logger?.Warning("[REBUILD-DEBUG] Adding certs for frame {frameId}", loadout.ChassisID);
            AddAutoCertificatesForFrame(loadout.ChassisID);
        }

        _shard?.Logger?.Warning("[REBUILD-DEBUG] After frame certs: {frameCertCount} frame-scoped certs added", _autoFrameCertificates.Count);

        foreach (uint itemSdbId in ownedItemSdbIds.Distinct())
        {
            var root = SDBInterface.GetRootItem(itemSdbId);
            if (root == null)
            {
                continue;
            }

            if (root.RequiredLevel != 0 && root.RequiredLevel > characterLevel)
            {
                continue;
            }

            if (root.ClassCertId != 0)
            {
                _autoGlobalCertificates.Add(root.ClassCertId);
            }

            var connected = SDBInterface.ResolveConnectedChains(itemSdbId);
            if (connected.Blueprint != null && connected.Blueprint.Id != 0)
            {
                _autoBlueprints.Add(connected.Blueprint.Id);
            }
        }

        // Frame-specific certs must never appear in the global collection — battleframe body items in
        // the inventory have ClassCertId matching the frame cert, which would otherwise allow those
        // abilities on any frame. Strip any cert that was identified as frame-scoped.
        var frameCertIds = new HashSet<uint>(_autoFrameCertificates.Select(f => f.CertId));
        _autoGlobalCertificates.ExceptWith(frameCertIds);
    }

    public void LoadPersistedUnlocks(IEnumerable<CharacterUnlockEntry> persistedUnlocks)
    {
        _manualGlobalCertificates.Clear();
        _manualFrameCertificates.Clear();
        _manualBlueprints.Clear();
        _manualUnlocksByType.Clear();

        if (persistedUnlocks == null)
        {
            return;
        }

        foreach (var persisted in persistedUnlocks)
        {
            if (persisted == null || persisted.UnlockId == 0)
            {
                continue;
            }

            string normalizedType = NormalizeUnlockType(persisted.UnlockType);

            if (normalizedType == "certificate")
            {
                _manualGlobalCertificates.Add(persisted.UnlockId);
                if (persisted.FrameId != 0)
                {
                    _manualFrameCertificates.Add((persisted.UnlockId, persisted.FrameId));
                }

                continue;
            }

            if (normalizedType == "blueprint")
            {
                _manualBlueprints.Add(persisted.UnlockId);
                continue;
            }

            if (!_manualUnlocksByType.TryGetValue(normalizedType, out var unlocks))
            {
                unlocks = new HashSet<uint>();
                _manualUnlocksByType[normalizedType] = unlocks;
            }

            unlocks.Add(persisted.UnlockId);
        }

        // Frame-scoped certs must never appear in global collection
        // Same logic as RebuildAutoUnlocks
        var frameCertIds = new HashSet<uint>(_manualFrameCertificates.Select(f => f.CertId));
        _manualGlobalCertificates.ExceptWith(frameCertIds);
        
        _shard?.Logger?.Warning("[PERSIST-DEBUG] LoadPersistedUnlocks: {globalCerts} manual global certs, {frameCerts} manual frame certs", 
            _manualGlobalCertificates.Count, _manualFrameCertificates.Count);
    }

    public bool UnlockCertificate(uint certificateId, uint? frameId, string source)
    {
        if (certificateId == 0)
        {
            return false;
        }

        bool changed = false;
        
        // If this is a frame-specific unlock, add it to frame collection and remove from global
        if (frameId.HasValue && frameId.Value != 0)
        {
            _shard?.Logger?.Warning("[CERT-UNLOCK] Unlocking FRAME-SPECIFIC cert {certId} for frame {frameId}", certificateId, frameId);
            changed |= _manualFrameCertificates.Add((certificateId, frameId.Value));
            // Frame-scoped certs should NOT be in global collection
            if (_manualGlobalCertificates.Contains(certificateId))
            {
                _shard?.Logger?.Warning("[CERT-UNLOCK] *** REMOVING cert {certId} from GLOBAL collection ***", certificateId);
                _manualGlobalCertificates.Remove(certificateId);
                changed = true;
            }
        }
        else
        {
            // Global unlock
            _shard?.Logger?.Warning("[CERT-UNLOCK] Unlocking GLOBAL cert {certId}", certificateId);
            changed = _manualGlobalCertificates.Add(certificateId);
        }

        if (changed)
        {
            _shard.Logger.Information("Manual certificate unlock for {charId}: cert={certId}, frame={frameId}, source={source}", _character.EntityId, certificateId, frameId ?? 0, source);

            PersistManualUnlock("certificate", certificateId, 0);
            if (frameId.HasValue && frameId.Value != 0)
            {
                PersistManualUnlock("certificate", certificateId, frameId.Value);
            }
        }

        return changed;
    }

    public bool UnlockBlueprint(uint blueprintId, string source)
    {
        if (blueprintId == 0)
        {
            return false;
        }

        bool changed = _manualBlueprints.Add(blueprintId);
        if (changed)
        {
            _shard.Logger.Information("Manual blueprint unlock for {charId}: blueprint={blueprintId}, source={source}", _character.EntityId, blueprintId, source);
            PersistManualUnlock("blueprint", blueprintId, 0);
        }

        return changed;
    }

    public bool UnlockContent(uint contentId, string source)
    {
        return UnlockByType("content", contentId, source);
    }

    public bool ApplyUnlock(uint unlockId, string source)
    {
        return UnlockByType("appliedunlock", unlockId, source);
    }

    public bool UnlockByType(string unlockType, uint unlockId, string source)
    {
        if (unlockId == 0)
        {
            return false;
        }

        string normalizedType = NormalizeUnlockType(unlockType);

        if (!_manualUnlocksByType.TryGetValue(normalizedType, out var unlocks))
        {
            unlocks = new HashSet<uint>();
            _manualUnlocksByType[normalizedType] = unlocks;
        }

        bool changed = unlocks.Add(unlockId);
        if (changed)
        {
            _shard.Logger.Information("Manual unlock for {charId}: type={type}, id={unlockId}, source={source}", _character.EntityId, normalizedType, unlockId, source);
            PersistManualUnlock(normalizedType, unlockId, 0);
        }

        return changed;
    }

    public bool TryUnlockFromItem(uint itemSdbId, uint? frameId, string source)
    {
        bool changed = false;

        var root = SDBInterface.GetRootItem(itemSdbId);
        if (root != null && root.ClassCertId != 0)
        {
            changed |= UnlockCertificate(root.ClassCertId, frameId, source);
        }

        var connected = SDBInterface.ResolveConnectedChains(itemSdbId);
        if (connected.Blueprint != null && connected.Blueprint.Id != 0)
        {
            changed |= UnlockBlueprint(connected.Blueprint.Id, source);
        }

        return changed;
    }

    public bool HasCertificate(uint certificateId, uint? currentFrameId = null)
    {
        if (certificateId == 0)
        {
            return true;
        }

        // Always check global certificates (from owned items)
        bool inGlobal = _autoGlobalCertificates.Contains(certificateId) || _manualGlobalCertificates.Contains(certificateId);
        if (inGlobal)
        {
            _shard?.Logger?.Debug("[CERT-DEBUG] HasCertificate: Cert {certId} found in GLOBAL", certificateId);
            return true;
        }

        // For frame-specific certificates, only return true if we're on the correct frame
        if (currentFrameId.HasValue && currentFrameId.Value != 0)
        {
            bool inFrameAuto = _autoFrameCertificates.Any(f => f.CertId == certificateId && f.FrameId == currentFrameId.Value);
            bool inFrameManual = _manualFrameCertificates.Any(f => f.CertId == certificateId && f.FrameId == currentFrameId.Value);
            
            if (inFrameAuto || inFrameManual)
            {
                _shard?.Logger?.Debug("[CERT-DEBUG] HasCertificate: Cert {certId} found on frame {frameId} (auto={a}, manual={m})", 
                    certificateId, currentFrameId, inFrameAuto, inFrameManual);
                return true;
            }
            
            _shard?.Logger?.Debug("[CERT-DEBUG] HasCertificate: Cert {certId} NOT found on frame {frameId}", certificateId, currentFrameId);
        }

        _shard?.Logger?.Debug("[CERT-DEBUG] HasCertificate: Cert {certId} NOT FOUND anywhere", certificateId);
        return false;
    }

    public bool HasUnlock(string unlockType, uint unlockId)
    {
        if (unlockId == 0)
        {
            return true;
        }

        string normalizedType = NormalizeUnlockType(unlockType);

        if (normalizedType == "certificate")
        {
            return HasCertificate(unlockId);
        }

        if (normalizedType == "blueprint")
        {
            return _autoBlueprints.Contains(unlockId) || _manualBlueprints.Contains(unlockId);
        }

        return _manualUnlocksByType.TryGetValue(normalizedType, out var unlocks) && unlocks.Contains(unlockId);
    }

    public bool HasAppliedUnlock(uint unlockId)
    {
        return HasUnlock("appliedunlock", unlockId);
    }

    /// <summary>
    /// Get all certificate IDs required by an ability module from its command chain.
    /// </summary>
    public HashSet<uint> GetAbilityRequiredCertificates(uint abilityModuleSdbId)
    {
        var requiredCerts = new HashSet<uint>();
        
        if (abilityModuleSdbId == 0)
            return requiredCerts;

        var abilityModule = SDBInterface.GetAbilityModule(abilityModuleSdbId);
        if (abilityModule == null || abilityModule.AbilityChainId == 0)
        {
            _shard?.Logger?.Warning("[ABILITY-DEBUG] Module {moduleId}: NO ABILITY MODULE FOUND", abilityModuleSdbId);
            return requiredCerts;
        }

        _shard?.Logger?.Information("[ABILITY-DEBUG] Module {moduleId}: AbilityChainId={chainId}", abilityModuleSdbId, abilityModule.AbilityChainId);

        var abilityData = SDBInterface.GetAbilityData(abilityModule.AbilityChainId);
        if (abilityData == null || abilityData.Chain == 0)
        {
            _shard?.Logger?.Warning("[ABILITY-DEBUG] Module {moduleId}: NO ABILITY DATA for chain {chainId}", abilityModuleSdbId, abilityModule.AbilityChainId);
            return requiredCerts;
        }

        _shard?.Logger?.Information("[ABILITY-DEBUG] Module {moduleId}: Starting chain traversal from ID={startId}", abilityModuleSdbId, abilityData.Chain);

        // Traverse command chain to find all RequireHasCertificate commands
        uint next = abilityData.Chain;
        int maxIterations = 1000;
        int foundCerts = 0;
        int commandCount = 0;
        
        while (next != 0 && maxIterations-- > 0)
        {
            var baseCommandDef = SDBInterface.GetBaseCommandDef(next);
            if (baseCommandDef == null)
            {
                _shard?.Logger?.Warning("[ABILITY-DEBUG] Module {moduleId}: Hit null command def at ID={id}", abilityModuleSdbId, next);
                break;
            }

            commandCount++;
            _shard?.Logger?.Debug("[ABILITY-DEBUG] Module {moduleId}: Command {idx} - ID={cmdId}, Type={type}, Next={next}", 
                abilityModuleSdbId, commandCount, baseCommandDef.Id, baseCommandDef.Subtype, baseCommandDef.Next);

            if (baseCommandDef.Subtype == (uint)CommandType.RequireHasCertificate)
            {
                var certCommand = SDBInterface.GetRequireHasCertificateCommandDef(baseCommandDef.Id);
                if (certCommand != null)
                {
                    requiredCerts.Add(certCommand.CertificateId);
                    foundCerts++;
                    _shard?.Logger?.Warning("[ABILITY-DEBUG] Module {moduleId}: *** FOUND CERT REQUIREMENT: {certId} ***", abilityModuleSdbId, certCommand.CertificateId);
                }
            }

            next = baseCommandDef.Next;
        }
        
        _shard?.Logger?.Warning("[ABILITY-DEBUG] Module {moduleId}: Scanned {cmdCount} commands, found {certCount} cert requirements", abilityModuleSdbId, commandCount, foundCerts);
        return requiredCerts;
    }

    /// <summary>
    /// Check if an ability module can be equipped on the given frame.
    /// Validates that the frame provides all certificates required by the ability.
    /// </summary>
    public bool CanEquipAbilityOnFrame(uint abilityModuleSdbId, uint currentFrameChassisId)
    {
        if (abilityModuleSdbId == 0)
            return true;  // Empty slot is always valid

        _shard?.Logger?.Warning("[EQUIP-DEBUG] CanEquipAbilityOnFrame: Module {moduleId} on frame {frameId}", abilityModuleSdbId, currentFrameChassisId);

        // Get all certificates required by this ability
        var requiredCerts = GetAbilityRequiredCertificates(abilityModuleSdbId);
        
        // If the ability has NO certificate requirements, it can be used on any frame
        // This is by design for common/universal abilities
        if (requiredCerts.Count == 0)
        {
            _shard?.Logger?.Warning("[EQUIP-DEBUG] *** UNIVERSAL ABILITY: Module {moduleId} has NO cert requirements, ALLOWING on any frame ***", abilityModuleSdbId);
            return true;
        }
        
        _shard?.Logger?.Warning("[EQUIP-DEBUG] Module {moduleId}: Checking {certCount} required certificates...", abilityModuleSdbId, requiredCerts.Count);

        // Check that ALL required certificates are available on the current frame
        foreach (var cert in requiredCerts)
        {
            bool hasCert = HasCertificate(cert, currentFrameChassisId);
            _shard?.Logger?.Warning("[EQUIP-DEBUG] Module {moduleId}: Cert {certId} check: {result}", abilityModuleSdbId, cert, hasCert ? "PASS" : "FAIL");
            
            if (!hasCert)
            {
                _shard?.Logger?.Warning("[EQUIP-DEBUG] >>>>>>> MODULE {moduleId} BLOCKED - missing cert {certId} on frame {frameId} <<<<<<<", abilityModuleSdbId, cert, currentFrameChassisId);
                return false;  // Missing required certificate for this frame
            }
        }

        _shard?.Logger?.Warning("[EQUIP-DEBUG] +++++ MODULE {moduleId} ALLOWED on frame {frameId} +++++", abilityModuleSdbId, currentFrameChassisId);
        return true;  // All required certs are met
    }

    public void SendUnlocksUpdate()
    {
        var globalCertIds = _autoGlobalCertificates
            .Concat(_manualGlobalCertificates)
            .Distinct()
            .ToArray();

        var frameScopedCerts = _autoFrameCertificates
            .Concat(_manualFrameCertificates)
            .Distinct()
            .ToArray();

        var blueprintIds = _autoBlueprints
            .Concat(_manualBlueprints)
            .Distinct()
            .ToArray();

        var groups = new List<UnlockGroup>();

        if (globalCertIds.Length > 0 || frameScopedCerts.Length > 0)
        {
            var certEntries = BuildCertificateEntries(globalCertIds, frameScopedCerts);
            groups.Add(new UnlockGroup
            {
                Key = "certificate",
                AddEntries = certEntries,
                RemEntries = Array.Empty<UnlockGroupEntrySmall>(),
            });
        }

        if (blueprintIds.Length > 0)
        {
            groups.Add(new UnlockGroup
            {
                Key = "blueprint",
                AddEntries = BuildGlobalEntries(blueprintIds),
                RemEntries = Array.Empty<UnlockGroupEntrySmall>(),
            });
        }

        foreach (var kvp in _manualUnlocksByType)
        {
            var ids = kvp.Value.OrderBy(v => v).ToArray();
            if (ids.Length == 0)
            {
                continue;
            }

            groups.Add(new UnlockGroup
            {
                Key = kvp.Key,
                AddEntries = BuildGlobalEntries(ids),
                RemEntries = Array.Empty<UnlockGroupEntrySmall>(),
            });
        }

        if (groups.Count == 0)
        {
            return;
        }

        _shard.Logger.Information(
            "Sending unlock update for {charId}: certGlobal={certGlobalCount}, certFrame={certFrameCount}, blueprints={blueprintCount}, groups={groupCount}",
            _character.EntityId,
            globalCertIds.Length,
            frameScopedCerts.Length,
            blueprintIds.Length,
            groups.Count);

        var update = new UnlocksUpdate
        {
            ClearExistingData = 0,
            Groups = groups.ToArray(),
        };

        _player.NetChannels[ChannelType.ReliableGss].SendMessage(update, _character.EntityId);
    }

    // Mirror of client lib_Battleframes.lua cert grants per chassis.
    // Used as the authoritative source for which certs each frame grants — the SDB relationships
    // (CharCreateLoadout.BaseCertificate and RootItem.ClassCertId) have not proven reliable
    // for this purpose on the server side.
    private static readonly Dictionary<uint, uint[]> FrameCertsByChassis = new()
    {
        [76164] = [732],             // Assault
        [76133] = [733, 732],        // Firecat
        [76132] = [734, 732],        // Tigerclaw
        [75775] = [735],             // Engineer
        [76337] = [736, 735],        // Electron
        [76338] = [737, 735],        // Bastion
        [75772] = [741],             // Dreadnaught
        [76331] = [742, 741],        // Mammoth
        [76332] = [743, 741],        // Rhino
        [82360] = [748, 741],        // Arsenal
        [75774] = [738],             // Biotech
        [76335] = [739, 738],        // Dragonfly
        [76336] = [740, 738],        // Recluse
        [75773] = [744],             // Recon
        [76333] = [745, 744],        // Nighthawk
        [76334] = [746, 744],        // Raptor
    };

    private void AddAutoCertificatesForFrame(uint chassisId)
    {
        if (!FrameCertsByChassis.TryGetValue(chassisId, out var certs))
        {
            _shard?.Logger?.Warning("[REBUILD-DEBUG] NO FRAME CERTS FOUND for chassis {frameId}!", chassisId);
            return;
        }

        _shard?.Logger?.Warning("[REBUILD-DEBUG] AddAutoCertificatesForFrame: chassis {frameId} grants {certCount} certs: {certs}", 
            chassisId, certs.Length, string.Join(", ", certs));

        foreach (uint certId in certs)
        {
            _autoFrameCertificates.Add((certId, chassisId));
            _shard?.Logger?.Debug("[REBUILD-DEBUG] Added frame cert: ({certId}, {frameId})", certId, chassisId);
        }
    }

    private UnlockGroupEntry[] BuildCertificateEntries(IEnumerable<uint> globalCertIds, IEnumerable<(uint CertId, uint FrameId)> frameScopedCerts)
    {
        return globalCertIds
            .Select(certId => new UnlockGroupEntry
            {
                CertId = certId,
                HaveUnk1 = 0,
                HaveUnk2 = 0,
                HaveUnk3 = 0,
            })
            .Concat(globalCertIds.Select(certId => new UnlockGroupEntry
            {
                CertId = certId,
                HaveUnk1 = 0,
                HaveUnk2 = 0,
                HaveUnk3 = 1,
                Unk3 = "global",
            }))
            .Concat(frameScopedCerts.Select(scoped => new UnlockGroupEntry
            {
                CertId = scoped.CertId,
                HaveUnk1 = 0,
                HaveUnk2 = 1,
                Unk2 = scoped.FrameId,
                HaveUnk3 = 0,
            }))
            .Concat(frameScopedCerts.Select(scoped => new UnlockGroupEntry
            {
                CertId = scoped.CertId,
                HaveUnk1 = 1,
                Unk1 = scoped.FrameId,
                HaveUnk2 = 0,
                HaveUnk3 = 0,
            }))
            .Concat(frameScopedCerts.Select(scoped => new UnlockGroupEntry
            {
                CertId = scoped.CertId,
                HaveUnk1 = 0,
                HaveUnk2 = 0,
                HaveUnk3 = 1,
                Unk3 = scoped.FrameId.ToString(),
            }))
            .ToArray();
    }

    private UnlockGroupEntry[] BuildGlobalEntries(IEnumerable<uint> ids)
    {
        return ids
            .Select(id => new UnlockGroupEntry
            {
                CertId = id,
                HaveUnk1 = 0,
                HaveUnk2 = 0,
                HaveUnk3 = 0,
            })
            .Concat(ids.Select(id => new UnlockGroupEntry
            {
                CertId = id,
                HaveUnk1 = 0,
                HaveUnk2 = 0,
                HaveUnk3 = 1,
                Unk3 = "global",
            }))
            .ToArray();
    }

    private string NormalizeUnlockType(string unlockType)
    {
        if (string.IsNullOrWhiteSpace(unlockType))
        {
            return "content";
        }

        string normalized = unlockType.Trim().Replace("_", string.Empty).Replace("-", string.Empty).ToLowerInvariant();
        return normalized switch
        {
            "cert" => "certificate",
            "certs" => "certificate",
            "certificate" => "certificate",
            "blueprints" => "blueprint",
            "recipe" => "blueprint",
            "recipes" => "blueprint",
            "applied" => "appliedunlock",
            "apply" => "appliedunlock",
            "applyunlock" => "appliedunlock",
            "appliedunlock" => "appliedunlock",
            _ => normalized,
        };
    }

    private void PersistManualUnlock(string unlockType, uint unlockId, uint frameId)
    {
        if (_player is not NetworkPlayer networkPlayer)
        {
            return;
        }

        ulong grpcCharacterId = networkPlayer.CharacterId + 0xFE;
        _ = GRPCService.SaveCharacterUnlockAsync(grpcCharacterId, unlockType, unlockId, frameId);
    }
}
