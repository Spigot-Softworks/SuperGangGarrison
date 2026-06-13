#nullable enable

using Microsoft.Xna.Framework;
using OpenGarrison.Core;
using OpenGarrison.GameplayModding;
using System;
using System.Collections.Generic;

namespace OpenGarrison.Client;

public partial class Game1
{
    private sealed class MenuBottomBarRunners
    {
        private const float RunnerSpeed = 60f; // pixels per second (slowed by 50%)
        private const float SpawnInterval = 1.2f; // seconds between spawns
        private const float AnimationSpeed = 12f; // frames per second

        private readonly Game1 _game;
        private readonly Random _random;
        private readonly List<RunnerInstance> _runners = new();
        private float _spawnTimer;

        // All available player classes
        private static readonly PlayerClass[] AvailableClasses = new[]
        {
            PlayerClass.Scout,
            PlayerClass.Engineer,
            PlayerClass.Pyro,
            PlayerClass.Soldier,
            PlayerClass.Demoman,
            PlayerClass.Heavy,
            PlayerClass.Sniper,
            PlayerClass.Medic,
            PlayerClass.Spy
        };

        public MenuBottomBarRunners(Game1 game)
        {
            _game = game;
            _random = new Random(Environment.TickCount);
            
            // Pre-populate with initial runner (half a second in)
            _spawnTimer = SpawnInterval - 0.5f;
        }

        public void Update(float deltaTime)
        {
            // Only run when in animated mode
            if (_game._menuBackgroundMode == MenuBackgroundMode.Static)
            {
                _runners.Clear();
                _spawnTimer = SpawnInterval - 0.5f; // Reset timer for when mode switches back
                return;
            }

            // Spawn initial runner if needed
            if (_spawnTimer >= SpawnInterval - 0.5f && _spawnTimer < SpawnInterval && _runners.Count == 0)
            {
                SpawnNewRunner();
                _spawnTimer = 0f; // Reset timer after initial spawn to prevent bunching
            }

            // Update spawn timer
            _spawnTimer += deltaTime;
            if (_spawnTimer >= SpawnInterval)
            {
                _spawnTimer -= SpawnInterval;
                SpawnNewRunner();
            }

            // Update existing runners
            for (int i = _runners.Count - 1; i >= 0; i--)
            {
                var runner = _runners[i];
                runner.X -= RunnerSpeed * deltaTime; // Move left (subtract)
                runner.AnimationFrame += AnimationSpeed * deltaTime;

                // Remove runners that are off-screen to the left
                if (runner.X < -100f)
                {
                    _runners.RemoveAt(i);
                }
            }
        }

        public void Draw(Rectangle bottomBarBounds)
        {
            // Only draw when in animated mode
            if (_game._menuBackgroundMode == MenuBackgroundMode.Static || bottomBarBounds.Width <= 0)
            {
                return;
            }

            foreach (var runner in _runners)
            {
                DrawRunner(runner, bottomBarBounds);
            }
        }

        private void SpawnNewRunner()
        {
            // Pick a random class
            var classId = AvailableClasses[_random.Next(AvailableClasses.Length)];
            
            // Pick a random team (for sprite variety)
            var team = _random.Next(2) == 0 ? PlayerTeam.Red : PlayerTeam.Blue;

            _runners.Add(new RunnerInstance
            {
                ClassId = classId,
                Team = team,
                X = _game.ViewportWidth + 80f, // Start off-screen to the right
                AnimationFrame = 0f
            });
        }

