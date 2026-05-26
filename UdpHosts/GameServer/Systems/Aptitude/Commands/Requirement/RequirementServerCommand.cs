using GameServer.Data.SDB.Records.aptfs;

namespace GameServer.Aptitude;

public class RequirementServerCommand : Command, ICommand
{
    private RequirementServerCommandDef Params;

    public RequirementServerCommand(RequirementServerCommandDef par)
: base(par)
    {
        Params = par;
    }

    public bool Execute(Context context)
    {
        if (Params.Server == 1)
        {
            return true;
        }

        if (Params.Local == 1 || Params.LocalInit == 1 || Params.Client == 1)
        {
            Logger.Debug(
                "{Command} {CommandId} failed on server runtime. Flags: Server={Server} Local={Local} LocalInit={LocalInit} Client={Client}",
                nameof(RequirementServerCommand),
                Params.Id,
                Params.Server,
                Params.Local,
                Params.LocalInit,
                Params.Client);
        }

        return false;
    }
}
