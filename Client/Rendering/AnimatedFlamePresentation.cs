using System;
using OpenGarrison.Core;

namespace OpenGarrison.Client;

internal static class AnimatedFlamePresentation
{
    internal static string SpriteName(PlayerTeam team, bool critical = false)
        => "AnimatedFlame" + (critical ? "Critical" : "") + (team == PlayerTeam.Blue ? "BlueS" : "RedS");

    internal static int LifetimeFrame(float progress, int frames)
        => frames <= 1 ? 0 : Math.Clamp((int)MathF.Floor(Math.Clamp(progress, 0f, 1f) * frames), 0, frames - 1);

    internal static int LoopFrame(double seconds, int frames, int phase = 0)
        => frames <= 1 ? 0 : (int)(((long)Math.Floor(Math.Max(0, seconds) * 20) + (uint)phase) % frames);

    internal static int ProjectileFrame(FlameProjectileEntity flame, int frames, int airLifetimeTicks, double seconds)
        // A travelling flame grows and dissipates once. Attached/grounded fire
        // persists, so it cycles at a fixed rate independent of simulation Hz.
        => flame.IsAttached || flame.IsGrounded
            ? LoopFrame(seconds, frames, flame.Id)
            : LifetimeFrame(1f - flame.TicksRemaining / (float)Math.Max(1, airLifetimeTicks), frames);
}
