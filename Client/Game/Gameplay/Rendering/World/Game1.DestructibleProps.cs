#nullable enable
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using OpenGarrison.Core;

namespace OpenGarrison.Client;

public partial class Game1
{
    private sealed record PropFragment(PlayerGibEntity Physics, Texture2D Texture, Rectangle Source, Vector2 Scale, Color Tint, int PropIndex, bool Light);
    private SimpleLevel? _destructiblePresentationLevel;
    private readonly Dictionary<int, uint> _destructibleRevisions = new();
    private readonly List<PropFragment> _propFragments = new();
    private readonly List<MapLightBeam> _scratchDestructibleLightBeams = new();
    private int _propFragmentSequence;

    private float GetDestructiblePropOpacity(int zoneIndex)
    {
        var level = _world.Level;
        if (zoneIndex < 0 || zoneIndex >= level.DamageableZoneStates.Length) return 1f;
        var state = level.DamageableZoneStates[zoneIndex];
        if (state.Revision == 0) return 1f;
        var settings = level.GetRoomObject(zoneIndex).DestructionSettings;
        var age = state.StateAgeTicks * (float)_world.Config.FixedDeltaSeconds;
        var opacity = settings.GetOpacity(state.IsDestroyed, age);
        if (!state.IsDestroyed && settings.Respawn == PropRespawnEffect.StaticWarp && age < settings.TransitionSeconds)
            opacity *= ((state.StateAgeTicks / 2 + zoneIndex) % 3) == 0 ? 0.35f : 1f;
        return opacity;
    }

    private void AdvanceDestructiblePropPresentation()
    {
        var level = _world.Level;
        if (!ReferenceEquals(_destructiblePresentationLevel, level))
        {
            _destructiblePresentationLevel = level;
            _destructibleRevisions.Clear();
            _propFragments.Clear();
        }
        foreach (var index in level.GetRoomObjectIndices(RoomObjectType.DamageableZone))
        {
            if (index >= level.DamageableZoneStates.Length) continue;
            var state = level.DamageableZoneStates[index];
            var hadState = _destructibleRevisions.TryGetValue(index, out var revision);
            _destructibleRevisions[index] = state.Revision;
            if ((hadState && state.Revision < revision) || !state.IsDestroyed)
                _propFragments.RemoveAll(fragment => GetFragmentZone(fragment) == index);
            // A first snapshot seeds persistent state without replaying old sounds or debris.
            if ((!hadState && _world.ClientPredictionMode) || revision == state.Revision || state.Revision == 0) continue;
            if (_world.ClientPredictionMode && state.StateAgeTicks > _world.Config.TicksPerSecond) continue;
            var marker = level.GetRoomObject(index);
            var settings = marker.DestructionSettings;
            PlayDestructiblePropSound(state.IsDestroyed ? settings.DestructionSound : settings.RespawnSound, marker);
            if (state.IsDestroyed && settings.Destruction == PropDestructionEffect.BreakApart)
                SpawnDestructiblePropFragments(index, state.Revision);
        }
    }

    private int GetFragmentZone(PropFragment fragment) => fragment.Light
        ? _world.Level.MapLights[fragment.PropIndex].DestructibleZoneIndex
        : _world.Level.GetRoomObject(fragment.PropIndex).DestructibleZoneIndex;

    private void PlayDestructiblePropSound(string? name, RoomObjectMarker marker)
    {
        if (string.IsNullOrWhiteSpace(name)) return;
        var mix = GameplayRapidFireAudioController.GetWorldSoundMix(marker.CenterX, marker.CenterY, GetWorldSoundListenerPosition());
        if (mix.Volume <= 0f) return;
        if (TryGetGameplayMessageCustomSound(name, out var sound)) TryPlaySound(sound, mix.Volume, 0f, mix.Pan);
        else if (_runtimeAssets.GetSound(name) is { } stockSound) TryPlaySound(stockSound, mix.Volume, 0f, mix.Pan);
    }

    private void AdvanceDestructiblePropFragments()
    {
        for (var i = _propFragments.Count - 1; i >= 0; i--)
        {
            var fragment = _propFragments[i];
            if (fragment.Texture.IsDisposed || fragment.Physics.IsExpired) { _propFragments.RemoveAt(i); continue; }
            // The same gravity, wall/floor collision, bounce and rotation as player gibs.
            // Prop pieces never enter gore's lists, emit blood, or depend on gore settings.
            fragment.Physics.Advance(_world.Level, _world.Level.Bounds, topDown: _world.Level.IsTopDown);
        }
    }

