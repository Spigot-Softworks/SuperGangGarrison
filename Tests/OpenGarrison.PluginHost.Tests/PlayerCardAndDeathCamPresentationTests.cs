using Microsoft.Xna.Framework;
using OpenGarrison.Client;
using OpenGarrison.ClientShared;
using OpenGarrison.Core;
using Xunit;

namespace OpenGarrison.PluginHost.Tests;

public sealed class PlayerCardAndDeathCamPresentationTests
{
    [Fact]
    public void PlayerCardDefaultsToShiningHeroAndEmptyBio()
    {
        var profile = PlayerCardProfile.CreateDefault();

        Assert.Equal(2, profile.Version);
        Assert.Equal(PlayerCardProfile.DefaultMedalId, profile.Medal);
        Assert.Empty(profile.Bio);
    }

    [Fact]
    public void PlayerCardBioIsSingleLineQuoteSafeAndLimitedToTwentySixCharacters()
    {
        var profile = PlayerCardProfile.Sanitize(new PlayerCardProfile
        {
            Bio = "  \"0123456789\nABCDEFGHIJKLMNOPQRSTUVWXYZ\"  ",
        });

        Assert.Equal(PlayerCardProfile.MaximumBioLength, profile.Bio.Length);
        Assert.DoesNotContain('"', profile.Bio);
        Assert.DoesNotContain('\n', profile.Bio);
        Assert.Equal("0123456789ABCDEFGHIJKLMNOP", profile.Bio);
    }

    [Fact]
    public void PlayerCardBioAndMedalRoundTripThroughWireJson()
    {
        var json = PlayerCardProfile.Serialize(new PlayerCardProfile
        {
            Bio = "I SUCK, BUT YOU'RE WORSE",
            Medal = "mercenary",
        });

        var profile = PlayerCardProfile.Deserialize(json);

        Assert.Equal("I SUCK, BUT YOU'RE WORSE", profile.Bio);
        Assert.Equal("mercenary", profile.Medal);
    }

    [Theory]
    [InlineData("ScatterKL", DeathCamPhraseCategory.Bullet)]
    [InlineData("RocketKL", DeathCamPhraseCategory.Explosive)]
    [InlineData("BackstabKL", DeathCamPhraseCategory.Backstab)]
    [InlineData("FlameKL", DeathCamPhraseCategory.Fire)]
    [InlineData("BowKL", DeathCamPhraseCategory.Sniper)]
    [InlineData("TurretKL", DeathCamPhraseCategory.Sentry)]
    [InlineData("BladeKL", DeathCamPhraseCategory.Generic)]
    public void DeathCamWeaponSpritesMapToExpectedPhraseCategory(string spriteName, DeathCamPhraseCategory expected)
    {
        Assert.Equal(expected, DeathCamPhraseCatalog.ResolveCategory(spriteName));
    }

    [Fact]
    public void DeathCamPhraseCatalogContainsTheApprovedCopyOnly()
    {
        Assert.Equal(
            ["Sent to hell by", "Life ended by", "Heart stopped by", "Destroyed by", "Demolished by", "Retired by", "Fragged by", "Eliminated by"],
            DeathCamPhraseCatalog.GetPhrases(DeathCamPhraseCategory.Generic));
        Assert.Equal(
            ["Gunned down by", "Blasted by", "Shot dead by"],
            DeathCamPhraseCatalog.GetPhrases(DeathCamPhraseCategory.Bullet));
        Assert.Equal(
            ["Exploded by", "Gibbed by", "Blown to bits by", "Atomized by"],
            DeathCamPhraseCatalog.GetPhrases(DeathCamPhraseCategory.Explosive));
        Assert.Equal(
            ["Assassinated by", "Stabbed by", "Punctured by", "Shanked by"],
            DeathCamPhraseCatalog.GetPhrases(DeathCamPhraseCategory.Backstab));
        Assert.Equal(
            ["Burned to death by", "Made crispy by", "Overcooked by", "Turned to ash by"],
            DeathCamPhraseCatalog.GetPhrases(DeathCamPhraseCategory.Fire));
        Assert.Equal(
            ["Sniped by", "Noscoped by", "Hunted by", "Assassinated by"],
            DeathCamPhraseCatalog.GetPhrases(DeathCamPhraseCategory.Sniper));
        Assert.Equal(["Autogunned by"], DeathCamPhraseCatalog.GetPhrases(DeathCamPhraseCategory.Sentry));

        var allPhrases = Enum.GetValues<DeathCamPhraseCategory>()
            .SelectMany(DeathCamPhraseCatalog.GetPhrases);
        Assert.DoesNotContain("Charred to a crisp by", allPhrases);
    }

