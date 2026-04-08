#nullable enable

using Microsoft.Xna.Framework;
using OpenGarrison.Core;

namespace OpenGarrison.Client;

public partial class Game1
{
    private sealed class GameplayPlayerSpriteRenderController
    {
        private readonly Game1 _game;

        public GameplayPlayerSpriteRenderController(Game1 game)
        {
            _game = game;
        }

        public bool TryDrawPlayerSprite(PlayerEntity player, Vector2 cameraPosition, Color tint, PlayerBodySpriteSelection bodySelection)
        {
            var renderPosition = _game.GetRenderPosition(player, allowInterpolation: !ReferenceEquals(player, _game._world.LocalPlayer));
            return TryDrawPlayerSpriteAtPosition(player, renderPosition, cameraPosition, tint, bodySelection, true);
        }

        public bool TryDrawPlayerSpriteAtPosition(PlayerEntity player, Vector2 renderPosition, Vector2 cameraPosition, Color tint, PlayerBodySpriteSelection bodySelection, bool drawIntelOverlay)
        {
            var isHeavyEating = _game.GetPlayerIsHeavyEating(player);
            var spriteName = isHeavyEating ? GetHeavyEatSpriteName(player) : player.IsTaunting ? GetTauntSpriteName(player) : bodySelection.SpriteName;
            if (spriteName is null)
            {
                return false;
            }

            var sprite = _game._runtimeAssets.GetSprite(spriteName);
            if (sprite is null || sprite.Frames.Count == 0)
            {
                return false;
            }

            var facingScale = GetPlayerFacingScale(player);
            var playerScale = player.PlayerScale;
            var scale = new Vector2(facingScale * playerScale, playerScale);
            var frameIndex = isHeavyEating
                ? GetHeavyEatSpriteFrameIndex(_game.GetPlayerHeavyEatTicksRemaining(player), sprite.Frames.Count, player.Team)
                : player.IsTaunting
                    ? GetTauntSpriteFrameIndex(player, sprite.Frames.Count)
                    : bodySelection.IsHumiliated
                        ? GetHumiliationSpriteFrameIndex(player, bodySelection.AnimationImage, sprite.Frames.Count)
                        : GetPlayerBodySpriteFrameIndex(bodySelection.AnimationImage, sprite.Frames.Count);
            var roundedOrigin = GetRoundedPlayerSpriteOrigin(renderPosition);
            var bodyYOffset = isHeavyEating || player.IsTaunting ? 0f : bodySelection.BodyYOffset * playerScale;
            var position = new Vector2(roundedOrigin.X - cameraPosition.X, roundedOrigin.Y + bodyYOffset - cameraPosition.Y);

            if (drawIntelOverlay && !isHeavyEating && !player.IsTaunting && bodySelection.DrawIntelUnderlay)
            {
                DrawIntelUnderlaySprite(player, cameraPosition, tint, scale, bodySelection, roundedOrigin);
            }

            _game.DrawSpriteFrameWithOptionalShadow(sprite.Frames[frameIndex], position, tint, 0f, sprite.Origin.ToVector2(), scale);
            if (player.IsUbered)
            {
                _game.DrawSpriteFrameWithOptionalShadow(sprite.Frames[frameIndex], position, GameplayPlayerStatusEffectRenderController.GetUberOverlayColor(player.Team) * 0.7f, 0f, sprite.Origin.ToVector2(), scale);
            }

            if (drawIntelOverlay && !isHeavyEating && !player.IsTaunting && bodySelection.DrawIntelUnderlay)
            {
                DrawCarriedIntelTimerSprite(player, cameraPosition, roundedOrigin);
            }

            return true;
        }