    private void SpawnDestructiblePropFragments(int zoneIndex, uint revision)
    {
        var level = _world.Level;
        var resources = GetRuntimeCustomMapVisuals()?.SpriteResources;
        if (resources is null) return;
        for (var i = 0; i < level.RoomObjects.Count; i++)
        {
            var marker = level.GetRoomObject(i);
            if (marker.DestructibleZoneIndex != zoneIndex || !level.IsRoomObjectActive(i)) continue;
            var name = marker.Type switch
            {
                RoomObjectType.CustomMapSprite => marker.CustomMapSprite.ImageResourceName,
                RoomObjectType.ForegroundSprite => marker.ForegroundSprite.ImageResourceName,
                RoomObjectType.Spritesheet => marker.Spritesheet.ImageResourceName,
                _ => "",
            };
            if (!resources.TryGetValue(name, out var resource) || !TryGetCustomMapSpriteTexture(resource, out var texture)) continue;
            texture = marker.Type == RoomObjectType.ForegroundSprite
                ? GetCurrentCustomMapFrame(texture, marker.ForegroundSprite.AnimationFramesPerSecond)
                : GetCurrentCustomMapFrame(texture);
            var source = texture.Bounds;
            if (marker.Type == RoomObjectType.Spritesheet)
            {
                var frame = SpritesheetMetadata.ResolveFrameSourceRectangle(texture.Width, texture.Height, _world.MapLogic.GetSpritesheetFrame(i), marker.Spritesheet);
                source = new(frame.X, frame.Y, frame.Width, frame.Height);
            }
            var tint = Color.White * (marker.Type == RoomObjectType.ForegroundSprite ? ResolveForegroundSpriteDrawOpacity(i, marker.ForegroundSprite) : 1f);
            var tiled = marker.Type == RoomObjectType.CustomMapSprite && marker.CustomMapSprite.Tile
                || marker.Type == RoomObjectType.ForegroundSprite && marker.ForegroundSprite.Tile;
            if (tiled)
                SpawnTiledPropImagePieces(texture, marker, tint, i, revision);
            else
                SpawnPropImagePieces(texture, source, new(marker.Left, marker.Top), new(marker.Width / source.Width, marker.Height / source.Height), tint, i, false, revision);
        }
        for (var i = 0; i < level.MapLights.Count; i++)
        {
            var light = level.MapLights[i];
            if (light.DestructibleZoneIndex != zoneIndex || ResolveMapLightMask(light.Mask) is not { } texture) continue;
            var (center, pixelSize, angle) = GetDestructibleLightMaskPlacement(light, texture);
            SpawnPropImagePieces(texture, texture.Bounds, center - new Vector2(texture.Width, texture.Height) * pixelSize / 2,
                new(pixelSize), GetDestructibleLightTint(light, i), i, true, revision, angle);
        }
    }

    private (Vector2 Center, float PixelSize, float Angle) GetDestructibleLightMaskPlacement(MapLightMarker light, Texture2D texture)
    {
        var placement = _gameplayLightmap.GetMaskPlacement(light, texture);
        return (placement.Center, placement.WorldScale, MathHelper.ToDegrees(placement.Rotation));
    }

    private Color GetDestructibleLightTint(MapLightMarker light, int index)
    {
        var sample = _world.Level.MapLightState.GetSample(index, light.Style) with { Powered = true, Progress = 1f };
        _scratchMapLightEmitters.Clear();
        _scratchDestructibleLightBeams.Clear();
        MapLightEmission.Evaluate(light, index, _world.SimulationTimeSeconds, sample, MapLightMusic.Silent, _scratchMapLightEmitters, _scratchDestructibleLightBeams);
        var color = _scratchMapLightEmitters.Count > 0 ? _scratchMapLightEmitters[0].Color : light.Color;
        return new Color(color.R, color.G, color.B);
    }

