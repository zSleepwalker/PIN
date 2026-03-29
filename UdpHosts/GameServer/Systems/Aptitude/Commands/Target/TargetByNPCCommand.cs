using System;
using GameServer.Data.SDB.Records.customdata;
using GameServer.Entities.Character;

namespace GameServer.Aptitude;

public class TargetByNPCCommand : Command, ICommand
{
    private TargetByNPCCommandDef Params;

    public TargetByNPCCommand(TargetByNPCCommandDef par)
: base(par)
    {
        Params = par;
    }

    public bool Execute(Context context)
    {
        // Target an NPC that is owned by / associated with the initiator
        // NPC ownership system not yet fully implemented; logs and passes through
        Console.WriteLine($"[TargetByNPC] CMD {Id}: NPC targeting not fully implemented. Passing through existing targets.");
        return true;
    }
}