using GameServer.Data.SDB.Records.customdata;

namespace GameServer.Aptitude;

public class TinyObjectUpdateCommand : Command, ICommand
{
    private TinyObjectUpdateCommandDef Params;

    public TinyObjectUpdateCommand(TinyObjectUpdateCommandDef par)
: base(par)
    {
        Params = par;
    }

    public bool Execute(Context context)
    {
        // TinyObject lifecycle is not yet implemented server-side.
        Logger.Debug("TinyObjectUpdate CMD {Id}: no-op (TinyObject entity system not yet implemented)", Id);
        return true;
    }
}