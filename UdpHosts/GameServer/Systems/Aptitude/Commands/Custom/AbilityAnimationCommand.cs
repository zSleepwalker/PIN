using AeroMessages.GSS.V66.Character.Event;
using GameServer.Data.SDB.Records.apttf;

namespace GameServer.Aptitude;

public class AbilityAnimationCommand : Command, ICommand
{
    private readonly tfAbilityAnimationCommandDef _params;

    public AbilityAnimationCommand(tfAbilityAnimationCommandDef par)
        : base(par)
    {
        _params = par;
    }

    public bool Execute(Context context)
    {
        ushort animationValue = PackAnimationValue();
        byte animationFlags = PackAnimationFlags();
        int sentCount = ClientAnimationCommandHelper.BroadcastAnimationUpdated(context, animationValue, animationFlags);

        Logger.Information(
            "{Command} {CommandId} sent AnimationUpdated Unk1={Unk1} Unk2={Unk2} (animIndex={AnimIndex}, subState={SubState}, queueOffset={QueueOffset}, backpack={Backpack}, cancel={Cancel}, outro={Outro}, combo={Combo}, aim={AllowAiming}, reloads={AllowReloads}, movementTime={MovementTime}, fullBody={FullBody}) to {SentCount} clients",
            nameof(AbilityAnimationCommand),
            _params.Id,
            animationValue,
            animationFlags,
            _params.AbilityAnimIndex,
            _params.SubStateIndex,
            _params.QueueTimeOffset,
            _params.BackpackState,
            _params.Cancel,
            _params.Outro,
            _params.Combo,
            _params.AllowAiming,
            _params.AllowReloads,
            _params.MovementTime,
            _params.FullBody,
            sentCount);

        return true;
    }

    public override string ToString()
    {
        return $"AbilityAnimation (ID {_params.Id}, SubState={_params.SubStateIndex}, QueueOffset={_params.QueueTimeOffset}, AnimIndex={_params.AbilityAnimIndex}, Backpack={_params.BackpackState}, Cancel={_params.Cancel}, AllowReloads={_params.AllowReloads}, Outro={_params.Outro}, Combo={_params.Combo}, AllowAiming={_params.AllowAiming}, MovementTime={_params.MovementTime}, FullBody={_params.FullBody})";
    }

    private ushort PackAnimationValue()
    {
        uint packed = 0;
        packed |= _params.SubStateIndex & 0x3;
        packed |= (_params.QueueTimeOffset & 0x3) << 2;
        packed |= (_params.AbilityAnimIndex & 0xff) << 4;
        packed |= (_params.BackpackState & 0xf) << 12;
        return unchecked((ushort)packed);
    }

    private byte PackAnimationFlags()
    {
        uint packed = 0;
        packed |= _params.Cancel & 0x1;
        packed |= (_params.AllowReloads & 0x1) << 1;
        packed |= (_params.Outro & 0x1) << 2;
        packed |= (_params.Combo & 0x1) << 3;
        packed |= (_params.AllowAiming & 0x1) << 4;
        packed |= (_params.MovementTime & 0x1) << 5;
        packed |= (_params.FullBody & 0x1) << 6;
        return unchecked((byte)packed);
    }
}