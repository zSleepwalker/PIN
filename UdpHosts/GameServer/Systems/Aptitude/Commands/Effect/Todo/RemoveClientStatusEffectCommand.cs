using GameServer.Data.SDB.Records.aptfs;

namespace GameServer.Aptitude;

public class RemoveClientStatusEffectCommand : Command, ICommand
{
    private RemoveClientStatusEffectCommandDef Params;

    public RemoveClientStatusEffectCommand(RemoveClientStatusEffectCommandDef par)
: base(par)
    {
        Params = par;
    }

    public bool Execute(Context context)
    {
        if (Params.StatusEffectId == 0)
        {
            return true;
        }

        bool removed = false;

        if (Params.ApplyToSelf == 1)
        {
            context.Abilities.DoRemoveEffect(context.Self, Params.StatusEffectId);
            removed = true;
        }

        if (Params.UseTargetClients == 1)
        {
            foreach (var target in context.Targets)
            {
                context.Abilities.DoRemoveEffect(target, Params.StatusEffectId);
                removed = true;
            }
        }

        if (!removed)
        {
            context.Abilities.DoRemoveEffect(context.Self, Params.StatusEffectId);
        }

        return true;
    }
}