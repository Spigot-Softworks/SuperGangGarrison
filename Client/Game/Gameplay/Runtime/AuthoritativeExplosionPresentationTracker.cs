#nullable enable

using System;
using System.Collections.Generic;

namespace OpenGarrison.Client;

internal enum AuthoritativeExplosionPresentationChannel : byte
{
    Sound,
    Visual,
}

/// <summary>
/// Pairs the independently replicated sound and visual events for one explosion.
/// Event IDs cannot be used here because each channel receives its own ID.
/// </summary>
internal sealed class AuthoritativeExplosionPresentationTracker
{
    internal const int DefaultIdentityCapacity = 512;
    private const double PositionQuantizationScale = 4d;

    private readonly int _identityCapacity;
    private readonly Dictionary<Identity, int> _unmatchedChannelBalances = new();
    private readonly Queue<Identity> _identityOrder = new();

    internal AuthoritativeExplosionPresentationTracker(int identityCapacity = DefaultIdentityCapacity)
    {
        _identityCapacity = Math.Max(1, identityCapacity);
    }

    internal bool ShouldPresent(
        ulong sourceFrame,
        float x,
        float y,
        AuthoritativeExplosionPresentationChannel channel)
    {
        var identity = new Identity(sourceFrame, QuantizePosition(x), QuantizePosition(y));
        if (!_unmatchedChannelBalances.TryGetValue(identity, out var balance))
        {
            while (_unmatchedChannelBalances.Count >= _identityCapacity
                && _identityOrder.TryDequeue(out var oldestIdentity))
            {
                _unmatchedChannelBalances.Remove(oldestIdentity);
            }

            _unmatchedChannelBalances.Add(identity, 0);
            _identityOrder.Enqueue(identity);
        }

        if (channel == AuthoritativeExplosionPresentationChannel.Sound)
        {
            if (balance < 0)
            {
                _unmatchedChannelBalances[identity] = balance + 1;
                return false;
            }

            _unmatchedChannelBalances[identity] = balance + 1;
            return true;
        }

        if (balance > 0)
        {
            _unmatchedChannelBalances[identity] = balance - 1;
            return false;
        }

        _unmatchedChannelBalances[identity] = balance - 1;
        return true;
    }

    internal void Clear()
    {
        _unmatchedChannelBalances.Clear();
        _identityOrder.Clear();
    }

    private static int QuantizePosition(float value)
    {
        if (!float.IsFinite(value))
        {
            return 0;
        }

        var quantized = Math.Round(value * PositionQuantizationScale, MidpointRounding.AwayFromZero);
        return (int)Math.Clamp(quantized, int.MinValue, int.MaxValue);
    }

    private readonly record struct Identity(ulong SourceFrame, int QuantizedX, int QuantizedY);
}