        private void DrawRunner(RunnerInstance runner, Rectangle bottomBarBounds)
        {
            // Get the running sprite name for this class
            var spriteName = GetRunningSpriteName(runner.ClassId, runner.Team);
            if (spriteName is null)
            {
                return;
            }

            var sprite = _game.GetResolvedSprite(spriteName);
            if (sprite is null || sprite.Frames.Count == 0)
            {
                return;
            }

            // Calculate frame index
            var frameIndex = ((int)runner.AnimationFrame) % sprite.Frames.Count;
            
            // Position runners 24 pixels above the bar
            var scale = 1f;
            var yPosition = bottomBarBounds.Y - 24f;
            var position = new Vector2(runner.X, yPosition);

            // Calculate equipment offset (weapon bounce) based on animation frame
            var equipmentOffset = 0f;
            var frame = (int)System.MathF.Floor(runner.AnimationFrame) % 8;
            if (GameplayPlayerSpriteRenderController.IsRunEquipmentLowerFrame(frame))
            {
                equipmentOffset -= 2f;
            }

            // Draw weapon first (behind character)
            DrawWeapon(runner, position, scale, equipmentOffset);

            // Draw character as black silhouette
            _game.DrawSpriteFrame(
                sprite.Frames[frameIndex],
                position,
                Color.Black,
                0f,
                sprite.Origin.ToVector2(),
                new Vector2(-scale, scale)); // Flip horizontally for running left
        }

        private void DrawWeapon(RunnerInstance runner, Vector2 characterPosition, float scale, float equipmentOffset)
        {
            // Get the primary weapon for this class
            var weaponItem = CharacterClassCatalog.RuntimeRegistry.GetPrimaryItem(runner.ClassId);
            var weaponSpriteName = weaponItem.Presentation.WorldSpriteName;
            if (string.IsNullOrWhiteSpace(weaponSpriteName))
            {
                return;
            }

            var weaponSprite = _game.GetResolvedSprite(weaponSpriteName);
            if (weaponSprite is null || weaponSprite.Frames.Count == 0)
            {
                return;
            }

            // Get weapon offset and anchor origin
            var weaponOffsetX = weaponItem.Presentation.WeaponOffsetX;
            var weaponOffsetY = weaponItem.Presentation.WeaponOffsetY;
            var anchorOrigin = weaponSprite.Origin.ToVector2();
            
            // Calculate weapon position with proper anchor and bounce
            // For left-facing, flip the X offset
            var facingScale = -1f; // Running left
            var drawX = characterPosition.X + ((weaponOffsetX + anchorOrigin.X) * facingScale * scale);
            var drawY = characterPosition.Y + ((weaponOffsetY + equipmentOffset + anchorOrigin.Y) * scale);
            
            var weaponPosition = new Vector2(drawX, drawY);

            // Draw weapon as black silhouette
            _game.DrawSpriteFrame(
                weaponSprite.Frames[0],
                weaponPosition,
                Color.Black,
                0f,
                weaponSprite.Origin.ToVector2(),
                new Vector2(-scale, scale)); // Flip horizontally for running left
        }

        private static string? GetRunningSpriteName(PlayerClass classId, PlayerTeam team)
        {
            var prefix = GetPlayerSpritePrefix(classId);
            if (prefix is null)
            {
                return null;
            }

            var teamName = team switch
            {
                PlayerTeam.Red => "Red",
                PlayerTeam.Blue => "Blue",
                _ => null,
            };

            return teamName is null ? null : $"{prefix}{teamName}RunS";
        }

        private static string? GetPlayerSpritePrefix(PlayerClass classId)
        {
            return classId switch
            {
                PlayerClass.Scout => "Scout",
                PlayerClass.Engineer => "Engineer",
                PlayerClass.Pyro => "Pyro",
                PlayerClass.Soldier => "Soldier",
                PlayerClass.Demoman => "Demoman",
                PlayerClass.Heavy => "Heavy",
                PlayerClass.Sniper => "Sniper",
                PlayerClass.Medic => "Medic",
                PlayerClass.Spy => "Spy",
                _ => null,
            };
        }

        private sealed class RunnerInstance
        {
            public PlayerClass ClassId { get; set; }
            public PlayerTeam Team { get; set; }
            public float X { get; set; }
            public float AnimationFrame { get; set; }
        }
    }
}
