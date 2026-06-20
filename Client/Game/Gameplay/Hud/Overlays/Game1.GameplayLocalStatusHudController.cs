#nullable enable

using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Microsoft.Xna.Framework;
using OpenGarrison.ClientShared;
using OpenGarrison.Core;
using OpenGarrison.GameplayModding;

namespace OpenGarrison.Client;

public partial class Game1
{
    private const float PortraitRumbleDurationSeconds = 0.24f;
    private const float LowHealthHudThresholdMaxHealthDivisor = 3.5f;
    private const float DamageVignettePersistentHealthFraction = 0.45f;
    private const float DamageVignetteFadeInPerSecond = 8f;
    private const float DamageVignetteFadeOutPerSecond = 8.0f;
    private const float DamageVignetteReactiveFadePerSecond = 1.0f;
    private const float DamageVignetteReactiveMinimumIntensity = 0.34f;
    private const float DamageVignetteReactiveMaximumIntensity = 0.88f;
    private const float DamageVignetteReactiveDamageScale = 120f;
    private const float DamageVignettePersistentMinimumIntensity = 0.24f;
    private const float DamageVignettePersistentMaximumIntensity = 1f;
    private const float DamageVignetteMinimumVisibleIntensity = 0.002f;
    private const float DamageVignetteDrawAlphaFadeThreshold = 0.12f;
    private static readonly Color PortraitRumbleTintColor = new(255, 80, 80);

    private void TriggerLocalHudPortraitDamageFeedback(int damageAmount)
    {
        if (!_portraitRumbleEnabled || damageAmount <= 0)
        {
            return;
        }

        _portraitRumbleSeed += 1;
        _portraitRumbleRemainingSeconds = PortraitRumbleDurationSeconds;
        _portraitRumbleIntensity = Math.Clamp(0.65f + (damageAmount / 75f), 0.75f, 1.35f);
    }

    private void TriggerLocalHudDamageVignette(int damageAmount)
    {
        if (!_damageVignetteEnabled || damageAmount <= 0)
        {
            return;
        }

        var addedIntensity = Math.Clamp(
            DamageVignetteReactiveMinimumIntensity + (damageAmount / DamageVignetteReactiveDamageScale),
            DamageVignetteReactiveMinimumIntensity,
            DamageVignetteReactiveMaximumIntensity);
        _damageVignetteFlashIntensity = Math.Clamp(
            _damageVignetteFlashIntensity + addedIntensity,
            0f,
            DamageVignetteReactiveMaximumIntensity);
        _damageVignetteIntensity = Math.Max(_damageVignetteIntensity, _damageVignetteFlashIntensity);
    }

    private sealed class GameplayLocalStatusHudController
    {
        private const string CoreReplicatedOwnerId = "core.player";
        private const string SoldierShotgunAvailableKey = "soldier_shotgun_available";
        private const string SoldierShotgunAmmoKey = "soldier_shotgun_ammo";
        private const string SoldierShotgunMaxAmmoKey = "soldier_shotgun_max_ammo";
        private const string DemomanGrenadeLauncherAmmoKey = "demoman_gl_ammo";
        private const string DemomanGrenadeLauncherMaxAmmoKey = "demoman_gl_max_ammo";
        private const string ScoutNailgunAmmoKey = "scout_nailgun_ammo";
        private const string ScoutNailgunMaxAmmoKey = "scout_nailgun_max_ammo";
        private const string ScoutNailgunAvailableKey = "scout_nailgun_available";

        private static readonly Color AmmoHudBarColor = new(217, 217, 183);
        private static readonly Color AmmoHudTextColor = new(245, 235, 210);
        private static readonly Color LowAmmoHudColor = new(255, 0, 0);
        private static readonly Color DisabledAmmoHudColor = new(128, 128, 128);
        private static readonly Color HeavyCooldownHudColor = new(50, 50, 50);
        private const float AmmoCountBuildScale = 0.75f;
        private const float SourceHudWidth = 800f;
        private const float SourceHudHeight = 600f;
        private const float SourceAmmoHudBaseY = SourceHudHeight / 1.26f;
        private const float SourceMainAmmoHudY = SourceAmmoHudBaseY + 86f;
        private const float SourceAbilityHudX = 730f;
        private const float SourceAbilityHudY = 515f;
        private const string DefaultAbilityHudSpriteName = "StickyCounterS";
        private const float DefaultAbilityHudSourceX = SourceAbilityHudX + 5f;
        private const float AbilityCooldownBarSourceX = SourceAbilityHudX - 15f;
        private const float StickyCounterCountSourceX = DefaultAbilityHudSourceX - 18f;
        private const float StickyCounterMaxSourceX = DefaultAbilityHudSourceX - 5f;
        private const float DefaultAbilityHudSpriteScale = 3f;
        private const float DefaultAbilityHudSpriteWidth = 22f;
        private const float DefaultAbilityHudSpriteHeight = 15f;
        private const float DefaultAbilityHudSpriteOriginX = 11f;
        private const float DefaultAbilityHudSpriteOriginY = 7f;
        private const float DefaultAbilityHudWidth = DefaultAbilityHudSpriteWidth * DefaultAbilityHudSpriteScale;
        private const float DefaultAbilityHudHeight = DefaultAbilityHudSpriteHeight * DefaultAbilityHudSpriteScale;
        private const float AbilityHudPlaqueDrawScale = 2f;
        private const float AbilityHudPlaqueSpriteWidth = 38f;
        private const float AbilityHudPlaqueSpriteHeight = 28f;
        private const float AbilityHudPlaqueOriginX = 19f;
        private const float AbilityHudPlaqueOriginY = 14f;
        private const float AbilityHudPlaqueWidth = AbilityHudPlaqueSpriteWidth * AbilityHudPlaqueDrawScale;
        private const float AbilityHudPlaqueHeight = AbilityHudPlaqueSpriteHeight * AbilityHudPlaqueDrawScale;
        private const float WeaponHudPanelScale = 2.4f;
        private const float WeaponHudPanelGapPixels = 4f;
        private const float AbilityHudWidgetGapPixels = 10f;
        private const float WeaponHudFallbackPanelHeight = 38f;
        private const int WeaponHudOrderAcquired = 10;
        private const int WeaponHudOrderStowedPrimary = 20;
        private const int WeaponHudOrderUtility = 40;
        private const int WeaponHudOrderSecondary = 60;
        private const int WeaponHudOrderAbility = 80;
        private const int WeaponHudOrderPrimary = 100;
        private const int WeaponHudOrderPostPrimaryAbility = 110;

        private readonly Game1 _game;
        private Vector2 _sourceHudOffset;
        private Vector2 _sourceHudScaleOrigin;
        private float _sourceHudScale = 1f;

        private sealed record WeaponHudRow(
            string Id,
            float Height,
            Action<float> Draw,
            float? LegacySourceY = null,
            bool ReservesFlowSpace = true,
            int Order = 0,
            AbilityHudWidgetMetrics? AbilityMetrics = null,
            Vector2? EditorBoundsOffset = null,
            Vector2? EditorBoundsSize = null);

        private sealed record AbilityHudWidgetMetrics(
            float SourceX,
            string? SpriteName,
            float SpriteScale,
            Vector2 FallbackBoundsOffset,
            Vector2 FallbackBoundsSize);

        private sealed record AbilityHudWidget(
            string ElementId,
            WeaponHudRow Row,
            float SourceX,
            float SourceY,
            int LayerOffset,
            AbilityHudWidgetMetrics Metrics);

        public GameplayLocalStatusHudController(Game1 game)
        {
            _game = game;
        }

        public void DrawLocalHealthHud()
        {
            if (!_game._world.LocalPlayer.IsAlive)
            {
                return;
            }

            var viewportHeight = _game.ViewportHeight;
            var hudScale = 1f;
            var scale = new Vector2(2f, 2f);
            
            // Align base position to sprite pixel grid (2-pixel boundaries)
            var basePosition = new Vector2(
                MathF.Round(5f / scale.X) * scale.X,
                MathF.Round((viewportHeight - 75f) / scale.Y) * scale.Y
            );
            if (!_game.TryResolveHudElement(HudElementId.LocalHealth, out var resolved))
            {
                return;
            }

            hudScale = resolved.Layout.Scale;
            scale *= hudScale;
            basePosition = resolved.Origin;
            basePosition = new Vector2(
                MathF.Round(basePosition.X / scale.X) * scale.X,
                MathF.Round(basePosition.Y / scale.Y) * scale.Y);
            var portraitPosition = basePosition;
            var portraitColor = Color.White;
            UpdateLocalHudPortraitDamageFeedback(ref portraitPosition, ref portraitColor);
            
            // Draw team-colored base health sprite
            var healthSpriteName = _game._world.LocalPlayer.Team == PlayerTeam.Blue ? "PlayerHealthBlu" : "PlayerHealthRed";
            _game.TryDrawScreenSprite(healthSpriteName, 0, portraitPosition, portraitColor, scale);
            
            // Get background health sprite dimensions for masking
            var backgroundHealthSprite = _game.GetResolvedSprite(healthSpriteName);
            
            // Calculate cross position (move half width to the right)
            var crossSprite = _game.GetResolvedSprite("PlayerHealthCross");
            var crossOffsetX = 20f;
            if (crossSprite is not null && crossSprite.Frames.Count > 0)
            {
                crossOffsetX += (crossSprite.Frames[0].Width * scale.X) / 2f;
            }
            var crossPosition = basePosition + new Vector2(crossOffsetX - 4f, -2f);
            
            // Draw character sprite centered on background health sprite (always use basic standing sprite)
            var characterSpriteName = GameplayPlayerSpriteRenderController.GetHudStandingSpriteName(_game._world.LocalPlayer);
            if (characterSpriteName is not null && backgroundHealthSprite is not null && backgroundHealthSprite.Frames.Count > 0)
            {
                var characterSprite = _game.GetResolvedSprite(characterSpriteName);
                if (characterSprite is not null && characterSprite.Frames.Count > 0)
                {
                    var characterScale = scale;
                    var characterWidth = characterSprite.Frames[0].Width;
                    var characterHeight = characterSprite.Frames[0].Height;
                    var backgroundWidth = backgroundHealthSprite.Frames[0].Width * scale.X;
                    var backgroundHeight = backgroundHealthSprite.Frames[0].Height * scale.Y;
                    
                    // Calculate center of background sprite
                    var backgroundCenterX = portraitPosition.X + (backgroundWidth / 2f);
                    var backgroundCenterY = portraitPosition.Y + (backgroundHeight / 2f);
                    
                    // Account for sprite origin when centering
                    var spriteOrigin = characterSprite.Origin.ToVector2();
                    
                    // Calculate horizontal centering based on opaque bounds (trim transparent padding)
                    float characterCenterOffsetX;
                    var opaqueBounds = characterSprite.Frames[0].OpaqueBounds;
                    if (opaqueBounds.HasValue)
                    {
                        var opaqueWidth = opaqueBounds.Value.Width;
                        var opaqueCenterX = opaqueBounds.Value.X + opaqueWidth / 2f;
                        characterCenterOffsetX = (opaqueCenterX - spriteOrigin.X) * characterScale.X;
                    }
                    else
                    {
                        // Fallback to full sprite width if opaque bounds not available
                        characterCenterOffsetX = (characterWidth / 2f - spriteOrigin.X) * characterScale.X;
                    }
                    
                    var characterCenterOffsetY = (characterHeight / 2f - spriteOrigin.Y) * characterScale.Y;
                    
                    // Move up by quarter of character height
                    var upwardOffset = (characterHeight * characterScale.Y) / 4f;
                    
                    // Position where origin should be to center the sprite
                    // Round to sprite pixel grid (every 2 screen pixels) to align with HUD
                    var characterPosition = new Vector2(
                        MathF.Round((backgroundCenterX - characterCenterOffsetX - 11f) / scale.X) * scale.X,
                        MathF.Round((backgroundCenterY - characterCenterOffsetY - upwardOffset + 11f) / scale.Y) * scale.Y
                    );
                    
                    // Calculate masking - mask from bottom up to 1 sprite pixel into background sprite from its bottom
                    // Align to sprite pixel grid
                    var maskLineY = portraitPosition.Y + MathF.Round((backgroundHealthSprite.Frames[0].Height - 2f) * scale.Y);
                    var spriteBottomY = characterPosition.Y + (characterHeight - spriteOrigin.Y) * characterScale.Y;
                    var amountToMaskFromBottom = spriteBottomY - maskLineY;
                    
                    if (amountToMaskFromBottom > 0)
                    {
                        // Mask the bottom portion
                        var maskHeightUnscaled = amountToMaskFromBottom / characterScale.Y;
                        var visibleHeight = (int)(characterHeight - maskHeightUnscaled);
                        
                        if (visibleHeight > 0)
                        {
                            var sourceRect = new Rectangle(0, 0, characterWidth, visibleHeight);
                            _game.TryDrawScreenSpritePart(characterSpriteName, 0, sourceRect, characterPosition, portraitColor, characterScale);
                        }
                    }
                    else
                    {
                        // No masking needed
                        _game.TryDrawScreenSprite(characterSpriteName, 0, characterPosition, portraitColor, characterScale);
                    }
                    
                    // Draw weapon sprite for the character (static, always facing right like HUD sprite)
                    var localPlayer = _game._world.LocalPlayer;
                    var weaponAnimationMode = _game._gameplayWeaponRenderController.GetPlayerWeaponAnimationMode(localPlayer);
                    var forceCivvieUmbrellaPresentation = weaponAnimationMode is WeaponAnimationMode.CivvieUmbrellaOpening
                        or WeaponAnimationMode.CivvieUmbrellaHold
                        or WeaponAnimationMode.CivvieUmbrellaClosing;
                    if (localPlayer.IsCivvieUmbrellaActive
                        && localPlayer.HasSecondaryBehavior(BuiltInGameplayBehaviorIds.CivvieUmbrella)
                        && weaponAnimationMode is not WeaponAnimationMode.CivvieUmbrellaOpening
                            and not WeaponAnimationMode.CivvieUmbrellaHold
                            and not WeaponAnimationMode.CivvieUmbrellaClosing)
                    {
                        weaponAnimationMode = WeaponAnimationMode.CivvieUmbrellaHold;
                        forceCivvieUmbrellaPresentation = true;
                    }

                    var weaponDefinition = GameplayWeaponRenderController.GetWeaponRenderDefinitionProxy(
                        localPlayer,
                        forceCivvieUmbrellaPresentation);
                    if (weaponDefinition.NormalSpriteName is not null)
                    {
                        var weaponSprite = _game.GetResolvedSprite(weaponDefinition.NormalSpriteName);
                        if (weaponSprite is not null && weaponSprite.Frames.Count > 0)
                        {
                            var weaponAnchorOrigin = _game._gameplayWeaponRenderController.GetWeaponAnchorOrigin(weaponDefinition, weaponSprite);
                            var facingScale = 1f; // Always face right, matching HUD sprite
                            
                            // Calculate weapon position relative to character position
                            // Character position is where the character's origin is placed
                            var weaponX = characterPosition.X + ((weaponDefinition.XOffset + weaponAnchorOrigin.X) * facingScale * characterScale.X);
                            var weaponY = characterPosition.Y + ((weaponDefinition.YOffset + weaponAnchorOrigin.Y) * characterScale.Y);
                            
                            var weaponPosition = new Vector2(weaponX, weaponY);
                            var weaponScale = new Vector2(facingScale * characterScale.X, characterScale.Y);
                            var weaponFrameIndex = _game._gameplayWeaponRenderController.GetWeaponSpriteFrameIndex(
                                localPlayer,
                                weaponAnimationMode,
                                weaponDefinition,
                                weaponSprite.Frames.Count);

                            _game.TryDrawScreenSprite(weaponDefinition.NormalSpriteName, weaponFrameIndex, weaponPosition, portraitColor, weaponScale);
                        }
                    }
                }
            }
            
            // Draw the background cross (unmasked)
            _game.TryDrawScreenSprite("PlayerHealthCross", 0, crossPosition, Color.White, scale);
            
            // Draw the medical cross with health-based masking and green overlay
            var healthPercent = MathF.Max(0f, (float)_game._world.LocalPlayer.Health / _game._world.LocalPlayer.MaxHealth);
            var medicineSprite = _game.GetResolvedSprite("PlayerHealthCrossInternalMedicine");
            if (medicineSprite is not null && medicineSprite.Frames.Count > 0)
            {
                var medicineHeight = medicineSprite.Frames[0].Height;
                var maskedHeight = (int)MathF.Ceiling(healthPercent * medicineHeight);
                
                if (maskedHeight > 0)
                {
                    // Mask from bottom up (full health = full height)
                    var sourceRect = new Rectangle(0, medicineHeight - maskedHeight, medicineSprite.Frames[0].Width, maskedHeight);
                    var drawPosition = crossPosition + new Vector2(0f, (medicineHeight - maskedHeight) * scale.Y);
                    
                    _game.TryDrawScreenSpritePart("PlayerHealthCrossInternalMedicine", 0, sourceRect, drawPosition, Color.Green, scale);
                }
            }
            
            // Draw health text centered on top of the crosses
            var health = Math.Max(_game._world.LocalPlayer.Health, 0);
            var hpColor = _game._lowHealthColorMode == LowHealthColorMode.Red && IsLocalHudLowHealth(_game._world.LocalPlayer)
                ? Color.Red
                : Color.White;
            var healthTextPosition = crossPosition + new Vector2((crossSprite?.Frames[0].Width ?? 0) * scale.X / 2f, (crossSprite?.Frames[0].Height ?? 0) * scale.Y / 2f);
            _game.DrawHudTextCentered(health.ToString(CultureInfo.InvariantCulture), healthTextPosition, hpColor, 1f * hudScale);
            UpdateLocalHealthHudBounds(portraitPosition, scale, backgroundHealthSprite, crossPosition, crossSprite);
        }

