using System;
using GameServer.Data.SDB.Records.customdata;

namespace GameServer.Aptitude;

public class ApplyUnlockCommand : Command, ICommand
{
    private ApplyUnlockCommandDef Params;

    public ApplyUnlockCommand(ApplyUnlockCommandDef par)
: base(par)
    {
        Params = par;
    }

    public bool Execute(Context context)
    {
        // Unlock system not yet implemented.
        // When unlock system is available: apply the unlock (ID from Params.Id) to context.Self's account.
        Console.WriteLine($"[ApplyUnlock] CMD {Id}: Unlock system not implemented. Would apply unlock {Params.Id} to {context.Self}.");
        return true;
    }
}