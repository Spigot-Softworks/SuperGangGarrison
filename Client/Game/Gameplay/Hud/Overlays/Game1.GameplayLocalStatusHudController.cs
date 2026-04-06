#nullable enable

using System.Globalization;
using Microsoft.Xna.Framework;
using OpenGarrison.Core;

namespace OpenGarrison.Client;

public partial class Game1
{
    private sealed class GameplayLocalStatusHudController
    {
        private static readonly Color AmmoHudBarColor = new(217, 217, 183);
        private static readonly Color AmmoHudTextColor = new(245, 235, 210);
        private static readonly Color LowAmmoHudColor = new(255, 0, 0);
        private static readonly Color DisabledAmmoHudColor = new(128, 128, 128);
        private static readonly Color HeavyCooldownHudColor = new(50, 50, 50);
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
            var frameIndex = GetCharacterHudFrameIndex(_game._world.LocalPlayer);
            _game.DrawScreenHealthBar(new Rectangle(45, viewportHeight - 53, 42, 38), _game._world.LocalPlayer.Health, _game._world.LocalPlayer.MaxHealth, false, fillDirection: HudFillDirection.VerticalBottomToTop);
            _game.TryDrawScreenSprite("CharacterHUD", frameIndex, new Vector2(5f, viewportHeight - 75f), Color.White, new Vector2(2f, 2f));
            var hpColor = _game._world.LocalPlayer.Health > (_game._world.LocalPlayer.MaxHealth / 3.5f) ? Color.White : Color.Red;
            _game.DrawHudTextCentered(Math.Max(_game._world.LocalPlayer.Health, 0).ToString(CultureInfo.InvariantCulture), new Vector2(69f, viewportHeight - 35f), hpColor, 1f);
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
                        if (GetLocalDisplayedMainWeaponPresentationClassId() != PlayerClass.Soldier)
                        {
                            _game.DrawHudTextLeftAligned(GetLocalDisplayedMainWeaponCurrentShells().ToString(CultureInfo.InvariantCulture), GetSourceHudPoint(765f, SourceAmmoHudBaseY + 95f), AmmoHudTextColor, 1f);
                        }

