#nullable enable

using System;
using System.Diagnostics;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using OpenGarrison.Core;

namespace OpenGarrison.Client;

public partial class Game1
{
    private const int WeatherParticleModeDisabled = 1;
    private const int WeatherParticleModeFaster = 2;

    private readonly RetroWeatherField _weatherField = new();
    private readonly Stopwatch _weatherClock = Stopwatch.StartNew();
    private SimpleLevel? _weatherSkyMaskLevel;
    private WeatherSkyMask? _weatherSkyMask;
    private readonly System.Collections.Generic.List<FireflyInstance> _fireflies = new();

    /// <summary>
    /// Draws the map's ambient weather over the world (under the HUD). Runs inside the
    /// world sprite batch, so coordinates are camera-relative world pixels.
    /// </summary>
    /// <param name="worldViewWidth">Visible width in world pixels. The world draw
    /// already receives the zoomed view size, so it must not be divided by zoom again.</param>
    /// <param name="worldViewHeight">Visible height in world pixels.</param>
    private void DrawMapWeather(Vector2 cameraPosition, int worldViewWidth, int worldViewHeight)
    {
        var level = _world.Level;
        var weather = level.Weather;
        if (!weather.IsActive)
        {
            return;
        }

        // Weather follows the particle quality setting: off when particles are off,
        // half the drops in the faster mode or the reduced browser path.
        var particleMode = _gameplayManager.RuntimeSettings.ParticleMode;
        if (particleMode == WeatherParticleModeDisabled)
        {
            return;
        }

        var density = particleMode == WeatherParticleModeFaster || UseReducedBrowserEffects ? 0.5f : 1f;
        var worldViewport = new Point(worldViewWidth, worldViewHeight);
        var time = _weatherClock.Elapsed.TotalSeconds;
        if (weather.Kind == MapWeatherKind.Fireflies)
        {
            DrawFireflies(level, weather, cameraPosition, worldViewport, time, density);
            return;
        }

        _weatherField.Configure(weather.Kind, weather.Intensity);
        var rainColor = weather.ResolvedRainColor;
        var customRain = weather.Kind == MapWeatherKind.Rain && rainColor != MapWeatherMetadata.DefaultRainColor;
        _weatherField.SetRainColor(customRain ? new Color(rainColor.R, rainColor.G, rainColor.B) : null);

        DrawMapWeatherAtmosphere(level, weather, cameraPosition, worldViewport, time);

        var skyMask = level.IsTopDown ? null : GetWeatherSkyMask(level);
        var sink = new SpriteBatchWeatherSink(_spriteBatch, _pixel);
        _weatherField.Emit(
            ref sink,
            skyMask,
            cameraPosition.X,
            cameraPosition.Y,
            worldViewport.X,
            worldViewport.Y,
            level.Bounds.Width,
            level.Bounds.Height,
            time,
            weather.Wind,
            density,
            level.IsTopDown ? level.IndoorRegions : null);
    }

    /// <summary>
    /// Fireflies: a bright 1px core with a small soft pixel halo whose strength follows the
    /// glow setting. With map lighting on they also light the area around them (see
    /// <see cref="AddFireflyLights"/>), so they read as glowing in the dark.
    /// </summary>
    private void DrawFireflies(SimpleLevel level, MapWeather weather, Vector2 cameraPosition, Point worldViewport, double time, float densityScale)
    {
        CollectVisibleFireflies(level, weather, cameraPosition.X, cameraPosition.Y, worldViewport.X, worldViewport.Y, time, densityScale);
        if (_fireflies.Count == 0)
        {
            return;
        }

        var color = weather.ResolvedFireflyColor;
        var tint = new Color(color.R, color.G, color.B);
        var glow = weather.FireflyGlow / 100f;
        foreach (var firefly in _fireflies)
        {
            var x = (int)MathF.Floor(firefly.X - cameraPosition.X);
            var y = (int)MathF.Floor(firefly.Y - cameraPosition.Y);
            if (glow > 0f)
            {
                // Outer diamond, then a plus, then the core: a soft pixel-art halo.
                var outer = tint * (0.1f * glow * firefly.Brightness);
                _spriteBatch.Draw(_pixel, new Rectangle(x - 2, y - 1, 5, 3), outer);
                _spriteBatch.Draw(_pixel, new Rectangle(x - 1, y - 2, 3, 5), outer);
                var inner = tint * (0.32f * glow * firefly.Brightness);
                _spriteBatch.Draw(_pixel, new Rectangle(x - 1, y, 3, 1), inner);
                _spriteBatch.Draw(_pixel, new Rectangle(x, y - 1, 1, 3), inner);
            }

            // The core whitens a little at full brightness, like a real firefly's lantern.
            var core = Color.Lerp(tint, Color.White, 0.35f * firefly.Brightness) * firefly.Brightness;
            _spriteBatch.Draw(_pixel, new Rectangle(x, y, 1, 1), core);
        }
    }

    /// <summary>Fills <see cref="_fireflies"/> for a view (shared by drawing and the light map).</summary>
    private void CollectVisibleFireflies(SimpleLevel level, MapWeather weather, float viewLeft, float viewTop, float viewWidth, float viewHeight, double time, float densityScale)
    {
        FireflyField.Collect(
            _fireflies,
            level.IsTopDown ? null : GetWeatherSkyMask(level),
            viewLeft,
            viewTop,
            viewWidth,
            viewHeight,
            level.Bounds.Width,
            level.Bounds.Height,
            time,
            weather.Wind,
            weather.FireflyDensity,
            densityScale,
            level.IsTopDown ? level.IndoorRegions : null,
            weather.FireflyStyle);
    }

