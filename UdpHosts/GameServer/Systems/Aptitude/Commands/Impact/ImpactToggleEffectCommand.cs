using GameServer.Data.SDB.Records.apt;
using GameServer.Entities.Character;

namespace GameServer.Aptitude;

public class ImpactToggleEffectCommand : Command, ICommand
{
    private ImpactToggleEffectCommandDef Params;

    public ImpactToggleEffectCommand(ImpactToggleEffectCommandDef par)
: base(par)
    {
        Params = par;
    }

    public bool Execute(Context context)
    {
        Context effectContext = new Context(context.Shard, context.Initiator)
        {
            ChainId = context.ChainId,
            AbilityId = context.AbilityId,
            Success = context.Success,
            ExecutionId = context.ExecutionId,
            InitTime = context.InitTime,
            InitPosition = context.InitPosition,
            ItemId = context.ItemId,
            ActivationAcknowledged = context.ActivationAcknowledged,
            NamedVariables = new(context.NamedVariables),
            SourceContext = context.SourceContext,
            SourceEffect = context.SourceEffect,
        };

        effectContext.FormerRegister = context.FormerRegister;
        effectContext.FormerTargets = context.FormerTargets;

        if (Params.PassRegister == 1)
        {
            effectContext.Register = context.Register;
        }

        if (Params.PassBonus == 1)
        {
            effectContext.Bonus = context.Bonus;
        }

        foreach (IAptitudeTarget target in context.Targets)
        {
            bool targetHasEffect = false;
            foreach (EffectState active in target.GetActiveEffects())
            {
                if (active == null)
                {
                    continue;
                }

                if (active.Effect.Id == Params.EffectId)
                {
                    targetHasEffect = true;
                    effectContext.ExecutionHint = ExecutionHint.RemoveEffect;
                    effectContext.Abilities.DoRemoveEffect(active);

                    if (target is CharacterEntity targetCharacter)
                    {
                        targetCharacter.UntrackAbilityToggleEffect(context.AbilityId, Params.EffectId);
                    }

                    break;
                }
            }

            if (targetHasEffect)
            {
                continue;
            }

            if (Params.PreApplyChain != 0)
            {
                var chain = effectContext.Abilities.Factory.LoadChain(Params.PreApplyChain);
                chain.Execute(effectContext);
            }

            effectContext.ExecutionHint = ExecutionHint.ApplyEffect;
            effectContext.Abilities.DoApplyEffect(Params.EffectId, target, effectContext);

            if (target is CharacterEntity targetCharacterApplied)
            {
                targetCharacterApplied.TrackAbilityToggleEffect(context.AbilityId, Params.EffectId);
            }
        }

        return true;
    }
}