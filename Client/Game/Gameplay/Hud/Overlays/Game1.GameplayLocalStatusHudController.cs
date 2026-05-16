#nullable enable

using System.Globalization;
using Microsoft.Xna.Framework;
using OpenGarrison.Core;

namespace OpenGarrison.Client;

public partial class Game1
{
    private sealed class GameplayLocalStatusHudController
    {
        private const string CoreReplicatedOwnerId = "core.player";
        private const string SoldierShotgunAvailableKey = "soldier_shotgun_available";
        private const string SoldierShotgunAmmoKey = "soldier_shotgun_ammo";
        private const string SoldierShotgunMaxAmmoKey = "soldier_shotgun_max_ammo";
        private const string SoldierShotgunReloadTicksKey = "soldier_shotgun_reload_ticks";

        private static readonly Color AmmoHudBarColor = new(217, 217, 183);
        private static readonly Color AmmoHudTextColor = new(245, 235, 210);
        private static readonly Color LowAmmoHudColor = new(255, 0, 0);
        private static readonly Color DisabledAmmoHudColor = new(128, 128, 128);
        private static readonly Color HeavyCooldownHudColor = new(50, 50, 50);
        private const float AmmoCountBuildScale = 0.75f;
        private const float SourceHudWidth = 800f;
        private const float SourceHudHeight = 600f;
        private const float SourceAmmoHudBaseY = SourceHudHeight / 1.26f;

        private readonly Game1 _game;

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
            var scale = new Vector2(2f, 2f);
            
            // Align base position to sprite pixel grid (2-pixel boundaries)
            var basePosition = new Vector2(
                MathF.Round(5f / scale.X) * scale.X,
                MathF.Round((viewportHeight - 75f) / scale.Y) * scale.Y
            );
            
