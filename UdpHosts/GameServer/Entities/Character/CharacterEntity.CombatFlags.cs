using System.Collections.Generic;
using System.Linq;
using AeroMessages.GSS.V66.Character;
using GameServer.Aptitude;

namespace GameServer.Entities.Character;

public sealed partial class CharacterEntity
{
    private readonly Dictionary<ICommand, CombatFlagsData.CharacterCombatFlags> _activeCombatFlagSources = new();

    public void ApplyCombatFlags(ICommand source, CombatFlagsData.CharacterCombatFlags flags)
    {
        if (source == null)
        {
            return;
        }

        if (flags == 0)
        {
            RemoveCombatFlags(source);
            return;
        }

        _activeCombatFlagSources[source] = flags;
        Serilog.Log.Debug("Character {Character} applying combat flags from {Source}: {Flags}", this, source, flags);
        RefreshCombatFlags();
    }

    public void RemoveCombatFlags(ICommand source)
    {
        if (source == null)
        {
            return;
        }

        if (_activeCombatFlagSources.Remove(source))
        {
            Serilog.Log.Debug("Character {Character} removing combat flags from {Source}", this, source);
            RefreshCombatFlags();
        }
    }

    private void RefreshCombatFlags()
    {
        var combinedFlags = _activeCombatFlagSources.Values.Aggregate((CombatFlagsData.CharacterCombatFlags)0, static (current, next) => current | next);
        Serilog.Log.Debug("Character {Character} refreshing combat flags => {CombinedFlags} at {Time}", this, combinedFlags, Shard.CurrentTime);
        SetCombatFlags(new CombatFlagsData
        {
            Value = combinedFlags,
            Time = Shard.CurrentTime,
        });
    }
}