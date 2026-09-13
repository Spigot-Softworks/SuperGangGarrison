using Microsoft.Xna.Framework;
using OpenGarrison.Client;
using Xunit;

namespace OpenGarrison.PluginHost.Tests;

public sealed class HudLayoutTests
{
    [Fact]
    public void SavedSlidingBuildMenuPositionDoesNotMoveTheReplacementWheelOffScreen()
    {
        var document = new HudLayoutDocument();
        document.Elements[HudElementId.LegacyClassEngineerBuildMenu] = new()
        { Anchor = HudAnchor.CenterLeft, OffsetX = 11, OffsetY = -106, Scale = 1, Visible = true };
        document.Elements[HudElementId.LocalHealth] = new() { OffsetX = 37, OffsetY = -41, Scale = 1.5f };
        var profile = document.ToProfile();
        Assert.True(profile.TryResolve(HudElementId.ClassEngineerBuildMenu, 960, 540, out var wheel));
        Assert.Equal(new Vector2(480, 270), wheel.Origin);
        Assert.Equal(37f, profile.Overrides[HudElementId.LocalHealth].OffsetX);
        Assert.Equal(1.5f, profile.Overrides[HudElementId.LocalHealth].Scale);
        Assert.DoesNotContain(HudElementId.LegacyClassEngineerBuildMenu, HudLayoutDocument.FromProfile(profile).Elements.Keys);
    }

    [Fact]
    public void NewWheelLayoutSurvivesReloadAlongsideAnObsoleteBuildMenuEntry()
    {
        var document = new HudLayoutDocument();
        document.Elements[HudElementId.LegacyClassEngineerBuildMenu] = new() { Anchor = HudAnchor.CenterLeft, OffsetX = 11 };
        document.Elements[HudElementId.ClassEngineerBuildMenu] = new() { Anchor = HudAnchor.Center, OffsetX = 32, OffsetY = -20, Scale = 1.25f };
        var restored = HudLayoutDocument.FromProfile(document.ToProfile()).ToProfile();
        Assert.True(restored.TryResolve(HudElementId.ClassEngineerBuildMenu, 960, 540, out var wheel));
        Assert.Equal(new Vector2(512, 250), wheel.Origin);
        Assert.Equal(1.25f, wheel.Layout.Scale);
    }

    [Theory]
    [InlineData(320, 240, 3f)]
    [InlineData(640, 480, 3f)]
    [InlineData(960, 540, 0.5f)]
    [InlineData(1920, 1080, 3f)]
    public void CustomizedWheelAndLabelStayInsideTheResizedViewport(int width, int height, float scale)
    {
        var profile = new HudLayoutProfile();
        profile.Overrides[HudElementId.ClassEngineerBuildMenu] = new()
        { Anchor = HudAnchor.TopLeft, OffsetX = -400, OffsetY = 2000, Scale = scale };
        Assert.True(profile.TryResolve(HudElementId.ClassEngineerBuildMenu, width, height, out var wheel));
        Assert.InRange(wheel.Bounds.Left, 0, width);
        Assert.InRange(wheel.Bounds.Right, 0, width);
        Assert.InRange(wheel.Bounds.Top, 0, height);
        Assert.InRange(wheel.Origin.Y + 110 * wheel.Layout.Scale + 16, 0, height);
        Assert.True(profile.TryResolveEvenIfHidden(HudElementId.ClassEngineerBuildMenu, width, height, out var editor));
        Assert.Equal(wheel, editor);
    }

    [Fact]
    public void WeaponWidgetsUseIndependentRuntimeLayouts()
    {
        var profile = new HudLayoutProfile();
        Assert.False(profile.TryResolve(HudElementId.LocalWeaponStack, 1280, 720, out _));

        profile.SetRuntimeDefault(CreateDefaultWeaponWidgetLayout(HudElementId.LocalWeaponPrimary, 0f));
        profile.SetRuntimeDefault(CreateDefaultWeaponWidgetLayout(HudElementId.LocalWeaponSecondary, -45f));
        Assert.True(profile.TryResolve(HudElementId.LocalWeaponPrimary, 1280, 720, out var primary));
        Assert.True(profile.TryResolve(HudElementId.LocalWeaponSecondary, 1280, 720, out var secondary));

        var legacyY = (600f / 1.26f) + 86f;
        var expected = HudLayoutResolver.ResolveLegacySourcePoint(728f, legacyY, 1280, 720);
        Assert.Equal(expected.X, primary.Origin.X, precision: 3);
        Assert.Equal(expected.Y, primary.Origin.Y, precision: 3);
        Assert.True(secondary.Origin.Y < primary.Origin.Y);
    }

