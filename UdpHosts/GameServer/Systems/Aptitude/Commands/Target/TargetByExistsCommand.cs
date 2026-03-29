using System;
using GameServer.Data.SDB.Records.customdata;

namespace GameServer.Aptitude;

public class TargetByExistsCommand : Command, ICommand
{
    private TargetByExistsCommandDef Params;

    public TargetByExistsCommand(TargetByExistsCommandDef par)
: base(par)
    {
        Params = par;
    }

    public bool Execute(Context context)
    {
        // TargetByExists: returns true if there are targets in the set, false otherwise
        // Useful as a condition check within a chain
        return context.Targets.Count > 0;
    }
}