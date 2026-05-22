#nullable enable
using System.Collections.Generic;
using System.Numerics;
using GameServer.Data.SDB.Records.dbitems;
using GameServer.Entities.Character;

namespace GameServer.Systems.ProjectileSim;

public class ProjectileSim
{
    private readonly Shard _shard;

    // Keyed by lower 16 bits of the trace uint (matches client ReportProjectileHit.TraceRef)
    private readonly Dictionary<ushort, PendingProjectileHit> _pendingHits = new();

    public ProjectileSim(Shard shard)
    {
        _shard = shard;
    }

    public void FireProjectile(CharacterEntity entity, uint trace, Vector3 origin, Vector3 direction, Ammo ammo)
    {
        ulong hitEntityId = _shard.Physics.ProjectileRayCast(origin, direction, entity, trace);
        ushort traceRef = (ushort)trace;
        _pendingHits[traceRef] = new PendingProjectileHit
        {
            ShooterEntityId = entity.EntityId,
            HitEntityId = hitEntityId,
            AmmoId = ammo?.Id ?? 0,
        };
    }

    /// <summary>
    /// Called from ReportProjectileHit to claim a pending server-side hit result.
    /// Returns null if the TraceRef was not found (hit rejected).
    /// </summary>
    public PendingProjectileHit? ResolveHit(ushort traceRef)
    {
        if (_pendingHits.Remove(traceRef, out var hit))
        {
            return hit;
        }

        return null;
    }

    /// <summary>
    /// Force-resolves and removes all pending hits for the given ammo type.
    /// Used by <c>DetonateProjectilesCommand</c> to cancel in-flight projectiles and
    /// prevent stale entries from being claimed later by a ReportProjectileHit.
    /// </summary>
    public List<PendingProjectileHit> DetonateByAmmoType(uint ammoTypeId)
    {
        var toRemove = new List<ushort>();
        var result = new List<PendingProjectileHit>();

        foreach (var kvp in _pendingHits)
        {
            if (kvp.Value.AmmoId == ammoTypeId)
            {
                toRemove.Add(kvp.Key);
                result.Add(kvp.Value);
            }
        }

        foreach (var key in toRemove)
        {
            _pendingHits.Remove(key);
        }

        return result;
    }

    /*
    public void Tick(double deltaTime, ulong currentTime, CancellationToken ct)
    {
    }
    */
}

public class PendingProjectileHit
{
    public ulong ShooterEntityId;
    public ulong HitEntityId;
    public uint AmmoId;
}