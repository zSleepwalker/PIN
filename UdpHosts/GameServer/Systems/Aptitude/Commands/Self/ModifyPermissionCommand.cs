using System.Collections.Generic;
using AeroMessages.GSS.V66.Character.Controller;
using GameServer.Data.SDB.Records.customdata;
using GameServer.Entities.Character;

namespace GameServer.Aptitude;

public class ModifyPermissionCommand : Command, ICommand
{
    private ModifyPermissionCommandDef Params;

    public ModifyPermissionCommand(ModifyPermissionCommandDef par)
: base(par)
    {
        Params = par;
    }

    public bool Execute(Context context)
    {
        var target = context.Self; // NOTE: Based on glider, it seems like it should use self, maybe that is reasonable for all 'active' style commands?
        if (target is CharacterEntity character)
        {
            var updates = BuildPermissionUpdates();
            if (updates.Count == 0)
            {
                return true;
            }

            var previousValues = new Dictionary<PermissionFlagsData.CharacterPermissionFlags, bool>(updates.Count);
            foreach (var update in updates)
            {
                previousValues[update.Key] = character.CurrentPermissions.GetValueOrDefault(update.Key);
            }

            context.Actives[this] = new ModifyPermissionActiveContext
            {
                Character = character,
                PreviousValues = previousValues,
                NewValues = updates,
            };
        }

        return true;
    }

    public void OnApply(Context context, ICommandActiveContext activeCommandContext)
    {
        if (activeCommandContext is ModifyPermissionActiveContext permissionContext)
        {
            foreach (var update in permissionContext.NewValues)
            {
                permissionContext.Character.SetPermissionFlag(update.Key, update.Value);
            }
        }
    }

    public void OnRemove(Context context, ICommandActiveContext activeCommandContext)
    {
        if (activeCommandContext is ModifyPermissionActiveContext permissionContext)
        {
            foreach (var previousValue in permissionContext.PreviousValues)
            {
                permissionContext.Character.SetPermissionFlag(previousValue.Key, previousValue.Value);
            }
        }
    }

    private Dictionary<PermissionFlagsData.CharacterPermissionFlags, bool> BuildPermissionUpdates()
    {
        var updates = new Dictionary<PermissionFlagsData.CharacterPermissionFlags, bool>();

        if (Params.Glider != null)
        {
            updates[PermissionFlagsData.CharacterPermissionFlags.glider] = (bool)Params.Glider;
        }

        if (Params.GliderHud != null)
        {
            updates[PermissionFlagsData.CharacterPermissionFlags.glider_hud] = (bool)Params.GliderHud;
        }

        if (Params.Jetpack != null)
        {
            updates[PermissionFlagsData.CharacterPermissionFlags.jetpack] = (bool)Params.Jetpack;
        }

        return updates;
    }
}

public sealed class ModifyPermissionActiveContext : ICommandActiveContext
{
    public CharacterEntity Character { get; set; }
    public Dictionary<PermissionFlagsData.CharacterPermissionFlags, bool> PreviousValues { get; set; } = new();
    public Dictionary<PermissionFlagsData.CharacterPermissionFlags, bool> NewValues { get; set; } = new();
}