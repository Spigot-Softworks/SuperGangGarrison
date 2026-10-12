#nullable enable

using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace OpenGarrison.Client;

/// <summary>
/// GG2 never acknowledges inputs; it echoes every player's current key state each tick.
/// Client prediction needs to know which of its inputs the state it receives already
/// includes, so this estimates it: the round trip is measured from key-state changes
/// (sent at a known time, seen again in the server's echo), and an input counts as
/// processed once a round trip has passed since it was sent.
/// </summary>
internal sealed class Gg2InputAckEstimator
{
    private const double InitialRoundTripSeconds = 0.12;
    private const double MinRoundTripSeconds = 0.01;
    private const double MaxRoundTripSeconds = 1.0;
    private const double Smoothing = 0.2;
    private const int MaxTrackedInputs = 256;

    private readonly object _sync = new();
    private readonly Queue<(uint Sequence, long Timestamp)> _sent = new();
    private readonly Queue<(byte KeyState, long Timestamp)> _keyChanges = new();
    private byte _lastSentKeyState;
    private bool _hasSent;
    private uint _acknowledged;
    private double _roundTripSeconds = InitialRoundTripSeconds;

    /// <summary>The current round-trip estimate.</summary>
    public double RoundTripSeconds
    {
        get
        {
            lock (_sync)
            {
                return _roundTripSeconds;
            }
        }
    }

    public void RecordSent(uint sequence, byte keyState)
    {
        var now = Stopwatch.GetTimestamp();
        lock (_sync)
        {
            if (sequence != 0)
            {
                _sent.Enqueue((sequence, now));
                while (_sent.Count > MaxTrackedInputs)
                {
                    _sent.Dequeue();
                }
            }

            if (!_hasSent || keyState != _lastSentKeyState)
            {
                _keyChanges.Enqueue((keyState, now));
                while (_keyChanges.Count > 32)
                {
                    _keyChanges.Dequeue();
                }
            }

            _hasSent = true;
            _lastSentKeyState = keyState;
        }
    }

    /// <summary>The server's echo of the local player's key state from an INPUTSTATE.</summary>
    public void ObserveEcho(byte keyState)
    {
        var now = Stopwatch.GetTimestamp();
        lock (_sync)
        {
            // The echo shows the newest change the server has seen. Changes sent before
            // it are behind us; the matching one gives a round-trip sample.
            while (_keyChanges.Count > 0)
            {
                var (sentKeyState, sentAt) = _keyChanges.Peek();
                if (sentKeyState != keyState)
                {
                    if (_keyChanges.Count > 1 && ContainsLater(keyState))
                    {
                        _keyChanges.Dequeue();
                        continue;
                    }

                    break;
                }

                _keyChanges.Dequeue();
                var sample = Stopwatch.GetElapsedTime(sentAt, now).TotalSeconds;
                if (sample is >= MinRoundTripSeconds and <= MaxRoundTripSeconds)
                {
                    _roundTripSeconds += (sample - _roundTripSeconds) * Smoothing;
                }

                break;
            }
        }
    }

    /// <summary>The newest input sequence sent at least one round trip ago (0 for none).</summary>
    public uint GetAcknowledgedSequence()
    {
        var now = Stopwatch.GetTimestamp();
        lock (_sync)
        {
            while (_sent.Count > 0
                && Stopwatch.GetElapsedTime(_sent.Peek().Timestamp, now).TotalSeconds >= _roundTripSeconds)
            {
                _acknowledged = _sent.Dequeue().Sequence;
            }

            return _acknowledged;
        }
    }

    private bool ContainsLater(byte keyState)
    {
        var first = true;
        foreach (var (state, _) in _keyChanges)
        {
            if (first)
            {
                first = false;
                continue;
            }

            if (state == keyState)
            {
                return true;
            }
        }

        return false;
    }
}
