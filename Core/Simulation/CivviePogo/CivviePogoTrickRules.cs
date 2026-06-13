using System;

namespace OpenGarrison.Core;

public static class CivviePogoTrickRules
{
    private const int TrickFrameSalt = 0x504F474F;

    public static int GetDeterministicFrameIndex(int sessionSeed, int playerId, ulong startFrame, int frameCount)
    {
        if (frameCount <= 1)
        {
            return 0;
        }

        return PositiveModulo(
            HashCode.Combine(sessionSeed, playerId, (int)(startFrame & 0x7FFFFFFF), TrickFrameSalt),
            frameCount);
    }

    public static ulong ResolveTrickStartFrame(ulong currentFrame, int durationTicks, int ticksRemaining)
    {
        var elapsed = Math.Max(0, durationTicks - ticksRemaining);
        return currentFrame - (ulong)elapsed;
    }

    public static int ResolveTrickFrameIndex(
        int sessionSeed,
        int playerId,
        ulong currentFrame,
        int durationTicks,
        int ticksRemaining,
        int frameCount)
    {
        if (ticksRemaining <= 0 || frameCount <= 0)
        {
            return 0;
        }

        var startFrame = ResolveTrickStartFrame(currentFrame, durationTicks, ticksRemaining);
        return GetDeterministicFrameIndex(sessionSeed, playerId, startFrame, frameCount);
    }

    private static int PositiveModulo(int value, int modulus)
    {
        var result = value % modulus;
        return result < 0 ? result + modulus : result;
    }
}
