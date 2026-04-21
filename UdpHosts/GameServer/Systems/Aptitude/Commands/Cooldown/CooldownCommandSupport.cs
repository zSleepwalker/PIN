using System;
using System.Collections.Generic;
using GameServer.Entities.Character;
using GameServer.Enums;

namespace GameServer.Aptitude;

internal static class CooldownCommandSupport
{
    public static uint ClampDuration(float value)
    {
        if (!float.IsFinite(value) || value <= 0.0f)
        {
            return 0;
        }

        if (value >= uint.MaxValue)
        {
            return uint.MaxValue;
        }

        return (uint)Math.Round(value, MidpointRounding.AwayFromZero);
    }

    public static uint EvaluateDuration(float register, uint value, Operand op)
    {
        if (value == 0)
        {
            return 0;
        }

        return ClampDuration(AbilitySystem.RegistryOp(register, value, op));
    }

    public static IEnumerable<CharacterEntity> ResolveCooldownTargets(Context context)
    {
        var seenTargets = new HashSet<ulong>();

        if (context.Targets.Count == 0)
        {
            if (context.Self is CharacterEntity selfCharacter)
            {
                yield return selfCharacter;
            }

            yield break;
        }

        foreach (var target in context.Targets)
        {
            if (target is CharacterEntity character && seenTargets.Add(character.EntityId))
            {
                yield return character;
            }
        }
    }

    public static uint ResolveCommandTime(Context context)
    {
        // Cooldown windows are stored and published on authoritative server time.
        // Using client-provided activation timestamps here can leave cooldown checks
        // behind the server clock and make a short cooldown appear to last much longer.
        return (uint)context.Shard.CurrentTime;
    }
}