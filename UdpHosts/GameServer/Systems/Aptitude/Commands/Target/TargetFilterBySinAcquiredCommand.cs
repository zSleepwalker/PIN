using System;
using GameServer.Data.SDB.Records.aptfs;

namespace GameServer.Aptitude;

public class TargetFilterBySinAcquiredCommand : Command, ICommand
{
    private TargetFilterBySinAcquiredCommandDef Params;

    public TargetFilterBySinAcquiredCommand(TargetFilterBySinAcquiredCommandDef par)
: base(par)
    {
        Params = par;
    }

    public bool Execute(Context context)
    {
        // SIN (Surveillance and Identification Network) acquire system not yet implemented.
        // For now: if Negate=0, targets are kept as-is (assuming all targets have SIN acquired).
        // If Negate=1, targets are removed (no SIN acquired).
        Console.WriteLine($"[TargetFilterBySinAcquired] CMD {Id}: SIN system not implemented. Negate={Params.Negate}, FailNoTargets={Params.FailNoTargets}");

        if (Params.Negate == 1)
        {
            // Negate: treat as if no targets are SIN-acquired → clear targets
            context.FormerTargets = context.Targets;
            context.Targets = new AptitudeTargets();

            if (Params.FailNoTargets == 1)
            {
                return false;
            }
        }

        // Non-negate: pass targets through (assume all are SIN-acquired)
        return true;
    }
}