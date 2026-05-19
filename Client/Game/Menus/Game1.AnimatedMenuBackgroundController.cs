#nullable enable

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using OpenGarrison.Core;
using OpenGarrison.Core.BotBrain;
using System;
using System.Collections.Generic;
using System.Linq;

namespace OpenGarrison.Client;

public partial class Game1
{
    private sealed class AnimatedMenuBackgroundController
    {
        private const float MinTransitionDurationSeconds = 7f;
        private const float MaxTransitionDurationSeconds = 9f;
        private const float FadeDurationSeconds = 1.0f;
        private const float ScrollSpeedPixelsPerSecond = 20f;
        private const float MaxOffsetPixels = 50f;

        private readonly Game1 _game;
        private readonly Random _random;
        private readonly List<string> _mapNames = new();
        private int _currentMapIndex;
        private float _transitionTimer;
        private float _currentTransitionDuration;
        private float _fadeAlpha;
        private bool _isInitialized;
        private AnimatedBackgroundMapState? _currentMap;
        private AnimatedBackgroundMapState? _nextMap;
        private bool _nextMapLoaded;

        public AnimatedMenuBackgroundController(Game1 game)
        {
            _game = game;
            // Use time-based seed for menu background randomization so patterns don't repeat
            _random = new Random(Environment.TickCount);
        }

        public void Initialize(MenuBackgroundMode mode)
        {
            if (_isInitialized)
            {
                return;
            }

            var viewportWidth = _game.ViewportWidth;
            var viewportHeight = _game.ViewportHeight;

            // Build list of available map names based on mode
            _mapNames.Clear();

            if (mode == MenuBackgroundMode.DefaultMaps || mode == MenuBackgroundMode.AllMaps)
            {
                // Add default maps
                var stockMaps = OpenGarrisonStockMapCatalog.Definitions
                    .Where(def => def.DefaultOrder > 0)
                    .Select(def => def.LevelName)
                    .ToList();

                _mapNames.AddRange(stockMaps);
            }

            if (mode == MenuBackgroundMode.AllMaps)
            {
                // Add custom maps
                var catalog = SimpleLevelFactory.GetAvailableSourceLevels();
                var customMapNames = catalog
                    .Select(entry => entry.Name)
                    .Where(name => !_mapNames.Contains(name, StringComparer.OrdinalIgnoreCase))
                    .ToList();

                _mapNames.AddRange(customMapNames);
            }

            if (_mapNames.Count == 0)
            {
                return;
            }

            // Filter maps by size - only keep maps that can fill the screen
            var validMaps = new List<string>();
            foreach (var mapName in _mapNames)
            {
                var level = SimpleLevelFactory.CreateImportedLevel(mapName);
                if (level is not null && level.Bounds.Width >= viewportWidth && level.Bounds.Height >= viewportHeight)
                {
                    validMaps.Add(mapName);
                }
            }

            _mapNames.Clear();
            _mapNames.AddRange(validMaps);

            if (_mapNames.Count == 0)
            {
                return;
            }

            // Shuffle map names
            for (int i = _mapNames.Count - 1; i > 0; i--)
            {
                var j = _random.Next(i + 1);
                (_mapNames[i], _mapNames[j]) = (_mapNames[j], _mapNames[i]);
            }

            // Load first map - try up to all maps in case some fail
            AnimatedBackgroundMapState? firstMap = null;
            for (int attempt = 0; attempt < _mapNames.Count; attempt++)
            {
                firstMap = LoadMap(_mapNames[attempt]);
                if (firstMap is not null)
                {
                    _currentMapIndex = attempt;
                    break;
                }
            }

            if (firstMap is null)
            {
                _mapNames.Clear();
                return;
            }

            _currentMap = firstMap;
            InitializeMapCamera(_currentMap);

            _transitionTimer = 0f;
            _currentTransitionDuration = MinTransitionDurationSeconds +
                (_random.NextSingle() * (MaxTransitionDurationSeconds - MinTransitionDurationSeconds));
            _fadeAlpha = 1f;
            _nextMapLoaded = false;
            _isInitialized = true;
        }

        public void Reset()
        {
            _mapNames.Clear();
            _currentMap = null;
            _nextMap = null;
            _isInitialized = false;
            _nextMapLoaded = false;
        }

