#nullable enable

using Microsoft.Xna.Framework;
using System;

namespace OpenGarrison.Client;

/// <summary>
/// Render-only interpolation for the locally controlled player.
/// </summary>
/// <remarks>
/// The simulation and the prediction input lane advance at the fixed tick rate
/// (30 Hz by default) while frames are drawn at the display rate. Drawing the
/// local player at the latest tick sample makes the player, and the camera that
/// follows it, step once per tick: at 60 fps every other frame repeats, which
/// reads as 30 fps and as a back-and-forth shimmer against smoothly interpolated
/// projectiles and remote players. Presentation instead blends from the sample
/// at the start of the latest tick to the sample at its end by the fraction of
/// the next tick interval that has already elapsed.
/// </remarks>
internal static class LocalPlayerRenderInterpolation
{
    /// <summary>Displacement within one tick treated as a teleport and drawn without blending.</summary>
    public const float TeleportSnapDistance = 128f;

    public static float ComputeAlpha(double accumulatorSeconds, double fixedDeltaSeconds)
    {
        if (!(fixedDeltaSeconds > 0d) || !double.IsFinite(accumulatorSeconds))
        {
            return 1f;
        }

        return (float)Math.Clamp(accumulatorSeconds / fixedDeltaSeconds, 0d, 1d);
    }

    public static Vector2 Interpolate(Vector2 tickStart, Vector2 tickEnd, float alpha)
    {
        if (!IsFinite(tickStart)
            || !IsFinite(tickEnd)
            || Vector2.DistanceSquared(tickStart, tickEnd) >= TeleportSnapDistance * TeleportSnapDistance)
        {
            return tickEnd;
        }

        return Vector2.Lerp(tickStart, tickEnd, Math.Clamp(alpha, 0f, 1f));
    }

    /// <summary>
    /// Returns the change to the render-correction offset that keeps the drawn
    /// position continuous across a prediction rebuild.
    /// </summary>
    /// <param name="advancedOneTick">
    /// True when the rebuild appended a new input tick. The old tick-end sample
    /// and the new tick-start sample then describe the same simulated tick, so
    /// only their difference is a misprediction. Otherwise the rebuild replayed
    /// the same tick window against new authority, and both endpoints are compared
    /// at the current blend fraction.
    /// </param>
    public static Vector2 ComputeContinuityOffsetDelta(
        Vector2 oldTickStart,
        Vector2 oldTickEnd,
        Vector2 newTickStart,
        Vector2 newTickEnd,
        float alpha,
        bool advancedOneTick)
    {
        if (advancedOneTick)
        {
            return oldTickEnd - newTickStart;
        }

        return Interpolate(oldTickStart, oldTickEnd, alpha) - Interpolate(newTickStart, newTickEnd, alpha);
    }

    private static bool IsFinite(Vector2 value)
        => float.IsFinite(value.X) && float.IsFinite(value.Y);
}
