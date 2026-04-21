using GameServer.Data.SDB.Records.apt;
using GameServer.Entities.Character;

namespace GameServer.Aptitude;

public class TimeCooldownCommand : Command, ICommand
{
    private TimeCooldownCommandDef Params;

    public TimeCooldownCommand(TimeCooldownCommandDef par)
    : base(par)
    {
        Params = par;
    }

    public bool Execute(Context context)
    {
        uint currentTime = CooldownCommandSupport.ResolveCommandTime(context);
        uint thresholdTime = currentTime + Params.Duration;

        bool result = true;
        bool anyChecked = false;
        string failureDetail = string.Empty;

        if (context.Targets.Count > 0)
        {
            foreach (var target in context.Targets)
            {
                if (!TargetSatisfiesCooldown(target, context, thresholdTime, currentTime, ref anyChecked, out failureDetail))
                {
                    result = false;
                    break;
                }
            }
        }
        else
        {
            result = TargetSatisfiesCooldown(context.Self, context, thresholdTime, currentTime, ref anyChecked, out failureDetail);
        }

        if (!anyChecked)
        {
            result = true;
        }

        if (!result)
        {
            Logger.Debug(
                "{Command} {CommandId} failed for ability {AbilityId}. Duration={Duration} CheckCategory={CheckCategory} CheckGlobal={CheckGlobal} CheckLocal={CheckLocal} Category={Category} CurrentTime={CurrentTime} InitTime={InitTime} ThresholdTime={ThresholdTime} Details={Details}",
                nameof(TimeCooldownCommand),
                Params.Id,
                context.AbilityId,
                Params.Duration,
                Params.CheckCategory,
                Params.CheckGlobal,
                Params.CheckLocal,
                Params.Category,
                currentTime,
                context.InitTime,
                thresholdTime,
                failureDetail);
        }

        return result;
    }

    private bool TargetSatisfiesCooldown(IAptitudeTarget target, Context context, uint thresholdTime, uint currentTime, ref bool anyChecked, out string failureDetail)
    {
        failureDetail = string.Empty;

        if (target is not CharacterEntity character)
        {
            return true;
        }

        if (Params.CheckLocal == 1)
        {
            anyChecked = true;
            uint localReadyAgainTime = character.GetAbilityCooldownReadyAgainTime(context.AbilityId, currentTime);
            if (localReadyAgainTime > thresholdTime)
            {
                failureDetail = $"Target={character} Scope=Local ReadyAgain={localReadyAgainTime}";
                return false;
            }
        }

        if (Params.CheckCategory == 1)
        {
            anyChecked = true;
            uint categoryReadyAgainTime = character.GetResolvedCategoryCooldownReadyAgainTime(context.AbilityId, Params.Category, currentTime);
            if (categoryReadyAgainTime > thresholdTime)
            {
                failureDetail = $"Target={character} Scope=Category Category={Params.Category} ReadyAgain={categoryReadyAgainTime}";
                return false;
            }
        }

        if (Params.CheckGlobal == 1)
        {
            anyChecked = true;
            uint globalReadyAgainTime = character.GetGlobalCooldownReadyAgainTime(currentTime);
            if (globalReadyAgainTime > thresholdTime)
            {
                failureDetail = $"Target={character} Scope=Global ReadyAgain={globalReadyAgainTime}";
                return false;
            }
        }

        return true;
    }
}