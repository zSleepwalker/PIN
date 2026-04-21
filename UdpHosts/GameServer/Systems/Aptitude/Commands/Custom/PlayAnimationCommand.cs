using GameServer.Data.SDB.Records.apttf;

namespace GameServer.Aptitude;

public class PlayAnimationCommand : Command, ICommand
{
    private readonly tfPlayAnimationCommandDef _params;

    public PlayAnimationCommand(tfPlayAnimationCommandDef par)
        : base(par)
    {
        _params = par;
    }

    public bool Execute(Context context)
    {
        return ClientAnimationCommandHelper.TryApplyEmoteToken(context, _params.AnimationName, Logger, nameof(PlayAnimationCommand), _params.Id);
    }

    public override string ToString()
    {
        return $"PlayAnimation (ID {_params.Id}, AnimationName={_params.AnimationName}, P2={_params.Param2}, P3={_params.Param3})";
    }
}