using GameServer.Data.SDB.Records.apt;
using GameServer.Entities.Character;
using GameServer.Enums;

namespace GameServer.Aptitude;

public class InflictCooldownCommand : Command, ICommand
{
    private InflictCooldownCommandDef Params;

    public InflictCooldownCommand(InflictCooldownCommandDef par)
    : base(par)
    {
        Params = par;
    }

    public bool Execute(Context context)
    {
        if (context.ExecutionHint == ExecutionHint.RemoveEffect && context.Self is CharacterEntity selfCharacter && selfCharacter.ConsumeManualDeactivationCooldownSuppression(context.AbilityId))
        {
            return true;
        }

        uint currentTime = CooldownCommandSupport.ResolveCommandTime(context);
        uint localPrecoolMs = CooldownCommandSupport.EvaluateDuration(context.Register, Params.LocalCooldownPrecoolCount, (Operand)Params.PrecoolRegop);
        uint categoryPrecoolMs = CooldownCommandSupport.EvaluateDuration(context.Register, Params.CategoryCooldownPrecoolCount, (Operand)Params.CategoryPrecoolRegop);
        uint localDurationMs = CooldownCommandSupport.EvaluateDuration(context.Register, Params.LocalCooldown, (Operand)Params.DurationRegop);
        uint categoryDurationMs = CooldownCommandSupport.EvaluateDuration(context.Register, Params.CategoryCooldown, (Operand)Params.DurationRegop);
        uint globalDurationMs = CooldownCommandSupport.EvaluateDuration(context.Register, Params.GlobalCooldown, (Operand)Params.DurationRegop);
        bool preventReset = Params.PreventReset != 0;

        foreach (var character in CooldownCommandSupport.ResolveCooldownTargets(context))
        {
            bool allowFallbackAbilityId = character.EntityId == context.Self.EntityId || character.EntityId == context.Initiator.EntityId;
            character.ApplyAbilityCooldowns(
                context.AbilityId,
                context.ItemId,
                currentTime,
                localPrecoolMs,
                localDurationMs,
                Params.Category,
                categoryPrecoolMs,
                categoryDurationMs,
                globalDurationMs,
                Params.MainSlot,
                preventReset,
                allowFallbackAbilityId);
        }

        return true;
    }
}