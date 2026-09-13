using Microsoft.Xna.Framework;
using OpenGarrison.Client;
using Xunit;

namespace OpenGarrison.PluginHost.Tests;

public sealed class BrandIntroTimelineTests
{
    [Fact]
    public void TimelineTriggersBurstOnceAndWaitsAtTheTitleScreen()
    {
        var beforeBurst = BrandIntroTimeline.Evaluate(
            BrandIntroTimeline.BurstSoundSeconds - 0.01f,
            exitElapsedSeconds: -1f,
            burstSoundPlayed: false);
        var atBurst = BrandIntroTimeline.Evaluate(
            BrandIntroTimeline.BurstSoundSeconds,
            exitElapsedSeconds: -1f,
            burstSoundPlayed: false);
        var afterBurst = BrandIntroTimeline.Evaluate(1f, exitElapsedSeconds: -1f, burstSoundPlayed: true);
        var title = BrandIntroTimeline.Evaluate(
            BrandIntroTimeline.TitleReadySeconds + 20f,
            exitElapsedSeconds: -1f,
            burstSoundPlayed: true);

        Assert.False(beforeBurst.ShouldPlayBurstSound);
        Assert.True(atBurst.ShouldPlayBurstSound);
        Assert.False(afterBurst.ShouldPlayBurstSound);
        Assert.True(title.IsAwaitingInput);
        Assert.False(title.IsExiting);
        Assert.False(title.IsComplete);
        Assert.Equal(1f, title.FlameBlend);
        Assert.Equal(1f, title.ShowcaseReveal);
        Assert.Equal(0f, title.CornerTransition);
        Assert.Equal(0f, title.MenuReveal);
        Assert.True(title.PromptOpacity > 0f);
    }

    [Fact]
    public void ExitFlashesTheLogoBeforeMovingItIntoTheMenu()
    {
        var flashPeak = BrandIntroTimeline.Evaluate(
            BrandIntroTimeline.TitleReadySeconds,
            exitElapsedSeconds: 0.09f,
            burstSoundPlayed: true);
        var beforeMovement = BrandIntroTimeline.Evaluate(
            BrandIntroTimeline.TitleReadySeconds,
            exitElapsedSeconds: 0.20f,
            burstSoundPlayed: true);
        var moving = BrandIntroTimeline.Evaluate(
            BrandIntroTimeline.TitleReadySeconds,
            exitElapsedSeconds: 0.60f,
            burstSoundPlayed: true);
        var complete = BrandIntroTimeline.Evaluate(
            BrandIntroTimeline.TitleReadySeconds,
            BrandIntroTimeline.ExitDurationSeconds,
            burstSoundPlayed: true);

        Assert.InRange(flashPeak.LogoFlash, 0.99f, 1f);
        Assert.True(beforeMovement.LogoFlash > 0f);
        Assert.Equal(0f, beforeMovement.CornerTransition);
        Assert.Equal(0f, beforeMovement.MenuReveal);
        Assert.True(moving.CornerTransition > 0f);
        Assert.True(moving.MenuReveal > 0f);
        Assert.Equal(0f, moving.PromptOpacity);
        Assert.True(complete.IsComplete);
        Assert.Equal(1f, complete.CornerTransition);
        Assert.Equal(1f, complete.MenuReveal);
    }

    [Fact]
    public void TitlePromptKeepsFlashingWhileWaitingForInput()
    {
        var first = BrandIntroTimeline.Evaluate(
            BrandIntroTimeline.TitleReadySeconds,
            exitElapsedSeconds: -1f,
            burstSoundPlayed: true);
        var later = BrandIntroTimeline.Evaluate(
            BrandIntroTimeline.TitleReadySeconds + 0.25f,
            exitElapsedSeconds: -1f,
            burstSoundPlayed: true);

        Assert.True(MathF.Abs(first.PromptOpacity - later.PromptOpacity) > 0.05f);
        Assert.InRange(first.PromptOpacity, 0.30f, 1f);
        Assert.InRange(later.PromptOpacity, 0.30f, 1f);
    }

    [Fact]
    public void TimelineKeepsStaticAndFlamingLogoAlignedDuringCrossfade()
    {
        var frame = BrandIntroTimeline.Evaluate(1.825f, exitElapsedSeconds: -1f, burstSoundPlayed: true);

        Assert.InRange(frame.FlameBlend, 0.45f, 0.55f);
        Assert.Equal(0f, frame.CornerTransition);
        Assert.Equal(0f, frame.MenuReveal);
        Assert.Equal(1f, frame.LogoOpacity);
    }

    [Fact]
    public void ShowcaseUsesOnlyTheSuperGangGarrisonMapSection()
    {
        var mapNames = Game1.GetSuperGangGarrisonShowcaseMapNames();
        var expected = new HashSet<string>(
            ["Conflict", "Harvest", "Docking", "Kulay", "cp_coldfront_js"],
            StringComparer.OrdinalIgnoreCase);

        Assert.Equal(5, mapNames.Count);
        Assert.True(expected.SetEquals(mapNames));
    }

    [Fact]
    public void IntroEndpointUsesThePermanentMenuBoundsExactly()
    {
        var centered = Game1.GetCenteredBrandLogoBounds(1440, 810);
        var permanent = Game1.GetPermanentBrandLogoBounds(1440, 810);
        var endpoint = Game1.InterpolateBrandLogoBounds(centered, permanent, 1f);

        Assert.Equal(permanent, endpoint);
        Assert.Equal(new Rectangle(913, 20, 507, 160), permanent);
    }

    [Theory]
    [InlineData(320, 240)]
    [InlineData(1024, 768)]
    [InlineData(1280, 720)]
    [InlineData(1920, 1080)]
    [InlineData(2560, 1080)]
    public void LogoLayoutsRemainInsideTheViewport(int width, int height)
    {
        var centered = Game1.GetCenteredBrandLogoBounds(width, height);
        var permanent = Game1.GetPermanentBrandLogoBounds(width, height);
        var viewport = new Rectangle(0, 0, width, height);

        Assert.True(viewport.Contains(centered));
        Assert.True(viewport.Contains(permanent));
        Assert.True(centered.Width > permanent.Width);
    }
}