    private void SpawnTiledPropImagePieces(Texture2D texture, RoomObjectMarker marker, Color tint, int index, uint revision)
    {
        var scale = marker.Type == RoomObjectType.CustomMapSprite ? marker.CustomMapSprite.Scale : marker.ForegroundSprite.Scale;
        var anchor = marker.Type == RoomObjectType.CustomMapSprite ? marker.CustomMapSprite.TileAnchor : marker.ForegroundSprite.TileAnchor;
        var tileWidth = MathF.Max(1, texture.Width * scale);
        var tileHeight = MathF.Max(1, texture.Height * scale);
        var (anchorX, anchorY) = MapSpriteTileMetadata.ResolveAnchorTopLeft(marker.Left, marker.Top, marker.Width, marker.Height, tileWidth, tileHeight, anchor);
        var startColumn = (int)MathF.Floor((marker.Left - anchorX) / tileWidth);
        var startRow = (int)MathF.Floor((marker.Top - anchorY) / tileHeight);
        for (var y = anchorY + startRow * tileHeight; y < marker.Bottom && _propFragments.Count < 768; y += tileHeight)
            for (var x = anchorX + startColumn * tileWidth; x < marker.Right && _propFragments.Count < 768; x += tileWidth)
            {
                var left = MathF.Max(x, marker.Left);
                var top = MathF.Max(y, marker.Top);
                var right = MathF.Min(x + tileWidth, marker.Right);
                var bottom = MathF.Min(y + tileHeight, marker.Bottom);
                var sx = Math.Clamp((int)MathF.Floor((left - x) * texture.Width / tileWidth), 0, texture.Width - 1);
                var sy = Math.Clamp((int)MathF.Floor((top - y) * texture.Height / tileHeight), 0, texture.Height - 1);
                var width = Math.Clamp((int)MathF.Ceiling((right - left) * texture.Width / tileWidth), 1, texture.Width - sx);
                var height = Math.Clamp((int)MathF.Ceiling((bottom - top) * texture.Height / tileHeight), 1, texture.Height - sy);
                SpawnPropImagePieces(texture, new(sx, sy, width, height), new(left, top),
                    new((right - left) / width, (bottom - top) / height), tint, index, false, revision);
            }
    }

    private void SpawnPropImagePieces(Texture2D texture, Rectangle source, Vector2 topLeft, Vector2 scale, Color tint, int propIndex, bool light, uint revision, float angle = 0f)
    {
        // At most 64 pieces per image and 768 in flight; large PNGs do not create a pixel per particle.
        var columns = Math.Clamp((int)MathF.Sqrt(64f * source.Width / source.Height), 1, Math.Min(64, source.Width));
        var rows = Math.Clamp(64 / columns, 1, source.Height);
        var stepX = (source.Width + columns - 1) / columns;
        var stepY = (source.Height + rows - 1) / rows;
        var random = new Random(unchecked((int)revision * 397 ^ propIndex ^ (light ? 0x513 : 0)));
        var center = topLeft + new Vector2(source.Width * scale.X, source.Height * scale.Y) / 2;
        var radians = MathHelper.ToRadians(angle);
        for (var y = 0; y < source.Height && _propFragments.Count < 768; y += stepY)
            for (var x = 0; x < source.Width && _propFragments.Count < 768; x += stepX)
            {
                var piece = new Rectangle(source.X + x, source.Y + y, Math.Min(stepX, source.Width - x), Math.Min(stepY, source.Height - y));
                var position = topLeft + new Vector2((x + piece.Width / 2f) * scale.X, (y + piece.Height / 2f) * scale.Y);
                var offset = position - center;
                position = center + new Vector2(offset.X * MathF.Cos(radians) - offset.Y * MathF.Sin(radians), offset.X * MathF.Sin(radians) + offset.Y * MathF.Cos(radians));
                var physics = new PlayerGibEntity(--_propFragmentSequence, "", 0, position.X, position.Y,
                    (random.NextSingle() - 0.5f) * 9f, -2f - random.NextSingle() * 7f,
                    (random.NextSingle() - 0.5f) * 24f, 0.4f, 0.6f, 150, bloodChance: 0f);
                physics.ApplyNetworkState(position.X, position.Y, physics.VelocityX, physics.VelocityY, angle, physics.RotationSpeedDegrees, 150);
                _propFragments.Add(new(physics, texture, piece, scale, tint, propIndex, light));
            }
    }