        private void UpdateLocalHealthHudBounds(
            Vector2 portraitPosition,
            Vector2 scale,
            LoadedGameMakerSprite? backgroundHealthSprite,
            Vector2 crossPosition,
            LoadedGameMakerSprite? crossSprite)
        {
            var minX = float.PositiveInfinity;
            var minY = float.PositiveInfinity;
            var maxX = float.NegativeInfinity;
            var maxY = float.NegativeInfinity;

            if (backgroundHealthSprite is not null && backgroundHealthSprite.Frames.Count > 0)
            {
                var frame = backgroundHealthSprite.Frames[0];
                minX = Math.Min(minX, portraitPosition.X);
                minY = Math.Min(minY, portraitPosition.Y);
                maxX = Math.Max(maxX, portraitPosition.X + (frame.Width * scale.X));
                maxY = Math.Max(maxY, portraitPosition.Y + (frame.Height * scale.Y));
            }

            if (crossSprite is not null && crossSprite.Frames.Count > 0)
            {
                var frame = crossSprite.Frames[0];
                minX = Math.Min(minX, crossPosition.X);
                minY = Math.Min(minY, crossPosition.Y);
                maxX = Math.Max(maxX, crossPosition.X + (frame.Width * scale.X));
                maxY = Math.Max(maxY, crossPosition.Y + (frame.Height * scale.Y));
            }

            if (!float.IsFinite(minX)
                || !float.IsFinite(maxX)
                || !float.IsFinite(minY)
                || !float.IsFinite(maxY))
            {
                return;
            }

            _game.UpdateHudElementBounds(
                HudElementId.LocalHealth,
                new Rectangle(
                    (int)MathF.Floor(minX),
                    (int)MathF.Floor(minY),
                    Math.Max(1, (int)MathF.Ceiling(maxX - minX)),
                    Math.Max(1, (int)MathF.Ceiling(maxY - minY))));
        }

        public void DrawAmmoHud()
        {
            if (!_game._world.LocalPlayer.IsAlive)
            {
                return;
            }

            if (!_game.TryResolveHudElement(HudElementId.LocalWeaponStack, out var stack))
            {
                DrawAcquiredMedigunPrompt();
                return;
            }

            var previousSourceHudOffset = _sourceHudOffset;
            var previousSourceHudScaleOrigin = _sourceHudScaleOrigin;
            var previousSourceHudScale = _sourceHudScale;
            var stackOrigin = stack.Origin;
            var defaultStackOrigin = GetUnshiftedSourceHudPoint(728f, SourceMainAmmoHudY);
            _sourceHudOffset = stackOrigin - defaultStackOrigin;
            _sourceHudScaleOrigin = stackOrigin;
            _sourceHudScale = stack.Layout.Scale;
            try
            {
                DrawWeaponStackHud();
            }
            finally
            {
                _sourceHudOffset = previousSourceHudOffset;
                _sourceHudScaleOrigin = previousSourceHudScaleOrigin;
                _sourceHudScale = previousSourceHudScale;
            }
        }

        public void DrawAbilityHud()
        {
            if (!_game._world.LocalPlayer.IsAlive)
            {
                return;
            }

            var rows = BuildAbilityHudRows();
            if (rows.Count == 0)
            {
                return;
            }

            if (!_game.TryResolveHudElement(HudElementId.LocalAbilityStack, out var stack))
            {
                return;
            }

            var previousSourceHudOffset = _sourceHudOffset;
            var previousSourceHudScaleOrigin = _sourceHudScaleOrigin;
            var previousSourceHudScale = _sourceHudScale;
            var stackOrigin = stack.Origin;
            var defaultStackOrigin = GetUnshiftedSourceHudPoint(SourceAbilityHudX, SourceAbilityHudY);
            _sourceHudOffset = stackOrigin - defaultStackOrigin;
            _sourceHudScaleOrigin = stackOrigin;
            _sourceHudScale = stack.Layout.Scale;
            try
            {
                DrawAbilityStackHud(rows);
            }
            finally
            {
                _sourceHudOffset = previousSourceHudOffset;
                _sourceHudScaleOrigin = previousSourceHudScaleOrigin;
                _sourceHudScale = previousSourceHudScale;
            }
        }

        public void CollectAbilityHudElements(List<HudElementInstance> elements)
        {
            if (!_game._world.LocalPlayer.IsAlive)
            {
                return;
            }

            foreach (var widget in BuildAbilityHudWidgets())
            {
                _game.SetHudElementRuntimeDefault(CreateAbilityHudElementLayout(widget));
                elements.Add(new HudElementInstance(
                    widget.ElementId,
                    HudElementRendererId.LocalAbilityWidget,
                    HudElementLayerLocalAbilityStack + widget.LayerOffset));
            }
        }

        public void DrawAbilityHudElement(string id)
        {
            if (!_game._world.LocalPlayer.IsAlive)
            {
                return;
            }

            var widget = BuildAbilityHudWidgets()
                .FirstOrDefault(candidate => string.Equals(candidate.ElementId, id, StringComparison.Ordinal));
            if (widget is null || !_game.TryResolveHudElement(widget.ElementId, out var resolved))
            {
                return;
            }

            var previousSourceHudOffset = _sourceHudOffset;
            var previousSourceHudScaleOrigin = _sourceHudScaleOrigin;
            var previousSourceHudScale = _sourceHudScale;
            var defaultOrigin = GetUnshiftedSourceHudPoint(widget.SourceX, widget.SourceY);
            _sourceHudOffset = resolved.Origin - defaultOrigin;
            _sourceHudScaleOrigin = resolved.Origin;
            _sourceHudScale = resolved.Layout.Scale;
            try
            {
                widget.Row.Draw(widget.SourceY);
                _game.UpdateHudElementBounds(widget.ElementId, resolved.Layout.ResolveBounds(resolved.Origin));
            }
            finally
            {
                _sourceHudOffset = previousSourceHudOffset;
                _sourceHudScaleOrigin = previousSourceHudScaleOrigin;
                _sourceHudScale = previousSourceHudScale;
            }
        }

        public HudElementLayout CreateAbilityStackedHudElementLayout(
            string id,
            float sourceX,
            Vector2 boundsOffset,
            Vector2 boundsSize,
            int layer)
        {
            var sourceY = GetNextAbilityStackSourceY(boundsOffset, boundsSize);
            return new HudElementLayout(
                id,
                HudAnchor.BottomRight,
                new Vector2(sourceX - SourceHudWidth, sourceY - SourceHudHeight),
                boundsSize,
                boundsOffset,
                Layer: layer);
        }

        private void DrawWeaponStackHud()
        {
            var rows = BuildWeaponHudRows();
            DrawWeaponHudRows(rows);
            DrawAcquiredMedigunPrompt();
        }

        private void DrawAbilityStackHud(List<WeaponHudRow> rows)
        {
            DrawAbilityHudRows(rows);
        }

        private List<WeaponHudRow> BuildWeaponHudRows()
        {
            var rows = new List<WeaponHudRow>();
            if (_game._world.LocalPlayer.IsExperimentalDemoknightEnabled)
            {
                rows.Add(new WeaponHudRow(
                    "local.weapon.demoknight",
                    GetMainAmmoHudPanelHeight(),
                    _ => DrawDemoknightHud(),
                    SourceMainAmmoHudY,
                    Order: WeaponHudOrderPrimary,
                    EditorBoundsOffset: new Vector2(-64f, -70f),
                    EditorBoundsSize: new Vector2(132f, 98f)));
                return rows;
            }

            var displayedWeaponStats = GetLocalDisplayedMainWeaponStats();
            var hasGrenadeLauncher = HasLocalDemomanGrenadeLauncher();
            var showOnlyActiveWeapon = _game._hudShowOnlyActiveWeapon;
            var selectedOffhandItemId = IsLocalDisplayedOffhandWeaponSelected()
                ? GetLocalDisplayedOffhandPresentationItemId()
                : null;
            var selectedUtilityItem = string.Equals(
                selectedOffhandItemId,
                _game._world.LocalPlayer.GameplayLoadoutState.UtilityItemId,
                StringComparison.Ordinal);
            if (!showOnlyActiveWeapon && hasGrenadeLauncher && !selectedUtilityItem)
            {
                var utilityItem = TryGetLocalUtilityHudItem(out var resolvedUtilityItem)
                    ? resolvedUtilityItem
                    : null;
                var utilityHud = utilityItem?.Presentation.Hud;
                var utilityHudSpriteName = utilityItem?.Presentation.HudSpriteName ?? "GrenadeLauncherAmmoS";
                rows.Add(new WeaponHudRow(
                    "local.weapon.utility",
                    GetHudSpriteFrameHeight(utilityHudSpriteName, WeaponHudPanelScale, WeaponHudFallbackPanelHeight),
                    sourceY =>
                    {
                        if (utilityItem is not null && IsWeaponAmmoPanelHud(utilityHud))
                        {
                            DrawConfiguredItemHudPanel(utilityItem, sourceY);
                            return;
                        }

                        DrawGrenadeLauncherHud(sourceY);
                    },
                    Order: GetHudRowOrder(utilityHud, WeaponHudOrderUtility)));
            }

            AddPrimaryWeaponHudRow(rows, displayedWeaponStats, hasGrenadeLauncher: false);

            if (!showOnlyActiveWeapon && selectedOffhandItemId is not null)
            {
                AddStowedPrimaryWeaponHudRow(rows);
            }

            if (!showOnlyActiveWeapon && IsLocalMedicKritzHealNeedlesPresented())
            {
                AddStowedPrimaryWeaponHudRow(rows);
            }

            if (!showOnlyActiveWeapon && ShouldDrawAcquiredWeaponHud())
            {
                var alternateItemId = GetLocalAlternatePrimaryWeaponPresentationItemId();
                var alternateItem = CharacterClassCatalog.RuntimeRegistry.GetRequiredItem(alternateItemId);
                rows.Add(new WeaponHudRow(
                    "local.weapon.acquired",
                    GetHudSpriteFrameHeight(alternateItem.Presentation.HudSpriteName, WeaponHudPanelScale, WeaponHudFallbackPanelHeight),
                    DrawAcquiredWeaponHudRow,
                    Order: WeaponHudOrderAcquired));
            }

            if (!showOnlyActiveWeapon && ShouldDrawSecondaryWeaponHudRow(out var secondaryItem))
            {
                rows.Add(new WeaponHudRow(
                    "local.weapon.secondary",
                    GetHudSpriteFrameHeight(secondaryItem.Presentation.HudSpriteName, WeaponHudPanelScale, WeaponHudFallbackPanelHeight),
                    DrawSecondaryWeaponHudRow,
                    Order: WeaponHudOrderSecondary));
            }

            if (!showOnlyActiveWeapon)
            {
                AddConfiguredUtilityHudRow(rows, hasGrenadeLauncher || selectedUtilityItem);
            }

            return rows;
        }

        private List<WeaponHudRow> BuildAbilityHudRows()
        {
            var rows = new List<WeaponHudRow>();
            var hasGrenadeLauncher = HasLocalDemomanGrenadeLauncher();

            if (_game._world.LocalPlayer.ClassId == PlayerClass.Demoman)
            {
                var stickyCount = CountLocalOwnedStickyMines();
                var maxStickies = Math.Max(1, _game._world.LocalPlayer.PrimaryWeapon.MaxAmmo);
                var frameIndex = _game._world.LocalPlayer.Team == PlayerTeam.Blue ? 1 : 0;
                rows.Add(new WeaponHudRow(
                    HudElementId.LocalAbility("class.demoman.sticky"),
                    GetHudSpriteFrameHeight("StickyCounterS", 3f, 36f),
                    sourceY => DrawStickyCounterHud(stickyCount, maxStickies, frameIndex, sourceY),
                    GetDemomanStickyAbilityHudSourceY(hasGrenadeLauncher),
                    Order: hasGrenadeLauncher ? WeaponHudOrderPostPrimaryAbility : WeaponHudOrderAbility,
                    AbilityMetrics: new AbilityHudWidgetMetrics(
                        DefaultAbilityHudSourceX,
                        "StickyCounterS",
                        3f,
                        new Vector2(-33f, -21f),
                        new Vector2(66f, 42f))));
            }

            AddConfiguredAbilityHudRows(rows);
            return rows;
        }

