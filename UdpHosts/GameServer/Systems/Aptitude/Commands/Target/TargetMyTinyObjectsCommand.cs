using System;
using GameServer.Data.SDB.Records.customdata;

namespace GameServer.Aptitude;

public class TargetMyTinyObjectsCommand : Command, ICommand
{
    private TargetMyTinyObjectsCommandDef Params;

    public TargetMyTinyObjectsCommand(TargetMyTinyObjectsCommandDef par)
: base(par)
    {
        Params = par;
    }

    public bool Execute(Context context)
    {
        // TinyObject system not fully implemented.
        // TinyObjects are small-scale interactive ability objects (e.g. turret pellets, drones).
        Serilog.Log.Information($"[TargetMyTinyObjects] CMD {Id}: TinyObject system not implemented. No targets added.");

        context.FormerTargets = context.Targets;
        context.Targets = new AptitudeTargets();

        return true;
    }
}