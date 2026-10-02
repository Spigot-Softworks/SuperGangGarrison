#nullable enable

using System;

namespace OpenGarrison.Client;

/// <summary>Frame-time conditioning for the fixed-step visual client tick.</summary>
internal static class ClientTickCadence
{
    /// <summary>Largest measured deviation still treated as an exact whole number of steps.</summary>
    public const double SnapToleranceSeconds = 0.0015d;

    /// <summary>
    /// Snaps a measured frame time that is within <see cref="SnapToleranceSeconds"/>
    /// of one or two whole steps to exactly that many steps.
    /// </summary>
    /// <remarks>
    /// When frames arrive at the step rate (60 Hz frames, 60 Hz visual tick)
    /// the accumulator's phase barely moves. If it settles near a step
    /// boundary, sub-millisecond timestamp jitter alternates 0 and 2 steps per
    /// frame, and effects stutter until a hitch happens to move the phase.
    /// Snapping removes that dependence on phase. It only applies to
    /// presentation ticks: input and simulation clocks keep measured time.
    /// </remarks>
    public static double SnapFrameSeconds(double measuredSeconds, double stepSeconds)
    {
        if (!(stepSeconds > 0d) || !double.IsFinite(measuredSeconds))
        {
            return measuredSeconds;
        }

        for (var steps = 1; steps <= 2; steps += 1)
        {
            var whole = steps * stepSeconds;
            if (Math.Abs(measuredSeconds - whole) <= SnapToleranceSeconds)
            {
                return whole;
            }
        }

        return measuredSeconds;
    }
}