        private List<AbilityHudWidget> BuildAbilityHudWidgets()
        {
            var widgets = new List<AbilityHudWidget>();
            float? flowTopSourceY = null;
            foreach (var row in BuildAbilityHudRows().OrderBy(static row => row.Order))
            {
                if (row.AbilityMetrics is null)
                {
                    continue;
                }

                var (boundsOffset, boundsSize) = GetAbilityHudWidgetBounds(row.AbilityMetrics);
                var sourceY = row.LegacySourceY
                    ?? (flowTopSourceY.HasValue
                        ? flowTopSourceY.Value - AbilityHudWidgetGapPixels - boundsOffset.Y - boundsSize.Y
                        : SourceAbilityHudY);
                widgets.Add(new AbilityHudWidget(
                    HudElementId.LocalAbilitySlot(widgets.Count),
                    row,
                    row.AbilityMetrics.SourceX,
                    sourceY,
                    widgets.Count,
                    row.AbilityMetrics));

                if (row.ReservesFlowSpace)
                {
                    var topSourceY = sourceY + boundsOffset.Y;
                    flowTopSourceY = flowTopSourceY.HasValue
                        ? Math.Min(flowTopSourceY.Value, topSourceY)
                        : topSourceY;
                }
            }

            for (var dummyIndex = 0; dummyIndex < _game.GetHudEditorDummyAbilitySlotCount(); dummyIndex += 1)
            {
                var dummyRow = CreateDummyAbilityHudRow(widgets.Count);
                var metrics = dummyRow.AbilityMetrics!;
                var (boundsOffset, boundsSize) = GetAbilityHudWidgetBounds(metrics);
                var sourceY = flowTopSourceY.HasValue
                    ? flowTopSourceY.Value - AbilityHudWidgetGapPixels - boundsOffset.Y - boundsSize.Y
                    : SourceAbilityHudY;
                widgets.Add(new AbilityHudWidget(
                    HudElementId.LocalAbilitySlot(widgets.Count),
                    dummyRow,
                    metrics.SourceX,
                    sourceY,
                    widgets.Count,
                    metrics));

                var topSourceY = sourceY + boundsOffset.Y;
                flowTopSourceY = flowTopSourceY.HasValue
                    ? Math.Min(flowTopSourceY.Value, topSourceY)
                    : topSourceY;
            }

            return widgets;
        }

        private HudElementLayout CreateAbilityHudElementLayout(AbilityHudWidget widget)
        {
            var (boundsOffset, boundsSize) = GetAbilityHudWidgetBounds(widget.Metrics);
            return new HudElementLayout(
                widget.ElementId,
                HudAnchor.BottomRight,
                new Vector2(widget.SourceX - SourceHudWidth, widget.SourceY - SourceHudHeight),
                boundsSize,
                boundsOffset,
                Layer: HudElementLayerLocalAbilityStack + widget.LayerOffset);
        }

        private float GetNextAbilityStackSourceY(Vector2 boundsOffset, Vector2 boundsSize)
        {
            var flowTopSourceY = GetAbilityStackFlowTopSourceY();
            return flowTopSourceY - AbilityHudWidgetGapPixels - boundsOffset.Y - boundsSize.Y;
        }

        private float GetAbilityStackFlowTopSourceY()
        {
            float? flowTopSourceY = null;
            foreach (var widget in BuildAbilityHudWidgets())
            {
                if (!widget.Row.ReservesFlowSpace)
                {
                    continue;
                }

                var (boundsOffset, _) = GetAbilityHudWidgetBounds(widget.Metrics);
                var topSourceY = widget.SourceY + boundsOffset.Y;
                flowTopSourceY = flowTopSourceY.HasValue
                    ? Math.Min(flowTopSourceY.Value, topSourceY)
                    : topSourceY;
            }

            return flowTopSourceY ?? (SourceAbilityHudY + GetAbilityHudFallbackBoundsOffset(DefaultAbilityHudSpriteName).Y);
        }

        private (Vector2 BoundsOffset, Vector2 BoundsSize) GetAbilityHudWidgetBounds(AbilityHudWidgetMetrics metrics)
        {
            if (!string.IsNullOrWhiteSpace(metrics.SpriteName))
            {
                var sprite = _game.GetResolvedSprite(metrics.SpriteName);
                if (sprite is not null && sprite.Frames.Count > 0)
                {
                    var size = new Vector2(sprite.Frames[0].Width * metrics.SpriteScale, sprite.Frames[0].Height * metrics.SpriteScale);
                    var offset = -sprite.Origin.ToVector2() * metrics.SpriteScale;
                    return (offset, size);
                }
            }

            return (metrics.FallbackBoundsOffset, metrics.FallbackBoundsSize);
        }

        private WeaponHudRow CreateDummyAbilityHudRow(int slotIndex)
        {
            return new WeaponHudRow(
                $"local.ability.dummy.{slotIndex + 1}",
                DefaultAbilityHudHeight,
                DrawDummyAbilityHud,
                Order: WeaponHudOrderAbility + slotIndex,
                AbilityMetrics: new AbilityHudWidgetMetrics(
                    DefaultAbilityHudSourceX,
                    DefaultAbilityHudSpriteName,
                    DefaultAbilityHudSpriteScale,
                    GetAbilityHudFallbackBoundsOffset(DefaultAbilityHudSpriteName),
                    GetAbilityHudFallbackBoundsSize(DefaultAbilityHudSpriteName)));
        }

        private bool TryGetLocalUtilityHudItem(out GameplayItemDefinition item)
        {
            item = null!;
            var utilityItemId = _game._world.LocalPlayer.GameplayLoadoutState.UtilityItemId;
            if (string.IsNullOrWhiteSpace(utilityItemId))
            {
                return false;
            }

            item = CharacterClassCatalog.RuntimeRegistry.GetRequiredItem(utilityItemId);
            return true;
        }

        private void AddPrimaryWeaponHudRow(List<WeaponHudRow> rows, PrimaryWeaponDefinition displayedWeaponStats, bool hasGrenadeLauncher)
        {
            switch (displayedWeaponStats.Kind)
            {
                case PrimaryWeaponKind.FlameThrower:
                    rows.Add(new WeaponHudRow("local.weapon.primary", GetMainAmmoHudPanelHeight(), DrawPyroAmmoHudAt, SourceMainAmmoHudY, Order: WeaponHudOrderPrimary));
                    return;
                case PrimaryWeaponKind.Minigun:
                    rows.Add(new WeaponHudRow("local.weapon.primary", GetMainAmmoHudPanelHeight(), DrawHeavyAmmoHudAt, SourceMainAmmoHudY, Order: WeaponHudOrderPrimary));
                    return;
                case PrimaryWeaponKind.Blade:
                    rows.Add(new WeaponHudRow("local.weapon.primary", GetMainAmmoHudPanelHeight(), DrawQuoteAmmoHudAt, SourceMainAmmoHudY, Order: WeaponHudOrderPrimary));
                    return;
                case PrimaryWeaponKind.Rifle:
                    return;
                default:
                    rows.Add(new WeaponHudRow(
                        "local.weapon.primary",
                        GetMainAmmoHudPanelHeight(),
                        DrawStandardAmmoHudPanel,
                        hasGrenadeLauncher ? null : SourceMainAmmoHudY,
                        Order: WeaponHudOrderPrimary));
                    return;
            }
        }

        private void AddStowedPrimaryWeaponHudRow(List<WeaponHudRow> rows)
        {
            var primaryItemId = _game._world.LocalPlayer.GameplayLoadoutState.PrimaryItemId;
            var primaryItem = CharacterClassCatalog.RuntimeRegistry.GetRequiredItem(primaryItemId);
            rows.Add(new WeaponHudRow(
                "local.weapon.primary.stowed",
                GetHudSpriteFrameHeight(primaryItem.Presentation.HudSpriteName, WeaponHudPanelScale, WeaponHudFallbackPanelHeight),
                DrawStowedPrimaryWeaponHudRow,
                Order: WeaponHudOrderStowedPrimary));
        }

        private void AddConfiguredUtilityHudRow(List<WeaponHudRow> rows, bool hasGrenadeLauncher)
        {
            if (hasGrenadeLauncher)
            {
                return;
            }

            if (!TryGetLocalUtilityHudItem(out var utilityItem))
            {
                return;
            }

            var hud = utilityItem.Presentation.Hud;
            if (!IsWeaponAmmoPanelHud(hud)
                || string.IsNullOrWhiteSpace(utilityItem.Presentation.HudSpriteName))
            {
                return;
            }

            rows.Add(new WeaponHudRow(
                "local.weapon.utility",
                GetHudSpriteFrameHeight(utilityItem.Presentation.HudSpriteName, WeaponHudPanelScale, WeaponHudFallbackPanelHeight),
                sourceY => DrawConfiguredItemHudPanel(utilityItem, sourceY),
                Order: GetHudRowOrder(hud, WeaponHudOrderUtility)));
        }

        private void AddConfiguredAbilityHudRows(List<WeaponHudRow> rows)
        {
            foreach (var item in GetLocalConfiguredAbilityHudItems())
            {
                var hud = item.Presentation.Hud;
                if (hud is null
                    || !string.Equals(hud.StackGroup, GameplayItemHudStackGroups.Ability, StringComparison.OrdinalIgnoreCase)
                    || !IsAbilityCooldownHudStateProvider(hud.StateProvider)
                    || !IsAbilityMeterHud(hud))
                {
                    continue;
                }

                if (string.Equals(item.Ability?.ExecutorId, BuiltInGameplayBehaviorIds.HeavySandvich, StringComparison.Ordinal)
                    && IsLocalDisplayedMainWeaponAcquired())
                {
                    continue;
                }

                var hudSpriteName = GetAbilityCooldownHudSpriteName(item);
                var sourceX = GetAbilityHudSourceX(hudSpriteName);
                var spriteScale = GetAbilityHudSpriteScale(hudSpriteName);
                var height = GetAbilityCooldownHudHeight(hudSpriteName);
                rows.Add(new WeaponHudRow(
                    HudElementId.LocalAbility(item.Id),
                    height,
                    sourceY => DrawConfiguredAbilityCooldownHud(item, sourceY),
                    GetConfiguredAbilityLegacySourceY(item),
                    Order: GetHudRowOrder(hud, WeaponHudOrderAbility),
                    AbilityMetrics: new AbilityHudWidgetMetrics(
                        sourceX,
                        hudSpriteName,
                        spriteScale,
                        GetAbilityHudFallbackBoundsOffset(hudSpriteName),
                        GetAbilityHudFallbackBoundsSize(hudSpriteName))));
            }
        }

        private static bool IsAbilityCooldownHudStateProvider(string stateProvider)
        {
            return string.Equals(stateProvider, GameplayItemHudStateProviders.AbilityCooldown, StringComparison.OrdinalIgnoreCase)
                || string.Equals(stateProvider, GameplayItemHudStateProviders.HeavySandvichCooldown, StringComparison.OrdinalIgnoreCase)
                || string.Equals(stateProvider, GameplayItemHudStateProviders.HeavyGhostDashCooldown, StringComparison.OrdinalIgnoreCase)
                || string.Equals(stateProvider, GameplayItemHudStateProviders.SpySuperjumpCooldown, StringComparison.OrdinalIgnoreCase);
        }

        private float GetDemomanStickyAbilityHudSourceY(bool hasGrenadeLauncher)
        {
            if (!hasGrenadeLauncher)
            {
                return 522f;
            }

            var mineLauncherPanelHeight = GetMainAmmoHudPanelHeight();
            return SourceMainAmmoHudY - mineLauncherPanelHeight - WeaponHudPanelGapPixels - mineLauncherPanelHeight - WeaponHudPanelGapPixels;
        }

        private static float? GetConfiguredAbilityLegacySourceY(GameplayItemDefinition item)
        {
            return item.Ability?.ExecutorId switch
            {
                BuiltInGameplayBehaviorIds.HeavySandvich => SourceAbilityHudY,
                BuiltInGameplayBehaviorIds.SpySuperjump => SourceAbilityHudY,
                _ => null,
            };
        }

        private static string GetAbilityCooldownHudSpriteName(GameplayItemDefinition item)
        {
            return string.IsNullOrWhiteSpace(item.Presentation.HudSpriteName)
                ? DefaultAbilityHudSpriteName
                : item.Presentation.HudSpriteName!.Trim();
        }

        private float GetAbilityCooldownHudHeight(string hudSpriteName)
        {
            return GetHudSpriteFrameHeight(hudSpriteName, GetAbilityHudSpriteScale(hudSpriteName), GetAbilityHudFallbackBoundsSize(hudSpriteName).Y);
        }

        private static float GetAbilityHudSourceX(string spriteName)
        {
            return IsDefaultAbilityHudSprite(spriteName) ? DefaultAbilityHudSourceX : SourceAbilityHudX;
        }

        private static float GetAbilityHudSpriteScale(string spriteName)
        {
            return IsDefaultAbilityHudSprite(spriteName) ? DefaultAbilityHudSpriteScale : AbilityHudPlaqueDrawScale;
        }

        private static Vector2 GetAbilityHudFallbackBoundsOffset(string spriteName)
        {
            return IsDefaultAbilityHudSprite(spriteName)
                ? new Vector2(-DefaultAbilityHudSpriteOriginX * DefaultAbilityHudSpriteScale, -DefaultAbilityHudSpriteOriginY * DefaultAbilityHudSpriteScale)
                : new Vector2(-AbilityHudPlaqueOriginX * AbilityHudPlaqueDrawScale, -AbilityHudPlaqueOriginY * AbilityHudPlaqueDrawScale);
        }

        private static Vector2 GetAbilityHudFallbackBoundsSize(string spriteName)
        {
            return IsDefaultAbilityHudSprite(spriteName)
                ? new Vector2(DefaultAbilityHudWidth, DefaultAbilityHudHeight)
                : new Vector2(AbilityHudPlaqueWidth, AbilityHudPlaqueHeight);
        }

        private static bool IsDefaultAbilityHudSprite(string spriteName)
        {
            return string.Equals(spriteName, DefaultAbilityHudSpriteName, StringComparison.Ordinal)
                || string.Equals(spriteName, "CivvieUmbrellaAbilityHudS", StringComparison.Ordinal);
        }

