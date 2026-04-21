using AeroMessages.GSS.V66.Character;
using GameServer.Data.SDB.Records.aptfs;
using GameServer.Entities.Character;

namespace GameServer.Aptitude;

public class CombatFlagsCommand : Command, ICommand
{
    private readonly CombatFlagsCommandDef Params;

    public CombatFlagsCommand(CombatFlagsCommandDef par)
: base(par)
    {
        Params = par;
    }

    public bool Execute(Context context)
    {
        if (context.Self is not CharacterEntity character)
        {
            Logger.Warning("{Command} {CommandId} does nothing because self is not a Character. Self is {SourceType}.", nameof(CombatFlagsCommand), Params.Id, context.Self.GetType().Name);
            return true;
        }

        var flags = BuildFlags();
        if (flags == 0)
        {
            if (Params.ImmunePhysics != 0 || Params.ImmuneDeath != 0)
            {
                Logger.Information(
                    "{Command} {CommandId} only contains currently-unmapped fields ImmunePhysics={ImmunePhysics} ImmuneDeath={ImmuneDeath}",
                    nameof(CombatFlagsCommand),
                    Params.Id,
                    Params.ImmunePhysics,
                    Params.ImmuneDeath);
            }

            return true;
        }

        context.Actives[this] = new CombatFlagsCommandActiveContext
        {
            Character = character,
            Flags = flags,
        };

        return true;
    }

    public void OnApply(Context context, ICommandActiveContext activeCommandContext)
    {
        if (activeCommandContext is CombatFlagsCommandActiveContext combatFlagsContext)
        {
            combatFlagsContext.Character.ApplyCombatFlags(this, combatFlagsContext.Flags);
        }
    }

    public void OnRemove(Context context, ICommandActiveContext activeCommandContext)
    {
        if (activeCommandContext is CombatFlagsCommandActiveContext combatFlagsContext)
        {
            combatFlagsContext.Character.RemoveCombatFlags(this);
        }
    }

    public override string ToString()
    {
        return $"CombatFlags (ID {Params.Id}, Flags={BuildFlags()}, ImmunePhysics={Params.ImmunePhysics}, ImmuneDeath={Params.ImmuneDeath})";
    }

    private CombatFlagsData.CharacterCombatFlags BuildFlags()
    {
        CombatFlagsData.CharacterCombatFlags flags = 0;

        if (Params.RestrictMovement != 0)
        {
            flags |= CombatFlagsData.CharacterCombatFlags.restrict_movement;
        }

        if (Params.RestrictWeapon != 0)
        {
            flags |= CombatFlagsData.CharacterCombatFlags.restrict_weapon;
        }

        if (Params.RestrictAbilities != 0)
        {
            flags |= CombatFlagsData.CharacterCombatFlags.restrict_abilities;
        }

        if (Params.KnockDown != 0)
        {
            flags |= CombatFlagsData.CharacterCombatFlags.knock_down;
        }

        if (Params.MoveThroughObjects != 0)
        {
            flags |= CombatFlagsData.CharacterCombatFlags.move_through_objects;
        }

        if (Params.RestrictSprint != 0)
        {
            flags |= CombatFlagsData.CharacterCombatFlags.restrict_sprint;
        }

        if (Params.RestrictMelee != 0)
        {
            flags |= CombatFlagsData.CharacterCombatFlags.restrict_melee;
        }

        if (Params.RestrictInteraction != 0)
        {
            flags |= CombatFlagsData.CharacterCombatFlags.restrict_interaction;
        }

        if (Params.RemoveHitboxes != 0)
        {
            flags |= CombatFlagsData.CharacterCombatFlags.remove_hitboxes;
        }

        if (Params.ImmuneFalldamage != 0)
        {
            flags |= CombatFlagsData.CharacterCombatFlags.immune_falldamage;
        }

        if (Params.ReversedControls != 0)
        {
            flags |= CombatFlagsData.CharacterCombatFlags.reversed_controls;
        }

        if (Params.RestrictStumble != 0)
        {
            flags |= CombatFlagsData.CharacterCombatFlags.restrict_stumble;
        }

        return flags;
    }
}

public sealed class CombatFlagsCommandActiveContext : ICommandActiveContext
{
    public CharacterEntity Character { get; set; }
    public CombatFlagsData.CharacterCombatFlags Flags { get; set; }
}