            // Draw team-colored base health sprite
            var healthSpriteName = _game._world.LocalPlayer.Team == PlayerTeam.Blue ? "PlayerHealthBlu" : "PlayerHealthRed";
            _game.TryDrawScreenSprite(healthSpriteName, 0, basePosition, Color.White, scale);
            
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
            var characterSpriteName = GameplayPlayerSpriteRenderController.GetTeamSpriteNameProxy(_game._world.LocalPlayer.ClassId, _game._world.LocalPlayer.Team, "StandS");
            if (characterSpriteName is not null && backgroundHealthSprite is not null && backgroundHealthSprite.Frames.Count > 0)
            {
                var characterSprite = _game.GetResolvedSprite(characterSpriteName);
                if (characterSprite is not null && characterSprite.Frames.Count > 0)
                {
                    var characterScale = new Vector2(2f, 2f);
                    var characterWidth = characterSprite.Frames[0].Width;
                    var characterHeight = characterSprite.Frames[0].Height;
                    var backgroundWidth = backgroundHealthSprite.Frames[0].Width * scale.X;
                    var backgroundHeight = backgroundHealthSprite.Frames[0].Height * scale.Y;
                    
                    // Calculate center of background sprite
                    var backgroundCenterX = basePosition.X + (backgroundWidth / 2f);
                    var backgroundCenterY = basePosition.Y + (backgroundHeight / 2f);
                    
                    // Account for sprite origin when centering
                    var spriteOrigin = characterSprite.Origin.ToVector2();
                    
                    // Calculate horizontal centering based on opaque bounds (trim transparent padding)
                    float characterCenterOffsetX;
                    if (characterSprite.Frames[0].OpaqueBounds.HasValue)
                    {
                        var opaqueBounds = characterSprite.Frames[0].OpaqueBounds.Value;
                        var opaqueWidth = opaqueBounds.Width;
                        var opaqueCenterX = opaqueBounds.X + opaqueWidth / 2f;
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
                    var maskLineY = basePosition.Y + MathF.Round((backgroundHealthSprite.Frames[0].Height - 2f) * scale.Y);
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
                            _game.TryDrawScreenSpritePart(characterSpriteName, 0, sourceRect, characterPosition, Color.White, characterScale);
                        }
                    }
                    else
                    {
                        // No masking needed
                        _game.TryDrawScreenSprite(characterSpriteName, 0, characterPosition, Color.White, characterScale);
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
                            
                            _game.TryDrawScreenSprite(weaponDefinition.NormalSpriteName, 0, weaponPosition, Color.White, weaponScale);
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
            var hpColor = _game._world.LocalPlayer.Health > (_game._world.LocalPlayer.MaxHealth / 3.5f) ? Color.White : Color.Red;
            var healthTextPosition = crossPosition + new Vector2((crossSprite?.Frames[0].Width ?? 0) * scale.X / 2f, (crossSprite?.Frames[0].Height ?? 0) * scale.Y / 2f);
            _game.DrawHudTextCentered(Math.Max(_game._world.LocalPlayer.Health, 0).ToString(CultureInfo.InvariantCulture), healthTextPosition, hpColor, 1f);
        }

        public void DrawAmmoHud()
        {
            if (!_game._world.LocalPlayer.IsAlive)
            {
                return;
            }

            if (_game._world.LocalPlayer.IsExperimentalDemoknightEnabled)
            {
                DrawDemoknightHud();
                return;
            }

            var displayedWeaponStats = GetLocalDisplayedMainWeaponStats();
            switch (displayedWeaponStats.Kind)
            {
                case PrimaryWeaponKind.FlameThrower:
                    DrawPyroAmmoHud();
                    break;
                case PrimaryWeaponKind.Minigun:
                    DrawHeavyAmmoHud();
                    if (!IsLocalDisplayedMainWeaponAcquired())
                    {
                        DrawHeavySandwichHud();
                    }
                    break;
                case PrimaryWeaponKind.Blade:
                    DrawQuoteAmmoHud();
                    break;
                case PrimaryWeaponKind.Rifle:
                    break;
                default:
                    var hudSpriteName = GetAmmoHudSpriteName();
                    if (hudSpriteName is not null && _game.TryDrawScreenSprite(hudSpriteName, GetAmmoHudFrameIndex(), GetSourceHudPoint(728f, SourceAmmoHudBaseY + 86f), Color.White, new Vector2(2.4f, 2.4f)))
                    {
                        if (!string.Equals(GetLocalDisplayedMainWeaponPresentationItemId(), "weapon.rocketlauncher", StringComparison.Ordinal))
                        {
                            var currentShells = GetLocalDisplayedMainWeaponCurrentShells();
                            var ammoCountScale = GetAmmoCountBuildScaleForValue(currentShells);
                            var reloadBarBottomSourceY = SourceAmmoHudBaseY + 98f;
                            var ammoTextSourceY = reloadBarBottomSourceY - _game.MeasureMenuBitmapFontHeight(ammoCountScale);
                            _game.DrawMenuBitmapFontText(currentShells.ToString(CultureInfo.InvariantCulture), GetSourceHudPoint(755f, ammoTextSourceY), AmmoHudTextColor, ammoCountScale);
                        }

                        DrawAmmoReloadBar(GetReloadAmmoHudBarRectangle());
                    }
                    break;
            }

            DrawAcquiredWeaponHud();
            DrawExperimentalOffhandHud();
            DrawAcquiredMedigunPrompt();

            // Show spy superjump cooldown HUD for Spy class
            if (_game._world.LocalPlayer.ClassId == PlayerClass.Spy)
            {
                DrawSpySuperjumpHud();
            }

            if (_game._world.LocalPlayer.ClassId == PlayerClass.Demoman)
            {
                DrawDemomanStickyHud();
            }
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
        public void DrawSpySuperjumpHud() => DrawSpySuperjumpHudCore();
        public void DrawQuoteAmmoHud() => DrawQuoteAmmoHudCore();
        public void DrawDemomanStickyHud() => DrawDemomanStickyHudCore();
        public void DrawExperimentalOffhandHud() => DrawExperimentalOffhandHudCore();
        public void DrawAcquiredMedigunPrompt() => DrawAcquiredMedigunPromptCore();
        public void DrawAcquiredWeaponHud() => DrawAcquiredWeaponHudCore();
        public void DrawPyroFlareHud(int frameIndex) => DrawPyroFlareHudCore(frameIndex);
        public bool TryDrawSourceAmmoHudSprite(string spriteName, int frameIndex) => TryDrawSourceAmmoHudSpriteCore(spriteName, frameIndex);
        public void DrawSourceAmmoHudBar(float left, float width, float value, float maxValue, Color fillColor) => DrawSourceAmmoHudBarCore(left, width, value, maxValue, fillColor);
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
                _game.TryDrawScreenSprite(presentation.HudSpriteName, frameIndex, GetSourceHudPoint(728f, SourceAmmoHudBaseY + 86f), Color.White, new Vector2(2.4f, 2.4f));
            }

            var meterColor = _game._world.LocalPlayer.IsExperimentalDemoknightCharging ? new Color(226, 188, 92) : AmmoHudBarColor;
            var meterFraction = _game._world.LocalPlayer.ExperimentalDemoknightChargeFraction;
            var isChargeReady = !_game._world.LocalPlayer.IsExperimentalDemoknightCharging
                && _game._world.LocalPlayer.ExperimentalDemoknightChargeTicksRemaining >= PlayerEntity.ExperimentalDemoknightChargeMaxTicks;
            var statusText = _game._world.LocalPlayer.IsExperimentalDemoknightCharging ? "CHARGING" : isChargeReady ? "READY" : "RECHARGING";
            _game.DrawBitmapFontText("SWING", GetSourceHudPoint(694f, 498f), new Color(210, 210, 210), 0.72f);
            _game.DrawBitmapFontText("CHARGE", GetSourceHudPoint(690f, 514f), new Color(210, 210, 210), 0.72f);
            if (!isChargeReady || !_game.TryDrawScreenSprite(ExperimentalDemoknightCatalog.FullChargeHudSpriteName, 0, GetSourceHudPoint(713f, 540f), Color.White, Vector2.One))
            {
                _game.DrawHudTextLeftAligned(statusText, GetSourceHudPoint(689f, 540f), meterColor, 0.9f);
            }

            _game.DrawScreenHealthBar(GetSourceHudRectangle(689f, SourceAmmoHudBaseY + 90f, 50f, 8f), meterFraction, 1f, false, meterColor, Color.Black);
            _game.DrawBitmapFontText("M1", GetSourceHudPoint(756f, 498f), new Color(240, 232, 208), 0.72f);
            _game.DrawBitmapFontText("M2", GetSourceHudPoint(756f, 514f), new Color(240, 232, 208), 0.72f);
        }

