#nullable enable

using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using OpenGarrison.Core;

namespace OpenGarrison.Client;

public partial class Game1
{
    private static readonly Color PlayerLightColor = new(255, 242, 222);
    private static readonly Color FlameLightColor = new(255, 140, 50);
    private static readonly Color BurningLightColor = new(255, 120, 40);
    private static readonly Color RocketLightColor = new(255, 176, 96);
    private static readonly Color FlareLightColor = new(255, 96, 64);

    /// <summary>Player light radius at 100% range, in world pixels.</summary>
    private const float PlayerLightRadius = 64f;

    private readonly LightmapRenderer _gameplayLightmap = new();
    private RasterizerState _gameplayWorldRasterizerState = RasterizerState.CullNone;

    /// <summary>
    /// Builds this frame's light map for the main gameplay camera. Runs before the logical
    /// frame target is bound (like the death-cam capture), because switching render
    /// targets mid-frame would discard the logical canvas.
    /// </summary>
    public void PrepareGameplayLightmap(Vector2 cameraPosition, int worldViewWidth, int worldViewHeight)
    {
        var level = _world.Level;
        var lighting = level.Lighting;
        if (!lighting.IsActive)
        {
            _gameplayLightmap.Begin(MapLighting.None, 0f, 0f, 1f, 1f, 1f, 0d);
            return;
        }

        var time = _weatherClock.Elapsed.TotalSeconds;
        var flash = GetMapWeatherLightningFlash(level.Weather, time);
        _gameplayLightmap.Begin(
            lighting,
            cameraPosition.X,
            cameraPosition.Y,
            worldViewWidth,
            worldViewHeight,
            level.Bounds.Height,
            time,
            flash);
        AddMapLights(_gameplayLightmap, level.MapLights, time);
        AddGameplayLights(lighting, time);
        _gameplayLightmap.Render(GraphicsDevice, _spriteBatch);
    }

    /// <summary>
    /// Applies the light map over everything drawn in the world pass so far (map, players,
    /// effects, weather). Only the main world pass is lit; captures drawn without the world
    /// batch (death cam, Last-to-Die focus) are skipped because the map was built for the
    /// main camera.
    /// </summary>
    private void DrawGameplayLightingOverlay(Vector2 cameraPosition)
    {
        if (!_gameplayLightmap.HasFrame || !_gameplayWorldSpriteBatchActive)
        {
            return;
        }

        var transform = GetActiveGameplayWorldSpriteBatchTransform();
        var sampler = _gameplayLightmap.Lighting.Banded ? SamplerState.PointClamp : SamplerState.LinearClamp;
        var offset = -cameraPosition;

        _spriteBatch.End();
        _spriteBatch.Begin(SpriteSortMode.Deferred, LightmapRenderer.Multiply2x, sampler, DepthStencilState.None, _gameplayWorldRasterizerState, null, transform);
        _gameplayLightmap.DrawMultiply(_spriteBatch, 1f, offset);
        _spriteBatch.End();

        if (_gameplayLightmap.Lighting.Glow > 0)
        {
            _spriteBatch.Begin(SpriteSortMode.Deferred, LightmapRenderer.AddOne, SamplerState.LinearClamp, DepthStencilState.None, _gameplayWorldRasterizerState, null, transform);
            _gameplayLightmap.DrawGlow(_spriteBatch, 1f, offset);
            _spriteBatch.End();
        }

        _spriteBatch.Begin(samplerState: SamplerState.PointClamp, rasterizerState: _gameplayWorldRasterizerState, transformMatrix: transform);
    }

    private static void AddMapLights(LightmapRenderer lightmap, System.Collections.Generic.IReadOnlyList<MapLightMarker> lights, double time)
    {
        for (var index = 0; index < lights.Count; index += 1)
        {
            var light = lights[index];
            var flicker = LightmapRenderer.GetFlicker(light.Flicker, time, index * 7.31f);
            lightmap.AddLight(
                light.X,
                light.Y,
                light.Radius,
                new Color(light.Color.R, light.Color.G, light.Color.B),
                light.Intensity / 100f * flicker);
        }
    }

    /// <summary>
    /// Gameplay light sources: a soft light on every player (so darkness never hides
    /// anyone unfairly), burning players, flames, rockets, flares and explosions.
    /// Strength and range are separate settings for players and for effects.
    /// </summary>
    private void AddGameplayLights(MapLighting lighting, double time)
    {
        var effects = lighting.EffectLights / 100f;
        var players = lighting.PlayerLight / 100f;
        var playerRadius = PlayerLightRadius * (lighting.PlayerRange / 100f);
        var range = lighting.EffectRange / 100f;
        foreach (var player in EnumerateRenderablePlayers())
        {
            if (!player.IsAlive)
            {
                continue;
            }

            var position = GetRenderPosition(player, allowInterpolation: true);
            if (players > 0f)
            {
                _gameplayLightmap.AddLight(position.X, position.Y, playerRadius, PlayerLightColor, 0.6f * players, glows: false);
            }

            if (effects > 0f && player.IsBurning)
            {
                var flicker = LightmapRenderer.GetFlicker(MapLightFlicker.Flicker, time, player.Id * 1.7f);
                _gameplayLightmap.AddLight(position.X, position.Y, 74f * range, BurningLightColor, effects * 0.85f * flicker);
            }
        }

        if (effects <= 0f || range <= 0f)
        {
            return;
        }

        foreach (var explosion in _explosions)
        {
            var age = Math.Clamp(explosion.ElapsedSourceTicks / (float)ExplosionVisual.LifetimeSourceTicks, 0f, 1f);
            var strength = 1f - age;
            _gameplayLightmap.AddLight(explosion.X, explosion.Y, (90f + (110f * MathF.Sqrt(strength))) * range, explosion.FallbackOuterColor, effects * 1.3f * strength);
        }

        foreach (var rocket in _world.Rockets)
        {
            _gameplayLightmap.AddLight(rocket.X, rocket.Y, 48f * range, RocketLightColor, effects * 0.7f);
        }

        foreach (var flare in _world.Flares)
        {
            _gameplayLightmap.AddLight(flare.X, flare.Y, 84f * range, FlareLightColor, effects * 0.9f);
        }

        // Flamethrower streams can hold hundreds of particles; sample them so the light
        // budget stays bounded while the stream still glows along its length.
        var flames = _world.Flames;
        var stride = Math.Max(1, flames.Count / 96);
        for (var index = 0; index < flames.Count; index += stride)
        {
            var flame = flames[index];
            _gameplayLightmap.AddLight(flame.X, flame.Y, (36f + (stride * 2f)) * range, FlameLightColor, effects * 0.45f);
        }
    }
}