    [Fact]
    public void BrowserBootstrapIncludesEveryPlayerCardAsset()
    {
        var paths = BrowserBootstrapAssetCatalog.DefaultBinaryAssetPaths;

        Assert.Contains("Content/Sprites/Menu/PlayerCards/redplayercard.png", paths);
        Assert.Contains("Content/Sprites/Menu/PlayerCards/blueplayercard.png", paths);
        Assert.Contains("Content/Sprites/Menu/PlayerCards/Medals/Shining Hero.png", paths);
        Assert.Equal(6, paths.Count(path => path.StartsWith("Content/Sprites/Menu/PlayerCards/Medals/", StringComparison.Ordinal)));
    }

    [Fact]
    public void PlayerCardAndReplayTextUseNaturalPixelScale()
    {
        Assert.Equal(1f, PixelPerfectTextLayout.NaturalScale);
        Assert.Equal(PixelPerfectTextLayout.NaturalScale, Game1.PlayerCardTextScale);
        Assert.Equal(PixelPerfectTextLayout.NaturalScale, Game1.ReplayPlaybackTextScale);
    }

    [Fact]
    public void NaturalPixelTextTrimmingEllipsizesWithoutExceedingItsContainer()
    {
        static float Measure(string value) => value.Length * 8f;

        Assert.Equal("SHORT", PixelPerfectTextLayout.TrimToWidth("SHORT", 40f, Measure));

        var trimmed = PixelPerfectTextLayout.TrimToWidth("A VERY LONG LABEL", 56f, Measure);

        Assert.EndsWith("...", trimmed, StringComparison.Ordinal);
        Assert.True(Measure(trimmed) <= 56f);
        Assert.Equal(string.Empty, PixelPerfectTextLayout.TrimToWidth("LONG", 7f, Measure));
    }

    [Fact]
    public void NaturalPixelTextPlacementIsRoundedAndContained()
    {
        var bounds = new Rectangle(11, 13, 101, 31);
        const float naturalWidth = 73.2f;
        const float naturalHeight = 11f;

        var position = PixelPerfectTextLayout.CenterNaturalText(bounds, naturalWidth, naturalHeight);
        var pixelWidth = (int)MathF.Ceiling(naturalWidth);
        var pixelHeight = (int)MathF.Ceiling(naturalHeight);

        Assert.InRange(position.X, bounds.Left, bounds.Right - pixelWidth);
        Assert.InRange(position.Y, bounds.Top, bounds.Bottom - pixelHeight);
        Assert.InRange(
            MathF.Abs((position.X + (pixelWidth * 0.5f)) - (bounds.X + (bounds.Width * 0.5f))),
            0f,
            0.5f);
        Assert.InRange(
            MathF.Abs((position.Y + (pixelHeight * 0.5f)) - (bounds.Y + (bounds.Height * 0.5f))),
            0f,
            0.5f);
    }

    [Fact]
    public void ReplayStatusExpandsForNaturalTextWithoutMovingSeekButtons()
    {
        var compactStatus = Game1.GetReplayPlaybackControlLayout(960, 540);
        var naturalStatus = Game1.GetReplayPlaybackControlLayout(960, 540, 232f, 11f);

        Assert.Equal(compactStatus.Backward, naturalStatus.Backward);
        Assert.Equal(compactStatus.Forward, naturalStatus.Forward);
        Assert.True(naturalStatus.Status.Width >= 232);
        Assert.True(naturalStatus.Status.Height >= 11);
        Assert.True(naturalStatus.Panel.Contains(naturalStatus.Status));

        var textPosition = PixelPerfectTextLayout.CenterNaturalText(naturalStatus.Status, 232f, 11f);
        Assert.InRange(textPosition.X, naturalStatus.Status.Left, naturalStatus.Status.Right - 232);
        Assert.InRange(textPosition.Y, naturalStatus.Status.Top, naturalStatus.Status.Bottom - 11);
    }

    [Theory]
    [InlineData(800, 432, 150)]
    [InlineData(960, 336, 180)]
    [InlineData(780, 288, 146)]
    public void DeathCamPhraseGapAndCardAreCenteredAsOneUnit(int viewportWidth, int phraseWidth, int cardWidth)
    {
        var layout = Game1.CenterDeathCamHeader(
            viewportWidth,
            phraseWidth,
            cardWidth,
            Game1.DeathCamHeaderGap);

        Assert.Equal(Game1.DeathCamHeaderGap, layout.CardX - layout.PhraseRight);
        Assert.Equal(layout.PhraseX, layout.GroupLeft);
        Assert.Equal(layout.CardX + cardWidth, layout.GroupRight);
        Assert.InRange(MathF.Abs(layout.GroupCenter - (viewportWidth * 0.5f)), 0f, 0.5f);
    }

    [Theory]
    [InlineData(144f, 501f, 3f)]
    [InlineData(180f, 400f, 2f)]
    [InlineData(220f, 150f, 1f)]
    public void DeathCamPhraseScaleAlwaysSelectsAnIntegralPixelScale(
        float naturalWidth,
        float maximumWidth,
        float expectedScale)
    {
        Assert.Equal(
            expectedScale,
            PixelPerfectTextLayout.SelectLargestIntegerScale(naturalWidth, maximumWidth, preferredScale: 3));
    }
}
