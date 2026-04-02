using System;

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
        // For client-environment aptitude commands, Id is the command ID, not a validated
        // particle asset ID. Emitting it as PfxAssetId causes placeholder/missing-FX visuals
        // (question-mark style effects) across many activations.
        // TODO: Load apttf::tfParticleEffectAssetCommandDef payload and emit the real asset ID.
        return true;
    }

    public override string ToString()
    {
        return $"ParticleEffectAsset (ID {Id})";
    }
}
