using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Reflection;

namespace Shared.Udp.Packets;

public static class ViewFactory
{
    private static readonly object InitLock = new();
    private static ConcurrentDictionary<Type, IPacketView> _instances = new();

    public static void Init()
    {
        _instances = new ConcurrentDictionary<Type, IPacketView>();

        var packetViewType = typeof(IPacketView);
        var types = AppDomain.CurrentDomain.GetAssemblies()
            .SelectMany(GetLoadableTypes)
            .Where(t => packetViewType.IsAssignableFrom(t) && t is { IsClass: true, IsAbstract: false });

        foreach (var t in types)
        {
            _ = AddPacketView(t);
        }
    }

    public static T Get<T>()
        where T : IPacketView
    {
        return (T)Get(typeof(T));
    }

    public static IPacketView Get(Type t)
    {
        EnsureInitialized();

        if (_instances.TryGetValue(t, out var instance))
        {
            return instance;
        }

        return AddPacketView(t);
    }

    private static IPacketView AddPacketView(Type t)
    {
        if (!typeof(IPacketView).IsAssignableFrom(t))
        {
            throw new ArgumentException($"{t.FullName} does not implement {nameof(IPacketView)}", nameof(t));
        }

        return _instances.GetOrAdd(t, static packetViewType => CreatePacketView(packetViewType));
    }

    private static void EnsureInitialized()
    {
        if (!_instances.IsEmpty)
        {
            return;
        }

        lock (InitLock)
        {
            if (!_instances.IsEmpty)
            {
                return;
            }

            Init();
        }
    }

    private static Type[] GetLoadableTypes(Assembly assembly)
    {
        try
        {
            return assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException ex)
        {
            return ex.Types.Where(t => t != null).Cast<Type>().ToArray();
        }
    }

    private static IPacketView CreatePacketView(Type packetViewType)
    {
        var defaultConstructor = packetViewType.GetConstructor(Type.EmptyTypes);
        if (defaultConstructor != null)
        {
            return (IPacketView)defaultConstructor.Invoke(null);
        }

        var endiannessConstructor = packetViewType.GetConstructor(new[] { typeof(BitEndianness) });
        if (endiannessConstructor != null)
        {
            return (IPacketView)endiannessConstructor.Invoke(new object[] { BitEndianness.Unknown });
        }

        throw new InvalidOperationException(
            $"Unable to construct packet view '{packetViewType.FullName}'. Expected parameterless or {nameof(BitEndianness)} constructor.");
    }
}