        private void DrawPyroAmmoHudCore()
        {
            var frameIndex = GetAmmoHudFrameIndex();
            var hudSpriteName = GetAmmoHudSpriteName();
            if (hudSpriteName is null || !TryDrawSourceAmmoHudSprite(hudSpriteName, frameIndex))
            {
                return;
            }

            var currentShells = GetLocalDisplayedMainWeaponCurrentShells();
            var maxShells = GetLocalDisplayedMainWeaponMaxShells();
            var barColor = currentShells <= (maxShells * 0.25f) ? LowAmmoHudColor : AmmoHudBarColor;
            DrawSourceAmmoHudBar(689f, 34f, currentShells, maxShells, barColor);
            DrawPyroFlareHud(frameIndex);
        }

        private void DrawHeavyAmmoHudCore()
        {
            var hudSpriteName = GetAmmoHudSpriteName();
            if (hudSpriteName is null || !TryDrawSourceAmmoHudSprite(hudSpriteName, GetAmmoHudFrameIndex()))
            {
                return;
            }

            var currentShells = GetLocalDisplayedMainWeaponCurrentShells();
            var maxShells = GetLocalDisplayedMainWeaponMaxShells();
            var ammoFraction = maxShells <= 0 ? 0f : float.Clamp(currentShells / (float)maxShells, 0f, 1f);
            var barColor = Color.Lerp(AmmoHudBarColor, LowAmmoHudColor, 1f - ammoFraction);
            var cooldownFraction = float.Clamp(Math.Max(GetLocalDisplayedMainWeaponCooldownTicks(), GetLocalDisplayedMainWeaponReloadTicks()) / 25f, 0f, 1f);
            barColor = Color.Lerp(barColor, HeavyCooldownHudColor, cooldownFraction);
            DrawSourceAmmoHudBar(689f, 34f, currentShells, maxShells, barColor);
        }

