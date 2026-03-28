using GameServer.Data.SDB.Records.customdata;

namespace GameServer.Aptitude;

public class GrantOwnerItemCommand : Command, ICommand
{
    private GrantOwnerItemCommandDef Params;

    public GrantOwnerItemCommand(GrantOwnerItemCommandDef par)
: base(par)
    {
        Params = par;
    }

    public bool Execute(Context context)
    {
        System.Console.WriteLine($"GrantOwnerItemCommand: Triggered for {context.Initiator} (ItemId: {context.ItemId})");
        return true;
    }
}