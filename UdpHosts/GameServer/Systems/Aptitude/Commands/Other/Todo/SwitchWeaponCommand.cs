using System;
using AeroMessages.GSS.V66.Character;
using AeroMessages.GSS.V66.Character.Event;
using GameServer.Data.SDB.Records.aptfs;
using GameServer.Entities.Character;

namespace GameServer.Aptitude;

public class SwitchWeaponCommand : Command, ICommand
{
    private SwitchWeaponCommandDef Params;

    public SwitchWeaponCommand(SwitchWeaponCommandDef par)
: base(par)
    {
        Params = par;
    }

    public bool Execute(Context context)
    {
        if (context.Self is not CharacterEntity character)
        {
            return true;
        }

        var targetWeaponIndex = ResolveRuntimeWeaponIndex();
        if (targetWeaponIndex == null)
        {
            Serilog.Log.Information($"SwitchWeaponCommand {Id} ignored invalid TargetWeaponSlot {Params.TargetWeaponSlot}");
            return true;
        }

        if (Params.RestoreOnRollback == 1)
        {
            context.Actives[this] = new SwitchWeaponCommandActiveContext
            {
                PreviousWeaponIndex = character.WeaponIndex,
            };
        }

        ApplyWeaponIndex(context, character, targetWeaponIndex.Value);

        return true;
    }

    public void OnRemove(Context context, ICommandActiveContext activeCommandContext)
    {
        if (context.Self is not CharacterEntity character || activeCommandContext is not SwitchWeaponCommandActiveContext switchContext)
        {
            return;
        }

        ApplyWeaponIndex(context, character, switchContext.PreviousWeaponIndex.Index, switchContext.PreviousWeaponIndex);
    }

    private byte? ResolveRuntimeWeaponIndex()
    {
        // Aptitude weapon indices appear to be 1-based: 1 holstered, 2 primary, 3 secondary.
        // Character weapon indices on the wire/runtime are 0 holstered, 1 primary, 2 secondary.
        var runtimeIndex = Params.TargetWeaponSlot - 1;
        return runtimeIndex is >= 0 and <= 2 ? (byte)runtimeIndex : null;
    }

    private void ApplyWeaponIndex(Context context, CharacterEntity character, byte weaponIndex, WeaponIndexData? existing = null)
    {
        var current = existing ?? character.WeaponIndex;
        var weaponIndexData = new WeaponIndexData
        {
            Index = weaponIndex,
            Unk1 = current.Unk1,
            Unk2 = current.Unk2,
            Time = context.Shard.CurrentTime,
        };

        character.SetWeaponIndex(weaponIndexData);

        if (Params.Forced == 1 && character.IsPlayerControlled)
        {
            character.Player.NetChannels[ChannelType.ReliableGss].SendMessage(new ForcedWeaponSwap
            {
                WeaponIndex = weaponIndex,
                ShortTime = context.Shard.CurrentShortTime,
            }, character.EntityId);
        }
    }
}

public class SwitchWeaponCommandActiveContext : ICommandActiveContext
{
    public WeaponIndexData PreviousWeaponIndex { get; set; }
}