        private void DrawHeavySandwichHudCore()
        {
            if (_game._world.LocalPlayer.ClassId != PlayerClass.Heavy)
            {
                return;
            }

            var sandwichHudSpriteName = StockGameplayModCatalog.GetSecondaryItem(PlayerClass.Heavy)?.Presentation.HudSpriteName ?? "SandwichHudS";
            if (!_game.TryDrawScreenSprite(sandwichHudSpriteName, _game._world.LocalPlayer.Team == PlayerTeam.Blue ? 1 : 0, GetSourceHudPoint(730f, 515f), Color.White, new Vector2(2f, 2f)))
            {
                return;
            }

            var cooldownRemaining = Math.Clamp(_game.GetPlayerHeavyEatCooldownTicksRemaining(_game._world.LocalPlayer), 0, PlayerEntity.HeavySandvichCooldownTicks);
            _game.DrawScreenHealthBar(GetSourceHudRectangle(715f, 528f, 35f, 5f), PlayerEntity.HeavySandvichCooldownTicks - cooldownRemaining, PlayerEntity.HeavySandvichCooldownTicks, false, AmmoHudBarColor, Color.Black);
        }

        private void DrawSpySuperjumpHudCore()
        {
            if (_game._world.LocalPlayer.ClassId != PlayerClass.Spy)
            {
                return;
            }

            // Gray out the icon when carrying intel
            var isDisabled = _game._world.LocalPlayer.IsCarryingIntel;
            var iconColor = isDisabled ? DisabledAmmoHudColor : Color.White;

            // Draw the charge jump sprite from the Spy HUD folder
            if (!_game.TryDrawScreenSprite("ChargeJumpS", _game._world.LocalPlayer.Team == PlayerTeam.Blue ? 1 : 0, GetSourceHudPoint(730f, 515f), iconColor, new Vector2(2f, 2f)))
            {
                return;
            }

            // Draw cooldown bar (always visible to show when ability will be ready)
            // Use darker colors when disabled to match the darkened icon
            var barColor = isDisabled ? DisabledAmmoHudColor : AmmoHudBarColor;
            var cooldownRemaining = Math.Clamp(_game._world.LocalPlayer.SpySuperjumpCooldownTicksRemaining, 0, PlayerEntity.SpySuperjumpCooldownTicks);
            _game.DrawScreenHealthBar(GetSourceHudRectangle(715f, 528f, 35f, 5f), PlayerEntity.SpySuperjumpCooldownTicks - cooldownRemaining, PlayerEntity.SpySuperjumpCooldownTicks, false, barColor, Color.Black);
        }

        private void DrawQuoteAmmoHudCore()
        {
            var hudSpriteName = GetAmmoHudSpriteName();
            if (hudSpriteName is null || !TryDrawSourceAmmoHudSprite(hudSpriteName, GetAmmoHudFrameIndex()))
            {
                return;
            }

            DrawSourceAmmoHudBar(689f, 34f, _game.GetPlayerCurrentShells(_game._world.LocalPlayer), _game._world.LocalPlayer.MaxShells, AmmoHudBarColor);
        }

        private void DrawDemomanStickyHudCore()
        {
            var stickyCount = CountLocalOwnedStickyMines();
            var maxStickies = Math.Max(1, _game._world.LocalPlayer.PrimaryWeapon.MaxAmmo);
            var frameIndex = _game._world.LocalPlayer.Team == PlayerTeam.Blue ? 1 : 0;
            if (!_game.TryDrawScreenSprite("StickyCounterS", frameIndex, GetSourceHudPoint(735f, 522f), Color.White, new Vector2(3f, 3f)))
            {
                return;
            }

            _game.DrawHudTextLeftAligned(stickyCount.ToString(CultureInfo.InvariantCulture), GetSourceHudPoint(717f, 524f), AmmoHudBarColor, 1.5f);
            _game.DrawHudTextLeftAligned($"/{maxStickies.ToString(CultureInfo.InvariantCulture)}", GetSourceHudPoint(730f, 524f), AmmoHudBarColor, 1.5f);
        }