    [Fact]
    public void AbilityStackDefaultAnchorsAboveWeaponStack()
    {
        var profile = new HudLayoutProfile();
        Assert.True(profile.TryResolve(HudElementId.LocalAbilityStack, 1280, 720, out var resolved));

        var expected = HudLayoutResolver.ResolveLegacySourcePoint(730f, 515f, 1280, 720);
        Assert.Equal(expected.X, resolved.Origin.X, precision: 3);
        Assert.Equal(expected.Y, resolved.Origin.Y, precision: 3);
        var weaponOrigin = HudLayoutResolver.ResolveLegacySourcePoint(728f, (600f / 1.26f) + 86f, 1280, 720);
        Assert.Equal(2f, resolved.Origin.X - weaponOrigin.X, precision: 3);
        Assert.True(resolved.Origin.Y < weaponOrigin.Y);
    }

    [Fact]
    public void EngineerSentryDefaultStacksAboveAbilityArea()
    {
        var profile = new HudLayoutProfile();
        Assert.True(profile.TryResolve(HudElementId.ClassEngineerSentry, 1280, 720, out var sentry));
        Assert.True(profile.TryResolve(HudElementId.LocalHealth, 1280, 720, out var health));

        var expected = HudLayoutResolver.ResolveLegacySourcePoint(696f, 420f, 1280, 720);
        Assert.Equal(expected.X, sentry.Origin.X, precision: 3);
        Assert.Equal(expected.Y, sentry.Origin.Y, precision: 3);
        Assert.False(sentry.Bounds.Intersects(health.Bounds));
    }

    [Theory]
    [InlineData(800, 600)]
    [InlineData(1280, 720)]
    [InlineData(640, 480)]
    public void EngineerBuildWheelStaysCenteredWhenViewportChanges(int width, int height)
    {
        var profile = new HudLayoutProfile();

        Assert.True(profile.TryResolve(HudElementId.ClassEngineerBuildMenu, width, height, out var resolved));
        Assert.Equal(width / 2f, resolved.Origin.X, precision: 3);
        Assert.Equal(height / 2f, resolved.Origin.Y, precision: 3);
        Assert.Equal(new Rectangle(width / 2 - 100, height / 2 - 100, 201, 201), resolved.Bounds);
    }

    [Theory]
    [InlineData(800, 600)]
    [InlineData(1280, 720)]
    [InlineData(1920, 1080)]
    public void EngineerDefaultHudElementsDoNotOverlap(int viewportWidth, int viewportHeight)
    {
        var profile = new HudLayoutProfile();
        profile.SetRuntimeDefault(CreateDefaultWeaponWidgetLayout(HudElementId.LocalWeaponPrimary, 0f));

        AssertDefaultElementsDoNotOverlap(
            profile,
            viewportWidth,
            viewportHeight,
            HudElementId.LocalWeaponPrimary,
            HudElementId.ClassEngineerMetal,
            HudElementId.ClassEngineerSentry,
            HudElementId.LastToDieRage);
        AssertDefaultElementsDoNotOverlap(
            profile,
            viewportWidth,
            viewportHeight,
            HudElementId.LocalAbilityStack,
            HudElementId.ClassEngineerMetal,
            HudElementId.ClassEngineerSentry,
            HudElementId.LastToDieRage);
    }

    [Theory]
    [InlineData(800, 600)]
    [InlineData(1280, 720)]
    [InlineData(1920, 1080)]
    public void MedicLastToDieDefaultHudElementsDoNotOverlap(int viewportWidth, int viewportHeight)
    {
        var profile = new HudLayoutProfile();
        profile.SetRuntimeDefault(CreateDefaultWeaponWidgetLayout(HudElementId.LocalWeaponPrimary, 0f));

        AssertDefaultElementsDoNotOverlap(
            profile,
            viewportWidth,
            viewportHeight,
            HudElementId.LocalWeaponPrimary,
            HudElementId.ClassMedicUber,
            HudElementId.LastToDieRage);
        AssertDefaultElementsDoNotOverlap(
            profile,
            viewportWidth,
            viewportHeight,
            HudElementId.LocalAbilityStack,
            HudElementId.ClassMedicUber,
            HudElementId.LastToDieRage);
    }

