using GameServer.Data.SDB.Records.aptfs;
using GameServer.Entities.Character;

namespace GameServer.Aptitude;

public class TargetClassTypeCommand : Command, ICommand
{
    private TargetClassTypeCommandDef Params;

    public TargetClassTypeCommand(TargetClassTypeCommandDef par)
: base(par)
    {
        Params = par;
    }

    public bool Execute(Context context)
    {
        var prevTargets = context.Targets;
        var newTargets = new AptitudeTargets();

        foreach (IAptitudeTarget target in prevTargets)
        {
            if (target is not CharacterEntity character)
            {
                continue;
            }

            // Classtype corresponds to the creature/character type classification
            bool matches = character.StaticInfo.CharacterTypeId == Params.Classtype;

            if (Params.Negate == 1)
            {
                matches = !matches;
            }

            if (matches)
            {
                newTargets.Push(target);
            }
        }

        context.FormerTargets = prevTargets;
        context.Targets = newTargets;

        return true;
    }
}