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
        ClientFeedbackEmitter.EmitParticleEffect(context, Id);
        return true;
    }

    public override string ToString()
    {
        return $"ParticleEffectAsset (ID {Id})";
    }
}