    [Theory]
    [InlineData(800, 600)]
    [InlineData(1280, 720)]
    [InlineData(1920, 1080)]
    public void LastToDieSpyCloakMeterDoesNotOverlapRage(int viewportWidth, int viewportHeight)
    {
        var profile = new HudLayoutProfile();

        AssertDefaultElementsDoNotOverlap(
            profile,
            viewportWidth,
            viewportHeight,
            HudElementId.LastToDieSpyCloak,
            HudElementId.LastToDieRage);
    }

    [Fact]
    public void ExplicitHudOverridesCanStillOverlapOtherElements()
    {
        var profile = new HudLayoutProfile();
        profile.SetRuntimeDefault(CreateDefaultWeaponWidgetLayout(HudElementId.LocalWeaponPrimary, 0f));
        Assert.True(profile.TryResolve(HudElementId.LocalWeaponPrimary, 1280, 720, out var weapon));
        Assert.True(profile.TryResolve(HudElementId.ClassEngineerMetal, 1280, 720, out var defaultMetal));
        Assert.False(defaultMetal.Bounds.Intersects(weapon.Bounds));

        profile.SetElementOrigin(HudElementId.ClassEngineerMetal, weapon.Origin, 1280, 720);

        Assert.True(profile.TryResolve(HudElementId.ClassEngineerMetal, 1280, 720, out var metal));
        Assert.True(metal.Bounds.Intersects(weapon.Bounds));
    }

    [Fact]
    public void KillFeedAlignmentFollowsResolvedScreenRegion()
    {
        var bounds = new Rectangle(100, 40, 340, 104);

        Assert.Equal(KillFeedHudAlignment.Left, KillFeedHudAlignmentResolver.Resolve(300f, 1000));
        Assert.Equal(KillFeedHudAlignment.Center, KillFeedHudAlignmentResolver.Resolve(500f, 1000));
        Assert.Equal(KillFeedHudAlignment.Right, KillFeedHudAlignmentResolver.Resolve(700f, 1000));

        Assert.Equal(100f, KillFeedHudAlignmentResolver.ResolveAnchorX(bounds, KillFeedHudAlignment.Left), precision: 3);
        Assert.Equal(270f, KillFeedHudAlignmentResolver.ResolveAnchorX(bounds, KillFeedHudAlignment.Center), precision: 3);
        Assert.Equal(440f, KillFeedHudAlignmentResolver.ResolveAnchorX(bounds, KillFeedHudAlignment.Right), precision: 3);

        Assert.Equal(100f, KillFeedHudAlignmentResolver.ResolveEntryLeft(100f, 120f, KillFeedHudAlignment.Left), precision: 3);
        Assert.Equal(210f, KillFeedHudAlignmentResolver.ResolveEntryLeft(270f, 120f, KillFeedHudAlignment.Center), precision: 3);
        Assert.Equal(320f, KillFeedHudAlignmentResolver.ResolveEntryLeft(440f, 120f, KillFeedHudAlignment.Right), precision: 3);
    }