        public PlayerBodySpriteSelection GetPlayerBodySpriteSelection(PlayerEntity player)
        {
            var renderState = _game._playerRenderStates.GetValueOrDefault(_game.GetPlayerStateKey(player));
            var animationImage = WrapAnimationImage(renderState?.BodyAnimationImage ?? 0f, _game.GetPlayerBodyAnimationLength(player));
            var renderHorizontalSpeed = renderState?.RenderHorizontalSpeed ?? player.HorizontalSpeed;
            var horizontalSourceStepSpeed = GetPlayerAnimationSourceStepSpeed(renderHorizontalSpeed);
            var appearsAirborne = renderState?.AppearsAirborne ?? !player.IsGrounded;
            if (_game._world.IsPlayerHumiliated(player))
            {
                return new PlayerBodySpriteSelection(GetTeamSpriteName(player.ClassId, player.Team, "HS"), animationImage, 0f, 0f, false, true);
            }

            if (player.ClassId == PlayerClass.Quote)
            {
                return new PlayerBodySpriteSelection(GetPlayerSpriteName(player), animationImage, 0f, 0f, false, false);
            }

            if (player.ClassId == PlayerClass.Sniper && player.IsSniperScoped)
            {
                return new PlayerBodySpriteSelection(GetTeamSpriteName(player.ClassId, player.Team, "CrouchS"), WrapAnimationImage(animationImage, 2f), 0f, 0f, false, false);
            }

            string? spriteName;
            var bodyYOffset = 0f;
            var isRunSprite = false;
            var isHeavySlowWalk = false;
            if (appearsAirborne)
            {
                spriteName = GetTeamSpriteName(player.ClassId, player.Team, "JumpS");
            }
            else if (horizontalSourceStepSpeed < 0.2f)
            {
                spriteName = GetStandingSpriteName(player);
                if (spriteName is not null && spriteName.Contains("Lean", System.StringComparison.Ordinal))
                {
                    bodyYOffset = 6f;
                }
            }
            else if (player.ClassId == PlayerClass.Heavy && horizontalSourceStepSpeed < 3f)
            {
                spriteName = GetTeamSpriteName(player.ClassId, player.Team, "WalkS");
                isHeavySlowWalk = true;
            }
            else
            {
                spriteName = GetTeamSpriteName(player.ClassId, player.Team, "RunS");
                isRunSprite = true;
            }

            var equipmentOffset = bodyYOffset;
            if (isRunSprite && !appearsAirborne && (System.Math.Abs((int)System.MathF.Floor(animationImage) % 2) == 0))
            {
                equipmentOffset -= 2f;
            }

            if (isHeavySlowWalk || player.ClassId == PlayerClass.Soldier)
            {
                equipmentOffset = bodyYOffset;
            }
            else if (horizontalSourceStepSpeed < 3f && equipmentOffset < bodyYOffset)
            {
                bodyYOffset += 2f;
                equipmentOffset += 2f;
            }

            return new PlayerBodySpriteSelection(spriteName, animationImage, bodyYOffset, equipmentOffset, player.IsCarryingIntel, false);
        }

        public static float GetPlayerFacingScale(PlayerEntity player) => IsFacingLeftByAim(player) ? -1f : 1f;
        public static bool IsFacingLeftByAim(PlayerEntity player)
        {
            var radians = System.MathF.PI * player.AimDirectionDegrees / 180f;
            return System.MathF.Cos(radians) < 0f;
        }

        public static string? GetTauntSpriteName(PlayerEntity player) => GetTeamSpriteName(player.ClassId, player.Team, "TauntS");
        public static string? GetHeavyEatSpriteName(PlayerEntity player) => player.ClassId == PlayerClass.Heavy ? GetTeamSpriteName(player.ClassId, player.Team, "OmnomnomnomS") : null;
        public static string? GetPlayerSpriteName(PlayerEntity player) => GetPlayerSpriteName(player.ClassId, player.Team);
        public static string? GetPlayerSpriteName(PlayerClass classId, PlayerTeam team) => GetTeamSpriteName(classId, team, "S");

        public static string? GetDeadBodySpriteName(PlayerClass classId, PlayerTeam team, DeadBodyAnimationKind animationKind = DeadBodyAnimationKind.Default)
        {
            if (animationKind == DeadBodyAnimationKind.Decapitated)
            {
                return ExperimentalDemoknightCatalog.GetDecapitatedDeadBodySpriteName(classId, team);
            }

            return GetTeamSpriteName(classId, team, "DeadS");
        }

        public void DrawIntelUnderlaySprite(PlayerEntity player, Vector2 cameraPosition, Color tint, Vector2 scale, PlayerBodySpriteSelection bodySelection, Vector2 roundedOrigin)
        {
            DrawIntelUnderlaySpriteCore(player, cameraPosition, tint, scale, bodySelection, roundedOrigin);
        }

        public void DrawCarriedIntelTimerSprite(PlayerEntity player, Vector2 cameraPosition, Vector2 roundedOrigin)
        {
            DrawCarriedIntelTimerSpriteCore(player, cameraPosition, roundedOrigin);
        }

