using GameServer.Data.SDB.Records.aptfs;
using GameServer.Entities.Character;

namespace GameServer.Aptitude;

public class RegisterMovementEffectCommand : Command, ICommand
{
    private RegisterMovementEffectCommandDef Params;

    public RegisterMovementEffectCommand(RegisterMovementEffectCommandDef par)
: base(par)
    {
        Params = par;
    }

    public bool Execute(Context context)
    {
        if (context.Self is not CharacterEntity character)
        {
            Logger.Warning("{Command} {CommandId} does nothing because self is not a Character. Self is {SourceType}.", nameof(RegisterMovementEffectCommand), Params.Id, context.Self.GetType().Name);
            return true;
        }

        if (!TryResolveMovestate(Params.MovestateIndex, out var movestate))
        {
            Logger.Warning("{Command} {CommandId} ignored unsupported movestate index {MovestateIndex}.", nameof(RegisterMovementEffectCommand), Params.Id, Params.MovestateIndex);
            return true;
        }

        Logger.Information(
            "{Command} {CommandId} registering statusfx {StatusEffectId} for movestateIndex {MovestateIndex} ({Movestate}), currentMovestate={CurrentMovestate}, onClient={OnClient}, onServer={OnServer}, reapply={Reapply}",
            nameof(RegisterMovementEffectCommand),
            Params.Id,
            Params.StatusfxId,
            Params.MovestateIndex,
            movestate,
            character.MovementStateContainer.Movestate,
            Params.OnClient,
            Params.OnServer,
            Params.Reapply);

        context.Actives[this] = new RegisterMovementEffectCommandActiveContext
        {
            CommandId = Params.Id,
            StatusEffectId = Params.StatusfxId,
            Movestate = movestate,
            TemplateContext = Context.CopyContext(context),
            OnClient = Params.OnClient,
            OnServer = Params.OnServer,
            Reapply = Params.Reapply
        };

        return true;
    }

    public void OnApply(Context context, ICommandActiveContext activeCommandContext)
    {
        if (context.Self is CharacterEntity character && activeCommandContext is RegisterMovementEffectCommandActiveContext movementEffectContext)
        {
            character.RegisterMovementEffect(movementEffectContext);
        }
    }

    public void OnRemove(Context context, ICommandActiveContext activeCommandContext)
    {
        if (context.Self is CharacterEntity character && activeCommandContext is RegisterMovementEffectCommandActiveContext movementEffectContext)
        {
            character.UnregisterMovementEffect(movementEffectContext);
        }
    }

    private static bool TryResolveMovestate(uint movestateIndex, out Movestate movestate)
    {
        if (movestateIndex is >= 1 and <= 13)
        {
            movestate = (Movestate)(movestateIndex << 4);
            return true;
        }

        movestate = default;
        return false;
    }
}

public class RegisterMovementEffectCommandActiveContext : ICommandActiveContext
{
    public uint CommandId { get; set; }
    public uint StatusEffectId { get; set; }
    public Movestate Movestate { get; set; }
    public Context TemplateContext { get; set; }
    public byte OnClient { get; set; }
    public byte OnServer { get; set; }
    public byte Reapply { get; set; }
}