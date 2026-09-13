#nullable enable

using Microsoft.Xna.Framework;

namespace OpenGarrison.Client;

internal readonly record struct BrandIntroFrame(
    float ElapsedSeconds,
    float ExplosionProgress,
    float ExplosionOpacity,
    float ExplosionScale,
    float LogoOpacity,
    float LogoBurstScale,
    float FlameBlend,
    float ShowcaseReveal,
    float PromptOpacity,
    float LogoFlash,
    float CornerTransition,
    float MenuReveal,
    bool ShouldPlayBurstSound,
    bool IsAwaitingInput,
    bool IsExiting,
    bool IsComplete);

internal static class BrandIntroTimeline
{
    public const float BurstSoundSeconds = 0.25f;
    public const float TitleReadySeconds = 3.25f;
    public const float ExitDurationSeconds = 1.65f;

    private const float ExplosionStartSeconds = 0.25f;
    private const float ExplosionEndSeconds = 1.20f;
    private const float LogoRevealStartSeconds = 0.30f;
    private const float LogoRevealEndSeconds = 0.62f;
    private const float LogoBurstEndSeconds = 0.90f;
    private const float FlameFadeStartSeconds = 1.40f;
    private const float FlameFadeEndSeconds = 2.25f;
    private const float ShowcaseRevealStartSeconds = 2.10f;
    private const float ShowcaseRevealEndSeconds = 3.05f;
    private const float LogoFlashPeakSeconds = 0.09f;
    private const float LogoFlashEndSeconds = 0.22f;
    private const float CornerMoveStartSeconds = 0.22f;
    private const float CornerMoveEndSeconds = 1.45f;
    private const float MenuRevealStartSeconds = 0.22f;
    private const float MenuRevealEndSeconds = 1.52f;

    public static BrandIntroFrame Evaluate(float elapsedSeconds, float exitElapsedSeconds, bool burstSoundPlayed)
    {
        elapsedSeconds = Math.Max(0f, elapsedSeconds);
        var isExiting = exitElapsedSeconds >= 0f;
        exitElapsedSeconds = Math.Max(0f, exitElapsedSeconds);
        var explosionProgress = Normalize(elapsedSeconds, ExplosionStartSeconds, ExplosionEndSeconds);
        var explosionOpacity = explosionProgress is <= 0f or >= 1f
            ? 0f
            : MathF.Pow(MathF.Sin(explosionProgress * MathF.PI), 0.42f);
        var logoReveal = SmoothStep(Normalize(elapsedSeconds, LogoRevealStartSeconds, LogoRevealEndSeconds));
        var logoBurstProgress = Normalize(elapsedSeconds, LogoRevealStartSeconds, LogoBurstEndSeconds);
        var logoBurstScale = MathHelper.Lerp(0.18f, 1f, EaseOutBack(logoBurstProgress));
        var flameBlend = SmoothStep(Normalize(elapsedSeconds, FlameFadeStartSeconds, FlameFadeEndSeconds));
        var showcaseReveal = SmoothStep(Normalize(elapsedSeconds, ShowcaseRevealStartSeconds, ShowcaseRevealEndSeconds));
        var promptPulse = 0.30f + (0.70f * ((MathF.Sin(elapsedSeconds * MathF.PI * 2f * 1.35f) + 1f) * 0.5f));
        var promptOpacity = !isExiting && elapsedSeconds >= TitleReadySeconds ? promptPulse : 0f;
        var logoFlash = isExiting
            ? exitElapsedSeconds <= LogoFlashPeakSeconds
                ? Normalize(exitElapsedSeconds, 0f, LogoFlashPeakSeconds)
                : 1f - Normalize(exitElapsedSeconds, LogoFlashPeakSeconds, LogoFlashEndSeconds)
            : 0f;
        var cornerTransition = isExiting
            ? SmoothStep(Normalize(exitElapsedSeconds, CornerMoveStartSeconds, CornerMoveEndSeconds))
            : 0f;
        var menuReveal = isExiting
            ? SmoothStep(Normalize(exitElapsedSeconds, MenuRevealStartSeconds, MenuRevealEndSeconds))
            : 0f;

        return new BrandIntroFrame(
            elapsedSeconds,
            explosionProgress,
            explosionOpacity,
            MathHelper.Lerp(0.72f, 1.08f, SmoothStep(explosionProgress)),
            logoReveal,
            logoBurstScale,
            flameBlend,
            showcaseReveal,
            promptOpacity,
            logoFlash,
            cornerTransition,
            menuReveal,
            !burstSoundPlayed && elapsedSeconds >= BurstSoundSeconds,
            !isExiting && elapsedSeconds >= TitleReadySeconds,
            isExiting,
            isExiting && exitElapsedSeconds >= ExitDurationSeconds);
    }

    private static float Normalize(float value, float start, float end)
    {
        if (end <= start)
        {
            return value >= end ? 1f : 0f;
        }

        return Math.Clamp((value - start) / (end - start), 0f, 1f);
    }

    private static float SmoothStep(float value)
    {
        value = Math.Clamp(value, 0f, 1f);
        return value * value * (3f - (2f * value));
    }

    private static float EaseOutBack(float value)
    {
        value = Math.Clamp(value, 0f, 1f);
        const float overshoot = 1.70158f;
        var shifted = value - 1f;
        return 1f + ((overshoot + 1f) * shifted * shifted * shifted) + (overshoot * shifted * shifted);
    }
}
