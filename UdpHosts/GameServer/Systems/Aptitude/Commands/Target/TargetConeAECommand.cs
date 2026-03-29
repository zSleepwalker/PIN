using System;
using System.Linq;
using System.Numerics;
using GameServer.Data.SDB.Records.apt;
using GameServer.Entities;
using GameServer.Entities.Character;
using GameServer.Enums;

namespace GameServer.Aptitude;

public class TargetConeAECommand : Command, ICommand
{
    private TargetConeAECommandDef Params;

    public TargetConeAECommand(TargetConeAECommandDef par)
: base(par)
    {
        Params = par;
    }

    public bool Execute(Context context)
    {
        context.FormerTargets = new AptitudeTargets(context.Targets);

        float range = AbilitySystem.RegistryOp(context.Register, Params.Range, (Operand)Params.RangeRegop);
        float angleRad = Params.Angle * MathF.PI / 180.0f; // Convert degrees to radians

        Vector3 origin = Params.UseBodyPosition == 1 ? context.Self.Position : context.InitPosition;

        // Determine the aim direction: use context aim direction if available, fallback to forward
        // We use the context.Initiator's AimDirection if it's a character
        Vector3 aimDir = Vector3.UnitX;
        if (context.Initiator is CharacterEntity initiatorChar)
        {
            aimDir = initiatorChar.AimDirection;
        }

        if (aimDir == Vector3.Zero)
        {
            aimDir = Vector3.UnitX;
        }
        else
        {
            aimDir = Vector3.Normalize(aimDir);
        }

        var matches = context.Shard.Entities
            .Where(pair =>
            {
                if (pair.Value is BaseAptitudeEntity target)
                {
                    // If target is Self, it's always included if within range/angle as TargetConeAEDef doesn't have IncludeSelf flag to exclude it
                    Vector3 toTarget = target.Position - origin;
                    float distance = toTarget.Length();

                    if (Params.MinRadius > 0 && distance < Params.MinRadius)
                    {
                        return false;
                    }

                    if (distance > range)
                    {
                        return false;
                    }

                    if (distance < 0.001f)
                    {
                        return true; // Origin itself always in cone
                    }

                    Vector3 toTargetNorm = Vector3.Normalize(toTarget);
                    float dot = Vector3.Dot(aimDir, toTargetNorm);
                    float halfAngle = angleRad / 2.0f;

                    return dot >= MathF.Cos(halfAngle);
                }

                return false;
            })
            .Select(pair => pair.Value as BaseAptitudeEntity)
            .ToList();

        if (Params.SortByAngle == 1)
        {
            matches.Sort((a, b) =>
            {
                Vector3 toA = Vector3.Normalize(a.Position - origin);
                Vector3 toB = Vector3.Normalize(b.Position - origin);
                float dotA = Vector3.Dot(aimDir, toA);
                float dotB = Vector3.Dot(aimDir, toB);
                return dotB.CompareTo(dotA); // Sort by angle ascending (most aligned first)
            });
        }

        int addCount = 0;
        foreach (var match in matches)
        {
            if (Params.MaxTargets > 0 && addCount >= Params.MaxTargets)
            {
                break;
            }

            context.Targets.Push(match);
            addCount++;
        }

        if (context.Targets.Count < Params.MinTargets)
        {
            return false;
        }

        return true;
    }
}