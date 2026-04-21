using GameServer.Data.SDB.Records.aptfs;
using GameServer.Entities.Character;

namespace GameServer.Aptitude;

public class ActivationDurationCommand : Command, ICommand
{
    private ActivationDurationCommandDef Params;

    public ActivationDurationCommand(ActivationDurationCommandDef par)
: base(par)
    {
        Params = par;
    }

    public bool Execute(Context context)
    {
        uint currentTime = (uint)context.Shard.CurrentTime;
        uint abilityId = Params.AbilityId != 0 ? Params.AbilityId : context.AbilityId;
        CharacterEntity sourceCharacter = ResolveSourceCharacter(context);
        bool isActive = sourceCharacter?.IsAbilityActivationActive(abilityId, currentTime) ?? false;

        if (!isActive
            && sourceCharacter != null
            && context.PendingActivationStateRequested
            && context.PendingActivationCharacter == sourceCharacter
            && abilityId == context.AbilityId)
        {
            isActive = true;
        }

        bool result = Params.Activated == 1 ? isActive : !isActive;

        if (!result)
        {
            Logger.Debug(
                "{Command} {CommandId} failed for ability {AbilityId}. Activated={Activated} IsActive={IsActive} SelfType={SelfType} InitiatorType={InitiatorType}",
                nameof(ActivationDurationCommand),
                Params.Id,
                abilityId,
                Params.Activated,
                isActive,
                context.Self.GetType().Name,
                context.Initiator.GetType().Name);
        }

        return result;
    }

    private static CharacterEntity ResolveSourceCharacter(Context context)
    {
        if (context.Self is CharacterEntity selfCharacter)
        {
            return selfCharacter;
        }

        if (context.Initiator is CharacterEntity initiatorCharacter)
        {
            return initiatorCharacter;
        }

        return null;
    }
}