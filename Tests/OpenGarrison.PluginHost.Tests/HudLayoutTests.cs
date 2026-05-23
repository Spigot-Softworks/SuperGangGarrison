using Microsoft.Xna.Framework;
using OpenGarrison.Client;
using Xunit;

namespace OpenGarrison.PluginHost.Tests;

public sealed class HudLayoutTests
{
    [Fact]
    public void WeaponStackDefaultMatchesLegacySourceCoordinates()
    {
        var profile = new HudLayoutProfile();
        Assert.True(profile.TryResolve(HudElementId.LocalWeaponStack, 1280, 720, out var resolved));

        var legacyY = (600f / 1.26f) + 86f;
        var expected = HudLayoutResolver.ResolveLegacySourcePoint(728f, legacyY, 1280, 720);
        Assert.Equal(expected.X, resolved.Origin.X, precision: 3);
        Assert.Equal(expected.Y, resolved.Origin.Y, precision: 3);
    }

    [Fact]
    public void AbilityStackDefaultMatchesLegacySourceCoordinates()
    {
        var profile = new HudLayoutProfile();
        Assert.True(profile.TryResolve(HudElementId.LocalAbilityStack, 1280, 720, out var resolved));

        var expected = HudLayoutResolver.ResolveLegacySourcePoint(730f, 515f, 1280, 720);
        Assert.Equal(expected.X, resolved.Origin.X, precision: 3);
        Assert.Equal(expected.Y, resolved.Origin.Y, precision: 3);
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
}
