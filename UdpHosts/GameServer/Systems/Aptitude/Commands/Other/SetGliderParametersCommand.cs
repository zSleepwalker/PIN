using GameServer.Data.SDB;
using GameServer.Data.SDB.Records.customdata;
using GameServer.Entities.Character;

namespace GameServer.Aptitude;

public class SetGliderParametersCommand : Command, ICommand
{
    private SetGliderParametersCommandDef Params;

    public SetGliderParametersCommand(SetGliderParametersCommandDef par)
: base(par)
    {
        Params = par;
    }

    public bool Execute(Context context)
    {
        var target = context.Self;

        if (target is CharacterEntity character)
        {
            if (Params.Value == null || Params.Value == 0)
            {
                Logger.Information("{Command} {CommandId} ignoring glider profile value {ProfileValue}", nameof(SetGliderParametersCommand), Params.Id, Params.Value?.ToString() ?? "null");
                return true;
            }

            var profileId = (uint)Params.Value;
            if (SDBInterface.GetGliderParameters(profileId) == null)
            {
                Logger.Warning("{Command} {CommandId} ignores unknown glider profile {ProfileId}", nameof(SetGliderParametersCommand), Params.Id, profileId);
                return true;
            }

            Logger.Information(
                "{Command} {CommandId} applying glider profile {ProfileId}, movement={MovementDebug}",
                nameof(SetGliderParametersCommand),
                Params.Id,
                profileId,
                character.DescribeMovementTransitionDebugState());
            character.SetGliderProfileId(profileId);
        }

        return true;
    }
}