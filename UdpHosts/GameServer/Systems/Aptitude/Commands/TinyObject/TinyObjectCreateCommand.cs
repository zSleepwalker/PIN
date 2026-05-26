using GameServer.Data.SDB.Records.customdata;

namespace GameServer.Aptitude;

public class TinyObjectCreateCommand : Command, ICommand
{
    private TinyObjectCreateCommandDef Params;

    public TinyObjectCreateCommand(TinyObjectCreateCommandDef par)
: base(par)
    {
        Params = par;
    }

    public bool Execute(Context context)
    {
        // TinyObject lifecycle is not yet implemented server-side.
        Logger.Debug("TinyObjectCreate CMD {Id}: no-op (TinyObject entity system not yet implemented)", Id);
        return true;
    }
}