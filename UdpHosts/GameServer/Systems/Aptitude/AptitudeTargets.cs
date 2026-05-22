using System;
using System.Collections;
using System.Collections.Generic;

namespace GameServer.Aptitude;

public class AptitudeTargets : IEnumerable<IAptitudeTarget>
{
    private readonly List<IAptitudeTarget> _targets;

    public AptitudeTargets()
    {
        _targets = [];
    }

    public AptitudeTargets(AptitudeTargets initialTargets)
    {
        _targets = [.. initialTargets];
    }

    public AptitudeTargets(params IAptitudeTarget[] initialTargets)
    {
        _targets = new(initialTargets.Length);

        foreach (var target in initialTargets)
        {
            if (target != null)
            {
                _targets.Add(target);
            }
        }
    }

    public int Count => _targets.Count;

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }

    public IEnumerator<IAptitudeTarget> GetEnumerator()
    {
        return _targets.GetEnumerator();
    }

    public void Push(IAptitudeTarget target)
    {
        if (target == null)
        {
            return;
        }

        _targets.Add(target);
    }

    public bool TryPop(out IAptitudeTarget result)
    {
        var ok = _targets.Count != 0;

        if (ok)
        {
            result = _targets[^1];

            _targets.RemoveAt(_targets.Count - 1);

            return true;
        }

        result = null;

        return false;
    }

    public bool TryPeek(out IAptitudeTarget result)
    {
        var ok = _targets.Count != 0;

        if (ok)
        {
            result = _targets[^1];

            return true;
        }

        result = null;

        return false;
    }

    public IAptitudeTarget Peek()
    {
        return _targets[^1];
    }

    public void RemoveBottomN(int number)
    {
        _targets.RemoveRange(0, Math.Min(number, _targets.Count));
    }

    public void PopN(int number)
    {
        _targets.RemoveRange(_targets.Count - Math.Min(number, _targets.Count), Math.Min(number, _targets.Count));
    }

    public void Clear()
    {
        _targets.Clear();
    }

    public IAptitudeTarget[] ToArray()
    {
        return [.. _targets];
    }

    public void PrintTargets()
    {
    }
}