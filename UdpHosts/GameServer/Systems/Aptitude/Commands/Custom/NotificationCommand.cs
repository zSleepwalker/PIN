using System;
using AeroMessages.GSS.V66.Character.Event;
using GameServer.Entities.Character;

namespace GameServer.Aptitude;

public class NotificationCommand : ICommand
{
    public uint Id { get; set; }

    public NotificationCommand(uint id)
    {
        Id = id;
    }

    public bool Execute(Context context)
    {
        if (context.Self is CharacterEntity character && character.IsPlayerControlled)
        {
            // For now, we at least log that a notification was triggered.
            // If the client executes its own chain, it should show its own notification.
            // But if we want the server to explicitly trigger one, we would send a message like PrivateCombatLog or PrivateDialog.
            
            // Serilog.Log.Information($"Notification triggered for {character.StaticInfo.DisplayName}: ID {Id}");
        }

        return true;
    }

    public override string ToString()
    {
        return $"Notification (ID {Id})";
    }
}
