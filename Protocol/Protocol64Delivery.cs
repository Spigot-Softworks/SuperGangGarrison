using System;
using System.Linq;
using System.Reflection;

namespace OpenGarrison.Protocol;

/// <summary>
/// Logical delivery channels. A backend maps these channels to its own streams,
/// queues, or lanes; the protocol does not depend on a particular transport.
/// </summary>
public enum ChannelType : byte
{
    Control = 1,
    Input = 2,
    State = 3,
    GameplayEvents = 4,
    Chat = 5,
    Social = 6,
    Plugin = 7,
}

public enum Protocol64DeliveryKind : byte
{
    ReliableOrdered = 1,
    ReliableUnordered = 2,
    LastWins = 3,
}

public readonly record struct Protocol64DeliveryDescriptor(
    Protocol64DeliveryKind Kind,
    ChannelType? Channel)
{
    public bool IsReliable => Kind is
        Protocol64DeliveryKind.ReliableOrdered or
        Protocol64DeliveryKind.ReliableUnordered;

    public bool IsOrdered => Kind == Protocol64DeliveryKind.ReliableOrdered;

    public bool IsLastWins => Kind == Protocol64DeliveryKind.LastWins;
}

[AttributeUsage(AttributeTargets.Class, Inherited = true, AllowMultiple = false)]
public abstract class Protocol64DeliveryAttribute : Attribute
{
    protected Protocol64DeliveryAttribute(Protocol64DeliveryKind kind, ChannelType? channel)
    {
        Kind = kind;
        Channel = channel;
    }

    public Protocol64DeliveryKind Kind { get; }

    public ChannelType? Channel { get; }

    public Protocol64DeliveryDescriptor Descriptor => new(Kind, Channel);
}

/// <summary>
/// Complete events that must be delivered in semantic send order.
/// </summary>
public sealed class ReliableOrderedAttribute : Protocol64DeliveryAttribute
{
    public ReliableOrderedAttribute(ChannelType channel)
        : base(Protocol64DeliveryKind.ReliableOrdered, channel)
    {
    }
}

/// <summary>
/// Complete events that must not be dropped, but do not require one global order.
/// </summary>
public sealed class ReliableUnorderedAttribute : Protocol64DeliveryAttribute
{
    public ReliableUnorderedAttribute(ChannelType channel)
        : base(Protocol64DeliveryKind.ReliableUnordered, channel)
    {
    }
}

/// <summary>
/// Complete, repairable state where an older instance may be replaced by a newer one.
/// Omitting the channel lets the backend choose its default latest-state lane.
/// </summary>
public sealed class LastWinsAttribute : Protocol64DeliveryAttribute
{
    public LastWinsAttribute()
        : base(Protocol64DeliveryKind.LastWins, null)
    {
    }

    public LastWinsAttribute(ChannelType channel)
        : base(Protocol64DeliveryKind.LastWins, channel)
    {
    }
}

public static class Protocol64DeliveryMetadata
{
    public static Protocol64DeliveryDescriptor GetDescriptor(Type schemaType)
    {
        ArgumentNullException.ThrowIfNull(schemaType);

        var attributes = schemaType
            .GetCustomAttributes<Protocol64DeliveryAttribute>(inherit: true)
            .ToArray();

        return attributes.Length switch
        {
            1 => attributes[0].Descriptor,
            0 => throw new InvalidOperationException(
                $"Protocol-64 schema '{schemaType.FullName}' must declare a delivery attribute."),
            _ => throw new InvalidOperationException(
                $"Protocol-64 schema '{schemaType.FullName}' declares more than one delivery attribute."),
        };
    }

    public static Protocol64DeliveryDescriptor GetDescriptor<TSchema>()
        => GetDescriptor(typeof(TSchema));
}
