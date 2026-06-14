#nullable enable

using Microsoft.Xna.Framework;
using OpenGarrison.Core;
using OpenGarrison.GameplayModding;

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
            var renderPosition = _game.GetRenderPosition(player);
            return TryDrawPlayerSpriteAtPosition(player, renderPosition, cameraPosition, tint, bodySelection, true);
        }

        public bool TryDrawPlayerSpriteAtPosition(PlayerEntity player, Vector2 renderPosition, Vector2 cameraPosition, Color tint, PlayerBodySpriteSelection bodySelection, bool drawIntelOverlay)
        {
            var isHeavyEating = _game.GetPlayerIsHeavyEating(player);
            var isPogo = _game.GetPlayerIsCivviePogoActive(player);
            var isPogoTrick = isPogo && player.IsCivviePogoTrickActive;
            var spriteName = isHeavyEating
                ? GetHeavyEatSpriteName(player)
                : isPogoTrick
                    ? GetPogoTrickSpriteName(player)
                    : isPogo
                        ? GetPogoSpriteName(player)
                        : player.IsTaunting
                            ? GetTauntSpriteName(player)
                            : bodySelection.SpriteName;
            if (spriteName is null)
            {
                return false;
            }

            var sprite = _game.GetResolvedSprite(spriteName);
            if (sprite is null || sprite.Frames.Count == 0)
            {
                return false;
            }

            var facingScale = GetRenderFacingScale(player);
            var playerScale = player.PlayerScale;
            var scale = new Vector2(facingScale * playerScale, playerScale);
            var frameIndex = isHeavyEating
                ? GetHeavyEatSpriteFrameIndex(_game.GetPlayerHeavyEatTicksRemaining(player), sprite.Frames.Count, player.Team)
                : isPogoTrick
                    ? _game.GetCivviePogoTrickPresentationFrameIndex(player, sprite.Frames.Count)
                    : isPogo
                        ? PlayerEntity.GetCivviePogoSpriteFrameIndex(player, sprite.Frames.Count)
                        : player.IsTaunting
                            ? GetTauntSpriteFrameIndex(player, sprite.Frames.Count)
                            : bodySelection.IsHumiliated
                                ? GetHumiliationSpriteFrameIndex(player, bodySelection.AnimationImage, sprite.Frames.Count)
                                : GetPlayerBodySpriteFrameIndex(bodySelection.AnimationImage, sprite.Frames.Count);
            var roundedOrigin = GetRoundedPlayerSpriteOrigin(renderPosition);
            var bodyYOffset = isHeavyEating || player.IsTaunting || isPogo ? 0f : bodySelection.BodyYOffset * playerScale;
            var position = new Vector2(roundedOrigin.X - cameraPosition.X, roundedOrigin.Y + bodyYOffset - cameraPosition.Y);

            if (drawIntelOverlay && !isHeavyEating && !player.IsTaunting && bodySelection.DrawIntelUnderlay)
            {
                if (isPogo)
                {
                    var intelFrameIndex = isPogoTrick ? 0 : frameIndex;
                    DrawPogoIntelUnderlaySprite(player, cameraPosition, tint, scale, roundedOrigin, intelFrameIndex);
                }
                else
                {
                    DrawIntelUnderlaySprite(player, cameraPosition, tint, scale, bodySelection, roundedOrigin);
                }
            }

            if (player.IsUbered)
            {
                if (_game.IsKritzUberWeaponOnlyVisual(player))
                {
                    _game.DrawSpriteFrameWithOptionalShadow(sprite.Frames[frameIndex], position, tint, 0f, sprite.Origin.ToVector2(), scale);
                }
                else
                {
                var teamColor = GameplayPlayerStatusEffectRenderController.GetUberOverlayColor(player.Team);
                var outlineTint = Color.Lerp(teamColor, Color.White, 0.75f);
                _game.DrawSpriteFrameShadow(sprite.Frames[frameIndex], position, tint, 0f, sprite.Origin.ToVector2(), scale);
                if (_game._uberOutlineEnabled)
                {
                    _game.DrawSpriteFrameOutline(sprite.Frames[frameIndex], position, outlineTint, 0f, sprite.Origin.ToVector2(), scale);
                }
                _game.DrawSpriteFrame(sprite.Frames[frameIndex], position, tint, 0f, sprite.Origin.ToVector2(), scale);
                _game.DrawSpriteFrameMultiplyColor(sprite.Frames[frameIndex], position, teamColor, 0f, sprite.Origin.ToVector2(), scale);
                }
            }
            else
            {
                _game.DrawSpriteFrameWithOptionalShadow(sprite.Frames[frameIndex], position, tint, 0f, sprite.Origin.ToVector2(), scale);
            }

            if (drawIntelOverlay && !isHeavyEating && !player.IsTaunting && bodySelection.DrawIntelUnderlay)
            {
                DrawCarriedIntelTimerSprite(player, cameraPosition, roundedOrigin);
            }

            if (player.ClassId == PlayerClass.Spy
                && !ReferenceEquals(player, _game._world.LocalPlayer)
                && player.Team != _game._world.LocalPlayer.Team
                && !(_game.IsSpyHiddenFromLocalViewer(player) && !_game.GetPlayerIsSpyVisibleToEnemies(player)))
            {
                _game.RecordLastVisibleEnemySpyFrame(
                    player,
                    spriteName,
                    frameIndex,
                    renderPosition,
                    sprite.Origin.ToVector2(),
                    scale,
                    bodyYOffset,
                    tint,
                    drawIntelOverlay);
            }

            if (player.ClassId == PlayerClass.Heavy && _game.GetPlayerIsExperimentalGhostDashing(player))
            {
                _game.RecordHeavyDashFrameState(
                    player,
                    spriteName,
                    frameIndex,
                    renderPosition,
                    sprite.Origin.ToVector2(),
                    scale,
                    bodyYOffset,
                    tint,
                    drawIntelOverlay);
            }

            return true;
        }

        public bool TryDrawPlayerHealingOutlineAtPosition(
            PlayerEntity player,
            Vector2 renderPosition,
            Vector2 cameraPosition,
            Color outlineTint,
            PlayerBodySpriteSelection bodySelection)
        {
            if (outlineTint.A <= 0)
            {
                return false;
            }

            var isHeavyEating = _game.GetPlayerIsHeavyEating(player);
            var isPogo = _game.GetPlayerIsCivviePogoActive(player);
            var isPogoTrick = isPogo && player.IsCivviePogoTrickActive;
            var spriteName = isHeavyEating
                ? GetHeavyEatSpriteName(player)
                : isPogoTrick
                    ? GetPogoTrickSpriteName(player)
                    : isPogo
                        ? GetPogoSpriteName(player)
                        : player.IsTaunting
                            ? GetTauntSpriteName(player)
                            : bodySelection.SpriteName;
            if (spriteName is null)
            {
                return false;
            }

            var sprite = _game.GetResolvedSprite(spriteName);
            if (sprite is null || sprite.Frames.Count == 0)
            {
                return false;
            }

            var facingScale = GetRenderFacingScale(player);
            var playerScale = player.PlayerScale;
            var scale = new Vector2(facingScale * playerScale, playerScale);
            var frameIndex = isHeavyEating
                ? GetHeavyEatSpriteFrameIndex(_game.GetPlayerHeavyEatTicksRemaining(player), sprite.Frames.Count, player.Team)
                : isPogoTrick
                    ? _game.GetCivviePogoTrickPresentationFrameIndex(player, sprite.Frames.Count)
                    : isPogo
                        ? PlayerEntity.GetCivviePogoSpriteFrameIndex(player, sprite.Frames.Count)
                        : player.IsTaunting
                            ? GetTauntSpriteFrameIndex(player, sprite.Frames.Count)
                            : bodySelection.IsHumiliated
                                ? GetHumiliationSpriteFrameIndex(player, bodySelection.AnimationImage, sprite.Frames.Count)
                                : GetPlayerBodySpriteFrameIndex(bodySelection.AnimationImage, sprite.Frames.Count);
            var roundedOrigin = GetRoundedPlayerSpriteOrigin(renderPosition);
            var bodyYOffset = isHeavyEating || player.IsTaunting || isPogo ? 0f : bodySelection.BodyYOffset * playerScale;
            var position = new Vector2(roundedOrigin.X - cameraPosition.X, roundedOrigin.Y + bodyYOffset - cameraPosition.Y);
            _game.DrawSpriteFrameOutline(sprite.Frames[frameIndex], position, outlineTint, 0f, sprite.Origin.ToVector2(), scale);
            return true;
        }

        public PlayerBodySpriteSelection GetPlayerBodySpriteSelection(PlayerEntity player)
        {
            if (_game.IsBackstabReplacementRenderActive(player))
            {
                return new PlayerBodySpriteSelection(
                    GetPresentationSpriteName(player, static presentation => presentation.StandSuffix ?? presentation.BaseSuffix, "StandS"),
                    0f,
                    0f,
                    0f,
                    player.IsCarryingIntel,
                    false);
            }

            var renderState = _game._playerRenderStates.GetValueOrDefault(_game.GetPlayerStateKey(player));
            var animationHorizontalSpeed = renderState?.AnimationHorizontalSpeed ?? player.HorizontalSpeed;
            var horizontalSourceStepSpeed = GetPlayerAnimationSourceStepSpeed(animationHorizontalSpeed);
            var animationImage = WrapAnimationImage(renderState?.BodyAnimationImage ?? 0f, _game.GetPlayerBodyAnimationLength(player, horizontalSourceStepSpeed));
            var appearsAirborne = renderState?.AppearsAirborne ?? !player.IsGrounded;

            if (_game.TryGetLastToDieHaxtonSpriteName(player, out var haxtonSpriteName))
            {
                return new PlayerBodySpriteSelection(haxtonSpriteName, animationImage, 0f, 0f, false, false);
            }

            if (_game._world.IsPlayerHumiliated(player))
            {
                return new PlayerBodySpriteSelection(GetPresentationSpriteName(player, static presentation => presentation.HumiliationSuffix ?? presentation.BaseSuffix, "HS"), animationImage, 0f, 0f, false, true);
            }

            if (player.ClassId == PlayerClass.Sniper && player.IsSniperScoped)
            {
                return new PlayerBodySpriteSelection(GetPresentationSpriteName(player, static presentation => presentation.ScopedSuffix ?? presentation.BaseSuffix, "CrouchS"), WrapAnimationImage(animationImage, 2f), 0f, 0f, false, false);
            }

            string? spriteName;
            var bodyYOffset = 0f;
            var isRunSprite = false;
            var isHeavySlowWalk = false;
            if (appearsAirborne)
            {
                spriteName = GetPresentationSpriteName(player, static presentation => presentation.JumpSuffix ?? presentation.BaseSuffix, "JumpS");
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
                spriteName = GetWalkSpriteName(player);
                isHeavySlowWalk = true;
            }
            else
            {
                spriteName = GetRunSpriteName(player);
                isRunSprite = true;
            }

            var equipmentOffset = bodyYOffset;
            if (isRunSprite && !appearsAirborne)
            {
                var frame = (int)System.MathF.Floor(animationImage) % 8;
                if (IsRunEquipmentLowerFrame(frame))
                {
                    equipmentOffset -= 2f;
                }
            }

            if (isRunSprite && !appearsAirborne && player.ClassId == PlayerClass.Scout)
            {
                bodyYOffset -= 1f;
                equipmentOffset += 1f;
            }

            if (isHeavySlowWalk)
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

        public static float GetPlayerFacingScale(PlayerEntity player)
        {
            return IsFacingLeftByAim(player) ? -1f : 1f;
        }

        public static bool IsFacingLeftByAim(PlayerEntity player)
        {
            var radians = System.MathF.PI * player.AimDirectionDegrees / 180f;
            return System.MathF.Cos(radians) < 0f;
        }

        public static string? GetTauntSpriteName(PlayerEntity player) => GetPresentationSpriteName(player, static presentation => presentation.TauntSuffix ?? presentation.BaseSuffix, "TauntS");
        public static string? GetPogoSpriteName(PlayerEntity player) => GetPresentationSpriteName(player, static presentation => presentation.PogoSuffix ?? presentation.BaseSuffix, "PogoS");
        public static string? GetPogoTrickSpriteName(PlayerEntity player) => GetPresentationSpriteName(player, static presentation => presentation.PogoTrickSuffix ?? presentation.PogoSuffix ?? presentation.BaseSuffix, "PogoTrickS");

        public static string? GetPogoIntelSpriteName(PlayerEntity player) => GetPresentationSpriteName(player, static presentation => presentation.PogoIntelSuffix ?? "PogoIntelS", "PogoIntelS");
        public static string? GetHeavyEatSpriteName(PlayerEntity player) => player.ClassId == PlayerClass.Heavy ? GetPresentationSpriteName(player, static presentation => presentation.HeavyEatSuffix ?? presentation.BaseSuffix, "OmnomnomnomS") : null;
        public static string? GetPlayerSpriteName(PlayerEntity player) => GetPresentationSpriteName(player, static presentation => presentation.BaseSuffix, "S");
        public static string? GetPlayerSpriteName(PlayerClass classId, PlayerTeam team) => GetPresentationSpriteName(classId, team, static presentation => presentation.BaseSuffix, "S");
        public static string? GetRunSpriteName(PlayerEntity player) => GetPresentationSpriteName(player, static presentation => presentation.RunSuffix ?? presentation.BaseSuffix, "RunS");

        /// <summary>
        /// Run equipment is drawn 2px lower on the down-bob frames of the run cycle.
        /// Run sprites were realigned so frame 0 matches the intended cycle start; down-bob is on {0,1,4,5}.
        /// </summary>
        public static bool IsRunEquipmentLowerFrame(int frameIndex)
        {
            var frame = ((frameIndex % 8) + 8) % 8;
            return frame is 0 or 1 or 4 or 5;
        }
        public static string? GetWalkSpriteName(PlayerEntity player) => GetPresentationSpriteName(player, static presentation => presentation.WalkSuffix ?? presentation.RunSuffix ?? presentation.BaseSuffix, "WalkS");
        public static string? GetHudStandingSpriteName(PlayerEntity player) => GetPresentationSpriteName(player, static presentation => presentation.StandSuffix ?? presentation.BaseSuffix, "StandS");

        public static string? GetDeadBodySpriteName(PlayerClass classId, PlayerTeam team, DeadBodyAnimationKind animationKind = DeadBodyAnimationKind.Default)
        {
            if (animationKind == DeadBodyAnimationKind.Decapitated)
            {
                return ExperimentalDemoknightCatalog.GetDecapitatedDeadBodySpriteName(classId, team);
            }

            return GetPresentationSpriteName(classId, team, static presentation => presentation.DeadSuffix ?? presentation.BaseSuffix, "DeadS");
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

        private float GetRenderFacingScale(PlayerEntity player)
        {
            if (_game.IsBackstabReplacementRenderActive(player))
            {
                var radians = System.MathF.PI * _game.GetBackstabReplacementDirectionDegrees(player) / 180f;
                return System.MathF.Cos(radians) < 0f ? -1f : 1f;
            }
            if (ReferenceEquals(player, _game._world.LocalPlayer)
                && _game._useLocalWeaponRotation
                && _game.TryGetLocalPlayerAimDirection(player, out var aimDirectionDegrees))
            {
                var radians = System.MathF.PI * aimDirectionDegrees / 180f;
                var cos = System.MathF.Cos(radians);
                if (System.MathF.Abs(cos) < 0.001f)
                {
                    return player.FacingDirectionX < 0f ? -1f : 1f;
                }
                return cos < 0f ? -1f : 1f;
            }
            return GetPlayerFacingScale(player);
        }

        private void DrawIntelUnderlaySpriteCore(PlayerEntity player, Vector2 cameraPosition, Color tint, Vector2 scale, PlayerBodySpriteSelection bodySelection, Vector2 roundedOrigin)
        {
            var spriteName = GetPresentationSpriteName(player, static presentation => presentation.IntelSuffix ?? presentation.BaseSuffix, "IntelS");
            if (spriteName is null)
            {
                return;
            }

            var sprite = _game.GetResolvedSprite(spriteName);
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

        private void DrawPogoIntelUnderlaySprite(
            PlayerEntity player,
            Vector2 cameraPosition,
            Color tint,
            Vector2 scale,
            Vector2 roundedOrigin,
            int frameIndex)
        {
            var spriteName = GetPogoIntelSpriteName(player);
            if (spriteName is null)
            {
                return;
            }

            var sprite = _game.GetResolvedSprite(spriteName);
            if (sprite is null || sprite.Frames.Count == 0)
            {
                return;
            }

            var clampedFrameIndex = Math.Clamp(frameIndex, 0, sprite.Frames.Count - 1);
            _game.DrawSpriteFrameWithOptionalShadow(
                sprite.Frames[clampedFrameIndex],
                new Vector2(roundedOrigin.X - cameraPosition.X, roundedOrigin.Y - cameraPosition.Y),
                tint,
                0f,
                sprite.Origin.ToVector2(),
                scale);
        }

        private void DrawCarriedIntelTimerSpriteCore(PlayerEntity player, Vector2 cameraPosition, Vector2 roundedOrigin)
        {
            var timerSprite = _game.GetResolvedSprite("IntelTimerS");
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
            if (OperatingSystem.IsBrowser())
            {
                return GetPresentationSpriteName(player, static presentation => presentation.StandSuffix ?? presentation.BaseSuffix, "StandS");
            }

            var leanDirection = GetPlayerLeanDirection(player);
            if (leanDirection == LeanDirection.None)
            {
                return GetPresentationSpriteName(player, static presentation => presentation.StandSuffix ?? presentation.BaseSuffix, "StandS");
            }

            var facingLeft = IsFacingLeftByAim(player);
            return leanDirection switch
            {
                LeanDirection.Left => GetPresentationFacingSpriteName(
                    player,
                    static presentation => presentation.LeanRightSuffix ?? presentation.BaseSuffix,
                    static presentation => presentation.LeanLeftSuffix ?? presentation.BaseSuffix,
                    facingLeft,
                    "LeanRS",
                    "LeanLS"),
                LeanDirection.Right => GetPresentationFacingSpriteName(
                    player,
                    static presentation => presentation.LeanLeftSuffix ?? presentation.BaseSuffix,
                    static presentation => presentation.LeanRightSuffix ?? presentation.BaseSuffix,
                    facingLeft,
                    "LeanLS",
                    "LeanRS"),
                _ => GetPresentationSpriteName(player, static presentation => presentation.StandSuffix ?? presentation.BaseSuffix, "StandS"),
            };
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

            return SimpleLevelBarrierCollision.BlocksPointForPlayer(
                _game._world.Level,
                player.Team,
                player.IsCarryingIntel,
                x,
                y);
        }

        private static string? GetTeamSpriteName(PlayerClass classId, PlayerTeam team, string suffix)
        {
            var prefix = GetPresentationSpritePrefix(classId) ?? GetPlayerSpritePrefix(classId);
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

        private static string? GetTeamSpriteName(PlayerEntity player, string suffix)
        {
            var prefix = GetPresentationSpritePrefix(player.GameplayClassId) ?? GetPlayerSpritePrefix(player.ClassId);
            if (prefix is null)
            {
                return null;
            }

            var teamName = player.Team switch
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

        private static string? GetPresentationSpriteName(
            PlayerClass classId,
            PlayerTeam team,
            Func<GameplayClassPresentationDefinition, string> suffixSelector,
            string legacySuffix)
        {
            var presentation = GetClassPresentation(classId);
            return GetTeamSpriteName(classId, team, presentation is null ? legacySuffix : suffixSelector(presentation));
        }

        private static string? GetPresentationSpriteName(
            PlayerEntity player,
            Func<GameplayClassPresentationDefinition, string> suffixSelector,
            string legacySuffix)
        {
            var presentation = GetClassPresentation(player.GameplayClassId);
            return GetTeamSpriteName(player, presentation is null ? legacySuffix : suffixSelector(presentation));
        }

        private static string? GetPresentationFacingSpriteName(
            PlayerClass classId,
            PlayerTeam team,
            Func<GameplayClassPresentationDefinition, string> facingLeftSuffixSelector,
            Func<GameplayClassPresentationDefinition, string> facingRightSuffixSelector,
            bool facingLeft,
            string legacyFacingLeftSuffix,
            string legacyFacingRightSuffix)
        {
            var presentation = GetClassPresentation(classId);
            return GetTeamSpriteName(
                classId,
                team,
                presentation is null
                    ? (facingLeft ? legacyFacingLeftSuffix : legacyFacingRightSuffix)
                    : (facingLeft ? facingLeftSuffixSelector(presentation) : facingRightSuffixSelector(presentation)));
        }

        private static string? GetPresentationFacingSpriteName(
            PlayerEntity player,
            Func<GameplayClassPresentationDefinition, string> facingLeftSuffixSelector,
            Func<GameplayClassPresentationDefinition, string> facingRightSuffixSelector,
            bool facingLeft,
            string legacyFacingLeftSuffix,
            string legacyFacingRightSuffix)
        {
            var presentation = GetClassPresentation(player.GameplayClassId);
            return GetTeamSpriteName(
                player,
                presentation is null
                    ? (facingLeft ? legacyFacingLeftSuffix : legacyFacingRightSuffix)
                    : (facingLeft ? facingLeftSuffixSelector(presentation) : facingRightSuffixSelector(presentation)));
        }

        private static GameplayClassPresentationDefinition? GetClassPresentation(PlayerClass classId)
        {
            return CharacterClassCatalog.RuntimeRegistry.GetClassDefinition(classId).Presentation;
        }

        private static GameplayClassPresentationDefinition? GetClassPresentation(string gameplayClassId)
        {
            return CharacterClassCatalog.RuntimeRegistry.GetClassDefinition(gameplayClassId).Presentation;
        }

        private static string? GetPresentationSpritePrefix(PlayerClass classId)
        {
            return GetClassPresentation(classId)?.SpritePrefix;
        }

        private static string? GetPresentationSpritePrefix(string gameplayClassId)
        {
            return GetClassPresentation(gameplayClassId)?.SpritePrefix;
        }
    }
}