                        DrawAmmoReloadBar(GetReloadAmmoHudBarRectangle());
                    }
                    break;
            }

            DrawAcquiredWeaponHud();
            DrawExperimentalOffhandHud();
            DrawAcquiredMedigunPrompt();

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
        public PlayerClass GetLocalDisplayedMainWeaponPresentationClassId() => GetLocalDisplayedMainWeaponPresentationClassIdCore();
        public PrimaryWeaponDefinition GetLocalDisplayedMainWeaponStats() => GetLocalDisplayedMainWeaponStatsCore();
        public int GetLocalDisplayedMainWeaponCurrentShells() => GetLocalDisplayedMainWeaponCurrentShellsCore();
        public int GetLocalDisplayedMainWeaponMaxShells() => GetLocalDisplayedMainWeaponMaxShellsCore();
        public int GetLocalDisplayedMainWeaponCooldownTicks() => GetLocalDisplayedMainWeaponCooldownTicksCore();
        public int GetLocalDisplayedMainWeaponReloadTicks() => GetLocalDisplayedMainWeaponReloadTicksCore();
        public PlayerClass GetLocalAlternatePrimaryWeaponPresentationClassId() => GetLocalAlternatePrimaryWeaponPresentationClassIdCore();
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
            if (_game._world.LocalPlayer.ClassId != PlayerClass.Soldier || !_game._world.LocalPlayer.HasExperimentalOffhandWeapon)
            {
                return;
            }

            var presentation = StockGameplayModCatalog.GetPrimaryItem(PlayerClass.Engineer).Presentation;
            var frameIndex = _game._world.LocalPlayer.Team == PlayerTeam.Blue ? presentation.BlueTeamHudFrameOffset : 0;
            var iconPosition = GetSourceHudPoint(688f, 507f);
            var iconDrawn = presentation.HudSpriteName is not null && _game.TryDrawScreenSprite(presentation.HudSpriteName, frameIndex, iconPosition, Color.White, new Vector2(1.5f, 1.5f));
            if (!iconDrawn)
            {
                _game.DrawBitmapFontText("SHOTGUN", GetSourceHudPoint(664f, 510f), Color.White, 0.72f);
            }

            var currentShells = _game._world.LocalPlayer.ExperimentalOffhandCurrentShells;
            var maxShells = Math.Max(1, _game._world.LocalPlayer.ExperimentalOffhandMaxShells);
            var reloadProgress = currentShells >= maxShells
                ? 1f
                : _game._world.LocalPlayer.ExperimentalOffhandReloadTicksUntilNextShell <= 0
                    ? 1f
                    : Math.Clamp(1f - (_game._world.LocalPlayer.ExperimentalOffhandReloadTicksUntilNextShell / (float)Math.Max(1, _game._world.LocalPlayer.ExperimentalOffhandWeapon?.AmmoReloadTicks ?? 1)), 0f, 1f);
            var ammoColor = currentShells <= Math.Max(1, maxShells / 4) ? LowAmmoHudColor : AmmoHudTextColor;

            _game.DrawBitmapFontText("SPACE", GetSourceHudPoint(684f, 500f), new Color(210, 210, 210), 0.68f);
            _game.DrawHudTextLeftAligned(currentShells.ToString(CultureInfo.InvariantCulture), GetSourceHudPoint(719f, 515f), ammoColor, 0.9f);
            _game.DrawScreenHealthBar(GetSourceHudRectangle(684f, 531f, 55f, 5f), currentShells, maxShells, false, AmmoHudBarColor, Color.Black);
            _game.DrawScreenHealthBar(GetSourceHudRectangle(684f, 538f, 55f, 4f), reloadProgress, 1f, false, new Color(188, 188, 188), Color.Black);
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

            var weaponClassId = GetLocalAlternatePrimaryWeaponPresentationClassId();
            var presentation = StockGameplayModCatalog.GetPrimaryItem(weaponClassId).Presentation;
            var frameIndex = _game._world.LocalPlayer.Team == PlayerTeam.Blue ? presentation.BlueTeamHudFrameOffset : 0;
            var iconPosition = GetSourceHudPoint(614f, 507f);
            var iconTint = Color.White * 0.72f;
            var iconDrawn = presentation.HudSpriteName is not null && _game.TryDrawScreenSprite(presentation.HudSpriteName, frameIndex, iconPosition, iconTint, new Vector2(1.5f, 1.5f));
            if (!iconDrawn)
            {
                _game.DrawBitmapFontText(weaponClassId.ToString().ToUpperInvariant(), GetSourceHudPoint(586f, 510f), iconTint, 0.68f);
            }

            var currentShells = GetLocalAlternatePrimaryWeaponCurrentShells();
            var maxShells = Math.Max(1, GetLocalAlternatePrimaryWeaponMaxShells());
            var reloadProgress = GetLocalAlternatePrimaryWeaponReloadProgress();
            var ammoColor = currentShells <= Math.Max(1, maxShells / 4) ? LowAmmoHudColor : AmmoHudTextColor;
            _game.DrawBitmapFontText("Q", GetSourceHudPoint(610f, 500f), new Color(210, 210, 210), 0.72f);
            _game.DrawHudTextLeftAligned(currentShells.ToString(CultureInfo.InvariantCulture), GetSourceHudPoint(646f, 515f), ammoColor, 0.9f);
            _game.DrawScreenHealthBar(GetSourceHudRectangle(610f, 531f, 55f, 5f), currentShells, maxShells, false, AmmoHudBarColor, Color.Black);
            _game.DrawScreenHealthBar(GetSourceHudRectangle(610f, 538f, 55f, 4f), reloadProgress, 1f, false, new Color(188, 188, 188), Color.Black);
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
            return GetLocalDisplayedMainWeaponPresentationClassId() == PlayerClass.Soldier
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
            return StockGameplayModCatalog.GetPrimaryItem(GetLocalDisplayedMainWeaponPresentationClassId()).Presentation.HudSpriteName;
        }

        private int GetAmmoHudFrameIndexCore()
        {
            var presentation = StockGameplayModCatalog.GetPrimaryItem(GetLocalDisplayedMainWeaponPresentationClassId()).Presentation;
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
        private PlayerClass GetLocalDisplayedMainWeaponPresentationClassIdCore() => _game._world.LocalPlayer.IsAcquiredWeaponPresented ? _game._world.LocalPlayer.AcquiredWeaponClassId ?? _game._world.LocalPlayer.ClassId : _game._world.LocalPlayer.ClassId;
        private PrimaryWeaponDefinition GetLocalDisplayedMainWeaponStatsCore() => _game._world.LocalPlayer.IsAcquiredWeaponPresented ? _game._world.LocalPlayer.AcquiredWeapon ?? _game._world.LocalPlayer.PrimaryWeapon : _game._world.LocalPlayer.PrimaryWeapon;
        private int GetLocalDisplayedMainWeaponCurrentShellsCore() => _game._world.LocalPlayer.IsAcquiredWeaponPresented ? _game._world.LocalPlayer.AcquiredWeaponCurrentShells : _game.GetPlayerCurrentShells(_game._world.LocalPlayer);
        private int GetLocalDisplayedMainWeaponMaxShellsCore() => _game._world.LocalPlayer.IsAcquiredWeaponPresented ? _game._world.LocalPlayer.AcquiredWeaponMaxShells : _game._world.LocalPlayer.MaxShells;
        private int GetLocalDisplayedMainWeaponCooldownTicksCore() => _game._world.LocalPlayer.IsAcquiredWeaponPresented ? _game._world.LocalPlayer.AcquiredWeaponCooldownTicks : _game.GetPlayerPrimaryCooldownTicks(_game._world.LocalPlayer);
        private int GetLocalDisplayedMainWeaponReloadTicksCore() => _game._world.LocalPlayer.IsAcquiredWeaponPresented ? _game._world.LocalPlayer.AcquiredWeaponReloadTicksUntilNextShell : _game.GetPlayerReloadTicksUntilNextShell(_game._world.LocalPlayer);
        private PlayerClass GetLocalAlternatePrimaryWeaponPresentationClassIdCore() => _game._world.LocalPlayer.IsAcquiredWeaponPresented ? _game._world.LocalPlayer.ClassId : _game._world.LocalPlayer.AcquiredWeaponClassId ?? _game._world.LocalPlayer.ClassId;
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
