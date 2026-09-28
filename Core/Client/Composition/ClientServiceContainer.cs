using System;
using System.Collections.Generic;

namespace OpenGarrison.Core;

public sealed class ClientServiceContainer
{
    private readonly Dictionary<Type, object> _services = new();

    public T Get<T>() where T : notnull
    {
        if (!_services.TryGetValue(typeof(T), out var instance))
        {
            throw new InvalidOperationException($"No service is registered for type {typeof(T).FullName}.");
        }

        return (T)instance;
    }

    public void Register<T>(T instance) where T : notnull
    {
        ArgumentNullException.ThrowIfNull(instance);

        if (!_services.TryAdd(typeof(T), instance))
        {
            throw new InvalidOperationException($"A service is already registered for type {typeof(T).FullName}.");
        }
    }
}
