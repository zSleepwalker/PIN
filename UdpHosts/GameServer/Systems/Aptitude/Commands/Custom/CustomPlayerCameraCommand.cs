using GameServer.Data.SDB.Records.apttf;
using SdbVector3 = FauFau.Util.CommmonDataTypes.Vector3;

namespace GameServer.Aptitude;

public class CustomPlayerCameraCommand : Command, ICommand
{
    private readonly tfCustomPlayerCameraCommandDef _params;

    public CustomPlayerCameraCommand(tfCustomPlayerCameraCommandDef par)
        : base(par)
    {
        _params = par;
    }

    public bool Execute(Context context)
    {
        return true;
    }

    public void OnApply(Context context, ICommandActiveContext activeCommandContext)
    {
    }

    public void OnRemove(Context context, ICommandActiveContext activeCommandContext)
    {
    }

    public override string ToString()
    {
        return $"CustomPlayerCamera (ID {_params.Id}, LookOffset={FormatVector(_params.LookOffset)}, RelativeOffset={FormatVector(_params.RelativeOffset)}, Fov={_params.FieldOfView}, LookChange={_params.LookChangeTime}, AimClamp={_params.DownAimClampDegrees}..{_params.UpAimClampDegrees}, PositionChange={_params.PositionChangeTime}, ExitChange={_params.ExitChangeTime}, UseAimOrientation={_params.UseAimOrientation})";
    }

    private static string FormatVector(SdbVector3 value)
    {
        return $"({value.x}, {value.y}, {value.z})";
    }
}
