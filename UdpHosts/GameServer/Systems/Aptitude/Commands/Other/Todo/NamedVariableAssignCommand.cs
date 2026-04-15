using GameServer.Data.SDB.Records.apt;
using GameServer.Enums;

namespace GameServer.Aptitude;

public class NamedVariableAssignCommand : Command, ICommand
{
    private NamedVariableAssignCommandDef Params;

    public NamedVariableAssignCommand(NamedVariableAssignCommandDef par)
: base(par)
    {
        Params = par;
    }

    public bool Execute(Context context)
    {
        var key = Context.CreateNamedVariableKey(Params.VarSrctype, Params.NameId, Params.MemberName);
        float currentValue = context.NamedVariables.TryGetValue(key, out var existingValue)
            ? existingValue
            : 0f;

        context.NamedVariables[key] = AbilitySystem.RegistryOp(currentValue, Params.Value, (Operand)Params.Regop);
        return true;
    }
}