using GameServer.Data.SDB.Records.apt;
using GameServer.Enums;

namespace GameServer.Aptitude;

public class LoadRegisterFromNamedVarCommand : Command, ICommand
{
    private LoadRegisterFromNamedVarCommandDef Params;

    public LoadRegisterFromNamedVarCommand(LoadRegisterFromNamedVarCommandDef par)
: base(par)
    {
        Params = par;
    }

    public bool Execute(Context context)
    {
        var key = Context.CreateNamedVariableKey(Params.VarSrctype, Params.NameId, Params.MemberName);
        float namedValue = context.NamedVariables.TryGetValue(key, out var value)
            ? value
            : Params.UndeclValue;

        context.Register = AbilitySystem.RegistryOp(context.Register, namedValue, (Operand)Params.Regop);
        return true;
    }
}