using GameServer.Data.SDB;
using GameServer.Data.SDB.Records.customdata;

namespace GameServer.Aptitude;

public class ActivateAbilityTriggerCommand : Command, ICommand
{
    private ActivateAbilityTriggerCommandDef Params;

    public ActivateAbilityTriggerCommand(ActivateAbilityTriggerCommandDef par)
: base(par)
    {
        Params = par;
    }

    public bool Execute(Context context)
    {
        var abilityId = Params.AbilityId;
        var chainId = Params.Chain;

        // Fallback for incomplete custom data: infer chain from base command linkage.
        if (abilityId == 0 && chainId == 0)
        {
            chainId = SDBInterface.GetBaseCommandDef(Params.Id)?.Next ?? 0;
        }

        if (abilityId != 0)
        {
            context.Abilities.HandleActivateAbility(context.Shard, context.Self, abilityId, context.InitTime, context.Targets);
            return true;
        }

        if (chainId != 0)
        {
            var chain = context.Abilities.Factory.LoadChain(chainId);
            if (chain != null)
            {
                chain.Execute(Context.CopyContext(context));
                return true;
            }
        }

        Serilog.Log.Information($"[ActivateAbilityTrigger] No ability/chain resolved for command {Params.Id}");
        return true;
    }
}