        public void Update(float deltaTime)
        {
            if (!_isInitialized || _currentMap is null)
            {
                return;
            }

            _transitionTimer += deltaTime;

            // Update camera scrolling for current map
            _currentMap.CameraPosition += _currentMap.ScrollVector * ScrollSpeedPixelsPerSecond * deltaTime;
            ClampCamera(_currentMap);

            // Load and initialize next map during fade-out to prevent jumps
            if (!_nextMapLoaded && _transitionTimer >= _currentTransitionDuration - FadeDurationSeconds)
            {
                // Try loading the next map, skip any that fail
                for (int attempt = 0; attempt < _mapNames.Count; attempt++)
                {
                    var nextMapIndex = (_currentMapIndex + 1 + attempt) % _mapNames.Count;
                    _nextMap = LoadMap(_mapNames[nextMapIndex]);
                    if (_nextMap is not null)
                    {
                        InitializeMapCamera(_nextMap);
                        _nextMapLoaded = true;
                        break;
                    }
                }

                // If we couldn't load any map, mark as loaded to prevent retry
                if (!_nextMapLoaded)
                {
                    _nextMapLoaded = true;
                }
            }

            // Update camera scrolling for next map during fade transition
            if (_nextMap is not null)
            {
                _nextMap.CameraPosition += _nextMap.ScrollVector * ScrollSpeedPixelsPerSecond * deltaTime;
                ClampCamera(_nextMap);
            }

            // Calculate fade alpha (only fade out, don't fade in)
            if (_transitionTimer > _currentTransitionDuration - FadeDurationSeconds)
            {
                // Fading out during last 0.5 seconds
                _fadeAlpha = (_currentTransitionDuration - _transitionTimer) / FadeDurationSeconds;
            }
            else
            {
                // Fully visible when not fading out
                _fadeAlpha = 1f;
            }

            // Handle transition to next map
            if (_transitionTimer >= _currentTransitionDuration)
            {
                // Transition to next map if available, otherwise keep current map
                if (_nextMap is not null)
                {
                    _currentMapIndex = (_currentMapIndex + 1) % _mapNames.Count;
                    _currentMap = _nextMap;
                    _nextMap = null;
                }
                else
                {
                    // No next map loaded, keep current and reinitialize camera for variety
                    if (_currentMap is not null)
                    {
                        InitializeMapCamera(_currentMap);
                    }
                }

                _transitionTimer = 0f;
                _fadeAlpha = 1f; // Ensure current map is fully visible
                _nextMapLoaded = false;
                // Pick a new random duration for the next map
                _currentTransitionDuration = MinTransitionDurationSeconds +
                    (_random.NextSingle() * (MaxTransitionDurationSeconds - MinTransitionDurationSeconds));
            }
        }

        public void Draw(int viewportWidth, int viewportHeight)
        {
            if (!_isInitialized || _currentMap is null)
            {
                return;
            }

            // Draw next map if we're in fade transition
            if (_nextMap is not null && _transitionTimer > _currentTransitionDuration - FadeDurationSeconds)
            {
                DrawMap(_nextMap, viewportWidth, viewportHeight, 1f);
            }

            // Draw current map with fade alpha
            DrawMap(_currentMap, viewportWidth, viewportHeight, _fadeAlpha);
        }

        private AnimatedBackgroundMapState? LoadMap(string mapName)
        {
            var level = SimpleLevelFactory.CreateImportedLevel(mapName);
            if (level is null)
            {
                return null;
            }

            return new AnimatedBackgroundMapState
            {
                Level = level,
                MapName = mapName
            };
        }

        private void DrawMap(AnimatedBackgroundMapState mapState, int viewportWidth, int viewportHeight, float alpha)
        {
            var level = mapState.Level;
            var cameraPosition = mapState.CameraPosition;

            // Calculate world rectangle in screen space
            var worldRectangle = new Rectangle(
                (int)(-cameraPosition.X),
                (int)(-cameraPosition.Y),
                (int)level.Bounds.Width,
                (int)level.Bounds.Height);

            // Draw level background with grayscale effect
            var backgroundName = level.BackgroundAssetName;
            if (_game._runtimeAssets is not null && !string.IsNullOrWhiteSpace(backgroundName))
            {
                var background = _game._runtimeAssets.GetBackground(backgroundName);
                if (background is not null)
                {
                    // End current batch and restart with grayscale effect
                    _game._spriteBatch.End();
                    _game._spriteBatch.Begin(
                        samplerState: SamplerState.PointClamp,
                        effect: _game._grayscaleEffect,
                        rasterizerState: RasterizerState.CullNone);

                    _game._spriteBatch.Draw(background, worldRectangle, Color.White * alpha);

                    // Restore normal SpriteBatch state
                    _game._spriteBatch.End();
                    _game._spriteBatch.Begin(
                        samplerState: SamplerState.PointClamp,
                        rasterizerState: RasterizerState.CullNone);
                    return;
                }
            }

            // Fallback background
            _game._spriteBatch.Draw(_game._pixel, worldRectangle, new Color(34, 44, 60) * alpha);
        }

        private static void AddPointsAlongLine(List<Vector2> points, Vector2 start, Vector2 end)
        {
            const float SampleInterval = 150f;

            var direction = end - start;
            var distance = direction.Length();

            // Skip if points are too close
            if (distance < SampleInterval)
            {
                return;
            }

            direction.Normalize();

            // Sample points along the line at regular intervals
            for (float d = SampleInterval; d < distance; d += SampleInterval)
            {
                points.Add(start + direction * d);
            }
        }

