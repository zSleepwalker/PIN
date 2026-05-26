using System.Numerics;
using GameServer.Data.SDB;
using GameServer.Data.SDB.Records.aptfs;
using GameServer.Entities.Character;
using GameServer.Systems.PRNG;

namespace GameServer.Aptitude;

public class FireProjectileCommand : Command, ICommand
{
    private FireProjectileCommandDef Params;

    public FireProjectileCommand(FireProjectileCommandDef par)
: base(par)
    {
        Params = par;
    }

    public bool Execute(Context context)
    {
        if (context.Self is not CharacterEntity entity)
        {
            Logger.Warning("FireProjectileCommand {Id}: Self is not a CharacterEntity, skipping", Id);
            return true;
        }

        var ammo = SDBInterface.GetAmmo(Params.Ammotype);
        if (ammo == null)
        {
            Logger.Warning("FireProjectileCommand {Id}: ammo type {AmmoType} not found in SDB", Id, Params.Ammotype);
            return true;
        }

        var origin = entity.GetProjectileOrigin();
        byte burstCount = Params.Burstcount == 0 ? (byte)1 : Params.Burstcount;

        for (byte round = 0; round < burstCount; round++)
        {
            Vector3 direction;
            if (Params.UseHomingTarget == 1 && context.HomingTarget != null)
            {
                var toTarget = context.HomingTarget.Position - origin;
                direction = toTarget.LengthSquared() > 0.0001f
                    ? Vector3.Normalize(toTarget)
                    : entity.AimDirection;
            }
            else
            {
                direction = entity.AimDirection;
            }

            uint trace = PRNG.Trace(context.InitTime, round);
            context.Shard.ProjectileSim.FireProjectile(entity, trace, origin, direction, ammo);
        }

        return true;
    }
}