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
        _weatherField.Configure(weather.Kind, weather.Intensity);

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

    /// <summary>A faint tint over the map, plus rare distant lightning in heavy rain.</summary>
    private void DrawMapWeatherAtmosphere(
        SimpleLevel level,
        MapWeather weather,
        Vector2 cameraPosition,
        Point worldViewport,
        double time)
    {
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