        private IEnumerable<GameplayItemDefinition> GetLocalConfiguredAbilityHudItems()
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var itemId in new[]
            {
                _game._world.LocalPlayer.GameplayLoadoutState.SecondaryItemId,
                _game._world.LocalPlayer.GameplayLoadoutState.UtilityItemId,
            })
            {
                if (string.IsNullOrWhiteSpace(itemId) || !seen.Add(itemId))
                {
                    continue;
                }

                yield return CharacterClassCatalog.RuntimeRegistry.GetRequiredItem(itemId);
            }
        }

        private static bool IsAbilityMeterHud(GameplayItemHudPresentationDefinition hud)
        {
            return string.Equals(hud.DisplayKind, GameplayItemHudDisplayKinds.Meter, StringComparison.OrdinalIgnoreCase)
                || string.Equals(hud.DisplayKind, GameplayItemHudDisplayKinds.CooldownIcon, StringComparison.OrdinalIgnoreCase)
                || (string.Equals(hud.DisplayKind, GameplayItemHudDisplayKinds.Custom, StringComparison.OrdinalIgnoreCase)
                    && string.Equals(hud.WidgetId, GameplayItemHudWidgetIds.AbilityCooldownMeter, StringComparison.OrdinalIgnoreCase));
        }

        private static bool IsWeaponAmmoPanelHud(GameplayItemHudPresentationDefinition? hud)
        {
            return hud is not null
                && string.Equals(hud.StackGroup, GameplayItemHudStackGroups.Weapon, StringComparison.OrdinalIgnoreCase)
                && (string.Equals(hud.DisplayKind, GameplayItemHudDisplayKinds.AmmoPanel, StringComparison.OrdinalIgnoreCase)
                    || (string.Equals(hud.DisplayKind, GameplayItemHudDisplayKinds.Custom, StringComparison.OrdinalIgnoreCase)
                        && string.Equals(hud.WidgetId, GameplayItemHudWidgetIds.WeaponAmmoPanel, StringComparison.OrdinalIgnoreCase)));
        }

        private static int GetHudRowOrder(GameplayItemHudPresentationDefinition? hud, int fallbackOrder)
        {
            return hud is null || hud.Order == 0 ? fallbackOrder : hud.Order;
        }

        private static bool IsSecondaryAmmoHudItem(GameplayItemDefinition item)
        {
            if (IsWeaponAmmoPanelHud(item.Presentation.Hud)
                && !string.IsNullOrWhiteSpace(item.Presentation.HudSpriteName))
            {
                return true;
            }

            return string.Equals(item.BehaviorId, BuiltInGameplayBehaviorIds.MedigunCrit, StringComparison.Ordinal)
                && !string.IsNullOrWhiteSpace(item.Presentation.HudSpriteName);
        }

        private void DrawWeaponHudRows(List<WeaponHudRow> rows)
        {
            var minSourceX = float.PositiveInfinity;
            var maxSourceX = float.NegativeInfinity;
            var minSourceY = float.PositiveInfinity;
            var maxSourceY = float.NegativeInfinity;
            var flowTopSourceY = SourceMainAmmoHudY;

            foreach (var row in rows.OrderBy(static row => row.Order))
            {
                var sourceY = row.LegacySourceY ?? (flowTopSourceY - row.Height - WeaponHudPanelGapPixels);
                row.Draw(sourceY);

                if (row.ReservesFlowSpace)
                {
                    flowTopSourceY = Math.Min(flowTopSourceY, sourceY);
                }

                AccumulateWeaponHudRowSourceBounds(ref minSourceX, ref minSourceY, ref maxSourceX, ref maxSourceY, row, sourceY);
            }

            if (!float.IsFinite(minSourceX)
                || !float.IsFinite(maxSourceX)
                || !float.IsFinite(minSourceY)
                || !float.IsFinite(maxSourceY))
            {
                return;
            }

            UpdateWeaponStackBounds(minSourceX, minSourceY, maxSourceX - minSourceX, maxSourceY - minSourceY);
        }

        private void AccumulateWeaponHudRowSourceBounds(
            ref float minSourceX,
            ref float minSourceY,
            ref float maxSourceX,
            ref float maxSourceY,
            WeaponHudRow row,
            float sourceY)
        {
            if (row.EditorBoundsOffset is not null && row.EditorBoundsSize is not null)
            {
                var left = 728f + row.EditorBoundsOffset.Value.X;
                var top = sourceY + row.EditorBoundsOffset.Value.Y;
                minSourceX = Math.Min(minSourceX, left);
                minSourceY = Math.Min(minSourceY, top);
                maxSourceX = Math.Max(maxSourceX, left + row.EditorBoundsSize.Value.X);
                maxSourceY = Math.Max(maxSourceY, top + row.EditorBoundsSize.Value.Y);
                return;
            }

            var itemId = ResolveWeaponHudRowItemId(row.Id);
            var spriteName = ResolveWeaponHudRowSpriteName(row.Id, itemId);
            ExpandSourceBoundsForStandardAmmoPanel(ref minSourceX, ref minSourceY, ref maxSourceX, ref maxSourceY, sourceY, spriteName, itemId);
        }

        private string? ResolveWeaponHudRowItemId(string rowId)
        {
            var player = _game._world.LocalPlayer;
            return rowId switch
            {
                "local.weapon.primary" => GetLocalDisplayedMainWeaponPresentationItemId(),
                "local.weapon.primary.stowed" => player.GameplayLoadoutState.PrimaryItemId,
                "local.weapon.secondary" => player.GameplayLoadoutState.SecondaryItemId,
                "local.weapon.utility" => player.GameplayLoadoutState.UtilityItemId,
                "local.weapon.acquired" when ShouldDrawAcquiredWeaponHud() => GetLocalAlternatePrimaryWeaponPresentationItemId(),
                _ => null,
            };
        }

        private string? ResolveWeaponHudRowSpriteName(string rowId, string? itemId)
        {
            if (string.IsNullOrWhiteSpace(itemId))
            {
                return rowId switch
                {
                    "local.weapon.primary" => GetAmmoHudSpriteName(),
                    _ => null,
                };
            }

            return CharacterClassCatalog.RuntimeRegistry.GetRequiredItem(itemId).Presentation.HudSpriteName;
        }

        private void ExpandSourceBoundsForStandardAmmoPanel(
            ref float minSourceX,
            ref float minSourceY,
            ref float maxSourceX,
            ref float maxSourceY,
            float sourceY,
            string? spriteName,
            string? itemId)
        {
            const float iconSourceX = 728f;
            minSourceX = Math.Min(minSourceX, 689f);
            minSourceY = Math.Min(minSourceY, sourceY);

            var iconWidth = GetHudSpriteFrameWidth(spriteName, WeaponHudPanelScale, 48f);
            var iconHeight = GetHudSpriteFrameHeight(spriteName, WeaponHudPanelScale, WeaponHudFallbackPanelHeight);
            maxSourceX = Math.Max(maxSourceX, iconSourceX + iconWidth);
            maxSourceY = Math.Max(maxSourceY, sourceY + iconHeight);

            var isRocketLauncher = string.Equals(itemId, "weapon.rocketlauncher", StringComparison.Ordinal);
            var reloadBarLeft = isRocketLauncher ? 689f : 700f;
            var reloadBarWidth = isRocketLauncher ? 34f : 50f;
            maxSourceX = Math.Max(maxSourceX, reloadBarLeft + reloadBarWidth);
            maxSourceY = Math.Max(maxSourceY, sourceY + 12f);

            if (!isRocketLauncher)
            {
                maxSourceX = Math.Max(maxSourceX, 770f);
                var ammoCountScale = GetAmmoCountBuildScaleForValue(99);
                var textHeight = _game.MeasureMenuBitmapFontHeight(ammoCountScale);
                minSourceY = Math.Min(minSourceY, sourceY + 12f - textHeight);
            }
        }

        private void DrawAbilityHudRows(List<WeaponHudRow> rows)
        {
            if (rows.Count == 0)
            {
                return;
            }

            var minSourceX = SourceAbilityHudX - 40f;
            var maxSourceX = SourceAbilityHudX + 88f;
            var minSourceY = 492f;
            var maxSourceY = 558f;
            var flowTopSourceY = SourceAbilityHudY;

            foreach (var row in rows.OrderBy(static row => row.Order))
            {
                var sourceY = row.LegacySourceY ?? (flowTopSourceY - row.Height - AbilityHudWidgetGapPixels);
                row.Draw(sourceY);

                if (row.ReservesFlowSpace)
                {
                    flowTopSourceY = Math.Min(flowTopSourceY, sourceY);
                }

                minSourceY = Math.Min(minSourceY, sourceY - 8f);
                maxSourceY = Math.Max(maxSourceY, sourceY + row.Height + 10f);
            }

            UpdateAbilityStackBounds(minSourceX, minSourceY, maxSourceX - minSourceX, maxSourceY - minSourceY);
        }

        public static int GetCharacterHudFrameIndex(PlayerEntity player)
        {
            var teamOffset = player.Team == PlayerTeam.Blue ? 10 : 0;
            var classIndex = player.ClassId switch
            {
                PlayerClass.Scout => 0,
                PlayerClass.Soldier => 1,
                PlayerClass.Sniper => 2,
                PlayerClass.Demoman => 3,
                PlayerClass.Medic => 4,
                PlayerClass.Engineer => 5,
                PlayerClass.Heavy => 6,
                PlayerClass.Spy => 7,
                PlayerClass.Pyro => 8,
                PlayerClass.Quote => 9,
                _ => 0,
            };

            return classIndex + teamOffset;
        }

        public void DrawDemoknightHud() => DrawDemoknightHudCore();
        public void DrawPyroAmmoHud() => DrawPyroAmmoHudCore();
        public void DrawHeavyAmmoHud() => DrawHeavyAmmoHudCore();

        public void DrawDamageVignette() => DrawDamageVignetteCore();

        private void UpdateLocalHudPortraitDamageFeedback(ref Vector2 portraitPosition, ref Color portraitColor)
        {
            if (!_game._portraitRumbleEnabled || _game._portraitRumbleRemainingSeconds <= 0f)
            {
                _game._portraitRumbleRemainingSeconds = 0f;
                _game._portraitRumbleIntensity = 0f;
                return;
            }

            var progress = Math.Clamp(_game._portraitRumbleRemainingSeconds / PortraitRumbleDurationSeconds, 0f, 1f);
            var falloff = progress * progress;
            var shakePixels = 2.4f * _game._portraitRumbleIntensity * falloff;
            var seed = _game._portraitRumbleSeed * 17.31f;
            var phase = (PortraitRumbleDurationSeconds - _game._portraitRumbleRemainingSeconds) * 92f;
            portraitPosition += new Vector2(
                MathF.Sin(seed + phase) * shakePixels,
                MathF.Cos((seed * 0.73f) + (phase * 1.37f)) * shakePixels * 0.65f);
            portraitColor = Color.Lerp(Color.White, PortraitRumbleTintColor, Math.Clamp(0.75f * _game._portraitRumbleIntensity * progress, 0f, 1f));

            _game._portraitRumbleRemainingSeconds = Math.Max(0f, _game._portraitRumbleRemainingSeconds - Math.Max(0f, _game._clientUpdateElapsedSeconds));
        }

        private void DrawDamageVignetteCore()
        {
            if (!_game._damageVignetteEnabled)
            {
                _game._damageVignetteIntensity = 0f;
                _game._damageVignetteFlashIntensity = 0f;
                return;
            }

            var elapsedSeconds = Math.Max(0f, _game._clientUpdateElapsedSeconds);
            var lowHealthDepth = GetDamageVignetteLowHealthDepth();
            var persistentIntensity = GetDamageVignettePersistentTargetIntensity(lowHealthDepth);
            var flashIntensity = _game._damageVignetteFlashIntensity;
            var flashMultiplier = MathHelper.Lerp(1f, 1.75f, lowHealthDepth);
            var targetIntensity = Math.Clamp(
                persistentIntensity + (flashIntensity * flashMultiplier),
                0f,
                1f);

            if (targetIntensity > _game._damageVignetteIntensity)
            {
                _game._damageVignetteIntensity = Math.Min(
                    targetIntensity,
                    _game._damageVignetteIntensity + (DamageVignetteFadeInPerSecond * elapsedSeconds));
            }
            else
            {
                _game._damageVignetteIntensity = Math.Max(
                    targetIntensity,
                    _game._damageVignetteIntensity - (DamageVignetteFadeOutPerSecond * elapsedSeconds));
            }

            _game._damageVignetteFlashIntensity = Math.Max(
                0f,
                _game._damageVignetteFlashIntensity - (DamageVignetteReactiveFadePerSecond * elapsedSeconds));
            if (_game._damageVignetteFlashIntensity <= DamageVignetteMinimumVisibleIntensity)
            {
                _game._damageVignetteFlashIntensity = 0f;
            }

            if (_game._damageVignetteIntensity <= DamageVignetteMinimumVisibleIntensity)
            {
                _game._damageVignetteIntensity = 0f;
                return;
            }

            var renderIntensity = _game._damageVignetteIntensity
                * (ClientSettings.NormalizeDamageVignetteIntensityPercent(_game._damageVignetteIntensityPercent) / 100f)
                * 0.65f;
            if (renderIntensity <= DamageVignetteMinimumVisibleIntensity)
            {
                return;
            }

            if (!_game.TryEnsureDamageVignetteTexture(renderIntensity, out var texture))
            {
                return;
            }

            var drawAlpha = Math.Min(1.0f, renderIntensity / DamageVignetteDrawAlphaFadeThreshold);
            _game._spriteBatch.Draw(texture, new Rectangle(0, 0, _game.ViewportWidth, _game.ViewportHeight), Color.White * drawAlpha);
        }

        private static float GetDamageVignettePersistentTargetIntensity(float lowHealthDepth)
        {
            if (lowHealthDepth <= 0f)
            {
                return 0f;
            }

            var danger = 1f - ((1f - lowHealthDepth) * (1f - lowHealthDepth));
            return MathHelper.Lerp(DamageVignettePersistentMinimumIntensity, DamageVignettePersistentMaximumIntensity, danger);
        }

        private float GetDamageVignetteLowHealthDepth()
        {
            var player = _game._world.LocalPlayer;
            if (!player.IsAlive || player.MaxHealth <= 0)
            {
                return 0f;
            }

            var lowHealthThreshold = player.MaxHealth * DamageVignettePersistentHealthFraction;
            if (lowHealthThreshold <= 0f || player.Health > lowHealthThreshold)
            {
                return 0f;
            }

            var lowHealthDepth = Math.Clamp(
                (lowHealthThreshold - Math.Max(0, player.Health)) / lowHealthThreshold,
                0f,
                1f);
            return lowHealthDepth;
        }

        private static bool IsLocalHudLowHealth(PlayerEntity player)
        {
            return player.Health <= GetLocalHudLowHealthThreshold(player);
        }

        private static float GetLocalHudLowHealthThreshold(PlayerEntity player)
        {
            return player.MaxHealth <= 0
                ? 0f
                : player.MaxHealth / LowHealthHudThresholdMaxHealthDivisor;
        }

        public void DrawSpySuperjumpHud() => DrawSpySuperjumpHudCore();
        public void DrawQuoteAmmoHud() => DrawQuoteAmmoHudCore();
        public void DrawDemomanStickyHud() => DrawDemomanStickyHudCore();
        public void DrawExperimentalOffhandHud() => DrawExperimentalOffhandHudCore();
        public void DrawAcquiredMedigunPrompt() => DrawAcquiredMedigunPromptCore();
        public void DrawAcquiredWeaponHud() => DrawAcquiredWeaponHudCore(SourceMainAmmoHudY - GetAcquiredWeaponHudRowHeight() - WeaponHudPanelGapPixels);
        public void DrawPyroFlareHud(int frameIndex) => DrawPyroFlareHudCore(frameIndex);
        private void DrawPyroFlareHud(int frameIndex, float sourceY) => DrawPyroFlareHudCore(frameIndex, sourceY);
        public bool TryDrawSourceAmmoHudSprite(string spriteName, int frameIndex) => TryDrawSourceAmmoHudSpriteCore(spriteName, frameIndex);
        private bool TryDrawSourceAmmoHudSprite(string spriteName, int frameIndex, float sourceY) => TryDrawSourceAmmoHudSpriteCore(spriteName, frameIndex, sourceY);
        public void DrawSourceAmmoHudBar(float left, float width, float value, float maxValue, Color fillColor) => DrawSourceAmmoHudBarCore(left, width, value, maxValue, fillColor);
        private void DrawSourceAmmoHudBar(float left, float top, float width, float value, float maxValue, Color fillColor) => DrawSourceAmmoHudBarCore(left, top, width, value, maxValue, fillColor);
        public Rectangle GetReloadAmmoHudBarRectangle() => GetReloadAmmoHudBarRectangleCore();
        public Vector2 GetSourceHudPoint(float sourceX, float sourceY) => GetSourceHudPointCore(sourceX, sourceY);
        public Rectangle GetSourceHudRectangle(float sourceX, float sourceY, float width, float height) => GetSourceHudRectangleCore(sourceX, sourceY, width, height);
        public int CountLocalOwnedStickyMines() => CountLocalOwnedStickyMinesCore();
        public string? GetAmmoHudSpriteName() => GetAmmoHudSpriteNameCore();
        public int GetAmmoHudFrameIndex() => GetAmmoHudFrameIndexCore();
        public void DrawAmmoReloadBar(Rectangle barRectangle) => DrawAmmoReloadBarCore(barRectangle);
        public float GetAmmoReloadBarProgress(PlayerEntity player) => GetAmmoReloadBarProgressCore(player);
        public bool IsLocalDisplayedMainWeaponAcquired() => IsLocalDisplayedMainWeaponAcquiredCore();
        public string GetLocalDisplayedMainWeaponPresentationItemId() => GetLocalDisplayedMainWeaponPresentationItemIdCore();
        public PrimaryWeaponDefinition GetLocalDisplayedMainWeaponStats() => GetLocalDisplayedMainWeaponStatsCore();
        public int GetLocalDisplayedMainWeaponCurrentShells() => GetLocalDisplayedMainWeaponCurrentShellsCore();
        public int GetLocalDisplayedMainWeaponMaxShells() => GetLocalDisplayedMainWeaponMaxShellsCore();
        public int GetLocalDisplayedMainWeaponCooldownTicks() => GetLocalDisplayedMainWeaponCooldownTicksCore();
        public int GetLocalDisplayedMainWeaponReloadTicks() => GetLocalDisplayedMainWeaponReloadTicksCore();
        public string GetLocalAlternatePrimaryWeaponPresentationItemId() => GetLocalAlternatePrimaryWeaponPresentationItemIdCore();
        public PrimaryWeaponDefinition GetLocalAlternatePrimaryWeaponStats() => GetLocalAlternatePrimaryWeaponStatsCore();
        public int GetLocalAlternatePrimaryWeaponCurrentShells() => GetLocalAlternatePrimaryWeaponCurrentShellsCore();
        public int GetLocalAlternatePrimaryWeaponMaxShells() => GetLocalAlternatePrimaryWeaponMaxShellsCore();
        public float GetLocalAlternatePrimaryWeaponReloadProgress() => GetLocalAlternatePrimaryWeaponReloadProgressCore();
        public static float GetMedicNeedleReloadProgressProxy(int currentShells, int maxShells, int refillTicks) => GetMedicNeedleReloadProgress(currentShells, maxShells, refillTicks);

        private void DrawDemoknightHudCore()
        {
            var presentation = StockGameplayModCatalog.GetExperimentalDemoknightEyelanderItem().Presentation;
            var frameIndex = _game._world.LocalPlayer.Team == PlayerTeam.Blue ? presentation.BlueTeamHudFrameOffset : 0;
            if (presentation.HudSpriteName is not null)
            {
                _game.TryDrawScreenSprite(presentation.HudSpriteName, frameIndex, GetSourceHudPoint(728f, SourceMainAmmoHudY), Color.White, GetSourceHudSpriteScale(new Vector2(2.4f, 2.4f)));
            }

            var meterColor = _game._world.LocalPlayer.IsExperimentalDemoknightCharging ? new Color(226, 188, 92) : AmmoHudBarColor;
            var meterFraction = _game._world.LocalPlayer.ExperimentalDemoknightChargeFraction;
            var isChargeReady = !_game._world.LocalPlayer.IsExperimentalDemoknightCharging
                && _game._world.LocalPlayer.ExperimentalDemoknightChargeTicksRemaining >= PlayerEntity.ExperimentalDemoknightChargeMaxTicks;
            var statusText = _game._world.LocalPlayer.IsExperimentalDemoknightCharging ? "CHARGING" : isChargeReady ? "READY" : "RECHARGING";
            _game.DrawBitmapFontText("SWING", GetSourceHudPoint(694f, 498f), new Color(210, 210, 210), GetSourceHudTextScale(0.72f));
            _game.DrawBitmapFontText("CHARGE", GetSourceHudPoint(690f, 514f), new Color(210, 210, 210), GetSourceHudTextScale(0.72f));
            if (!isChargeReady || !_game.TryDrawScreenSprite(ExperimentalDemoknightCatalog.FullChargeHudSpriteName, 0, GetSourceHudPoint(713f, 540f), Color.White, GetSourceHudSpriteScale(Vector2.One)))
            {
                _game.DrawHudTextLeftAligned(statusText, GetSourceHudPoint(689f, 540f), meterColor, GetSourceHudTextScale(0.9f));
            }

            _game.DrawScreenHealthBar(GetSourceHudRectangle(689f, SourceAmmoHudBaseY + 90f, 50f, 8f), meterFraction, 1f, false, meterColor, Color.Black);
            _game.DrawBitmapFontText("M1", GetSourceHudPoint(756f, 498f), new Color(240, 232, 208), GetSourceHudTextScale(0.72f));
            _game.DrawBitmapFontText("M2", GetSourceHudPoint(756f, 514f), new Color(240, 232, 208), GetSourceHudTextScale(0.72f));
        }

        private void DrawPyroAmmoHudCore()
        {
            DrawPyroAmmoHudAt(SourceMainAmmoHudY);
        }

        private void DrawPyroAmmoHudAt(float sourceY)
        {
            var frameIndex = GetAmmoHudFrameIndex();
            var hudSpriteName = GetAmmoHudSpriteName();
            if (hudSpriteName is null || !TryDrawSourceAmmoHudSprite(hudSpriteName, frameIndex, sourceY))
            {
                return;
            }

            var currentShells = GetLocalDisplayedMainWeaponCurrentShells();
            var maxShells = GetLocalDisplayedMainWeaponMaxShells();
            var barColor = currentShells <= (maxShells * 0.25f) ? LowAmmoHudColor : AmmoHudBarColor;
            DrawSourceAmmoHudBar(689f, sourceY + 4f, 34f, currentShells, maxShells, barColor);
            DrawPyroFlareHud(frameIndex, sourceY);
        }

        private void DrawHeavyAmmoHudCore()
        {
            DrawHeavyAmmoHudAt(SourceMainAmmoHudY);
        }

        private void DrawHeavyAmmoHudAt(float sourceY)
        {
            var hudSpriteName = GetAmmoHudSpriteName();
            if (hudSpriteName is null || !TryDrawSourceAmmoHudSprite(hudSpriteName, GetAmmoHudFrameIndex(), sourceY))
            {
                return;
            }

            var currentShells = GetLocalDisplayedMainWeaponCurrentShells();
            var maxShells = GetLocalDisplayedMainWeaponMaxShells();
            var ammoFraction = maxShells <= 0 ? 0f : float.Clamp(currentShells / (float)maxShells, 0f, 1f);
            var barColor = Color.Lerp(AmmoHudBarColor, LowAmmoHudColor, 1f - ammoFraction);
            var cooldownFraction = float.Clamp(Math.Max(GetLocalDisplayedMainWeaponCooldownTicks(), GetLocalDisplayedMainWeaponReloadTicks()) / 25f, 0f, 1f);
            barColor = Color.Lerp(barColor, HeavyCooldownHudColor, cooldownFraction);
            DrawSourceAmmoHudBar(689f, sourceY + 4f, 34f, currentShells, maxShells, barColor);
        }

        private void DrawSpySuperjumpHudCore()
        {
            var utilityItemId = _game._world.LocalPlayer.GameplayLoadoutState.UtilityItemId;
            if (string.IsNullOrWhiteSpace(utilityItemId))
            {
                return;
            }

            DrawConfiguredAbilityCooldownHud(CharacterClassCatalog.RuntimeRegistry.GetRequiredItem(utilityItemId), 515f);
        }

        private void DrawConfiguredAbilityCooldownHud(GameplayItemDefinition item, float sourceY)
        {
            if (!TryResolveAbilityCooldownHudState(item, out var cooldownRemaining, out var maxCooldownTicks, out var isActive, out var isDisabled))
            {
                return;
            }

            var presentation = item.Presentation;
            var iconColor = isDisabled ? DisabledAmmoHudColor : Color.White;
            var hudSpriteName = GetAbilityCooldownHudSpriteName(item);
            var frameIndex = GetAbilityHudFrameIndex(presentation, hudSpriteName);
            var drewHudPlaque = _game.TryDrawScreenSprite(
                hudSpriteName,
                frameIndex,
                GetSourceHudPoint(GetAbilityHudSourceX(hudSpriteName), sourceY),
                iconColor,
                GetSourceHudSpriteScale(new Vector2(GetAbilityHudSpriteScale(hudSpriteName), GetAbilityHudSpriteScale(hudSpriteName))));
            if (!drewHudPlaque && !IsDefaultAbilityHudSprite(hudSpriteName))
            {
                DrawDefaultAbilityCooldownHudPlaque(sourceY, isDisabled);
            }

            cooldownRemaining = Math.Clamp(cooldownRemaining, 0, maxCooldownTicks);
            var meterFraction = !isActive && cooldownRemaining <= 0
                ? 1f
                : 1f - (cooldownRemaining / (float)maxCooldownTicks);
            var meterColor = isDisabled
                ? DisabledAmmoHudColor
                : isActive
                ? new Color(226, 188, 92)
                : AmmoHudBarColor;
            var barRectangle = GetSourceHudRectangle(AbilityCooldownBarSourceX, sourceY + 13f, 35f, 5f);
            _game.DrawScreenHealthBar(barRectangle, meterFraction, 1f, false, meterColor, Color.Black);
        }

        private void DrawDummyAbilityHud(float sourceY)
        {
            DrawDefaultAbilityCooldownHudPlaque(sourceY, disabled: false);
            _game.DrawScreenHealthBar(GetSourceHudRectangle(AbilityCooldownBarSourceX, sourceY + 13f, 35f, 5f), 1f, 1f, false, AmmoHudBarColor, Color.Black);
        }

        private void DrawDefaultAbilityCooldownHudPlaque(float sourceY, bool disabled)
        {
            var iconColor = disabled ? DisabledAmmoHudColor : Color.White;
            var frameIndex = _game._world.LocalPlayer.Team == PlayerTeam.Blue ? 1 : 0;
            _game.TryDrawScreenSprite(
                DefaultAbilityHudSpriteName,
                frameIndex,
                GetSourceHudPoint(DefaultAbilityHudSourceX, sourceY),
                iconColor,
                GetSourceHudSpriteScale(new Vector2(DefaultAbilityHudSpriteScale, DefaultAbilityHudSpriteScale)));
        }

        private int GetAbilityHudFrameIndex(GameplayItemPresentationDefinition presentation, string hudSpriteName)
        {
            if (IsDefaultAbilityHudSprite(hudSpriteName))
            {
                return _game._world.LocalPlayer.Team == PlayerTeam.Blue ? 1 : 0;
            }

            return _game._world.LocalPlayer.Team == PlayerTeam.Blue ? presentation.BlueTeamHudFrameOffset : 0;
        }

        private bool TryResolveAbilityCooldownHudState(
            GameplayItemDefinition item,
            out int cooldownRemaining,
            out int maxCooldownTicks,
            out bool isActive,
            out bool isDisabled)
        {
            cooldownRemaining = 0;
            maxCooldownTicks = 1;
            isActive = false;
            isDisabled = false;
            var player = _game._world.LocalPlayer;
            if (item.Ability is not { } ability)
            {
                return false;
            }

            switch (ability.ExecutorId)
            {
                case BuiltInGameplayBehaviorIds.HeavySandvich:
                    if (player.ClassId != PlayerClass.Heavy)
                    {
                        return false;
                    }

                    maxCooldownTicks = GameplayAbilityParameterReader.GetTicks(
                        ability,
                        "cooldownTicks",
                        "cooldownSeconds",
                        PlayerEntity.HeavySandvichCooldownTicks,
                        _game._config.TicksPerSecond);
                    var heavyEatCooldownRemaining = _game.GetPlayerHeavyEatCooldownTicksRemaining(player);
                    if (heavyEatCooldownRemaining > 0)
                    {
                        maxCooldownTicks = _game.GetPlayerHeavyEatCooldownDurationTicks(player);
                    }

                    cooldownRemaining = Math.Clamp(heavyEatCooldownRemaining, 0, maxCooldownTicks);
                    return true;
                case BuiltInGameplayBehaviorIds.HeavyGhostDash:
                    if (player.ClassId != PlayerClass.Heavy)
                    {
                        return false;
                    }

                    maxCooldownTicks = GameplayAbilityParameterReader.GetTicks(
                        ability,
                        "cooldownTicks",
                        "cooldownSeconds",
                        Math.Max(1, (int)MathF.Round(_game._config.TicksPerSecond * ExperimentalGameplaySettings.HeavyGhostDashCooldownSeconds)),
                        _game._config.TicksPerSecond);
                    cooldownRemaining = _game.GetPlayerExperimentalGhostDashCooldownTicksRemaining(player);
                    isActive = _game.GetPlayerIsExperimentalGhostDashing(player);
                    return true;
                case BuiltInGameplayBehaviorIds.SpySuperjump:
                    if (player.ClassId != PlayerClass.Spy)
                    {
                        return false;
                    }

                    maxCooldownTicks = GameplayAbilityParameterReader.GetTicks(
                        ability,
                        "cooldownTicks",
                        "cooldownSeconds",
                        PlayerEntity.SpySuperjumpCooldownTicks,
                        _game._config.TicksPerSecond);
                    cooldownRemaining = _game.GetPlayerSpySuperjumpCooldownTicksRemaining(player);
                    isActive = _game.GetPlayerIsSpySuperjumpActive(player);
                    isDisabled = _game.GetPlayerIsCarryingIntel(player);
                    return true;
            }

            return TryResolveGenericAbilityCooldownHudState(item, ability, player, out cooldownRemaining, out maxCooldownTicks, out isActive, out isDisabled);
        }

        private bool TryResolveGenericAbilityCooldownHudState(
            GameplayItemDefinition item,
            GameplayAbilityDefinition ability,
            PlayerEntity player,
            out int cooldownRemaining,
            out int maxCooldownTicks,
            out bool isActive,
            out bool isDisabled)
        {
            cooldownRemaining = 0;
            maxCooldownTicks = 1;
            isActive = false;
            isDisabled = false;
            var hud = item.Presentation.Hud;
            if (hud is null
                || !string.Equals(hud.StateProvider, GameplayItemHudStateProviders.AbilityCooldown, StringComparison.OrdinalIgnoreCase)
                || string.IsNullOrWhiteSpace(hud.StateOwner)
                || string.IsNullOrWhiteSpace(hud.CooldownKey))
            {
                return false;
            }

            var stateOwner = hud.StateOwner.Trim();
            var cooldownKey = hud.CooldownKey.Trim();
            var isCoreAbilityState = string.Equals(stateOwner, GameplayAbilityConstants.CoreAbilityReplicatedStateOwnerId, StringComparison.Ordinal);
            if (!(isCoreAbilityState && GameplayAbilityReplicatedState.TryGetInt(player, cooldownKey, out cooldownRemaining))
                && !player.TryGetReplicatedStateInt(stateOwner, cooldownKey, out cooldownRemaining))
            {
                return false;
            }

            maxCooldownTicks = hud.MaxCooldown > 0 ? hud.MaxCooldown : ResolveAbilityCooldownTicks(ability);
            if (maxCooldownTicks <= 0)
            {
                return false;
            }

            if (!string.IsNullOrWhiteSpace(hud.ActiveKey))
            {
                var activeKey = hud.ActiveKey.Trim();
                isActive = isCoreAbilityState && GameplayAbilityReplicatedState.TryGetBool(player, activeKey, out var coreActive)
                    ? coreActive
                    : player.TryGetReplicatedStateBool(stateOwner, activeKey, out var replicatedActive)
                        && replicatedActive;
            }

            if (!string.IsNullOrWhiteSpace(hud.DisabledKey))
            {
                var disabledKey = hud.DisabledKey.Trim();
                isDisabled = isCoreAbilityState && GameplayAbilityReplicatedState.TryGetBool(player, disabledKey, out var coreDisabled)
                    ? coreDisabled
                    : player.TryGetReplicatedStateBool(stateOwner, disabledKey, out var replicatedDisabled)
                        && replicatedDisabled;
            }

            return true;
        }

        private int ResolveAbilityCooldownTicks(GameplayAbilityDefinition ability)
        {
            return GameplayAbilityParameterReader.GetTicks(
                ability,
                "cooldownTicks",
                "cooldownSeconds",
                1,
                _game._config.TicksPerSecond);
        }

        private void DrawQuoteAmmoHudCore()
        {
            DrawQuoteAmmoHudAt(SourceMainAmmoHudY);
        }

        private void DrawQuoteAmmoHudAt(float sourceY)
        {
            var hudSpriteName = GetAmmoHudSpriteName();
            if (hudSpriteName is null || !TryDrawSourceAmmoHudSprite(hudSpriteName, GetAmmoHudFrameIndex(), sourceY))
            {
                return;
            }

            DrawSourceAmmoHudBar(689f, sourceY + 4f, 34f, _game.GetPlayerCurrentShells(_game._world.LocalPlayer), _game._world.LocalPlayer.MaxShells, AmmoHudBarColor);
        }

        private void DrawDemomanStickyHudCore()
        {
            var stickyCount = CountLocalOwnedStickyMines();
            var maxStickies = Math.Max(1, _game._world.LocalPlayer.PrimaryWeapon.MaxAmmo);
            var frameIndex = _game._world.LocalPlayer.Team == PlayerTeam.Blue ? 1 : 0;
            if (!HasLocalDemomanGrenadeLauncher())
            {
                DrawStickyCounterHud(stickyCount, maxStickies, frameIndex, 522f);
                return;
            }

            const float panelScale = 2.4f;
            const float panelGapPixels = 4f;
            const float fallbackPanelHeight = 38f;

            var grenadeLauncherHudY = SourceMainAmmoHudY;
            var grenadeLauncherPanelHeight = GetHudSpriteFrameHeight("GrenadeLauncherAmmoS", panelScale, fallbackPanelHeight);
            var mineLauncherPanelHeight = GetHudSpriteFrameHeight(GetAmmoHudSpriteName(), panelScale, fallbackPanelHeight);
            var mineLauncherHudY = grenadeLauncherHudY - grenadeLauncherPanelHeight - panelGapPixels;
            var stickyCounterY = mineLauncherHudY - mineLauncherPanelHeight - panelGapPixels;

            DrawStandardAmmoHudPanel(mineLauncherHudY);
            if (!DrawStickyCounterHud(stickyCount, maxStickies, frameIndex, stickyCounterY))
            {
                return;
            }

            DrawGrenadeLauncherHud(grenadeLauncherHudY);
        }

        private bool DrawStickyCounterHud(int stickyCount, int maxStickies, int frameIndex, float sourceY)
        {
            if (!_game.TryDrawScreenSprite("StickyCounterS", frameIndex, GetSourceHudPoint(DefaultAbilityHudSourceX, sourceY), Color.White, GetSourceHudSpriteScale(new Vector2(3f, 3f))))
            {
                return false;
            }

            _game.DrawHudTextLeftAligned(stickyCount.ToString(CultureInfo.InvariantCulture), GetSourceHudPoint(StickyCounterCountSourceX, sourceY + 2f), AmmoHudBarColor, GetSourceHudTextScale(1.5f));
            _game.DrawHudTextLeftAligned($"/{maxStickies.ToString(CultureInfo.InvariantCulture)}", GetSourceHudPoint(StickyCounterMaxSourceX, sourceY + 2f), AmmoHudBarColor, GetSourceHudTextScale(1.5f));
            return true;
        }

        private void DrawGrenadeLauncherHud(float sourceY)
        {
            var utilityItemId = _game._world.LocalPlayer.GameplayLoadoutState.UtilityItemId;
            if (string.IsNullOrWhiteSpace(utilityItemId))
            {
                return;
            }

            var utilityItem = CharacterClassCatalog.RuntimeRegistry.GetRequiredItem(utilityItemId);
            var presentation = utilityItem.Presentation;
            var hudFrameIndex = _game._world.LocalPlayer.Team == PlayerTeam.Blue ? presentation.BlueTeamHudFrameOffset : 0;
            var hudSpriteName = presentation.HudSpriteName ?? "GrenadeLauncherAmmoS";
            if (!_game.TryDrawScreenSprite(hudSpriteName, hudFrameIndex, GetSourceHudPoint(728f, sourceY), Color.White, GetSourceHudSpriteScale(new Vector2(2.4f, 2.4f))))
            {
                return;
            }

            var currentAmmo = _game._world.LocalPlayer.TryGetReplicatedStateInt(CoreReplicatedOwnerId, DemomanGrenadeLauncherAmmoKey, out var replicatedAmmo)
                ? replicatedAmmo
                : _game._world.LocalPlayer.ExperimentalOffhandCurrentShells;
            var reloadTicksRemaining = _game._world.LocalPlayer.ExperimentalOffhandReloadTicksUntilNextShell;
            var maxAmmo = GetLocalGrenadeLauncherMaxAmmo(utilityItem);
            var totalReloadTicks = GetLocalGrenadeLauncherReloadTicks(utilityItem);

            var ammoCountScale = GetAmmoCountBuildScaleForValue(currentAmmo);
            var reloadBarBottomSourceY = sourceY + 12f;
            var ammoTextSourceY = reloadBarBottomSourceY - _game.MeasureMenuBitmapFontHeight(ammoCountScale);
            var ammoColor = currentAmmo < maxAmmo && currentAmmo <= Math.Max(1, maxAmmo / 4) ? LowAmmoHudColor : AmmoHudTextColor;
            _game.DrawMenuBitmapFontText(currentAmmo.ToString(CultureInfo.InvariantCulture), GetSourceHudPoint(755f, ammoTextSourceY), ammoColor, GetSourceHudTextScale(ammoCountScale));

            var reloadProgress = currentAmmo >= maxAmmo || reloadTicksRemaining <= 0
                ? 1f
                : Math.Clamp(1f - (reloadTicksRemaining / (float)totalReloadTicks), 0f, 1f);
            _game.DrawScreenHealthBar(GetSourceHudRectangle(689f, sourceY + 4f, 34f, 8f), reloadProgress, 1f, false, AmmoHudBarColor, Color.Black);
        }

        private void DrawExperimentalOffhandHudCore()
        {
            if (!ShouldDrawSecondaryWeaponHudRow(out var secondaryItem))
            {
                return;
            }

            var sourceY = SourceMainAmmoHudY - GetHudSpriteFrameHeight(secondaryItem.Presentation.HudSpriteName, WeaponHudPanelScale, WeaponHudFallbackPanelHeight) - WeaponHudPanelGapPixels;
            DrawSecondaryWeaponHudRow(sourceY);
        }

        private void DrawSecondaryWeaponHudRow(float sourceY)
        {
            if (!ShouldDrawSecondaryWeaponHudRow(out var secondaryItem))
            {
                return;
            }

            if (_game._world.LocalPlayer.ClassId == PlayerClass.Medic
                && string.Equals(secondaryItem.BehaviorId, BuiltInGameplayBehaviorIds.MedigunCrit, StringComparison.Ordinal))
            {
                DrawMedicKritzStowedAmmoHudPanel(sourceY);
                return;
            }

            var presentation = secondaryItem.Presentation;
            var frameIndex = _game._world.LocalPlayer.Team == PlayerTeam.Blue ? presentation.BlueTeamHudFrameOffset : 0;
            var mainPanelSourceX = 728f;
            var shotgunPanelSourceX = mainPanelSourceX;
            var shotgunPanelSourceY = sourceY;
            var iconPosition = GetSourceHudPoint(shotgunPanelSourceX, shotgunPanelSourceY);
            var iconDrawn = presentation.HudSpriteName is not null && _game.TryDrawScreenSprite(presentation.HudSpriteName, frameIndex, iconPosition, Color.White, GetSourceHudSpriteScale(new Vector2(WeaponHudPanelScale, WeaponHudPanelScale)));
            if (!iconDrawn)
            {
                _game.DrawBitmapFontText("SHOTGUN", GetSourceHudPoint(shotgunPanelSourceX - 24f, shotgunPanelSourceY + 3f), Color.White, GetSourceHudTextScale(0.72f));
            }

            var isScout = _game._world.LocalPlayer.ClassId == PlayerClass.Scout;
            var offhandAmmoKey = isScout ? ScoutNailgunAmmoKey : SoldierShotgunAmmoKey;
            var offhandMaxAmmoKey = isScout ? ScoutNailgunMaxAmmoKey : SoldierShotgunMaxAmmoKey;

            var currentShells = _game._world.LocalPlayer.TryGetReplicatedStateInt(CoreReplicatedOwnerId, offhandAmmoKey, out var replicatedOffhandAmmo)
                ? replicatedOffhandAmmo
                : _game._world.LocalPlayer.ExperimentalOffhandCurrentShells;
            var maxShells = _game._world.LocalPlayer.TryGetReplicatedStateInt(CoreReplicatedOwnerId, offhandMaxAmmoKey, out var replicatedOffhandMaxAmmo)
                ? Math.Max(1, replicatedOffhandMaxAmmo)
                : Math.Max(1, _game._world.LocalPlayer.ExperimentalOffhandMaxShells);
            var reloadTicksRemaining = _game._world.LocalPlayer.ExperimentalOffhandReloadTicksUntilNextShell;
            var reloadTicksPerShell = Math.Max(1, _game._world.LocalPlayer.ExperimentalOffhandWeapon?.AmmoReloadTicks ?? CharacterClassCatalog.SoldierShotgun.AmmoReloadTicks);
            var reloadProgress = currentShells >= maxShells
                ? 1f
                : reloadTicksRemaining <= 0
                    ? 1f
                    : Math.Clamp(1f - (reloadTicksRemaining / (float)reloadTicksPerShell), 0f, 1f);
            var ammoColor = currentShells < maxShells && currentShells <= Math.Max(1, maxShells / 4) ? LowAmmoHudColor : AmmoHudTextColor;
            var ammoCountScale = GetAmmoCountBuildScaleForValue(currentShells);

            var shotgunReloadBarBottomSourceY = shotgunPanelSourceY + 12f;
            var shotgunAmmoTextSourceY = shotgunReloadBarBottomSourceY - _game.MeasureMenuBitmapFontHeight(ammoCountScale);
            _game.DrawMenuBitmapFontText(currentShells.ToString(CultureInfo.InvariantCulture), GetSourceHudPoint(shotgunPanelSourceX + 27f, shotgunAmmoTextSourceY), ammoColor, GetSourceHudTextScale(ammoCountScale));
            _game.DrawScreenHealthBar(GetSourceHudRectangle(shotgunPanelSourceX - 28f, shotgunPanelSourceY + 4f, 50f, 8f), reloadProgress, 1f, false, AmmoHudBarColor, Color.Black);
        }

        private bool ShouldDrawSecondaryWeaponHudRow(out GameplayItemDefinition item)
        {
            item = null!;
            if (_game._world.LocalPlayer.IsExperimentalOffhandSelected)
            {
                return false;
            }

            var hasReplicatedSecondaryAvailability = TryGetLocalSecondaryWeaponHudAvailability();

            var secondaryItemId = _game._world.LocalPlayer.GameplayLoadoutState.SecondaryItemId;
            if (string.IsNullOrWhiteSpace(secondaryItemId))
            {
                return false;
            }

            item = CharacterClassCatalog.RuntimeRegistry.GetRequiredItem(secondaryItemId);
            return ShouldDrawSecondaryWeaponHudRow(item, hasReplicatedSecondaryAvailability);
        }

        private bool TryGetLocalSecondaryWeaponHudAvailability()
        {
            var player = _game._world.LocalPlayer;
            if (player.HasExperimentalOffhandWeapon)
            {
                return true;
            }

            if (player.TryGetReplicatedStateBool(CoreReplicatedOwnerId, SoldierShotgunAvailableKey, out var shotgunAvailable)
                && shotgunAvailable)
            {
                return true;
            }

            if (player.TryGetReplicatedStateBool(CoreReplicatedOwnerId, ScoutNailgunAvailableKey, out var nailgunAvailable)
                && nailgunAvailable)
            {
                return true;
            }

            if (player.TryGetReplicatedStateInt(CoreReplicatedOwnerId, SoldierShotgunAmmoKey, out _)
                || player.TryGetReplicatedStateInt(CoreReplicatedOwnerId, SoldierShotgunMaxAmmoKey, out _)
                || player.TryGetReplicatedStateInt(CoreReplicatedOwnerId, ScoutNailgunAmmoKey, out _)
                || player.TryGetReplicatedStateInt(CoreReplicatedOwnerId, ScoutNailgunMaxAmmoKey, out _))
            {
                return true;
            }

            return false;
        }

        private bool ShouldDrawSecondaryWeaponHudRow(GameplayItemDefinition item, bool hasReplicatedSecondaryAvailability)
        {
            var localPlayer = _game._world.LocalPlayer;
            if (localPlayer.ClassId == PlayerClass.Medic
                && string.Equals(item.BehaviorId, BuiltInGameplayBehaviorIds.MedigunCrit, StringComparison.Ordinal))
            {
                return !IsLocalMedicKritzHealNeedlesPresented()
                    && (hasReplicatedSecondaryAvailability || localPlayer.HasExperimentalOffhandWeapon);
            }

            if (!IsSecondaryAmmoHudItem(item))
            {
                return false;
            }

            if (hasReplicatedSecondaryAvailability)
            {
                return true;
            }

            if (localPlayer.HasExperimentalOffhandWeapon)
            {
                var offhandItemId = CharacterClassCatalog.RuntimeRegistry.TryResolvePrimaryWeaponItemId(
                    localPlayer.ExperimentalOffhandWeapon, out var resolvedId) ? resolvedId : null;
                if (string.Equals(offhandItemId, item.Id, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            var hud = item.Presentation.Hud;
            return IsWeaponAmmoPanelHud(hud)
                && !string.IsNullOrWhiteSpace(item.Presentation.HudSpriteName)
                && !(hud?.HideWhenUnavailable ?? false);
        }

        private void DrawAcquiredMedigunPromptCore()
        {
            if (_game._world.LocalPlayer.ClassId != PlayerClass.Soldier || !_game._world.LocalPlayer.HasAcquiredMedigunEquipped)
            {
                return;
            }

            var position = new Vector2(_game.ViewportWidth / 2f, _game.ViewportHeight - 102f);
            var shadowColor = Color.White * 0.95f;
            var textColor = new Color(210, 28, 28);
            _game.DrawBitmapFontTextCentered("LEFT CLICK FOR HEALSPLOSION", position + new Vector2(2f, 2f), shadowColor, 1.2f);
            _game.DrawBitmapFontTextCentered("LEFT CLICK FOR HEALSPLOSION", position, textColor, 1.2f);
        }

        private void DrawStowedPrimaryWeaponHudRow(float sourceY)
        {
            var player = _game._world.LocalPlayer;
            DrawWeaponAmmoHudPanel(
                player.GameplayLoadoutState.PrimaryItemId,
                _game.GetPlayerCurrentShells(player),
                Math.Max(1, player.MaxShells),
                GetLocalPrimaryWeaponReloadProgress(),
                sourceY,
                Color.White);
        }

        private void DrawMedicKritzStowedAmmoHudPanel(float sourceY)
        {
            var player = _game._world.LocalPlayer;
            var itemId = player.GameplayLoadoutState.SecondaryItemId ?? "weapon.medigun.crit";
            var currentShells = player.ExperimentalOffhandCurrentShells;
            var maxShells = Math.Max(1, player.ExperimentalOffhandMaxShells);
            var reloadProgress = GetMedicNeedleReloadProgress(
                currentShells,
                maxShells,
                player.ExperimentalOffhandReloadTicksUntilNextShell);
            DrawWeaponAmmoHudPanel(itemId, currentShells, maxShells, reloadProgress, sourceY, Color.White);
        }

        private void DrawAcquiredWeaponHudRow(float sourceY)
        {
            DrawAcquiredWeaponHudCore(sourceY);
        }

        private void DrawAcquiredWeaponHudCore(float sourceY)
        {
            if (!ShouldDrawAcquiredWeaponHud())
            {
                return;
            }

            var weaponItemId = GetLocalAlternatePrimaryWeaponPresentationItemId();
            var currentShells = GetLocalAlternatePrimaryWeaponCurrentShells();
            var maxShells = Math.Max(1, GetLocalAlternatePrimaryWeaponMaxShells());
            var reloadProgress = GetLocalAlternatePrimaryWeaponReloadProgress();
            DrawWeaponAmmoHudPanel(weaponItemId, currentShells, maxShells, reloadProgress, sourceY, Color.White);
        }

        private float GetAcquiredWeaponHudRowHeight()
        {
            if (!ShouldDrawAcquiredWeaponHud())
            {
                return WeaponHudFallbackPanelHeight;
            }

            var weaponItemId = GetLocalAlternatePrimaryWeaponPresentationItemId();
            var item = CharacterClassCatalog.RuntimeRegistry.GetRequiredItem(weaponItemId);
            return GetHudSpriteFrameHeight(item.Presentation.HudSpriteName, WeaponHudPanelScale, WeaponHudFallbackPanelHeight);
        }

        private bool ShouldDrawAcquiredWeaponHud()
        {
            return _game._world.LocalPlayer.ClassId == PlayerClass.Soldier
                && _game._world.LocalPlayer.HasAcquiredWeapon
                && _game._world.LocalPlayer.AcquiredWeaponClassId.HasValue;
        }

        private float GetLocalPrimaryWeaponReloadProgress()
        {
            var player = _game._world.LocalPlayer;
            var currentShells = _game.GetPlayerCurrentShells(player);
            var maxShells = Math.Max(1, player.MaxShells);
            if (currentShells >= maxShells)
            {
                return 1f;
            }

            if (player.ClassId == PlayerClass.Medic)
            {
                if (IsLocalMedicKritzHealNeedlesPresented())
                {
                    return GetMedicNeedleReloadProgress(
                        player.ExperimentalOffhandCurrentShells,
                        Math.Max(1, player.ExperimentalOffhandMaxShells),
                        player.ExperimentalOffhandReloadTicksUntilNextShell);
                }

                return GetMedicNeedleReloadProgress(currentShells, maxShells, _game.GetPlayerMedicNeedleRefillTicks(player));
            }

            var reloadTicksRemaining = _game.GetPlayerReloadTicksUntilNextShell(player);
            if (reloadTicksRemaining <= 0)
            {
                return 1f;
            }

            var reloadTicksTotal = Math.Max(1, player.PrimaryWeapon.AmmoReloadTicks);
            return Math.Clamp(1f - (reloadTicksRemaining / (float)reloadTicksTotal), 0f, 1f);
        }

        private static float GetAmmoCountBuildScaleForValue(int ammoCount)
        {
            return Math.Abs(ammoCount) < 10 ? 1f : AmmoCountBuildScale;
        }

        private void DrawStandardAmmoHudPanel(float sourceY)
        {
            var hudSpriteName = GetAmmoHudSpriteName();
            if (hudSpriteName is null || !_game.TryDrawScreenSprite(hudSpriteName, GetAmmoHudFrameIndex(), GetSourceHudPoint(728f, sourceY), Color.White, GetSourceHudSpriteScale(new Vector2(2.4f, 2.4f))))
            {
                return;
            }

            if (!string.Equals(GetLocalDisplayedMainWeaponPresentationItemId(), "weapon.rocketlauncher", StringComparison.Ordinal))
            {
                var currentShells = GetLocalDisplayedMainWeaponCurrentShells();
                var ammoCountScale = GetAmmoCountBuildScaleForValue(currentShells);
                var reloadBarBottomSourceY = sourceY + 12f;
                var ammoTextSourceY = reloadBarBottomSourceY - _game.MeasureMenuBitmapFontHeight(ammoCountScale);
                _game.DrawMenuBitmapFontText(currentShells.ToString(CultureInfo.InvariantCulture), GetSourceHudPoint(755f, ammoTextSourceY), AmmoHudTextColor, GetSourceHudTextScale(ammoCountScale));
            }

            DrawAmmoReloadBar(GetReloadAmmoHudBarRectangleAt(sourceY));
        }

        private void DrawWeaponAmmoHudPanel(string itemId, int currentShells, int maxShells, float reloadProgress, float sourceY, Color tint)
        {
            var item = CharacterClassCatalog.RuntimeRegistry.GetRequiredItem(itemId);
            var presentation = item.Presentation;
            if (string.IsNullOrWhiteSpace(presentation.HudSpriteName))
            {
                return;
            }

            var frameIndex = GetWeaponHudFrameIndex(presentation, currentShells);
            if (!_game.TryDrawScreenSprite(presentation.HudSpriteName, frameIndex, GetSourceHudPoint(728f, sourceY), tint, GetSourceHudSpriteScale(new Vector2(WeaponHudPanelScale, WeaponHudPanelScale))))
            {
                return;
            }

            if (!string.Equals(itemId, "weapon.rocketlauncher", StringComparison.Ordinal))
            {
                var ammoCountScale = GetAmmoCountBuildScaleForValue(currentShells);
                var reloadBarBottomSourceY = sourceY + 12f;
                var ammoTextSourceY = reloadBarBottomSourceY - _game.MeasureMenuBitmapFontHeight(ammoCountScale);
                var ammoColor = currentShells < maxShells && currentShells <= Math.Max(1, maxShells / 4) ? LowAmmoHudColor : AmmoHudTextColor;
                _game.DrawMenuBitmapFontText(currentShells.ToString(CultureInfo.InvariantCulture), GetSourceHudPoint(755f, ammoTextSourceY), ammoColor, GetSourceHudTextScale(ammoCountScale));
            }

            _game.DrawScreenHealthBar(GetWeaponReloadAmmoHudBarRectangleAt(itemId, sourceY), reloadProgress, 1f, false, AmmoHudBarColor, Color.Black);
        }

        private int GetWeaponHudFrameIndex(GameplayItemPresentationDefinition presentation, int currentShells)
        {
            if (presentation.UseAmmoCountForHudFrame)
            {
                return currentShells
                    + (_game._world.LocalPlayerTeam == PlayerTeam.Blue ? presentation.BlueTeamAmmoHudFrameOffset : 0);
            }

            return _game._world.LocalPlayerTeam == PlayerTeam.Blue ? presentation.BlueTeamHudFrameOffset : 0;
        }

        private float GetHudSpriteFrameHeight(string? spriteName, float scale, float fallbackHeight)
        {
            if (string.IsNullOrWhiteSpace(spriteName))
            {
                return fallbackHeight;
            }

            var sprite = _game._runtimeAssets.GetSprite(spriteName);
            return sprite is not null && sprite.Frames.Count > 0
                ? sprite.Frames[0].Height * scale
                : fallbackHeight;
        }

        private float GetHudSpriteFrameWidth(string? spriteName, float scale, float fallbackWidth)
        {
            if (string.IsNullOrWhiteSpace(spriteName))
            {
                return fallbackWidth;
            }

            var sprite = _game._runtimeAssets.GetSprite(spriteName);
            return sprite is not null && sprite.Frames.Count > 0
                ? sprite.Frames[0].Width * scale
                : fallbackWidth;
        }

        private float GetMainAmmoHudPanelHeight()
        {
            return GetHudSpriteFrameHeight(GetAmmoHudSpriteName(), WeaponHudPanelScale, WeaponHudFallbackPanelHeight);
        }

        private void DrawConfiguredItemHudPanel(GameplayItemDefinition item, float sourceY)
        {
            var presentation = item.Presentation;
            if (string.IsNullOrWhiteSpace(presentation.HudSpriteName))
            {
                return;
            }

            if (string.Equals(presentation.Hud?.StateProvider, GameplayItemHudStateProviders.UtilityAmmo, StringComparison.OrdinalIgnoreCase)
                && TryDrawConfiguredUtilityAmmoHudPanel(item, sourceY))
            {
                return;
            }

            var frameIndex = _game._world.LocalPlayer.Team == PlayerTeam.Blue ? presentation.BlueTeamHudFrameOffset : 0;
            _game.TryDrawScreenSprite(presentation.HudSpriteName, frameIndex, GetSourceHudPoint(728f, sourceY), Color.White, GetSourceHudSpriteScale(new Vector2(WeaponHudPanelScale, WeaponHudPanelScale)));
        }

        private bool TryDrawConfiguredUtilityAmmoHudPanel(GameplayItemDefinition item, float sourceY)
        {
            if (!string.Equals(item.BehaviorId, BuiltInGameplayBehaviorIds.GrenadeLauncher, StringComparison.Ordinal))
            {
                return false;
            }

            DrawGrenadeLauncherHud(sourceY);
            return true;
        }

        private bool HasLocalDemomanGrenadeLauncher()
        {
            return _game._world.LocalPlayer.ClassId == PlayerClass.Demoman
                && _game._world.LocalPlayer.HasUtilityBehavior(BuiltInGameplayBehaviorIds.GrenadeLauncher);
        }

        private void DrawPyroFlareHudCore(int frameIndex)
        {
            DrawPyroFlareHudCore(frameIndex, SourceMainAmmoHudY);
        }

        private void DrawPyroFlareHudCore(int frameIndex, float sourceY)
        {
            var flareCount = GetLocalDisplayedMainWeaponCurrentShells() / PlayerEntity.PyroFlareAmmoRequirement;
            if (flareCount <= 0)
            {
                return;
            }

            var flareTint = _game.GetPlayerPyroFlareCooldownTicks(_game._world.LocalPlayer) <= 0 ? Color.White : DisabledAmmoHudColor;
            for (var flareIndex = 0; flareIndex < flareCount; flareIndex += 1)
            {
                _game.TryDrawScreenSprite("FlareS", frameIndex, GetSourceHudPoint(760f - (flareIndex * 20f), sourceY + 7f), flareTint, GetSourceHudSpriteScale(Vector2.One));
            }
        }

        private bool TryDrawSourceAmmoHudSpriteCore(string spriteName, int frameIndex)
        {
            return TryDrawSourceAmmoHudSpriteCore(spriteName, frameIndex, SourceMainAmmoHudY);
        }

        private bool TryDrawSourceAmmoHudSpriteCore(string spriteName, int frameIndex, float sourceY)
        {
            return _game.TryDrawScreenSprite(spriteName, frameIndex, GetSourceHudPoint(728f, sourceY), Color.White, GetSourceHudSpriteScale(new Vector2(2.4f, 2.4f)));
        }

        private void DrawSourceAmmoHudBarCore(float left, float width, float value, float maxValue, Color fillColor)
        {
            DrawSourceAmmoHudBarCore(left, SourceMainAmmoHudY + 4f, width, value, maxValue, fillColor);
        }

        private void DrawSourceAmmoHudBarCore(float left, float top, float width, float value, float maxValue, Color fillColor)
        {
            _game.DrawScreenHealthBar(GetSourceHudRectangle(left, top, width, 8f), value, maxValue, false, fillColor, Color.Black);
        }

        private Rectangle GetReloadAmmoHudBarRectangleCore()
        {
            return GetReloadAmmoHudBarRectangleAt(SourceMainAmmoHudY);
        }

        private Rectangle GetReloadAmmoHudBarRectangleAt(float sourceY)
        {
            return GetWeaponReloadAmmoHudBarRectangleAt(GetLocalDisplayedMainWeaponPresentationItemId(), sourceY);
        }

        private Rectangle GetWeaponReloadAmmoHudBarRectangleAt(string itemId, float sourceY)
        {
            return string.Equals(itemId, "weapon.rocketlauncher", StringComparison.Ordinal)
                ? GetSourceHudRectangle(689f, sourceY + 4f, 34f, 8f)
                : GetSourceHudRectangle(700f, sourceY + 4f, 50f, 8f);
        }

        private Vector2 GetSourceHudPointCore(float sourceX, float sourceY)
        {
            var unscaledPosition = new Vector2(_game.ViewportWidth - SourceHudWidth + sourceX, _game.ViewportHeight - SourceHudHeight + sourceY) + _sourceHudOffset;
            return _sourceHudScaleOrigin + ((unscaledPosition - _sourceHudScaleOrigin) * _sourceHudScale);
        }

        private Vector2 GetUnshiftedSourceHudPoint(float sourceX, float sourceY)
        {
            return new Vector2(_game.ViewportWidth - SourceHudWidth + sourceX, _game.ViewportHeight - SourceHudHeight + sourceY);
        }

        private Vector2 GetSourceHudSpriteScale(Vector2 scale)
        {
            return scale * _sourceHudScale;
        }

        private float GetSourceHudTextScale(float scale)
        {
            return scale * _sourceHudScale;
        }

        private void UpdateWeaponStackBounds(float sourceX, float sourceY, float width, float height)
        {
            _game.UpdateHudElementBounds(HudElementId.LocalWeaponStack, GetSourceHudRectangle(sourceX, sourceY, width, height));
        }

        private void UpdateAbilityStackBounds(float sourceX, float sourceY, float width, float height)
        {
            _game.UpdateHudElementBounds(HudElementId.LocalAbilityStack, GetSourceHudRectangle(sourceX, sourceY, width, height));
        }

        private Rectangle GetSourceHudRectangleCore(float sourceX, float sourceY, float width, float height)
        {
            var position = GetSourceHudPoint(sourceX, sourceY);
            return new Rectangle(
                (int)MathF.Round(position.X),
                (int)MathF.Round(position.Y),
                Math.Max(1, (int)MathF.Round(width * _sourceHudScale)),
                Math.Max(1, (int)MathF.Round(height * _sourceHudScale)));
        }

        private int CountLocalOwnedStickyMinesCore()
        {
            var count = 0;
            foreach (var mine in _game._world.Mines)
            {
                if (mine.OwnerId == _game._world.LocalPlayer.Id)
                {
                    count += 1;
                }
            }

            return count;
        }

        private string? GetAmmoHudSpriteNameCore()
        {
            return CharacterClassCatalog.RuntimeRegistry.GetRequiredItem(GetLocalDisplayedMainWeaponPresentationItemId()).Presentation.HudSpriteName;
        }

        private int GetAmmoHudFrameIndexCore()
        {
            var presentation = CharacterClassCatalog.RuntimeRegistry.GetRequiredItem(GetLocalDisplayedMainWeaponPresentationItemId()).Presentation;
            if (presentation.UseAmmoCountForHudFrame)
            {
                return GetLocalDisplayedMainWeaponCurrentShells()
                    + (_game._world.LocalPlayerTeam == PlayerTeam.Blue ? presentation.BlueTeamAmmoHudFrameOffset : 0);
            }

            return _game._world.LocalPlayerTeam == PlayerTeam.Blue ? presentation.BlueTeamHudFrameOffset : 0;
        }

        private void DrawAmmoReloadBarCore(Rectangle barRectangle)
        {
            _game.DrawScreenHealthBar(barRectangle, GetAmmoReloadBarProgress(_game._world.LocalPlayer), 1f, false, AmmoHudBarColor, Color.Black);
        }

        private float GetAmmoReloadBarProgressCore(PlayerEntity player)
        {
            if (ReferenceEquals(player, _game._world.LocalPlayer) && player.IsAcquiredWeaponPresented)
            {
                if (player.AcquiredWeaponClassId == PlayerClass.Medic)
                {
                    return GetMedicNeedleReloadProgress(player.AcquiredWeaponCurrentShells, player.AcquiredWeaponMaxShells, player.MedicNeedleRefillTicks);
                }

                var currentShells = player.AcquiredWeaponCurrentShells;
                var maxShells = player.AcquiredWeaponMaxShells;
                if (currentShells >= maxShells)
                {
                    return 1f;
                }

                var reloadTicksUntilNextShell = player.AcquiredWeaponReloadTicksUntilNextShell;
                if (reloadTicksUntilNextShell <= 0)
                {
                    return 1f;
                }

                var reloadTicks = Math.Max(1, player.AcquiredWeapon?.AmmoReloadTicks ?? 1);
                return Math.Clamp(1f - (reloadTicksUntilNextShell / (float)reloadTicks), 0f, 1f);
            }

            if (ReferenceEquals(player, _game._world.LocalPlayer) && IsLocalDisplayedOffhandWeaponSelected())
            {
                var currentShells = GetLocalDisplayedOffhandCurrentShells();
                var maxShells = GetLocalDisplayedOffhandMaxShells();
                if (currentShells >= maxShells)
                {
                    return 1f;
                }

                var reloadTicksUntilNextShell = GetLocalDisplayedOffhandReloadTicks();
                if (reloadTicksUntilNextShell <= 0)
                {
                    return 1f;
                }

                var reloadTicks = Math.Max(1, player.ExperimentalOffhandWeapon?.AmmoReloadTicks ?? 1);
                return Math.Clamp(1f - (reloadTicksUntilNextShell / (float)reloadTicks), 0f, 1f);
            }

            var displayedShells = _game.GetPlayerCurrentShells(player);
            if (ReferenceEquals(player, _game._world.LocalPlayer) && IsLocalMedicKritzHealNeedlesPresented())
            {
                return GetMedicNeedleReloadProgress(
                    player.ExperimentalOffhandCurrentShells,
                    Math.Max(1, player.ExperimentalOffhandMaxShells),
                    player.ExperimentalOffhandReloadTicksUntilNextShell);
            }

            if (displayedShells >= player.MaxShells)
            {
                return 1f;
            }

            if (player.ClassId == PlayerClass.Medic)
            {
                return GetMedicNeedleReloadProgress(displayedShells, player.MaxShells, _game.GetPlayerMedicNeedleRefillTicks(player));
            }

            var reloadTicksRemaining = _game.GetPlayerReloadTicksUntilNextShell(player);
            if (reloadTicksRemaining <= 0)
            {
                return 1f;
            }

            var reloadTicksTotal = Math.Max(1, player.PrimaryWeapon.AmmoReloadTicks);
            return Math.Clamp(1f - (reloadTicksRemaining / (float)reloadTicksTotal), 0f, 1f);
        }

        private bool IsLocalDisplayedMainWeaponAcquiredCore() => _game._world.LocalPlayer.IsAcquiredWeaponPresented;
        private string GetLocalDisplayedMainWeaponPresentationItemIdCore()
        {
            if (IsLocalMedicKritzHealNeedlesPresented())
            {
                return _game._world.LocalPlayer.GameplayLoadoutState.SecondaryItemId ?? "weapon.medigun.crit";
            }

            return _game._world.LocalPlayer.IsAcquiredWeaponPresented
                ? _game._world.LocalPlayer.GameplayLoadoutState.AcquiredItemId ?? _game._world.LocalPlayer.GameplayLoadoutState.PrimaryItemId
                : IsLocalDisplayedOffhandWeaponSelected()
                    ? GetLocalDisplayedOffhandPresentationItemId()
                    : _game._world.LocalPlayer.GameplayLoadoutState.PrimaryItemId;
        }

        private PrimaryWeaponDefinition GetLocalDisplayedMainWeaponStatsCore()
        {
            if (IsLocalMedicKritzHealNeedlesPresented())
            {
                return _game._world.LocalPlayer.ExperimentalOffhandWeapon ?? _game._world.LocalPlayer.PrimaryWeapon;
            }

            return _game._world.LocalPlayer.IsAcquiredWeaponPresented
                ? _game._world.LocalPlayer.AcquiredWeapon ?? _game._world.LocalPlayer.PrimaryWeapon
                : IsLocalDisplayedOffhandWeaponSelected()
                    ? _game._world.LocalPlayer.ExperimentalOffhandWeapon ?? _game._world.LocalPlayer.PrimaryWeapon
                    : _game._world.LocalPlayer.PrimaryWeapon;
        }

        private int GetLocalDisplayedMainWeaponCurrentShellsCore()
        {
            if (IsLocalMedicKritzHealNeedlesPresented())
            {
                return _game._world.LocalPlayer.ExperimentalOffhandCurrentShells;
            }

            return _game._world.LocalPlayer.IsAcquiredWeaponPresented
                ? _game._world.LocalPlayer.AcquiredWeaponCurrentShells
                : IsLocalDisplayedOffhandWeaponSelected()
                    ? GetLocalDisplayedOffhandCurrentShells()
                    : _game.GetPlayerCurrentShells(_game._world.LocalPlayer);
        }

        private int GetLocalDisplayedMainWeaponMaxShellsCore()
        {
            if (IsLocalMedicKritzHealNeedlesPresented())
            {
                return Math.Max(1, _game._world.LocalPlayer.ExperimentalOffhandMaxShells);
            }

            return _game._world.LocalPlayer.IsAcquiredWeaponPresented
                ? _game._world.LocalPlayer.AcquiredWeaponMaxShells
                : IsLocalDisplayedOffhandWeaponSelected()
                    ? GetLocalDisplayedOffhandMaxShells()
                    : _game._world.LocalPlayer.MaxShells;
        }

        private int GetLocalDisplayedMainWeaponCooldownTicksCore()
        {
            if (IsLocalMedicKritzHealNeedlesPresented())
            {
                return _game._world.LocalPlayer.ExperimentalOffhandCooldownTicks;
            }

            return _game._world.LocalPlayer.IsAcquiredWeaponPresented
                ? _game._world.LocalPlayer.AcquiredWeaponCooldownTicks
                : IsLocalDisplayedOffhandWeaponSelected()
                    ? _game._world.LocalPlayer.ExperimentalOffhandCooldownTicks
                    : _game.GetPlayerPrimaryCooldownTicks(_game._world.LocalPlayer);
        }

        private int GetLocalDisplayedMainWeaponReloadTicksCore()
        {
            if (IsLocalMedicKritzHealNeedlesPresented())
            {
                return _game._world.LocalPlayer.ExperimentalOffhandReloadTicksUntilNextShell;
            }

            return _game._world.LocalPlayer.IsAcquiredWeaponPresented
                ? _game._world.LocalPlayer.AcquiredWeaponReloadTicksUntilNextShell
                : IsLocalDisplayedOffhandWeaponSelected()
                    ? GetLocalDisplayedOffhandReloadTicks()
                    : _game.GetPlayerReloadTicksUntilNextShell(_game._world.LocalPlayer);
        }

        private bool IsLocalMedicKritzHealNeedlesPresented()
        {
            var player = _game._world.LocalPlayer;
            return player.ClassId == PlayerClass.Medic
                && player.IsExperimentalOffhandSelected
                && player.HasEquippedBehavior(BuiltInGameplayBehaviorIds.MedigunCrit);
        }

        private bool IsLocalDisplayedOffhandWeaponSelected()
        {
            var player = _game._world.LocalPlayer;
            if (player.IsAcquiredWeaponPresented
                || !player.IsExperimentalOffhandSelected
                || player.ExperimentalOffhandWeapon is null)
            {
                return false;
            }

            // Medic's mediguns are utility offhand weapons; the needlegun is always the
            // displayed ammo weapon regardless of which medigun slot is selected.
            if (player.HasEquippedBehavior(BuiltInGameplayBehaviorIds.Medigun)
                || player.HasEquippedBehavior(BuiltInGameplayBehaviorIds.MedigunCrit))
            {
                return false;
            }

            return true;
        }

        private string GetLocalDisplayedOffhandPresentationItemId()
        {
            var player = _game._world.LocalPlayer;
            if (!string.IsNullOrWhiteSpace(player.GameplayLoadoutState.EquippedItemId))
            {
                return player.GameplayLoadoutState.EquippedItemId;
            }

            return player.GameplayLoadoutState.SecondaryItemId
                ?? player.GameplayLoadoutState.UtilityItemId
                ?? player.GameplayLoadoutState.PrimaryItemId;
        }

        private int GetLocalDisplayedOffhandCurrentShells()
        {
            var player = _game._world.LocalPlayer;
            if (string.Equals(player.EquippedBehaviorId, BuiltInGameplayBehaviorIds.GrenadeLauncher, StringComparison.Ordinal)
                && player.TryGetReplicatedStateInt(CoreReplicatedOwnerId, DemomanGrenadeLauncherAmmoKey, out var replicatedGrenadeAmmo))
            {
                return replicatedGrenadeAmmo;
            }

            if (player.ClassId == PlayerClass.Scout
                && player.TryGetReplicatedStateInt(CoreReplicatedOwnerId, ScoutNailgunAmmoKey, out var replicatedNailgunAmmo))
            {
                return replicatedNailgunAmmo;
            }

            return player.TryGetReplicatedStateInt(CoreReplicatedOwnerId, SoldierShotgunAmmoKey, out var replicatedShotgunAmmo)
                ? replicatedShotgunAmmo
                : player.ExperimentalOffhandCurrentShells;
        }

        private int GetLocalDisplayedOffhandMaxShells()
        {
            var player = _game._world.LocalPlayer;
            if (string.Equals(player.EquippedBehaviorId, BuiltInGameplayBehaviorIds.GrenadeLauncher, StringComparison.Ordinal))
            {
                return GetLocalGrenadeLauncherMaxAmmo();
            }

            if (player.ClassId == PlayerClass.Scout
                && player.TryGetReplicatedStateInt(CoreReplicatedOwnerId, ScoutNailgunMaxAmmoKey, out var replicatedNailgunMaxAmmo))
            {
                return Math.Max(1, replicatedNailgunMaxAmmo);
            }

            return player.TryGetReplicatedStateInt(CoreReplicatedOwnerId, SoldierShotgunMaxAmmoKey, out var replicatedShotgunMaxAmmo)
                ? Math.Max(1, replicatedShotgunMaxAmmo)
                : Math.Max(1, player.ExperimentalOffhandMaxShells);
        }

        private int GetLocalGrenadeLauncherMaxAmmo(GameplayItemDefinition? fallbackItem = null)
        {
            var player = _game._world.LocalPlayer;
            if (player.TryGetReplicatedStateInt(CoreReplicatedOwnerId, DemomanGrenadeLauncherMaxAmmoKey, out var replicatedMaxAmmo))
            {
                return Math.Max(1, replicatedMaxAmmo);
            }

            var item = fallbackItem ?? TryGetLocalGrenadeLauncherItem();
            if (item is not null)
            {
                return Math.Max(1, item.Ammo.MaxAmmo);
            }

            return Math.Max(1, player.ExperimentalOffhandWeapon?.MaxAmmo ?? player.ExperimentalOffhandMaxShells);
        }

        private int GetLocalGrenadeLauncherReloadTicks(GameplayItemDefinition? fallbackItem = null)
        {
            var item = fallbackItem ?? TryGetLocalGrenadeLauncherItem();
            if (item is not null)
            {
                return Math.Max(1, (int)item.Ammo.ReloadSourceTicks);
            }

            return Math.Max(1, _game._world.LocalPlayer.ExperimentalOffhandWeapon?.AmmoReloadTicks ?? 1);
        }

        private GameplayItemDefinition? TryGetLocalGrenadeLauncherItem()
        {
            var utilityItemId = _game._world.LocalPlayer.GameplayLoadoutState.UtilityItemId;
            if (string.IsNullOrWhiteSpace(utilityItemId))
            {
                return null;
            }

            var item = CharacterClassCatalog.RuntimeRegistry.GetRequiredItem(utilityItemId);
            return string.Equals(item.BehaviorId, BuiltInGameplayBehaviorIds.GrenadeLauncher, StringComparison.Ordinal)
                ? item
                : null;
        }

        private int GetLocalDisplayedOffhandReloadTicks()
        {
            var player = _game._world.LocalPlayer;
            return player.ExperimentalOffhandReloadTicksUntilNextShell;
        }

        private string GetLocalAlternatePrimaryWeaponPresentationItemIdCore() => _game._world.LocalPlayer.IsAcquiredWeaponPresented
            ? _game._world.LocalPlayer.GameplayLoadoutState.PrimaryItemId
            : _game._world.LocalPlayer.GameplayLoadoutState.AcquiredItemId ?? _game._world.LocalPlayer.GameplayLoadoutState.PrimaryItemId;
        private PrimaryWeaponDefinition GetLocalAlternatePrimaryWeaponStatsCore() => _game._world.LocalPlayer.IsAcquiredWeaponPresented ? _game._world.LocalPlayer.PrimaryWeapon : _game._world.LocalPlayer.AcquiredWeapon ?? _game._world.LocalPlayer.PrimaryWeapon;
        private int GetLocalAlternatePrimaryWeaponCurrentShellsCore() => _game._world.LocalPlayer.IsAcquiredWeaponPresented ? _game.GetPlayerCurrentShells(_game._world.LocalPlayer) : _game._world.LocalPlayer.AcquiredWeaponCurrentShells;
        private int GetLocalAlternatePrimaryWeaponMaxShellsCore() => _game._world.LocalPlayer.IsAcquiredWeaponPresented ? _game._world.LocalPlayer.MaxShells : _game._world.LocalPlayer.AcquiredWeaponMaxShells;

        private float GetLocalAlternatePrimaryWeaponReloadProgressCore()
        {
            var currentShells = GetLocalAlternatePrimaryWeaponCurrentShells();
            var maxShells = Math.Max(1, GetLocalAlternatePrimaryWeaponMaxShells());
            if (currentShells >= maxShells)
            {
                return 1f;
            }

            if (_game._world.LocalPlayer.IsAcquiredWeaponPresented)
            {
                var reloadTicksUntilNextShell = _game.GetPlayerReloadTicksUntilNextShell(_game._world.LocalPlayer);
                if (reloadTicksUntilNextShell <= 0)
                {
                    return 1f;
                }

                var reloadTicks = Math.Max(1, _game._world.LocalPlayer.PrimaryWeapon.AmmoReloadTicks);
                return Math.Clamp(1f - (reloadTicksUntilNextShell / (float)reloadTicks), 0f, 1f);
            }

            if (_game._world.LocalPlayer.AcquiredWeaponClassId == PlayerClass.Medic)
            {
                return GetMedicNeedleReloadProgress(_game._world.LocalPlayer.AcquiredWeaponCurrentShells, _game._world.LocalPlayer.AcquiredWeaponMaxShells, _game._world.LocalPlayer.MedicNeedleRefillTicks);
            }

            var acquiredReloadTicksUntilNextShell = _game._world.LocalPlayer.AcquiredWeaponReloadTicksUntilNextShell;
            if (acquiredReloadTicksUntilNextShell <= 0)
            {
                return 1f;
            }

            var acquiredReloadTicks = Math.Max(1, GetLocalAlternatePrimaryWeaponStats().AmmoReloadTicks);
            return Math.Clamp(1f - (acquiredReloadTicksUntilNextShell / (float)acquiredReloadTicks), 0f, 1f);
        }

        private static float GetMedicNeedleReloadProgress(int currentShells, int maxShells, int refillTicks)
        {
            if (currentShells >= maxShells)
            {
                return 1f;
            }

            if (refillTicks <= 0)
            {
                return currentShells < maxShells ? 1f : 0f;
            }

            return Math.Clamp(1f - (refillTicks / (float)PlayerEntity.MedicNeedleRefillTicksDefault), 0f, 1f);
        }
    }
}