        public static PlayerTeam GetCarriedIntelTeamProxy(PlayerEntity player) => GetCarriedIntelTeam(player);
        public static int GetPlayerBodySpriteFrameIndexProxy(float animationImage, int frameCount) => GetPlayerBodySpriteFrameIndex(animationImage, frameCount);
        public int GetHumiliationSpriteFrameIndex(PlayerEntity player, float animationImage, int frameCount) => GetHumiliationSpriteFrameIndexCore(player, animationImage, frameCount);
        public static int GetTauntSpriteFrameIndexProxy(PlayerEntity player, int frameCount) => GetTauntSpriteFrameIndex(player, frameCount);
        public static int GetHeavyEatSpriteFrameIndexProxy(int heavyEatTicksRemaining, int frameCount, PlayerTeam team) => GetHeavyEatSpriteFrameIndex(heavyEatTicksRemaining, frameCount, team);
        public string? GetStandingSpriteName(PlayerEntity player) => GetStandingSpriteNameCore(player);
        public LeanDirection GetPlayerLeanDirection(PlayerEntity player) => GetPlayerLeanDirectionCore(player);
        public bool IsPointBlockedForPlayer(PlayerEntity player, float x, float y) => IsPointBlockedForPlayerCore(player, x, y);
        public static string? GetTeamSpriteNameProxy(PlayerClass classId, PlayerTeam team, string suffix) => GetTeamSpriteName(classId, team, suffix);
        public static string? GetPlayerSpritePrefixProxy(PlayerClass classId) => GetPlayerSpritePrefix(classId);

        private void DrawIntelUnderlaySpriteCore(PlayerEntity player, Vector2 cameraPosition, Color tint, Vector2 scale, PlayerBodySpriteSelection bodySelection, Vector2 roundedOrigin)
        {
            var spriteName = GetTeamSpriteName(player.ClassId, player.Team, "IntelS");
            if (spriteName is null)
            {
                return;
            }

            var sprite = _game._runtimeAssets.GetSprite(spriteName);
            if (sprite is null || sprite.Frames.Count == 0)
            {
                return;
            }

            _game.DrawSpriteFrameWithOptionalShadow(
                sprite.Frames[0],
                new Vector2(roundedOrigin.X - cameraPosition.X, roundedOrigin.Y + (bodySelection.EquipmentOffset * player.PlayerScale) - cameraPosition.Y),
                tint,
                0f,
                sprite.Origin.ToVector2(),
                scale);
        }

        private void DrawCarriedIntelTimerSpriteCore(PlayerEntity player, Vector2 cameraPosition, Vector2 roundedOrigin)
        {
            var timerSprite = _game._runtimeAssets.GetSprite("IntelTimerS");
            if (timerSprite is null || timerSprite.Frames.Count == 0)
            {
                return;
            }

            var rechargeTicks = float.Clamp(_game.GetPlayerIntelRechargeTicks(player), 0f, PlayerEntity.IntelRechargeMaxTicks);
            if (rechargeTicks >= PlayerEntity.IntelRechargeMaxTicks)
            {
                return;
            }

            var progress = rechargeTicks / PlayerEntity.IntelRechargeMaxTicks;
            var timerFrame = System.Math.Clamp((int)System.MathF.Floor(progress * 12f), 0, 12);
            if (GetCarriedIntelTeam(player) == PlayerTeam.Blue)
            {
                timerFrame += 12;
            }

            var playerScale = player.PlayerScale;
            _game.DrawSpriteFrameWithOptionalShadow(
                timerSprite.Frames[System.Math.Clamp(timerFrame, 0, timerSprite.Frames.Count - 1)],
                new Vector2(roundedOrigin.X + (2f * playerScale) - cameraPosition.X, roundedOrigin.Y - (33f * playerScale) - cameraPosition.Y),
                Color.White,
                0f,
                timerSprite.Origin.ToVector2(),
                new Vector2(2f * playerScale, 2f * playerScale));
        }

        private static PlayerTeam GetCarriedIntelTeam(PlayerEntity player) => player.Team == PlayerTeam.Blue ? PlayerTeam.Red : PlayerTeam.Blue;
        private static int GetPlayerBodySpriteFrameIndex(float animationImage, int frameCount) => frameCount <= 0 ? 0 : System.Math.Clamp((int)System.MathF.Floor(WrapAnimationImage(animationImage, frameCount)), 0, frameCount - 1);

        private int GetHumiliationSpriteFrameIndexCore(PlayerEntity player, float animationImage, int frameCount)
        {
            if (frameCount <= 0)
            {
                return 0;
            }

            const int framesPerPose = 3;
            if (frameCount <= framesPerPose)
            {
                return System.Math.Clamp((int)System.MathF.Floor(animationImage), 0, frameCount - 1);
            }

            var poseCount = System.Math.Max(1, System.Math.Min(frameCount / framesPerPose, 11));
            var poseOffset = System.Math.Abs(unchecked((_game.GetPlayerStateKey(player) * 97) + 13)) % poseCount;
            var poseFrame = System.Math.Clamp((int)System.MathF.Floor(animationImage), 0, framesPerPose - 1);
            return System.Math.Clamp((poseOffset * framesPerPose) + poseFrame, 0, frameCount - 1);
        }

