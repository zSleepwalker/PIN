using GameServer.Data.SDB.Records.customdata;

namespace GameServer.Aptitude;

public class ConsumeItemCommand : Command, ICommand
{
    private ConsumeItemCommandDef Params;

    public ConsumeItemCommand(ConsumeItemCommandDef par)
        : base(par)
    {
        Params = par;
    }

    public bool Execute(Context context)
    {
        if (context.ItemId != 0 && context.Initiator is Entities.Character.CharacterEntity character)
        {
            if (character.Player != null)
            {
                character.Player.Inventory.ConsumeResource(context.ItemId, 1);
                System.Console.WriteLine($"ConsumeItemCommand: Consumed 1 of {context.ItemId} for {character}");
            }
        }
        else
        {
            System.Console.WriteLine($"ConsumeItemCommand: Failed to consume item. ItemId: {context.ItemId}, Initiator: {context.Initiator}");
        }
        return true;
    }
}