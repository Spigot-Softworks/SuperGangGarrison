namespace OpenGarrison.Core.LastToDie;

public readonly record struct LastToDieRandomState(ulong State, ulong Increment);

/// <summary>
/// Version-stable PCG32 stream used for authoritative Last to Die decisions.
/// </summary>
public sealed class LastToDieRandom
{
    private ulong _state;
    private ulong _increment;

    public LastToDieRandom(ulong seed, ulong sequence)
    {
        _state = 0;
        _increment = (sequence << 1) | 1UL;
        _ = NextUInt32();
        _state = unchecked(_state + seed);
        _ = NextUInt32();
    }

    private LastToDieRandom(LastToDieRandomState state)
    {
        if ((state.Increment & 1UL) == 0)
        {
            throw new ArgumentException("PCG increment must be odd.", nameof(state));
        }

        _state = state.State;
        _increment = state.Increment;
    }

    public static LastToDieRandom Restore(LastToDieRandomState state) => new(state);

    public LastToDieRandomState CaptureState() => new(_state, _increment);

    public uint NextUInt32()
    {
        var oldState = _state;
        _state = unchecked((oldState * 6364136223846793005UL) + _increment);
        var xorShifted = (uint)(((oldState >> 18) ^ oldState) >> 27);
        var rotation = (int)(oldState >> 59);
        return (xorShifted >> rotation) | (xorShifted << ((-rotation) & 31));
    }

    public int NextInt32(int exclusiveMaximum)
    {
        if (exclusiveMaximum <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(exclusiveMaximum));
        }

        var bound = (uint)exclusiveMaximum;
        var threshold = unchecked(0U - bound) % bound;
        while (true)
        {
            var value = NextUInt32();
            if (value >= threshold)
            {
                return (int)(value % bound);
            }
        }
    }

    public void Shuffle<T>(IList<T> values)
    {
        ArgumentNullException.ThrowIfNull(values);
        for (var index = values.Count - 1; index > 0; index -= 1)
        {
            var swapIndex = NextInt32(index + 1);
            (values[index], values[swapIndex]) = (values[swapIndex], values[index]);
        }
    }

    internal static ulong DeriveSeed(ulong seed, ulong stream)
    {
        var value = unchecked(seed + 0x9E3779B97F4A7C15UL + (stream * 0xBF58476D1CE4E5B9UL));
        value = (value ^ (value >> 30)) * 0xBF58476D1CE4E5B9UL;
        value = (value ^ (value >> 27)) * 0x94D049BB133111EBUL;
        return value ^ (value >> 31);
    }
}