        private void DrawExperimentalOffhandHudCore()
        {
            if (_game._world.LocalPlayer.ClassId != PlayerClass.Soldier)
            {
                return;
            }

            var hasReplicatedShotgun = _game._world.LocalPlayer.TryGetReplicatedStateBool(CoreReplicatedOwnerId, SoldierShotgunAvailableKey, out var replicatedShotgunAvailable)
                && replicatedShotgunAvailable;
            if (!_game._world.LocalPlayer.HasExperimentalOffhandWeapon && !hasReplicatedShotgun)
            {
                return;
            }

            var secondaryItemId = _game._world.LocalPlayer.GameplayLoadoutState.SecondaryItemId;
            if (string.IsNullOrWhiteSpace(secondaryItemId))
            {
                return;
            }

            var presentation = CharacterClassCatalog.RuntimeRegistry.GetRequiredItem(secondaryItemId).Presentation;
            var frameIndex = _game._world.LocalPlayer.Team == PlayerTeam.Blue ? presentation.BlueTeamHudFrameOffset : 0;
            const float panelScale = 2.4f;
            const float panelGapPixels = 4f;
            const float fallbackPanelHeight = 38f;
            var mainPanelSourceX = 728f;
            var mainPanelSourceY = SourceAmmoHudBaseY + 86f;
            var panelHeightPixels = fallbackPanelHeight;
            if (presentation.HudSpriteName is not null)
            {
                var panelSprite = _game._runtimeAssets.GetSprite(presentation.HudSpriteName);
                if (panelSprite is not null && panelSprite.Frames.Count > 0)
                {
                    panelHeightPixels = panelSprite.Frames[0].Height * panelScale;
                }
            }

            var shotgunPanelSourceX = mainPanelSourceX;
            var shotgunPanelSourceY = mainPanelSourceY - panelHeightPixels - panelGapPixels;
            var iconPosition = GetSourceHudPoint(shotgunPanelSourceX, shotgunPanelSourceY);
            var iconDrawn = presentation.HudSpriteName is not null && _game.TryDrawScreenSprite(presentation.HudSpriteName, frameIndex, iconPosition, Color.White, new Vector2(panelScale, panelScale));
            if (!iconDrawn)
            {
                _game.DrawBitmapFontText("SHOTGUN", GetSourceHudPoint(shotgunPanelSourceX - 24f, shotgunPanelSourceY + 3f), Color.White, 0.72f);
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
            _game.DrawMenuBitmapFontText(currentShells.ToString(CultureInfo.InvariantCulture), GetSourceHudPoint(shotgunPanelSourceX + 27f, shotgunAmmoTextSourceY), ammoColor, ammoCountScale);
            _game.DrawScreenHealthBar(GetSourceHudRectangle(shotgunPanelSourceX - 28f, shotgunPanelSourceY + 4f, 50f, 8f), reloadProgress, 1f, false, AmmoHudBarColor, Color.Black);
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
            if (_game._world.LocalPlayer.ClassId != PlayerClass.Soldier || !_game._world.LocalPlayer.HasAcquiredWeapon || !_game._world.LocalPlayer.AcquiredWeaponClassId.HasValue)
            {
                return;
            }

            var weaponItemId = GetLocalAlternatePrimaryWeaponPresentationItemId();
            var presentation = CharacterClassCatalog.RuntimeRegistry.GetRequiredItem(weaponItemId).Presentation;
            var frameIndex = _game._world.LocalPlayer.Team == PlayerTeam.Blue ? presentation.BlueTeamHudFrameOffset : 0;
            var iconPosition = GetSourceHudPoint(614f, 507f);
            var iconTint = Color.White * 0.72f;
            var iconDrawn = presentation.HudSpriteName is not null && _game.TryDrawScreenSprite(presentation.HudSpriteName, frameIndex, iconPosition, iconTint, new Vector2(1.5f, 1.5f));
            if (!iconDrawn)
            {
                _game.DrawBitmapFontText(weaponItemId.ToUpperInvariant(), GetSourceHudPoint(586f, 510f), iconTint, 0.68f);
            }

            var currentShells = GetLocalAlternatePrimaryWeaponCurrentShells();
            var maxShells = Math.Max(1, GetLocalAlternatePrimaryWeaponMaxShells());
            var reloadProgress = GetLocalAlternatePrimaryWeaponReloadProgress();
            var ammoColor = currentShells <= Math.Max(1, maxShells / 4) ? LowAmmoHudColor : AmmoHudTextColor;
            var ammoCountScale = GetAmmoCountBuildScaleForValue(currentShells);
            _game.DrawBitmapFontText("Q", GetSourceHudPoint(610f, 500f), new Color(210, 210, 210), 0.72f);
            var acquiredReloadBarBottomSourceY = 542f;
            var acquiredAmmoTextSourceY = acquiredReloadBarBottomSourceY - _game.MeasureMenuBitmapFontHeight(ammoCountScale);
            _game.DrawMenuBitmapFontText(currentShells.ToString(CultureInfo.InvariantCulture), GetSourceHudPoint(640f, acquiredAmmoTextSourceY), ammoColor, ammoCountScale);
            _game.DrawScreenHealthBar(GetSourceHudRectangle(610f, 531f, 55f, 5f), currentShells, maxShells, false, AmmoHudBarColor, Color.Black);
            _game.DrawScreenHealthBar(GetSourceHudRectangle(610f, 538f, 55f, 4f), reloadProgress, 1f, false, new Color(188, 188, 188), Color.Black);
        }

        private static float GetAmmoCountBuildScaleForValue(int ammoCount)
        {
            return Math.Abs(ammoCount) < 10 ? 1f : AmmoCountBuildScale;
        }

        private void DrawPyroFlareHudCore(int frameIndex)
        {
            var flareCount = GetLocalDisplayedMainWeaponCurrentShells() / PlayerEntity.PyroFlareAmmoRequirement;
            if (flareCount <= 0)
            {
                return;
            }

            var flareTint = _game.GetPlayerPyroFlareCooldownTicks(_game._world.LocalPlayer) <= 0 ? Color.White : DisabledAmmoHudColor;
            for (var flareIndex = 0; flareIndex < flareCount; flareIndex += 1)
            {
                _game.TryDrawScreenSprite("FlareS", frameIndex, GetSourceHudPoint(760f - (flareIndex * 20f), SourceAmmoHudBaseY + 93f), flareTint, Vector2.One);
            }
        }

        private bool TryDrawSourceAmmoHudSpriteCore(string spriteName, int frameIndex)
        {
            return _game.TryDrawScreenSprite(spriteName, frameIndex, GetSourceHudPoint(728f, SourceAmmoHudBaseY + 86f), Color.White, new Vector2(2.4f, 2.4f));
        }

        private void DrawSourceAmmoHudBarCore(float left, float width, float value, float maxValue, Color fillColor)
        {
            _game.DrawScreenHealthBar(GetSourceHudRectangle(left, SourceAmmoHudBaseY + 90f, width, 8f), value, maxValue, false, fillColor, Color.Black);
        }

        private Rectangle GetReloadAmmoHudBarRectangleCore()
        {
            return string.Equals(GetLocalDisplayedMainWeaponPresentationItemId(), "weapon.rocketlauncher", StringComparison.Ordinal)
                ? GetSourceHudRectangle(689f, SourceAmmoHudBaseY + 90f, 34f, 8f)
                : GetSourceHudRectangle(700f, SourceAmmoHudBaseY + 90f, 50f, 8f);
        }

        private Vector2 GetSourceHudPointCore(float sourceX, float sourceY)
        {
            return new Vector2(_game.ViewportWidth - SourceHudWidth + sourceX, _game.ViewportHeight - SourceHudHeight + sourceY);
        }

        private Rectangle GetSourceHudRectangleCore(float sourceX, float sourceY, float width, float height)
        {
            var position = GetSourceHudPoint(sourceX, sourceY);
            return new Rectangle((int)MathF.Round(position.X), (int)MathF.Round(position.Y), (int)MathF.Round(width), (int)MathF.Round(height));
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
