using System.Numerics;
using GameServer.Data.SDB.Records.aptfs;
using GameServer.Entities.Character;

namespace GameServer.Aptitude;

public class BattleFrameDurationCommand : Command, ICommand
{
    private BattleFrameDurationCommandDef Params;

    public BattleFrameDurationCommand(BattleFrameDurationCommandDef par)
: base(par)
    {
        Params = par;
    }

    public bool Execute(Context context)
    {
        if (context.Self is not CharacterEntity character)
        {
            return Params.Negate == 1;
        }

        bool result = true;

        if (Params.Classtype != 0)
        {
            result &= character.StaticInfo.CharacterTypeId == Params.Classtype;
        }

        if (Params.Notchanged == 1)
        {
            // Notchanged duration gates are used by toggle modes that should drop when the
            // player starts locomotion. Keep a small position tolerance for floating-point drift.
            const float positionToleranceSq = 0.04f;
            bool positionUnchanged = Vector3.DistanceSquared(character.Position, context.InitPosition) <= positionToleranceSq;
            bool movementUnchanged = !character.IsMoving;
            result &= positionUnchanged && movementUnchanged;
        }

        if (Params.Negate == 1)
        {
            result = !result;
        }

        return result;
    }
}