    private void DrawPropFragments(Vector2 camera, int propIndex, bool light)
    {
        foreach (var fragment in _propFragments)
        {
            if (fragment.PropIndex != propIndex || fragment.Light != light || fragment.Texture.IsDisposed) continue;
            var physics = fragment.Physics;
            var position = new Vector2(physics.X, physics.Y) - camera;
            if (!light)
            {
                var marker = _world.Level.GetRoomObject(propIndex);
                if (marker.Type is RoomObjectType.CustomMapSprite or RoomObjectType.Spritesheet)
                {
                    var layer = marker.Type == RoomObjectType.CustomMapSprite ? marker.CustomMapSprite.Layer : marker.Spritesheet.Layer;
                    var residual = GetWorldPassCameraResidual();
                    var viewport = GetGameplayWorldPassViewport();
                    var (x, y) = CustomMapSpriteParallax.WorldToScreen(physics.X, physics.Y, layer,
                        camera.X + residual.X, camera.Y + residual.Y, viewport.Width, viewport.Height, _world.Level.CustomMapVisuals.ParallaxLayers);
                    position = new Vector2(x, y) + residual;
                }
            }
            _spriteBatch.Draw(fragment.Texture, position, fragment.Source,
                fragment.Tint * physics.Alpha, MathHelper.ToRadians(physics.RotationDegrees),
                new Vector2(fragment.Source.Width, fragment.Source.Height) / 2, fragment.Scale, SpriteEffects.None, 0f);
        }
    }

    private void DrawLightPropFragments(Vector2 camera)
    {
        for (var i = 0; i < _world.Level.MapLights.Count; i++)
        {
            DrawPropFragments(camera, i, true);
            var light = _world.Level.MapLights[i];
            var zoneIndex = light.DestructibleZoneIndex;
            if (zoneIndex < 0 || zoneIndex >= _world.Level.DamageableZoneStates.Length) continue;
            var state = _world.Level.DamageableZoneStates[zoneIndex];
            var settings = _world.Level.GetRoomObject(zoneIndex).DestructionSettings;
            if (state.IsDestroyed || state.Revision == 0 || settings.Respawn != PropRespawnEffect.StaticWarp
                || state.StateAgeTicks * _world.Config.FixedDeltaSeconds >= settings.TransitionSeconds) continue;
            if (ResolveMapLightMask(light.Mask) is not { } texture) continue;
            var (center, size, angle) = GetDestructibleLightMaskPlacement(light, texture);
            DrawPropRespawnWarp(texture, texture.Bounds, center - camera, new Vector2(size),
                GetDestructibleLightTint(light, i), zoneIndex, MathHelper.ToRadians(angle));
        }
    }

    private void DrawPropRespawnWarp(Texture2D texture, Rectangle source, Rectangle destination, Color tint, int zoneIndex)
        => DrawPropRespawnWarp(texture, source, new Vector2(destination.Center.X, destination.Center.Y),
            new Vector2(destination.Width / (float)source.Width, destination.Height / (float)source.Height), tint, zoneIndex);

    private void DrawPropRespawnWarp(Texture2D texture, Rectangle source, Vector2 center, Vector2 scale, Color tint, int zoneIndex, float rotation = 0f)
    {
        if (zoneIndex < 0 || zoneIndex >= _world.Level.DamageableZoneStates.Length) return;
        var state = _world.Level.DamageableZoneStates[zoneIndex];
        var settings = _world.Level.GetRoomObject(zoneIndex).DestructionSettings;
        if (state.IsDestroyed || state.Revision == 0 || settings.Respawn != PropRespawnEffect.StaticWarp) return;
        var progress = state.StateAgeTicks * (float)_world.Config.FixedDeltaSeconds / Math.Max(0.001f, settings.TransitionSeconds);
        if (progress >= 1f) return;
        for (var i = 0; i < 6; i++)
        {
            var y = (state.StateAgeTicks * 7 + i * 19 + zoneIndex) % Math.Max(1, source.Height);
            var row = new Rectangle(source.X, source.Y + y, source.Width, Math.Min(2, source.Height - y));
            var offset = ((state.StateAgeTicks + i * 3) % 9 - 4) * (1f - progress) * 2f;
            var local = new Vector2(-source.Width / 2f * scale.X + offset, (y - source.Height / 2f) * scale.Y);
            var position = center + Vector2.Transform(local, Matrix.CreateRotationZ(rotation));
            _spriteBatch.Draw(texture, position, row, tint * (1f - progress), rotation, Vector2.Zero, scale, SpriteEffects.None, 0f);
        }
    }
}
