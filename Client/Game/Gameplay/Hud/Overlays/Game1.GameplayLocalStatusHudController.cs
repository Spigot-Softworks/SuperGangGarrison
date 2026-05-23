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
    private const float DamageVignetteFadeOutPerSecond = 0.32f;
    private const float DamageVignetteReactiveFadePerSecond = 0.36f;
    private const float DamageVignetteReactiveMinimumIntensity = 0.34f;
    private const float DamageVignetteReactiveMaximumIntensity = 0.88f;
    private const float DamageVignetteReactiveDamageScale = 120f;
    private const float DamageVignettePersistentMinimumIntensity = 0.24f;
    private const float DamageVignettePersistentMaximumIntensity = 1f;
    private const float DamageVignetteMinimumVisibleIntensity = 0.025f;
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
        private const string SoldierShotgunReloadTicksKey = "soldier_shotgun_reload_ticks";
        private const string DemomanGrenadeLauncherAmmoKey = "demoman_gl_ammo";
        private const string DemomanGrenadeLauncherReloadTicksKey = "demoman_gl_reload_ticks";

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
        private const string DefaultAbilityCooldownHudSpriteName = "ChargeJumpS";
        private const float WeaponHudPanelScale = 2.4f;
        private const float WeaponHudPanelGapPixels = 4f;
        private const float WeaponHudFallbackPanelHeight = 38f;
        private const int WeaponHudOrderAcquired = 10;
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
            int Order = 0);

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
                    var weaponDefinition = GameplayWeaponRenderController.GetWeaponRenderDefinitionProxy(_game._world.LocalPlayer);
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
                                _game._world.LocalPlayer,
                                WeaponAnimationMode.Idle,
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
                    Order: WeaponHudOrderPrimary));
                return rows;
            }

            var displayedWeaponStats = GetLocalDisplayedMainWeaponStats();
            var hasGrenadeLauncher = HasLocalDemomanGrenadeLauncher();
            if (hasGrenadeLauncher)
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
                    SourceMainAmmoHudY,
                    Order: GetHudRowOrder(utilityHud, WeaponHudOrderUtility)));
            }

            AddPrimaryWeaponHudRow(rows, displayedWeaponStats, hasGrenadeLauncher);

            if (ShouldDrawAcquiredWeaponHud())
            {
                rows.Add(new WeaponHudRow(
                    "local.weapon.acquired",
                    44f,
                    _ => DrawAcquiredWeaponHud(),
                    507f,
                    ReservesFlowSpace: false,
                    Order: WeaponHudOrderAcquired));
            }

            if (ShouldDrawSecondaryWeaponHudRow(out var secondaryItem))
            {
                rows.Add(new WeaponHudRow(
                    "local.weapon.secondary",
                    GetHudSpriteFrameHeight(secondaryItem.Presentation.HudSpriteName, WeaponHudPanelScale, WeaponHudFallbackPanelHeight),
                    DrawSecondaryWeaponHudRow,
                    Order: WeaponHudOrderSecondary));
            }

            AddConfiguredUtilityHudRow(rows, hasGrenadeLauncher);
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
                    "class.demoman.sticky",
                    GetHudSpriteFrameHeight("StickyCounterS", 3f, 36f),
                    sourceY => DrawStickyCounterHud(stickyCount, maxStickies, frameIndex, sourceY),
                    GetDemomanStickyAbilityHudSourceY(hasGrenadeLauncher),
                    Order: hasGrenadeLauncher ? WeaponHudOrderPostPrimaryAbility : WeaponHudOrderAbility));
            }

            AddConfiguredAbilityHudRows(rows);
            return rows;
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
                var height = GetHudSpriteFrameHeight(hudSpriteName, 2f, 28f);
                rows.Add(new WeaponHudRow(
                    $"local.ability.{item.Id}",
                    height,
                    sourceY => DrawConfiguredAbilityCooldownHud(item, sourceY),
                    GetConfiguredAbilityLegacySourceY(item),
                    Order: GetHudRowOrder(hud, WeaponHudOrderAbility)));
            }
        }

        private static bool IsAbilityCooldownHudStateProvider(string stateProvider)
        {
            return string.Equals(stateProvider, GameplayItemHudStateProviders.AbilityCooldown, StringComparison.OrdinalIgnoreCase)
                || string.Equals(stateProvider, GameplayItemHudStateProviders.HeavySandvichCooldown, StringComparison.OrdinalIgnoreCase);
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
                BuiltInGameplayBehaviorIds.HeavyGhostDash => 498f,
                BuiltInGameplayBehaviorIds.SpySuperjump => SourceAbilityHudY,
                _ => null,
            };
        }

        private static string GetAbilityCooldownHudSpriteName(GameplayItemDefinition item)
        {
            return string.IsNullOrWhiteSpace(item.Presentation.HudSpriteName)
                ? DefaultAbilityCooldownHudSpriteName
                : item.Presentation.HudSpriteName!;
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
                || string.Equals(hud.DisplayKind, GameplayItemHudDisplayKinds.CooldownIcon, StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsWeaponAmmoPanelHud(GameplayItemHudPresentationDefinition? hud)
        {
            return hud is not null
                && string.Equals(hud.StackGroup, GameplayItemHudStackGroups.Weapon, StringComparison.OrdinalIgnoreCase)
                && string.Equals(hud.DisplayKind, GameplayItemHudDisplayKinds.AmmoPanel, StringComparison.OrdinalIgnoreCase);
        }

        private static int GetHudRowOrder(GameplayItemHudPresentationDefinition? hud, int fallbackOrder)
        {
            return hud is null || hud.Order == 0 ? fallbackOrder : hud.Order;
        }

        private void DrawWeaponHudRows(List<WeaponHudRow> rows)
        {
            var minSourceX = 586f;
            var maxSourceX = 800f;
            var minSourceY = 492f;
            var maxSourceY = 596f;
            var flowTopSourceY = SourceMainAmmoHudY;

            foreach (var row in rows.OrderBy(static row => row.Order))
            {
                var sourceY = row.LegacySourceY ?? (flowTopSourceY - row.Height - WeaponHudPanelGapPixels);
                row.Draw(sourceY);

                if (row.ReservesFlowSpace)
                {
                    flowTopSourceY = Math.Min(flowTopSourceY, sourceY);
                }

                minSourceY = Math.Min(minSourceY, sourceY - 8f);
                maxSourceY = Math.Max(maxSourceY, sourceY + row.Height + 10f);
            }

            UpdateWeaponStackBounds(minSourceX, minSourceY, maxSourceX - minSourceX, maxSourceY - minSourceY);
        }

        private void DrawAbilityHudRows(List<WeaponHudRow> rows)
        {
            if (rows.Count == 0)
            {
                return;
            }

            var minSourceX = 696f;
            var maxSourceX = 800f;
            var minSourceY = 492f;
            var maxSourceY = 558f;
            var flowTopSourceY = SourceAbilityHudY;

            foreach (var row in rows.OrderBy(static row => row.Order))
            {
                var sourceY = row.LegacySourceY ?? (flowTopSourceY - row.Height - WeaponHudPanelGapPixels);
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
        public void DrawHeavySandwichHud() => DrawHeavySandwichHudCore();
        public void DrawHeavyGhostDashHud() => DrawHeavyGhostDashHudCore();

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
                * (ClientSettings.NormalizeDamageVignetteIntensityPercent(_game._damageVignetteIntensityPercent) / 100f);
            if (renderIntensity <= DamageVignetteMinimumVisibleIntensity)
            {
                return;
            }

            if (!_game.TryEnsureDamageVignetteTexture(renderIntensity, out var texture))
            {
                return;
            }

            _game._spriteBatch.Draw(texture, new Rectangle(0, 0, _game.ViewportWidth, _game.ViewportHeight), Color.White);
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
        public void DrawAcquiredWeaponHud() => DrawAcquiredWeaponHudCore();
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

        private void DrawHeavySandwichHudCore()
        {
            if (_game._world.LocalPlayer.ClassId != PlayerClass.Heavy)
            {
                return;
            }

            var sandwichHudSpriteName = StockGameplayModCatalog.GetSecondaryItem(PlayerClass.Heavy)?.Presentation.HudSpriteName ?? "SandwichHudS";
            if (!_game.TryDrawScreenSprite(sandwichHudSpriteName, _game._world.LocalPlayer.Team == PlayerTeam.Blue ? 1 : 0, GetSourceHudPoint(730f, 515f), Color.White, GetSourceHudSpriteScale(new Vector2(2f, 2f))))
            {
                return;
            }

            var cooldownRemaining = Math.Clamp(_game.GetPlayerHeavyEatCooldownTicksRemaining(_game._world.LocalPlayer), 0, PlayerEntity.HeavySandvichCooldownTicks);
            _game.DrawScreenHealthBar(GetSourceHudRectangle(715f, 528f, 35f, 5f), PlayerEntity.HeavySandvichCooldownTicks - cooldownRemaining, PlayerEntity.HeavySandvichCooldownTicks, false, AmmoHudBarColor, Color.Black);
        }

        private void DrawHeavyGhostDashHudCore()
        {
            var utilityItemId = _game._world.LocalPlayer.GameplayLoadoutState.UtilityItemId;
            if (string.IsNullOrWhiteSpace(utilityItemId))
            {
                return;
            }

            DrawConfiguredAbilityCooldownHud(CharacterClassCatalog.RuntimeRegistry.GetRequiredItem(utilityItemId), 498f);
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
            var hasHudPlaque = !string.IsNullOrWhiteSpace(hudSpriteName);
            if (hasHudPlaque)
            {
                var frameIndex = _game._world.LocalPlayer.Team == PlayerTeam.Blue ? presentation.BlueTeamHudFrameOffset : 0;
                if (!_game.TryDrawScreenSprite(hudSpriteName, frameIndex, GetSourceHudPoint(730f, sourceY), iconColor, GetSourceHudSpriteScale(new Vector2(2f, 2f))))
                {
                    return;
                }
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
            var barRectangle = hasHudPlaque
                ? GetSourceHudRectangle(715f, sourceY + 13f, 35f, 5f)
                : GetSourceHudRectangle(706f, sourceY, 48f, 5f);
            _game.DrawScreenHealthBar(barRectangle, meterFraction, 1f, false, meterColor, Color.Black);
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

            if (TryResolveGenericAbilityCooldownHudState(item, ability, player, out cooldownRemaining, out maxCooldownTicks, out isActive, out isDisabled))
            {
                return true;
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
                    cooldownRemaining = Math.Clamp(
                        _game.GetPlayerHeavyEatCooldownTicksRemaining(player),
                        0,
                        maxCooldownTicks);
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
                    cooldownRemaining = player.TryGetReplicatedStateInt(
                        GameplayAbilityConstants.CoreAbilityReplicatedStateOwnerId,
                        GameplayAbilityReplicatedState.HeavyDashCooldownTicksKey,
                        out var replicatedHeavyDashCooldown)
                        ? replicatedHeavyDashCooldown
                        : _game.GetPlayerExperimentalGhostDashCooldownTicksRemaining(player);
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
                    cooldownRemaining = player.TryGetReplicatedStateInt(
                        GameplayAbilityConstants.CoreAbilityReplicatedStateOwnerId,
                        GameplayAbilityReplicatedState.SpySuperjumpCooldownTicksKey,
                        out var replicatedSpySuperjumpCooldown)
                        ? replicatedSpySuperjumpCooldown
                        : player.SpySuperjumpCooldownTicksRemaining;
                    isActive = player.SpySuperjumpChargeTicks > 0 || player.IsSpySuperjumping;
                    isDisabled = player.IsCarryingIntel;
                    return true;
                default:
                    return false;
            }
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
            if (!player.TryGetReplicatedStateInt(stateOwner, cooldownKey, out cooldownRemaining)
                && !(string.Equals(stateOwner, GameplayAbilityConstants.CoreAbilityReplicatedStateOwnerId, StringComparison.Ordinal)
                    && GameplayAbilityReplicatedState.TryGetInt(player, cooldownKey, out cooldownRemaining)))
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
                isActive = player.TryGetReplicatedStateBool(stateOwner, activeKey, out var replicatedActive)
                    ? replicatedActive
                    : string.Equals(stateOwner, GameplayAbilityConstants.CoreAbilityReplicatedStateOwnerId, StringComparison.Ordinal)
                        && GameplayAbilityReplicatedState.TryGetBool(player, activeKey, out replicatedActive)
                        && replicatedActive;
            }

            if (!string.IsNullOrWhiteSpace(hud.DisabledKey))
            {
                var disabledKey = hud.DisabledKey.Trim();
                isDisabled = player.TryGetReplicatedStateBool(stateOwner, disabledKey, out var replicatedDisabled)
                    ? replicatedDisabled
                    : string.Equals(stateOwner, GameplayAbilityConstants.CoreAbilityReplicatedStateOwnerId, StringComparison.Ordinal)
                        && GameplayAbilityReplicatedState.TryGetBool(player, disabledKey, out replicatedDisabled)
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
            if (!_game.TryDrawScreenSprite("StickyCounterS", frameIndex, GetSourceHudPoint(735f, sourceY), Color.White, GetSourceHudSpriteScale(new Vector2(3f, 3f))))
            {
                return false;
            }

            _game.DrawHudTextLeftAligned(stickyCount.ToString(CultureInfo.InvariantCulture), GetSourceHudPoint(717f, sourceY + 2f), AmmoHudBarColor, GetSourceHudTextScale(1.5f));
            _game.DrawHudTextLeftAligned($"/{maxStickies.ToString(CultureInfo.InvariantCulture)}", GetSourceHudPoint(730f, sourceY + 2f), AmmoHudBarColor, GetSourceHudTextScale(1.5f));
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
            var reloadTicksRemaining = _game._world.LocalPlayer.TryGetReplicatedStateInt(CoreReplicatedOwnerId, DemomanGrenadeLauncherReloadTicksKey, out var replicatedReloadTicks)
                ? Math.Max(0, replicatedReloadTicks)
                : _game._world.LocalPlayer.ExperimentalOffhandReloadTicksUntilNextShell;
            var weaponDefinition = _game._world.LocalPlayer.ExperimentalOffhandWeapon;
            var maxAmmo = Math.Max(1, weaponDefinition?.MaxAmmo ?? utilityItem.Ammo.MaxAmmo);
            var totalReloadTicks = Math.Max(1, weaponDefinition?.AmmoReloadTicks ?? (int)utilityItem.Ammo.ReloadSourceTicks);

            var ammoCountScale = GetAmmoCountBuildScaleForValue(currentAmmo);
            var reloadBarBottomSourceY = sourceY + 12f;
            var ammoTextSourceY = reloadBarBottomSourceY - _game.MeasureMenuBitmapFontHeight(ammoCountScale);
            var ammoColor = currentAmmo <= Math.Max(1, maxAmmo / 4) ? LowAmmoHudColor : AmmoHudTextColor;
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

            var currentShells = _game._world.LocalPlayer.TryGetReplicatedStateInt(CoreReplicatedOwnerId, SoldierShotgunAmmoKey, out var replicatedOffhandAmmo)
                ? replicatedOffhandAmmo
                : _game._world.LocalPlayer.ExperimentalOffhandCurrentShells;
            var maxShells = _game._world.LocalPlayer.TryGetReplicatedStateInt(CoreReplicatedOwnerId, SoldierShotgunMaxAmmoKey, out var replicatedOffhandMaxAmmo)
                ? Math.Max(1, replicatedOffhandMaxAmmo)
                : Math.Max(1, _game._world.LocalPlayer.ExperimentalOffhandMaxShells);
            var reloadTicksRemaining = _game._world.LocalPlayer.TryGetReplicatedStateInt(CoreReplicatedOwnerId, SoldierShotgunReloadTicksKey, out var replicatedOffhandReloadTicks)
                ? Math.Max(0, replicatedOffhandReloadTicks)
                : _game._world.LocalPlayer.ExperimentalOffhandReloadTicksUntilNextShell;
            var reloadTicksPerShell = Math.Max(1, _game._world.LocalPlayer.ExperimentalOffhandWeapon?.AmmoReloadTicks ?? CharacterClassCatalog.SoldierShotgun.AmmoReloadTicks);
            var reloadProgress = currentShells >= maxShells
                ? 1f
                : reloadTicksRemaining <= 0
                    ? 1f
                    : Math.Clamp(1f - (reloadTicksRemaining / (float)reloadTicksPerShell), 0f, 1f);
            var ammoColor = currentShells <= Math.Max(1, maxShells / 4) ? LowAmmoHudColor : AmmoHudTextColor;
            var ammoCountScale = GetAmmoCountBuildScaleForValue(currentShells);

            var shotgunReloadBarBottomSourceY = shotgunPanelSourceY + 12f;
            var shotgunAmmoTextSourceY = shotgunReloadBarBottomSourceY - _game.MeasureMenuBitmapFontHeight(ammoCountScale);
            _game.DrawMenuBitmapFontText(currentShells.ToString(CultureInfo.InvariantCulture), GetSourceHudPoint(shotgunPanelSourceX + 27f, shotgunAmmoTextSourceY), ammoColor, GetSourceHudTextScale(ammoCountScale));
            _game.DrawScreenHealthBar(GetSourceHudRectangle(shotgunPanelSourceX - 28f, shotgunPanelSourceY + 4f, 50f, 8f), reloadProgress, 1f, false, AmmoHudBarColor, Color.Black);
        }

        private bool ShouldDrawSecondaryWeaponHudRow(out GameplayItemDefinition item)
        {
            item = null!;
            var hasReplicatedShotgun = _game._world.LocalPlayer.TryGetReplicatedStateBool(CoreReplicatedOwnerId, SoldierShotgunAvailableKey, out var replicatedShotgunAvailable)
                && replicatedShotgunAvailable;

            var secondaryItemId = _game._world.LocalPlayer.GameplayLoadoutState.SecondaryItemId;
            if (string.IsNullOrWhiteSpace(secondaryItemId))
            {
                return false;
            }

            item = CharacterClassCatalog.RuntimeRegistry.GetRequiredItem(secondaryItemId);
            return ShouldDrawSecondaryWeaponHudRow(item, hasReplicatedShotgun);
        }

        private bool ShouldDrawSecondaryWeaponHudRow(GameplayItemDefinition item, bool hasReplicatedSecondaryAvailability)
        {
            if (_game._world.LocalPlayer.HasExperimentalOffhandWeapon || hasReplicatedSecondaryAvailability)
            {
                return true;
            }

            var hud = item.Presentation.Hud;
            return IsWeaponAmmoPanelHud(hud)
                && !string.IsNullOrWhiteSpace(item.Presentation.HudSpriteName);
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

        private void DrawAcquiredWeaponHudCore()
        {
            if (!ShouldDrawAcquiredWeaponHud())
            {
                return;
            }

            var weaponItemId = GetLocalAlternatePrimaryWeaponPresentationItemId();
            var presentation = CharacterClassCatalog.RuntimeRegistry.GetRequiredItem(weaponItemId).Presentation;
            var frameIndex = _game._world.LocalPlayer.Team == PlayerTeam.Blue ? presentation.BlueTeamHudFrameOffset : 0;
            var iconPosition = GetSourceHudPoint(614f, 507f);
            var iconTint = Color.White * 0.72f;
            var iconDrawn = presentation.HudSpriteName is not null && _game.TryDrawScreenSprite(presentation.HudSpriteName, frameIndex, iconPosition, iconTint, GetSourceHudSpriteScale(new Vector2(1.5f, 1.5f)));
            if (!iconDrawn)
            {
                _game.DrawBitmapFontText(weaponItemId.ToUpperInvariant(), GetSourceHudPoint(586f, 510f), iconTint, GetSourceHudTextScale(0.68f));
            }

            var currentShells = GetLocalAlternatePrimaryWeaponCurrentShells();
            var maxShells = Math.Max(1, GetLocalAlternatePrimaryWeaponMaxShells());
            var reloadProgress = GetLocalAlternatePrimaryWeaponReloadProgress();
            var ammoColor = currentShells <= Math.Max(1, maxShells / 4) ? LowAmmoHudColor : AmmoHudTextColor;
            var ammoCountScale = GetAmmoCountBuildScaleForValue(currentShells);
            _game.DrawBitmapFontText("Q", GetSourceHudPoint(610f, 500f), new Color(210, 210, 210), GetSourceHudTextScale(0.72f));
            var acquiredReloadBarBottomSourceY = 542f;
            var acquiredAmmoTextSourceY = acquiredReloadBarBottomSourceY - _game.MeasureMenuBitmapFontHeight(ammoCountScale);
            _game.DrawMenuBitmapFontText(currentShells.ToString(CultureInfo.InvariantCulture), GetSourceHudPoint(640f, acquiredAmmoTextSourceY), ammoColor, GetSourceHudTextScale(ammoCountScale));
            _game.DrawScreenHealthBar(GetSourceHudRectangle(610f, 531f, 55f, 5f), currentShells, maxShells, false, AmmoHudBarColor, Color.Black);
            _game.DrawScreenHealthBar(GetSourceHudRectangle(610f, 538f, 55f, 4f), reloadProgress, 1f, false, new Color(188, 188, 188), Color.Black);
        }

        private bool ShouldDrawAcquiredWeaponHud()
        {
            return _game._world.LocalPlayer.ClassId == PlayerClass.Soldier
                && _game._world.LocalPlayer.HasAcquiredWeapon
                && _game._world.LocalPlayer.AcquiredWeaponClassId.HasValue;
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
            return string.Equals(GetLocalDisplayedMainWeaponPresentationItemId(), "weapon.rocketlauncher", StringComparison.Ordinal)
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

            var displayedShells = _game.GetPlayerCurrentShells(player);
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
        private string GetLocalDisplayedMainWeaponPresentationItemIdCore() => _game._world.LocalPlayer.IsAcquiredWeaponPresented
            ? _game._world.LocalPlayer.GameplayLoadoutState.AcquiredItemId ?? _game._world.LocalPlayer.GameplayLoadoutState.PrimaryItemId
            : _game._world.LocalPlayer.GameplayLoadoutState.PrimaryItemId;
        private PrimaryWeaponDefinition GetLocalDisplayedMainWeaponStatsCore() => _game._world.LocalPlayer.IsAcquiredWeaponPresented ? _game._world.LocalPlayer.AcquiredWeapon ?? _game._world.LocalPlayer.PrimaryWeapon : _game._world.LocalPlayer.PrimaryWeapon;
        private int GetLocalDisplayedMainWeaponCurrentShellsCore() => _game._world.LocalPlayer.IsAcquiredWeaponPresented ? _game._world.LocalPlayer.AcquiredWeaponCurrentShells : _game.GetPlayerCurrentShells(_game._world.LocalPlayer);
        private int GetLocalDisplayedMainWeaponMaxShellsCore() => _game._world.LocalPlayer.IsAcquiredWeaponPresented ? _game._world.LocalPlayer.AcquiredWeaponMaxShells : _game._world.LocalPlayer.MaxShells;
        private int GetLocalDisplayedMainWeaponCooldownTicksCore() => _game._world.LocalPlayer.IsAcquiredWeaponPresented ? _game._world.LocalPlayer.AcquiredWeaponCooldownTicks : _game.GetPlayerPrimaryCooldownTicks(_game._world.LocalPlayer);
        private int GetLocalDisplayedMainWeaponReloadTicksCore() => _game._world.LocalPlayer.IsAcquiredWeaponPresented ? _game._world.LocalPlayer.AcquiredWeaponReloadTicksUntilNextShell : _game.GetPlayerReloadTicksUntilNextShell(_game._world.LocalPlayer);
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
