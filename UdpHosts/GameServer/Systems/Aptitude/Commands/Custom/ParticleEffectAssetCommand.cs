using GameServer.Entities.Deployable;

namespace GameServer.Aptitude;

public class ParticleEffectAssetCommand : ICommand
{
    public uint Id { get; set; }

    public ParticleEffectAssetCommand(uint id)
    {
        Id = id;
    }

    public bool Execute(Context context)
    {
        // For deployable entities the placement particle should only fire once at initial
        // placement, not every time the deployable's internal effect state machine cycles.
        if (context.Self is DeployableEntity deployable)
        {
            if (deployable.PlacementParticleFired)
            {
                return true;
            }

            deployable.PlacementParticleFired = true;
            Serilog.Log.Information($"[ParticleEffect] Placement particle fired for deployable {deployable} command={Id}");
        }

        Serilog.Log.Debug(
            "[ParticleEffect] Skipping unresolved particle command payload. CommandId={CommandId}, AbilityId={AbilityId}, ChainId={ChainId}, Self={Self}",
            Id,
            context.AbilityId,
            context.ChainId,
            context.Self);

        // TODO: Load apttf::tfParticleEffectAssetCommandDef payload and emit the resolved particle asset ID.
        return true;
    }

    public override string ToString()
    {
        return $"ParticleEffectAsset (ID {Id})";
    }
}