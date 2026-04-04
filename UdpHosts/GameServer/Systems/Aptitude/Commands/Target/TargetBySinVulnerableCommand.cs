using GameServer.Data.SDB.Records.customdata;

namespace GameServer.Aptitude;

public class TargetBySinVulnerableCommand : Command, ICommand
{
    private TargetBySinVulnerableCommandDef Params;

    public TargetBySinVulnerableCommand(TargetBySinVulnerableCommandDef par)
: base(par)
    {
        Params = par;
    }

    public bool Execute(Context context)
    {
        // SIN vulnerability system not yet implemented.
        // Pass through all targets (assume none are SIN-vulnerable by default).
        Serilog.Log.Information($"[TargetBySinVulnerable] CMD {Id}: SIN vulnerability system not implemented. Passing through targets.");
        return true;
    }
}