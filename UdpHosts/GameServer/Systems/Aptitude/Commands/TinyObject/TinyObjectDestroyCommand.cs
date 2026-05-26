using GameServer.Data.SDB.Records.customdata;

namespace GameServer.Aptitude;

public class TinyObjectDestroyCommand : Command, ICommand
{
    private TinyObjectDestroyCommandDef Params;

    public TinyObjectDestroyCommand(TinyObjectDestroyCommandDef par)
: base(par)
    {
        Params = par;
    }

    public bool Execute(Context context)
    {
        // TinyObject lifecycle is not yet implemented server-side.
        Logger.Debug("TinyObjectDestroy CMD {Id}: no-op (TinyObject entity system not yet implemented)", Id);
        return true;
    }
}