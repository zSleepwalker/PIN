using System.Collections.Generic;
using System.Numerics;

namespace GameServer.Aptitude;

public class Context
{
    public Context(IShard shard, IAptitudeTarget initiator)
    {
        Shard = shard;
        Initiator = initiator;
        Self = initiator;
        Abilities = shard.Abilities;
        Targets = new AptitudeTargets();
        FormerTargets = new AptitudeTargets();
        InitPosition = initiator.Position;
    }

    public uint ChainId { get; set; }
    public uint AbilityId { get; set; }
    public bool Success { get; set; }
    public IShard Shard { get; set; }
    public AbilitySystem Abilities { get; set; }
    public IAptitudeTarget Self { get; set; }
    public IAptitudeTarget Initiator { get; set; }
    public AptitudeTargets Targets { get; set; }
    public AptitudeTargets FormerTargets { get; set; }
    public float Register { get; set; }
    public float FormerRegister { get; set; }
    public int Bonus { get; set; }
    public Dictionary<string, float> NamedVariables { get; set; } = new Dictionary<string, float>();
    public uint InitTime { get; set; }
    public Vector3 InitPosition { get; set; }
    public uint ExecutionId { get; set; }
    public ExecutionHint ExecutionHint { get; set; }
    public uint ItemId { get; set; }
    public bool ActivationAcknowledged { get; set; }

    public Dictionary<ICommand, ICommandActiveContext> Actives { get; set; } = new Dictionary<ICommand, ICommandActiveContext>();

    public static string CreateNamedVariableKey(byte varSrcType, ushort nameId, string memberName)
    {
        return $"{varSrcType}:{nameId}:{memberName ?? string.Empty}";
    }

    public static Context CopyContext(Context original)
    {
        return new Context(original.Shard, original.Initiator)
        {
            ChainId = original.ChainId,
            AbilityId = original.AbilityId,
            Success = original.Success,
            Shard = original.Shard,
            Abilities = original.Abilities,
            Self = original.Self,
            Initiator = original.Initiator,
            Targets = original.Targets,
            FormerTargets = original.FormerTargets,
            Register = original.Register,
            Bonus = original.Bonus,
            NamedVariables = new Dictionary<string, float>(original.NamedVariables),
            InitTime = original.InitTime,
            InitPosition = original.InitPosition,
            ExecutionId = original.ExecutionId,
            ExecutionHint = original.ExecutionHint,
            ItemId = original.ItemId,
            ActivationAcknowledged = original.ActivationAcknowledged,
        };
    }

    /*
    public uint NamedVar;
    public uint Interaction;
    public uint SourceContext;
    public uint SourceEffect;
    */
}