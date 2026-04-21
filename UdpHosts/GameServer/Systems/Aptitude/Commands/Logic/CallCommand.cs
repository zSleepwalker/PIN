using GameServer.Data.SDB;
using GameServer.Data.SDB.Records.apt;

namespace GameServer.Aptitude;

public class CallCommand : Command, ICommand
{
    private CallCommandDef Params;

    public CallCommand(CallCommandDef par)
: base(par)
    {
        Params = par;
    }

    public bool Execute(Context context)
    {
        if (Params.AbilityId == 0)
        {
            return true;
        }

        var abilityData = SDBInterface.GetAbilityData(Params.AbilityId);
        if (abilityData?.Chain == 0)
        {
            return false;
        }

        uint previousChainId = context.ChainId;
        uint previousAbilityId = context.AbilityId;
        context.ChainId = abilityData.Chain;
        if (context.AbilityId == 0)
        {
            context.AbilityId = Params.AbilityId;
        }

        try
        {
            return context.Abilities.Factory.LoadChain(abilityData.Chain).Execute(context);
        }
        finally
        {
            context.ChainId = previousChainId;
            context.AbilityId = previousAbilityId;
        }
    }
}