        private void InitializeMapCamera(AnimatedBackgroundMapState mapState)
        {
            var level = mapState.Level;

            // Collect all possible focus points (spawn points, control points, and navigation nodes)
            var focusPoints = new List<Vector2>();

            // Add bot navigation nodes if available (default maps have this)
            if (BotNavigationAssetStore.TryLoadShipped(level, out var navAsset))
            {
                foreach (var node in navAsset.Nodes)
                {
                    focusPoints.Add(new Vector2(node.X, node.Y));
                }
            }

            // Collect spawn points
            var spawnPoints = new List<Vector2>();
            foreach (var spawn in level.RedSpawns)
            {
                var spawnPos = new Vector2(spawn.X, spawn.Y);
                spawnPoints.Add(spawnPos);
                focusPoints.Add(spawnPos);
            }
            foreach (var spawn in level.BlueSpawns)
            {
                var spawnPos = new Vector2(spawn.X, spawn.Y);
                spawnPoints.Add(spawnPos);
                focusPoints.Add(spawnPos);
            }

            // Collect objectives (control points and intel bases)
            var objectives = new List<Vector2>();
            var controlPoints = level.GetRoomObjects(RoomObjectType.ControlPoint);
            foreach (var cp in controlPoints)
            {
                var cpPos = new Vector2(cp.CenterX, cp.CenterY);
                objectives.Add(cpPos);
                focusPoints.Add(cpPos);
            }
            foreach (var intel in level.IntelBases)
            {
                var intelPos = new Vector2(intel.X, intel.Y);
                objectives.Add(intelPos);
                focusPoints.Add(intelPos);
            }

            // Add points along lines from spawns to objectives
            foreach (var spawn in spawnPoints)
            {
                foreach (var objective in objectives)
                {
                    AddPointsAlongLine(focusPoints, spawn, objective);
                }
            }

            // Add points along lines from objectives to other objectives
            for (int i = 0; i < objectives.Count; i++)
            {
                for (int j = i + 1; j < objectives.Count; j++)
                {
                    AddPointsAlongLine(focusPoints, objectives[i], objectives[j]);
                }
            }

            // If no focus points, use map center
            if (focusPoints.Count == 0)
            {
                focusPoints.Add(new Vector2(level.Bounds.Width / 2f, level.Bounds.Height / 2f));
            }

            // Pick a random focus point
            var focusPoint = focusPoints[_random.Next(focusPoints.Count)];

            // Add random offset (up to 50px in each direction)
            var randomOffsetX = (_random.NextSingle() - 0.5f) * 2f * MaxOffsetPixels;
            var randomOffsetY = (_random.NextSingle() - 0.5f) * 2f * MaxOffsetPixels;
            focusPoint.X += randomOffsetX;
            focusPoint.Y += randomOffsetY;

            // Get viewport dimensions
            var viewportWidth = _game.ViewportWidth;
            var viewportHeight = _game.ViewportHeight;

            // Position camera so focus point is at center of screen
            var cameraX = focusPoint.X - (viewportWidth / 2f);
            var cameraY = focusPoint.Y - (viewportHeight / 2f);
            mapState.CameraPosition = new Vector2(cameraX, cameraY);

            // Clamp camera to prevent showing out-of-bounds
            ClampCamera(mapState);

            // Calculate map center
            var mapCenterX = level.Bounds.Width / 2f;
            var mapCenterY = level.Bounds.Height / 2f;

            // Create horizontal vector towards map center
            var currentCenterX = mapState.CameraPosition.X + (viewportWidth / 2f);
            var directionX = mapCenterX - currentCenterX;

            // Normalize to create scroll vector (horizontal only)
            var scrollVector = new Vector2(directionX, 0f);
            if (scrollVector.LengthSquared() > 0.01f)
            {
                scrollVector.Normalize();
            }
            else
            {
                // If already at center, pick a random horizontal direction
                scrollVector = new Vector2(_random.Next(2) == 0 ? 1f : -1f, 0f);
            }

            mapState.ScrollVector = scrollVector;
        }

        private void ClampCamera(AnimatedBackgroundMapState mapState)
        {
            var level = mapState.Level;
            var viewportWidth = _game.ViewportWidth;
            var viewportHeight = _game.ViewportHeight;

            // Clamp camera X
            var minCameraX = 0f;
            var maxCameraX = Math.Max(0f, level.Bounds.Width - viewportWidth);
            var clampedX = Math.Clamp(mapState.CameraPosition.X, minCameraX, maxCameraX);

            // Clamp camera Y
            var minCameraY = 0f;
            var maxCameraY = Math.Max(0f, level.Bounds.Height - viewportHeight);
            var clampedY = Math.Clamp(mapState.CameraPosition.Y, minCameraY, maxCameraY);

            mapState.CameraPosition = new Vector2(clampedX, clampedY);
        }

        private sealed class AnimatedBackgroundMapState
        {
            public SimpleLevel Level { get; set; } = null!;
            public string MapName { get; set; } = string.Empty;
            public Vector2 CameraPosition { get; set; }
            public Vector2 ScrollVector { get; set; }
        }
    }
}