    [Fact]
    public void LayoutStoreRoundTripsElementOverrides()
    {
        var path = Path.Combine(Path.GetTempPath(), $"opengarrison-hud-layout-{Guid.NewGuid():N}.json");
        try
        {
            var profile = new HudLayoutProfile();
            profile.SetElementOrigin(HudElementId.LocalHealth, new Vector2(32f, 420f), 1280, 720);
            Assert.True(profile.SetElementScale(HudElementId.LocalHealth, 1.4f));
            profile.GridVisible = false;
            profile.SnapEnabled = false;
            HudLayoutStore.Save(profile, path);

            var loaded = HudLayoutStore.Load(path);
            Assert.False(loaded.GridVisible);
            Assert.False(loaded.SnapEnabled);
            Assert.True(loaded.TryResolve(HudElementId.LocalHealth, 1280, 720, out var resolved));
            Assert.Equal(32f, resolved.Origin.X, precision: 3);
            Assert.Equal(420f, resolved.Origin.Y, precision: 3);
            Assert.Equal(1.4f, resolved.Layout.Scale, precision: 3);
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }

    [Fact]
    public void LayoutStorePreservesUnknownElementOverrides()
    {
        var path = Path.Combine(Path.GetTempPath(), $"opengarrison-hud-layout-{Guid.NewGuid():N}.json");
        try
        {
            File.WriteAllText(
                path,
                """
                {
                  "version": 1,
                  "elements": {
                    "plugin.example.panel": {
                      "anchor": "TopRight",
                      "offsetX": -48,
                      "offsetY": 24,
                      "visible": true
                    }
                  }
                }
                """);

            var loaded = HudLayoutStore.Load(path);
            Assert.True(loaded.UnknownOverrides.ContainsKey("plugin.example.panel"));

            HudLayoutStore.Save(loaded, path);
            var saved = File.ReadAllText(path);
            Assert.Contains("plugin.example.panel", saved, StringComparison.Ordinal);
            Assert.Contains("TopRight", saved, StringComparison.Ordinal);
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }

    [Fact]
    public void LayoutStoreMigratesLegacyDefaultLayoutToUserDataLayout()
    {
        var root = Path.Combine(Path.GetTempPath(), $"opengarrison-hud-layout-migration-{Guid.NewGuid():N}");
        var userPath = Path.Combine(root, "user", "config", HudLayoutStore.DefaultFileName);
        var legacyPath = Path.Combine(root, "legacy", "config", HudLayoutStore.DefaultFileName);

        try
        {
            var legacyProfile = new HudLayoutProfile();
            legacyProfile.SetElementOrigin(HudElementId.MatchKillFeed, new Vector2(100f, 80f), 1280, 720);
            Assert.True(legacyProfile.SetElementScale(HudElementId.MatchKillFeed, 1.25f));
            HudLayoutStore.Save(legacyProfile, legacyPath);

            var loaded = HudLayoutStore.LoadDefault(userPath, legacyPath);

            Assert.True(File.Exists(userPath));
            Assert.True(loaded.TryResolve(HudElementId.MatchKillFeed, 1280, 720, out var resolved));
            Assert.Equal(100f, resolved.Origin.X, precision: 3);
            Assert.Equal(80f, resolved.Origin.Y, precision: 3);
            Assert.Equal(1.25f, resolved.Layout.Scale, precision: 3);
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [Fact]
    public void LayoutStorePrefersUserDataLayoutOverLegacyLayout()
    {
        var root = Path.Combine(Path.GetTempPath(), $"opengarrison-hud-layout-preference-{Guid.NewGuid():N}");
        var userPath = Path.Combine(root, "user", "config", HudLayoutStore.DefaultFileName);
        var legacyPath = Path.Combine(root, "legacy", "config", HudLayoutStore.DefaultFileName);

        try
        {
            var userProfile = new HudLayoutProfile();
            userProfile.SetElementOrigin(HudElementId.LocalHealth, new Vector2(48f, 500f), 1280, 720);
            HudLayoutStore.Save(userProfile, userPath);

            var legacyProfile = new HudLayoutProfile();
            legacyProfile.SetElementOrigin(HudElementId.LocalHealth, new Vector2(320f, 120f), 1280, 720);
            HudLayoutStore.Save(legacyProfile, legacyPath);

            var loaded = HudLayoutStore.LoadDefault(userPath, legacyPath);

            Assert.True(loaded.TryResolve(HudElementId.LocalHealth, 1280, 720, out var resolved));
            Assert.Equal(48f, resolved.Origin.X, precision: 3);
            Assert.Equal(500f, resolved.Origin.Y, precision: 3);
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [Fact]
    public void ElementScaleUpdatesResolvedBounds()
    {
        var profile = new HudLayoutProfile();
        Assert.True(profile.SetElementScale(HudElementId.LocalAbilityStack, 1.5f));
        Assert.True(profile.TryResolve(HudElementId.LocalAbilityStack, 1280, 720, out var resolved));

        var defaults = HudLayoutDefaults.Create()[HudElementId.LocalAbilityStack];
        Assert.Equal(MathF.Round(defaults.Size.X * 1.5f), resolved.Bounds.Width);
        Assert.Equal(MathF.Round(defaults.Size.Y * 1.5f), resolved.Bounds.Height);
    }

    [Fact]
    public void ElementVisibilityCanHideAndRestoreWithoutMovingIt()
    {
        var profile = new HudLayoutProfile();
        Assert.True(profile.SetElementVisibility(HudElementId.MatchKillFeed, false));
        Assert.False(profile.TryResolve(HudElementId.MatchKillFeed, 1280, 720, out _));
        Assert.True(profile.TryResolveEvenIfHidden(HudElementId.MatchKillFeed, 1280, 720, out var hidden));
        Assert.False(hidden.Layout.Visible);

        Assert.True(profile.SetElementVisibility(HudElementId.MatchKillFeed, true));
        Assert.True(profile.TryResolve(HudElementId.MatchKillFeed, 1280, 720, out var visible));
        Assert.Equal(hidden.Origin, visible.Origin);
    }

    [Fact]
    public void RuntimeDefaultPromotesAndResolvesSavedDynamicWidgetOverride()
    {
        var profile = new HudLayoutProfile();
        var id = HudElementId.LocalAbilitySlot(1);
        profile.UnknownOverrides[id] = new HudElementLayoutOverride
        {
            Anchor = HudAnchor.BottomRight,
            OffsetX = -128f,
            OffsetY = -96f,
            Scale = 1.3f,
            Visible = true,
        };
        profile.SetRuntimeDefault(new HudElementLayout(
            id,
            HudAnchor.BottomRight,
            new Vector2(-70f, -85f),
            new Vector2(76f, 56f),
            new Vector2(-38f, -28f),
            Layer: 21));

        Assert.False(profile.UnknownOverrides.ContainsKey(id));
        Assert.True(profile.Overrides.ContainsKey(id));
        Assert.True(profile.TryResolve(id, 1280, 720, out var resolved));
        Assert.Equal(1152f, resolved.Origin.X, precision: 3);
        Assert.Equal(624f, resolved.Origin.Y, precision: 3);
        Assert.Equal(1.3f, resolved.Layout.Scale, precision: 3);
    }

    [Fact]
    public void SnapperSnapsElementEdgesToGrid()
    {
        var layout = new HudElementLayout(
            "test.element",
            HudAnchor.TopLeft,
            Vector2.Zero,
            new Vector2(32f, 32f),
            Vector2.Zero);
        var snapped = HudEditorSnapper.SnapOrigin(
            new Vector2(31f, 47f),
            layout,
            Array.Empty<HudResolvedElement>(),
            1280,
            720,
            gridSize: 16);

        Assert.Equal(32f, snapped.X, precision: 3);
        Assert.Equal(48f, snapped.Y, precision: 3);
    }

    [Fact]
    public void HudElementRegistryCollectsProviderInstancesAndDrawsRenderers()
    {
        var registry = new HudElementRegistry();
        registry.RegisterDefinition(new HudElementDefinition("test.a", "renderer.a", Layer: 10));
        registry.RegisterDefinition(new HudElementDefinition("test.b", "renderer.b", Layer: 20));
        registry.RegisterProvider(new DelegateHudElementProvider((context, elements) =>
        {
            context.AddIfRegistered(elements, "test.b");
            context.AddIfRegistered(elements, "test.a");
        }));

        var drawn = new List<string>();
        registry.RegisterRenderer("renderer.a", new DelegateHudElementRenderer((_, _) => drawn.Add("test.a")));
        registry.RegisterRenderer("renderer.b", new DelegateHudElementRenderer((_, _) => drawn.Add("test.b")));

        var elements = new List<HudElementInstance>();
        registry.Collect(null!, elements);
        foreach (var element in elements.OrderBy(static element => element.Layer))
        {
            registry.Draw(null!, element);
        }

        Assert.Equal(["test.a", "test.b"], drawn);
    }

    private static void AssertDefaultElementsDoNotOverlap(
        HudLayoutProfile profile,
        int viewportWidth,
        int viewportHeight,
        params string[] elementIds)
    {
        var elements = new HudResolvedElement[elementIds.Length];
        for (var index = 0; index < elementIds.Length; index += 1)
        {
            Assert.True(profile.TryResolve(elementIds[index], viewportWidth, viewportHeight, out elements[index]));
        }

        for (var left = 0; left < elements.Length; left += 1)
        {
            for (var right = left + 1; right < elements.Length; right += 1)
            {
                Assert.False(
                    elements[left].Bounds.Intersects(elements[right].Bounds),
                    $"{elements[left].Layout.Id} overlaps {elements[right].Layout.Id}: {elements[left].Bounds} vs {elements[right].Bounds}");
            }
        }
    }

    private static HudElementLayout CreateDefaultWeaponWidgetLayout(string id, float sourceYOffset)
    {
        var legacyY = (600f / 1.26f) + 86f + sourceYOffset;
        return new HudElementLayout(
            id,
            HudAnchor.BottomRight,
            new Vector2(728f - 800f, legacyY - 600f),
            new Vector2(120f, 41f),
            new Vector2(-60f, -20f),
            Layer: 20);
    }
}
