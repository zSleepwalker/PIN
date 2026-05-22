using GameServer.Data.SDB.Records.aptfs;

namespace GameServer.Aptitude;

public class DetonateProjectilesCommand : Command, ICommand
{
    private DetonateProjectilesCommandDef Params;

    public DetonateProjectilesCommand(DetonateProjectilesCommandDef par)
: base(par)
    {
        Params = par;
    }

    public bool Execute(Context context)
    {
        // Force-resolve and discard all pending server-side projectile hits for the given
        // ammo type. This prevents stale entries from being claimed later by a spurious
        // ReportProjectileHit if the client's in-flight projectile is "detonated" early.
        //
        // Full detonation (applying area/splash damage via the ammo's TouchAbilityId or
        // AirburstAbilityId) is deferred until the detonation ability-chain re-entry is
        // better understood through RE. The current server-authoritative hit model requires
        // the client to send ReportProjectileHit for normal hit resolution, so force-firing
        // that pipeline here without more context could duplicate damage.
        var hits = context.Shard.ProjectileSim.DetonateByAmmoType(Params.AmmoTypeId);
        if (hits.Count > 0)
        {
            Logger.Debug(
                "DetonateProjectilesCommand {Id}: cleared {Count} pending hit(s) for ammo type {AmmoTypeId}",
                Id, hits.Count, Params.AmmoTypeId);
        }

        return true;
    }
}