    /// <summary>With map lighting on, each firefly casts a small light sized by the glow setting.</summary>
    private void AddFireflyLights(SimpleLevel level, Vector2 cameraPosition, int worldViewWidth, int worldViewHeight, double time)
    {
        var weather = level.Weather;
        var particleMode = _gameplayManager.RuntimeSettings.ParticleMode;
        if (weather.Kind != MapWeatherKind.Fireflies || weather.FireflyGlow <= 0 || particleMode == WeatherParticleModeDisabled)
        {
            return;
        }

        var density = particleMode == WeatherParticleModeFaster || UseReducedBrowserEffects ? 0.5f : 1f;
        CollectVisibleFireflies(level, weather, cameraPosition.X, cameraPosition.Y, worldViewWidth, worldViewHeight, time, density);
        var color = weather.ResolvedFireflyColor;
        var lightColor = new Color(color.R, color.G, color.B);
        var glow = weather.FireflyGlow / 100f;
        var radius = 8f + (26f * glow);
        foreach (var firefly in _fireflies)
        {
            _gameplayLightmap.AddLight(firefly.X, firefly.Y, radius, lightColor, firefly.Brightness * (0.35f + (0.85f * glow)));
        }
    }

    /// <summary>A faint tint over the map, plus rare distant lightning in heavy rain.</summary>
    private void DrawMapWeatherAtmosphere(
        SimpleLevel level,
        MapWeather weather,
        Vector2 cameraPosition,
        Point worldViewport,
        double time)
    {
        var rainColor = weather.ResolvedRainColor;
        var customRain = weather.Kind == MapWeatherKind.Rain && rainColor != MapWeatherMetadata.DefaultRainColor;
        var left = MathF.Max(cameraPosition.X, 0f);
        var top = MathF.Max(cameraPosition.Y, 0f);
        var right = MathF.Min(cameraPosition.X + worldViewport.X, level.Bounds.Width);
        var bottom = MathF.Min(cameraPosition.Y + worldViewport.Y, level.Bounds.Height);
        if (right <= left || bottom <= top)
        {
            return;
        }

        var rectangle = new Rectangle(
            (int)MathF.Floor(left - cameraPosition.X),
            (int)MathF.Floor(top - cameraPosition.Y),
            (int)MathF.Ceiling(right - left) + 1,
            (int)MathF.Ceiling(bottom - top) + 1);

        var storm = weather.Intensity == MapWeatherIntensity.Storm;
        var tintAlpha = (weather.Kind, weather.Intensity) switch
        {
            (MapWeatherKind.Rain, MapWeatherIntensity.Light) => 0.035f,
            (MapWeatherKind.Rain, MapWeatherIntensity.Heavy) => 0.10f,
            (MapWeatherKind.Rain, MapWeatherIntensity.Storm) => 0.14f,
            (MapWeatherKind.Rain, _) => 0.06f,
            // Snowstorm whiteout breathes with the gusts.
            (MapWeatherKind.Snow, MapWeatherIntensity.Storm) => 0.07f + (0.08f * RetroWeatherField.GetStormGust(time)),
            (MapWeatherKind.Leaves, MapWeatherIntensity.Light) => 0.015f,
            (MapWeatherKind.Leaves, MapWeatherIntensity.Heavy) => 0.035f,
            (MapWeatherKind.Leaves, MapWeatherIntensity.Storm) => 0.045f,
            (MapWeatherKind.Leaves, _) => 0.025f,
            (_, MapWeatherIntensity.Light) => 0.02f,
            (_, MapWeatherIntensity.Heavy) => 0.06f,
            _ => 0.035f,
        };
        var tint = weather.Kind switch
        {
            // Coloured rain tints the air with a dark shade of its colour.
            MapWeatherKind.Rain when customRain => new Color(rainColor.R / 9, rainColor.G / 9, rainColor.B / 9),
            MapWeatherKind.Rain => new Color(18, 26, 40),
            MapWeatherKind.Leaves => new Color(196, 132, 58),
            _ => new Color(214, 224, 240),
        };
        _spriteBatch.Draw(_pixel, rectangle, tint * tintAlpha);

        var flash = GetMapWeatherLightningFlash(weather, time);
        if (flash > 0f)
        {
            _spriteBatch.Draw(_pixel, rectangle, new Color(214, 224, 255) * ((storm ? 0.14f : 0.11f) * flash));
        }
    }

    /// <summary>Current lightning brightness (0..1); heavy rain and thunderstorms only.</summary>
    private static float GetMapWeatherLightningFlash(MapWeather weather, double time)
    {
        if (weather.Kind != MapWeatherKind.Rain
            || weather.Intensity is not (MapWeatherIntensity.Heavy or MapWeatherIntensity.Storm))
        {
            return 0f;
        }

        return RetroWeatherField.GetLightningFlash(time, weather.Intensity == MapWeatherIntensity.Storm);
    }

    private WeatherSkyMask GetWeatherSkyMask(SimpleLevel level)
    {
        if (_weatherSkyMask is null || !ReferenceEquals(level, _weatherSkyMaskLevel))
        {
            _weatherSkyMask = WeatherSkyMask.Build(level.Solids, level.Bounds, level.IndoorRegions);
            _weatherSkyMaskLevel = level;
        }

        return _weatherSkyMask;
    }

    private readonly struct SpriteBatchWeatherSink : IWeatherPixelSink
    {
        private readonly SpriteBatch _batch;
        private readonly Texture2D _pixelTexture;

        public SpriteBatchWeatherSink(SpriteBatch batch, Texture2D pixelTexture)
        {
            _batch = batch;
            _pixelTexture = pixelTexture;
        }

        public void Fill(int x, int y, int width, int height, Color color)
        {
            _batch.Draw(_pixelTexture, new Rectangle(x, y, width, height), color);
        }
    }
}