        private static int GetTauntSpriteFrameIndex(PlayerEntity player, int frameCount) => frameCount <= 0 ? 0 : System.Math.Clamp((int)System.MathF.Floor(player.TauntFrameIndex), 0, frameCount - 1);

        private static int GetHeavyEatSpriteFrameIndex(int heavyEatTicksRemaining, int frameCount, PlayerTeam team)
        {
            if (frameCount <= 0)
            {
                return 0;
            }

            var expectedFrames = System.Math.Max(1, (int)System.MathF.Ceiling(PlayerEntity.HeavyEatDurationTicks * 0.25f) + 1);
            var hasTeamVariants = frameCount >= expectedFrames * 2;
            var perTeamFrames = hasTeamVariants ? frameCount / 2 : frameCount;
            var elapsedTicks = System.Math.Clamp(PlayerEntity.HeavyEatDurationTicks - heavyEatTicksRemaining, 0, PlayerEntity.HeavyEatDurationTicks);
            var animationIndex = System.Math.Clamp((int)System.MathF.Floor(elapsedTicks * 0.25f), 0, perTeamFrames - 1);
            var teamOffset = team == PlayerTeam.Blue && hasTeamVariants ? perTeamFrames : 0;
            return System.Math.Clamp(animationIndex + teamOffset, 0, frameCount - 1);
        }

        private string? GetStandingSpriteNameCore(PlayerEntity player)
        {
            var leanDirection = GetPlayerLeanDirection(player);
            if (leanDirection == LeanDirection.None)
            {
                return GetTeamSpriteName(player.ClassId, player.Team, "StandS");
            }

            var facingLeft = IsFacingLeftByAim(player);
            var suffix = leanDirection switch
            {
                LeanDirection.Left => facingLeft ? "LeanRS" : "LeanLS",
                LeanDirection.Right => facingLeft ? "LeanLS" : "LeanRS",
                _ => "StandS",
            };

            return GetTeamSpriteName(player.ClassId, player.Team, suffix);
        }

        private LeanDirection GetPlayerLeanDirectionCore(PlayerEntity player)
        {
            var playerScale = player.PlayerScale;
            var bottom = player.Bottom + (2f * playerScale);
            var openRight = !IsPointBlockedForPlayer(player, player.X + (6f * playerScale), bottom) && !IsPointBlockedForPlayer(player, player.X + (2f * playerScale), bottom);
            var openLeft = !IsPointBlockedForPlayer(player, player.X - (7f * playerScale), bottom) && !IsPointBlockedForPlayer(player, player.X - (3f * playerScale), bottom);
            var leanDirection = LeanDirection.None;
            if (openRight)
            {
                leanDirection = LeanDirection.Right;
            }

            if (openLeft)
            {
                leanDirection = LeanDirection.Left;
            }

            if (openRight && openLeft)
            {
                openRight = !IsPointBlockedForPlayer(player, player.Right - playerScale, bottom);
                openLeft = !IsPointBlockedForPlayer(player, player.Left, bottom);
                leanDirection = LeanDirection.None;
                if (openRight)
                {
                    leanDirection = LeanDirection.Right;
                }

                if (openLeft)
                {
                    leanDirection = LeanDirection.Left;
                }
            }

            return leanDirection;
        }

        private bool IsPointBlockedForPlayerCore(PlayerEntity player, float x, float y)
        {
            foreach (var solid in _game._world.Level.Solids)
            {
                if (x >= solid.Left && x < solid.Right && y >= solid.Top && y < solid.Bottom)
                {
                    return true;
                }
            }

            foreach (var gate in _game._world.Level.GetBlockingTeamGates(player.Team, player.IsCarryingIntel))
            {
                if (x >= gate.Left && x < gate.Right && y >= gate.Top && y < gate.Bottom)
                {
                    return true;
                }
            }

            foreach (var wall in _game._world.Level.GetRoomObjects(RoomObjectType.PlayerWall))
            {
                if (x >= wall.Left && x < wall.Right && y >= wall.Top && y < wall.Bottom)
                {
                    return true;
                }
            }

            return false;
        }

        private static string? GetTeamSpriteName(PlayerClass classId, PlayerTeam team, string suffix)
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

            return teamName is null ? null : $"{prefix}{teamName}{suffix}";
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
                PlayerClass.Quote => "Querly",
                _ => null,
            };
        }
    }
}
