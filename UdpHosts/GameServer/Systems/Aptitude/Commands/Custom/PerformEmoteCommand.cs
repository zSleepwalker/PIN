using GameServer.Data.SDB.Records.apttf;

namespace GameServer.Aptitude;

public class PerformEmoteCommand : Command, ICommand
{
    private readonly tfPerformEmoteCommandDef _params;

    public PerformEmoteCommand(tfPerformEmoteCommandDef par)
        : base(par)
    {
        _params = par;
    }

    public bool Execute(Context context)
    {
        return ClientAnimationCommandHelper.TryApplyEmoteToken(context, _params.EmoteName, Logger, nameof(PerformEmoteCommand), _params.Id);
    }

    public override string ToString()
    {
        return $"PerformEmote (ID {_params.Id}, EmoteName={_params.EmoteName})";
    }
}