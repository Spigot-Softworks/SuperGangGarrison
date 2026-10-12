#nullable enable

using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using OpenGarrison.Core;
using OpenGarrison.GameplayModding;

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
    private readonly EmissiveWorldLayer _gameplayEmissiveLayer = new();
    private SimpleLevel? _lightOcclusionLevel;
    private float[]?[] _mapLightReach = Array.Empty<float[]?>();
    private RasterizerState _gameplayWorldRasterizerState = RasterizerState.CullNone;
    private bool _gameplayLightingCaptureActive;
    private Effect? _lightBandsEffect;
    private bool _lightBandsEffectLoadAttempted;
    private RasterizerState? _gameplayLightingCaptureSavedRasterizer;

    /// <summary>
    /// True while a world pass that should be lit is drawing: the main world batch, or an
    /// off-screen capture (death cam, Last-to-Die focus) opened with
    /// <see cref="BeginGameplayLightingCapture"/>.
    /// </summary>
    private bool IsGameplayLightingPassActive => _gameplayWorldSpriteBatchActive || _gameplayLightingCaptureActive;

    /// <summary>
    /// Builds this frame's light map for the main gameplay camera. Runs before the logical
    /// frame target is bound (like the death-cam capture), because switching render
    /// targets mid-frame would discard the logical canvas.
    /// </summary>
    public void PrepareGameplayLightmap(Vector2 cameraPosition, int worldViewWidth, int worldViewHeight)
    {
        AdvanceCharacterLightingFrame();
        BuildGameplayLightmap(cameraPosition, worldViewWidth, worldViewHeight);
    }

    /// <summary>
    /// Lights an off-screen capture of the world (death cam, Last-to-Die focus) from its own
    /// camera. Call before binding the capture's render target (building the light map
    /// switches targets), and close with <see cref="EndGameplayLightingCapture"/>. The main
    /// camera's light map is rebuilt afterwards in <see cref="PrepareGameplayLightmap"/>.
    /// </summary>
    private void BeginGameplayLightingCapture(Vector2 cameraPosition, int worldViewWidth, int worldViewHeight)
    {
        BuildGameplayLightmap(cameraPosition, worldViewWidth, worldViewHeight);
        _gameplayLightingCaptureActive = true;
        // Captures draw unscaled and unclipped, whatever the last world pass used.
        _gameplayLightingCaptureSavedRasterizer = _gameplayWorldRasterizerState;
        _gameplayWorldRasterizerState = RasterizerState.CullNone;
    }

    private void EndGameplayLightingCapture()
    {
        _gameplayLightingCaptureActive = false;
        _gameplayWorldRasterizerState = _gameplayLightingCaptureSavedRasterizer ?? RasterizerState.CullNone;
        _gameplayLightingCaptureSavedRasterizer = null;
    }

    private void BuildGameplayLightmap(Vector2 cameraPosition, int worldViewWidth, int worldViewHeight)
    {
        _brightmapDraws.Clear();
        var level = _world.Level;
        var lighting = level.Lighting;
        var settings = _gameplayManager.RuntimeSettings;
        // The player can turn map lighting off, or tone it down (Graphics > Lighting & Weather).
        if (!lighting.IsActive || !settings.MapLightingEnabled)
        {
            _gameplayLightmap.Begin(MapLighting.None, 0f, 0f, 1f, 1f, 1f, 0d);
            ResolveDamagingLaserBeamsOnly(level, _weatherClock.Elapsed.TotalSeconds);
            return;
        }

        var time = _weatherClock.Elapsed.TotalSeconds;
        var flash = settings.WeatherEnabled ? GetMapWeatherLightningFlash(level.Weather, time) : 0f;
        _gameplayLightmap.Intensity = OpenGarrisonPreferencesDocument.NormalizeLightingIntensityPercent(settings.LightingIntensityPercent) / 100f;
        _gameplayLightmap.Begin(
            lighting,
            cameraPosition.X,
            cameraPosition.Y,
            worldViewWidth,
            worldViewHeight,
            level.Bounds.Height,
            time,
            flash);
        EnsureMapLightOcclusion(level);
        AddGameplayMapLights(level, time);
        AddGameplayLights(lighting, time);
        AddMuzzleFlashLights(lighting, time);
        AddMedicBeamLights(lighting);
        AddTeamGlowLights(lighting);
        _gameplayManager.GoreEffects.AppendBloodLights(_gameplayLightmap);
        AddFireflyLights(level, cameraPosition, worldViewWidth, worldViewHeight, time);
        AddJungleAmbientOnlyShapes(level);
        _gameplayLightmap.BandEffect = lighting.Banded ? GetLightBandsEffect() : null;
        _gameplayLightmap.SetBandClipArt(
            lighting.Banded && lighting.BandClip && TryGetLevelBackgroundTexture(out var art) ? art : null,
            level.Bounds.Width,
            level.Bounds.Height);
        _gameplayLightmap.Render(GraphicsDevice, _spriteBatch);
    }

    /// <summary>The retro band shader, loaded once; null (old pre-banded lights) if it is missing.</summary>
    private Effect? GetLightBandsEffect()
    {
        if (_lightBandsEffect is null && !_lightBandsEffectLoadAttempted)
        {
            _lightBandsEffectLoadAttempted = true;
            try
            {
                _lightBandsEffect = Content.Load<Effect>("LightBands");
            }
            catch (Exception ex)
            {
                AddConsoleLine($"retro light bands shader unavailable ({ex.GetType().Name}: {ex.Message})");
            }
        }

        return _lightBandsEffect;
    }

    /// <summary>Draws sharp painted mask pixels with the map, using the active camera/capture transform.</summary>
    private void DrawGameplayPaintedMaskEmission(Vector2 cameraPosition)
    {
        if (!IsGameplayLightingPassActive || !_gameplayLightmap.HasPaintedMaskEmission) return;
        var transform = GetActiveGameplayWorldSpriteBatchTransform();
        _spriteBatch.End();
        // Light the background first, then add its emissive artwork. Everything above
        // it goes into a transparent layer and receives the same surface lighting once.
        _spriteBatch.Begin(SpriteSortMode.Deferred, LightmapRenderer.Multiply2x,
            _gameplayLightmap.PrefersPointSampling ? SamplerState.PointClamp : SamplerState.LinearClamp,
            DepthStencilState.None, _gameplayWorldRasterizerState, null, transform);
        _gameplayLightmap.DrawMultiply(_spriteBatch, 1f, -cameraPosition);
        _spriteBatch.End();
        _spriteBatch.Begin(SpriteSortMode.Deferred, LightmapRenderer.AddOne, SamplerState.PointClamp,
            DepthStencilState.None, _gameplayWorldRasterizerState, null, transform);
        _gameplayLightmap.DrawPaintedMaskEmission(_spriteBatch, 1f, -cameraPosition);
        _spriteBatch.End();
        _gameplayEmissiveLayer.BeginForeground(GraphicsDevice);
        _spriteBatch.Begin(samplerState: SamplerState.PointClamp, rasterizerState: _gameplayWorldRasterizerState,
            transformMatrix: transform);
    }

    /// <summary>
    /// Applies the light map to the current world layer (players, remains, blood, effects,
    /// weather, and the map when no separate emissive layer is needed). Captures are lit
    /// too when opened with <see cref="BeginGameplayLightingCapture"/>, which builds a light
    /// map for their camera.
    /// </summary>
    private void DrawGameplayLightingOverlay(Vector2 cameraPosition)
    {
        if (!IsGameplayLightingPassActive)
        {
            return;
        }

        if (!_gameplayLightmap.HasFrame)
        {
            DiscardCharacterBrightmaps();

            // Lighting off: only damaging lasers (hazards stay visible) are left to draw.
            if (_resolvedMapBeams.Count > 0)
            {
                var unlitTransform = GetActiveGameplayWorldSpriteBatchTransform();
                _spriteBatch.End();
                DrawMapBeamsInWorldPass(_resolvedMapBeams, 0f, unlitTransform, _gameplayWorldRasterizerState, -cameraPosition, 1f);
                _spriteBatch.Begin(samplerState: SamplerState.PointClamp, rasterizerState: _gameplayWorldRasterizerState, transformMatrix: unlitTransform);
            }

            return;
        }

        var transform = GetActiveGameplayWorldSpriteBatchTransform();
        var sampler = _gameplayLightmap.Lighting.Banded ? SamplerState.PointClamp : SamplerState.LinearClamp;
        var multiplySampler = _gameplayLightmap.PrefersPointSampling ? SamplerState.PointClamp : sampler;
        var offset = -cameraPosition;

        _spriteBatch.End();
        _spriteBatch.Begin(SpriteSortMode.Deferred, LightmapRenderer.Multiply2x, multiplySampler, DepthStencilState.None, _gameplayWorldRasterizerState, null, transform);
        _gameplayLightmap.DrawMultiply(_spriteBatch, 1f, offset);
        _spriteBatch.End();

        CompositeGameplayLitForeground(GraphicsDevice, transform);

        if (_gameplayLightmap.HasGlow)
        {
            _spriteBatch.Begin(SpriteSortMode.Deferred, LightmapRenderer.AddOne, _gameplayLightmap.GlowSampler, DepthStencilState.None, _gameplayWorldRasterizerState, null, transform);
            _gameplayLightmap.DrawGlow(_spriteBatch, 1f, offset);
            _spriteBatch.End();
        }

        // Searchlight shafts and laser beams in the air, over the lit world.
        DrawMapBeamsInWorldPass(
            _resolvedMapBeams,
            _gameplayLightmap.Lighting.BeamHaze / 100f,
            transform,
            _gameplayWorldRasterizerState,
            offset,
            _gameplayLightmap.Intensity,
            _gameplayLightmap.IsBanded);

        _spriteBatch.Begin(samplerState: SamplerState.PointClamp, rasterizerState: _gameplayWorldRasterizerState, transformMatrix: transform);
    }

    /// <summary>
    /// Casts every placed light's rays against the walkmask once per level, so walls block
    /// light. Placed lights never move, and doing them all at load keeps the hitch out of
    /// gameplay (a few milliseconds even for large maps).
    /// </summary>
    private void EnsureMapLightOcclusion(SimpleLevel level)
    {
        if (ReferenceEquals(level, _lightOcclusionLevel))
        {
            return;
        }

        _lightOcclusionLevel = level;
        _lightOcclusionField = null;
        var lights = level.MapLights;
        _mapLightReach = new float[]?[lights.Count];
        if (lights.Count == 0)
        {
            return;
        }

        var field = LightOcclusionField.Build(level.Solids, level.Bounds);
        _lightOcclusionField = field;
        for (var index = 0; index < lights.Count; index += 1)
        {
            var light = lights[index];
            _mapLightReach[index] = field.ComputeVisibility(light.X, light.Y, light.Radius);
        }
    }

    private readonly LightMaskTextureCache _lightMaskTextures = new();
    private readonly LightMaskTextureCache _jungleLightShapes = new(alphaShape: true);

    /// <summary>
    /// Jungle foliage keeps lights off itself (unless set to overlap lights): its shape
    /// takes only the ambient light, so lamps and flames behind or inside it do not shine
    /// through the leaves.
    /// </summary>
    private void AddJungleAmbientOnlyShapes(SimpleLevel level)
    {
        var spriteResources = GetRuntimeCustomMapVisuals()?.SpriteResources;
        var roomObjects = level.RoomObjects;
        for (var index = 0; index < roomObjects.Count; index += 1)
        {
            var marker = roomObjects[index];
            if (marker.Type != RoomObjectType.ForegroundSprite
                || !marker.ForegroundSprite.Jungle
                || marker.ForegroundSprite.OverlapLights
                || !level.IsRoomObjectActive(index))
            {
                continue;
            }

            Texture2D? shape = _pixel;
            if (!marker.ForegroundSprite.Tile)
            {
                var name = marker.ForegroundSprite.ImageResourceName;
                shape = !string.IsNullOrWhiteSpace(name)
                    && spriteResources is not null
                    && spriteResources.TryGetValue(name, out var resource)
                        ? _jungleLightShapes.Get(GraphicsDevice, resource.Bytes)
                        : null;
            }

            if (shape is null)
            {
                continue;
            }

            _gameplayLightmap.AddAmbientOnlyShape(
                shape,
                marker.X,
                marker.Y,
                marker.Width,
                marker.Height,
                ResolveForegroundSpriteDrawOpacity(index, marker.ForegroundSprite));
        }
    }


    /// <summary>The texture for a light's mask resource on the current map, or null.</summary>
    private Texture2D? ResolveMapLightMask(string? resourceName)
    {
        if (string.IsNullOrWhiteSpace(resourceName))
        {
            return null;
        }

        var visuals = _world.Level.CustomMapVisuals;
        var name = resourceName.Trim();
        foreach (var candidate in new[] { name, System.IO.Path.GetFileNameWithoutExtension(name) })
        {
            if (visuals.SpriteResources.TryGetValue(candidate, out var resource)
                || visuals.Resources.TryGetValue(candidate, out resource))
            {
                return _lightMaskTextures.Get(GraphicsDevice, resource.Bytes);
            }
        }

        return null;
    }

    /// <summary>Reused each frame for the evaluated map lights (no per-frame allocation).</summary>
    private readonly List<MapLightEmitter> _mapLightEmitters = new();

    /// <summary>
    /// Queues evaluated map lights on a light map. Shared by gameplay and the builder preview,
    /// so the editor always shows what the game draws.
    /// </summary>
    /// <param name="reach">Per placed light wall reach (by source index), or empty for none.</param>
    private static void SubmitMapLightEmitters(
        LightmapRenderer lightmap,
        IReadOnlyList<MapLightEmitter> emitters,
        float[]?[] reach,
        Func<string?, Texture2D?>? resolveMask)
    {
        for (var index = 0; index < emitters.Count; index += 1)
        {
            var emitter = emitters[index];
            var source = emitter.SourceIndex;
            lightmap.AddLight(
                emitter.X,
                emitter.Y,
                emitter.Radius,
                new Color(emitter.Color.R, emitter.Color.G, emitter.Color.B),
                emitter.Intensity,
                falloff: emitter.Falloff,
                directionDegrees: emitter.Direction,
                spreadDegrees: emitter.Spread,
                // Painted pixels are emissive map details, including pixels on solid walls.
                reach: emitter.MaskPixelSize > 0f ? null : source >= 0 && source < reach.Length ? reach[source] : null,
                mask: emitter.Mask is null ? null : resolveMask?.Invoke(emitter.Mask),
                maskPixelSize: emitter.MaskPixelSize,
                maskGlowRadius: emitter.MaskGlowRadius);
        }
    }

    /// <summary>
    /// Gameplay light sources: a soft light on every player (so darkness never hides
    /// anyone unfairly), burning players, flames, rockets, flares and explosions.
    /// Strength and range are separate settings for players and for effects.
    /// </summary>
    private static readonly Color MuzzleFlashColor = new(255, 214, 140);

    /// <summary>Muzzle flash radius at 100% size, in world pixels.</summary>
    private const float MuzzleFlashRadius = 52f;

    /// <summary>Medic beam lights sit about this far apart along the beam.</summary>
    private const float MedicBeamLightSpacing = 36f;

    private readonly Dictionary<int, double> _muzzleFlashStarts = new();
    private readonly List<int> _staleMuzzleFlashes = new();

    /// <summary>
    /// A weapon just fired: guns that leave a flash (not flamethrowers, mediguns or
    /// blades) light up at the barrel tip for the map's muzzle flash length.
    /// </summary>
    private void NoteWeaponFired(PlayerEntity player, PrimaryWeaponKind kind)
    {
        if (kind is PrimaryWeaponKind.PelletGun
            or PrimaryWeaponKind.RocketLauncher
            or PrimaryWeaponKind.MineLauncher
            or PrimaryWeaponKind.Minigun
            or PrimaryWeaponKind.Rifle
            or PrimaryWeaponKind.Revolver
            or PrimaryWeaponKind.GrenadeLauncher)
        {
            _muzzleFlashStarts[GetPlayerStateKey(player)] = _weatherClock.Elapsed.TotalSeconds;
        }
    }

    /// <summary>
    /// Soft flashes at the tip of each gun that just fired, following the barrel as the
    /// weapon moves. They brighten the surroundings and glow a little in the air.
    /// </summary>
    private void AddMuzzleFlashLights(MapLighting lighting, double time)
    {
        if (_muzzleFlashStarts.Count == 0)
        {
            return;
        }

        var length = Math.Clamp(lighting.MuzzleFlashLength, MapLightingMetadata.MinMuzzleFlashLength, MapLightingMetadata.MaxMuzzleFlashLength)
            / (double)LegacyMovementModel.SourceTicksPerSecond;
        var strength = lighting.MuzzleFlash / 100f;
        var radius = MuzzleFlashRadius * (lighting.MuzzleFlashSize / 100f);
        if (strength > 0f && radius > 0.5f)
        {
            foreach (var player in EnumerateRenderablePlayers())
            {
                if (!_muzzleFlashStarts.TryGetValue(GetPlayerStateKey(player), out var start))
                {
                    continue;
                }

                var age = time - start;
                if (age < 0d || age > length
                    || !_gameplayWeaponRenderController.TryGetWeaponMuzzle(player, out var muzzle, out var forward))
                {
                    continue;
                }

                // Brightest on the shot, easing out; centred just past the barrel.
                var fade = (float)(1d - (age / length));
                fade *= fade;
                var center = muzzle + (forward * (radius * 0.12f));
                _gameplayLightmap.AddLight(
                    center.X,
                    center.Y,
                    radius * (0.75f + (0.25f * fade)),
                    MuzzleFlashColor,
                    strength * 1.5f * fade,
                    glowBoost: strength * fade);
            }
        }

        _staleMuzzleFlashes.Clear();
        foreach (var (key, start) in _muzzleFlashStarts)
        {
            if (time - start > length + 1d || time < start)
            {
                _staleMuzzleFlashes.Add(key);
            }
        }

        foreach (var key in _staleMuzzleFlashes)
        {
            _muzzleFlashStarts.Remove(key);
        }
    }

    /// <summary>Medic (and similar) beams light their surroundings along their length, like flames.</summary>
    private void AddMedicBeamLights(MapLighting lighting)
    {
        var strength = lighting.MedicBeamLight / 100f;
        if (strength <= 0f)
        {
            return;
        }

        var range = MathF.Max(0.25f, lighting.EffectRange / 100f);
        foreach (var medic in EnumerateRenderablePlayers())
        {
            if (!medic.IsMedicHealing
                || !medic.MedicHealTargetId.HasValue
                || !HasFreshPlayerRenderHistory(medic))
            {
                continue;
            }

            var origin = GetMedicBeamOrigin(medic, out _);
            if (!IsFiniteVector(origin))
            {
                continue;
            }

            AddMedicBeamLightSegment(medic, medic.MedicHealTargetId, origin, strength, range);
            AddMedicBeamLightSegment(medic, medic.ExperimentalAdditionalMedicBeamTargetPlayerId1, origin, strength, range);
            AddMedicBeamLightSegment(medic, medic.ExperimentalAdditionalMedicBeamTargetPlayerId2, origin, strength, range);
        }
    }

    private void AddMedicBeamLightSegment(PlayerEntity medic, int? targetId, Vector2 origin, float strength, float range)
    {
        if (!targetId.HasValue
            || FindPlayerById(targetId.Value) is not { IsAlive: true } target
            || !HasFreshPlayerRenderHistory(target))
        {
            return;
        }

        var end = GetRenderPosition(target);
        var delta = end - origin;
        if (!IsFiniteVector(end) || !IsMedicBeamPresentationDistanceValid(delta.LengthSquared()))
        {
            return;
        }

        var isFreezeRayBeam = medic.IsExperimentalEngineerFreezeRayPresented;
        GetMedicBeamSegmentColors(
            target.Team,
            isFreezeRayBeam,
            medic.IsExperimentalEngineerEssenceExtractorPresented && !isFreezeRayBeam,
            medic.HasEquippedBehavior(BuiltInGameplayBehaviorIds.MedigunCrit) && target.Team != medic.Team,
            out var beamStartColor,
            out _,
            out _);
        var color = new Color(beamStartColor.R, beamStartColor.G, beamStartColor.B);
        var steps = Math.Max(1, (int)MathF.Ceiling(delta.Length() / MedicBeamLightSpacing));
        for (var step = 0; step <= steps; step += 1)
        {
            var point = origin + (delta * (step / (float)steps));
            _gameplayLightmap.AddLight(point.X, point.Y, 40f * range, color, strength * 0.45f);
        }
    }

    private void CompositeGameplayLitForeground(GraphicsDevice device, Matrix? transform)
    {
        // Brightmaps borrow the current layer's alpha as a visibility marker. Resolve
        // them and restore sprite coverage before alpha-compositing over the map art.
        DrawCharacterBrightmaps(transform);
        _gameplayEmissiveLayer.CompositeForeground(device, _spriteBatch, _gameplayWorldRasterizerState);
    }

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
                _gameplayLightmap.AddLight(position.X, position.Y, playerRadius,
                    TeamFireLighting.ResolveLightColor(lighting.IsActive && lighting.PlayerTeamColor, player.Team, PlayerLightColor), 0.6f * players, glows: false);
            }

            if (effects > 0f && player.IsBurning)
            {
                var flicker = LightmapRenderer.GetFlicker(MapLightFlicker.Flicker, time, player.Id * 1.7f);
                _gameplayLightmap.AddLight(position.X, position.Y, 74f * range,
                    TeamFireLighting.ResolveLightColor(lighting.IsActive && lighting.FireTeamColor, player.Team, BurningLightColor), effects * 0.85f * flicker);
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
            _gameplayLightmap.AddLight(flare.X, flare.Y, 84f * range,
                TeamFireLighting.ResolveLightColor(lighting.IsActive && lighting.FireTeamColor, flare.Team, FlareLightColor), effects * 0.9f);
        }

        // Flamethrower streams can hold hundreds of particles; sample them so the light
        // budget stays bounded while the stream still glows along its length.
        var flames = _world.Flames;
        var stride = Math.Max(1, flames.Count / 96);
        for (var index = 0; index < flames.Count; index += stride)
        {
            var flame = flames[index];
            _gameplayLightmap.AddLight(flame.X, flame.Y, (36f + (stride * 2f)) * range,
                TeamFireLighting.ResolveLightColor(lighting.IsActive && lighting.FireTeamColor, flame.Team, FlameLightColor), effects * 0.45f);
        }
    }
}
