using System.Numerics;
using AeroMessages.Common;
using AeroMessages.GSS.V66.AreaVisualData;
using GameServer.Entities;
using GameServer.Entities.Carryable;
using GameServer.Entities.Deployable;

namespace GameServer.Aptitude;

internal static class ClientFeedbackEmitter
{
    private const uint DefaultParticleLifetimeMs = 5000;

    public static void EmitParticleEffect(Context context, uint particleEffectId)
    {
        if (particleEffectId == 0)
        {
            return;
        }

        var anchor = ResolveAnchor(context);
        if (anchor == null)
        {
            return;
        }

        var areaVisualData = context.Shard.EntityMan.SpawnAreaVisualData(
            anchor.Position,
            new ScopingComponent { Global = anchor.IsGlobalScope(), Range = anchor.GetScopeRange() });

        areaVisualData.AreaVisualData_ObserverView.PositionProp = anchor.Position;
        areaVisualData.AreaVisualData_ParticleEffectsView.ParticleEffects_0Prop = new ParticleEffect
        {
            PfxEntityId = anchor.AeroEntityId,
            PfxAssetId = particleEffectId,
            Position = anchor.Position,
            HaveUnk4 = 0,
            Rotation = ResolveRotation(anchor),
            Unk9 = 1,
            Unk10 = context.InitTime != 0 ? context.InitTime : context.Shard.CurrentTime,
            Scale = 1f,
            HaveUnk12 = 0,
        };

        context.Shard.EntityMan.SetRemainingLifetime(areaVisualData, DefaultParticleLifetimeMs);
        context.Shard.EntityMan.FlushChanges(areaVisualData);
    }

    private static BaseEntity ResolveAnchor(Context context)
    {
        if (context.Self is BaseEntity self)
        {
            return self;
        }

        if (context.Initiator is BaseEntity initiator)
        {
            return initiator;
        }

        return null;
    }

    private static QuantisedQuaternion ResolveRotation(BaseEntity anchor)
    {
        return anchor switch
        {
            DeployableEntity deployable => deployable.Orientation,
            CarryableEntity carryable => carryable.Orientation,
            _ => Quaternion.Identity,
        };
    }
}