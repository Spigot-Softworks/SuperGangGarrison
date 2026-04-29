#nullable enable

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using OpenGarrison.BotAI;
using OpenGarrison.Core;
using OpenGarrison.GameplayModding;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;

namespace OpenGarrison.Client;

public partial class Game1
{
    private const int LastToDieStartingEnemyBotCount = 2;
    private const int LastToDieFinalEnemyBotCount = 10;
    private const int LastToDieStartingStageMinutes = 3;
    private const int LastToDieStageMinuteIncrement = 1;
    private const int LastToDieFinalStageMinutes =
        LastToDieStartingStageMinutes + ((LastToDieFinalEnemyBotCount - LastToDieStartingEnemyBotCount) * LastToDieStageMinuteIncrement);
    private const int LastToDieStageCount = (LastToDieFinalEnemyBotCount - LastToDieStartingEnemyBotCount) + 1;
    private const int LastToDieMatchTimeLimitMinutes = 30;
    private const int LastToDieCapLimit = 5;
    private const int LastToDieRespawnSeconds = 5;
    private const int LastToDiePerkChoiceCount = 3;
    private const int LastToDieStageClearFadeTicks = 24;
    private const int LastToDieStageClearContinueDelayTicks = 18;
    private const int LastToDieFailureFadeTicks = 45;
    private const int LastToDieFailureContinueDelayTicks = 18;
    private const float LastToDieStageIntroDurationSeconds = 2f;
    private const int LastToDieKillTimerReductionSeconds = 3;
    private const float LastToDieAccessoryChoiceChance = 0.2f;

    private enum LastToDieSurvivorKind
    {
        Soldier,
        Demoknight,
    }

    private enum LastToDiePerkKind
    {
        SoldierShotgun,
        HealOnDamage,
        HealOnKill,
        RateOfFireOnDamage,
        SoldierInstantReload,
        SpeedOnDamage,
        SpeedOnKill,
        PassiveHealthRegeneration,
        InvincibilityOnKill,
        ProjectileSpeedMultiplier,
        AirshotDamageMultiplier,
        SoldierStingerRockets,
        SoldierRageExtensionOnKill,
        SoldierDangerClose,
        SoldierSelfDamageHealing,
        SoldierReloadSpeedMultiplier,
        SoldierAmmoRegeneratesWhileSwappedOut,
        SoldierInfiniteAmmoDuringRage,
        SoldierRageCaptureLockout,
        SoldierRageCaptureDuringRage,
        SoldierNapalmRockets,
        SoldierFinalClipRocketBurst,
        SoldierCivilDefenseTurret,
        SoldierLuckyBastard,
        SoldierThundergunner,
        SoldierBattleborn,
        SoldierFogOfWar,
        DemoknightMeleeRange,
        DemoknightLifesteal,
        DemoknightMoveSpeed,
        DemoknightKillHeal,
        DemoknightKillInvincibility,
        DemoknightChargeRate,
        DemoknightChargeResistance,
        DemoknightDamageMultiplier,
        DemoknightFullHealOnKill,
        DemoknightAttackSpeed,
        DemoknightPostRageRegeneration,
        DemoknightFullControlDuringCharge,
        DemoknightGhostDash,
    }

    private readonly record struct LastToDiePerkDefinition(
        LastToDiePerkKind Kind,
        string Label,
        string Description);

    private enum LastToDieAccessorySlot
    {
        Helmet,
        Dogtags,
    }

    private enum LastToDieAccessoryStatKind
    {
        ExplosiveDamage,
        BulletDamage,
        MovementSpeed,
        JumpHeight,
        BonusJumps,
        Evasion,
        Thorns,
        BulletResist,
        ExplosiveResist,
        FireResist,
        HealthRegeneration,
        DamageResist,
        AcquiredWeaponDamage,
        AcquiredWeaponHealing,
        DamageToClass,
        ProjectilesPerShot,
    }

    private readonly record struct LastToDieAccessoryDefinition(
        LastToDieAccessorySlot Slot,
        LastToDieAccessoryStatKind StatKind,
        int Value,
        PlayerClass? TargetClass = null)
    {
        public string SlotLabel => Slot == LastToDieAccessorySlot.Helmet ? "Helmet" : "Dogtags";
    }

    private readonly record struct LastToDieRewardChoice(
        LastToDiePerkDefinition? Perk,
        LastToDieAccessoryDefinition? Accessory)
    {
        public bool IsAccessory => Accessory.HasValue;
    }

    private readonly record struct LastToDieSurvivorDefinition(
        LastToDieSurvivorKind Kind,
        string Label,
        string Description);

    private readonly record struct LastToDieChoiceMenuLayout(
        Rectangle Panel,
        Rectangle[] CardBounds);

    private sealed class LastToDieRunState
    {
        public LastToDieRunState(string levelName)
        {
            CurrentLevelName = levelName;
        }

        public int StageNumber { get; set; } = 1;

        public int EnemyBotCount { get; set; } = LastToDieStartingEnemyBotCount;

        public int StageDurationMinutes { get; set; } = LastToDieStartingStageMinutes;

        public int StageRemainingTicks { get; set; }

        public int StageIntroTicksRemaining { get; set; }

        public string CurrentLevelName { get; set; }

        public bool AwaitingOpeningPerkSelection { get; set; }

        public bool AwaitingOpeningSurvivorSelection { get; set; }

        public LastToDieSurvivorKind SurvivorKind { get; set; } = LastToDieSurvivorKind.Soldier;

        public HashSet<LastToDiePerkKind> ChosenPerks { get; } = [];

        public LastToDieRewardChoice[] PendingRewardChoices { get; set; } = [];

        public LastToDieAccessoryDefinition? EquippedHelmet { get; set; }

        public LastToDieAccessoryDefinition? EquippedDogtags { get; set; }

        public int LevelsCompleted { get; set; }

        public int TotalKills { get; set; }

        public int TotalDamageDealt { get; set; }

        public int ObservedStageKills { get; set; }
    }

    private static readonly LastToDieSurvivorDefinition[] LastToDieSurvivorCatalog =
    [
        new(
            LastToDieSurvivorKind.Soldier,
            "Soldier",
            string.Empty),
        new(
            LastToDieSurvivorKind.Demoknight,
            "Demoknight",
            string.Empty),
    ];

    private static readonly LastToDiePerkDefinition[] LastToDieSoldierPerkCatalog =
    [
        new(LastToDiePerkKind.SoldierShotgun, "12 Gauge", "Doubles shotgun pellet count."),
        new(LastToDiePerkKind.HealOnDamage, "Sadist", "Restore 35% of dealt damage."),
        new(LastToDiePerkKind.HealOnKill, "Natural Born Killer", "Restore 75 health on kill."),
        new(LastToDiePerkKind.RateOfFireOnDamage, "Rocket Frenzy", "Landing a hit instantly requeues primary fire."),
        new(LastToDiePerkKind.SoldierInstantReload, "Re-Armament", "Landing a rocket hit refills 1 rocket."),
        new(LastToDiePerkKind.PassiveHealthRegeneration, "Tough as Nails", "Restore 8 health per second while alive."),
        new(LastToDiePerkKind.InvincibilityOnKill, "Untouchable", "Gain 1s ghost phase on kill: partial transparency and projectile passthrough."),
        new(LastToDiePerkKind.ProjectileSpeedMultiplier, "Hypersonic", "All spawned projectile weapons fly 60% faster."),
        new(LastToDiePerkKind.AirshotDamageMultiplier, "AA Gun", "Direct projectile airshots deal bonus damage."),
        new(LastToDiePerkKind.SoldierStingerRockets, "STINGER Rockets", "Phase 1 rockets deal 100% more damage, have 60% larger blast radius, fly 70% slower, steer harder, burst to double speed on primary fire, and detonate on right click."),
        new(LastToDiePerkKind.SoldierRageExtensionOnKill, "Fury Drive", "Kills scored during rage add 2 seconds to rage duration."),
        new(LastToDiePerkKind.SoldierDangerClose, "Danger Close", "Gibbed enemies explode where they die and can chain nearby explosives."),
        new(LastToDiePerkKind.SoldierSelfDamageHealing, "Shrapnel Junkie", "Self damage heals instead of damaging."),
        new(LastToDiePerkKind.SoldierReloadSpeedMultiplier, "Speed Loader", "All weapons recharge and reload 40% faster."),
        new(LastToDiePerkKind.SoldierAmmoRegeneratesWhileSwappedOut, "Bandolier", "Reload stowed weapons alongside the active weapon."),
        new(LastToDiePerkKind.SoldierInfiniteAmmoDuringRage, "Locked N' Loaded", "Fire rockets without spending ammo while raging."),
        new(LastToDiePerkKind.SoldierRageCaptureLockout, "Area Denial", "Enemies are locked out of captures during rage."),
        new(LastToDiePerkKind.SoldierNapalmRockets, "Napalm Rockets", "Napalm fucking rockets."),
        new(LastToDiePerkKind.SoldierFinalClipRocketBurst, "Last Kiss", "Emptying your clip fires a delayed bonus rocket."),
        new(LastToDiePerkKind.SoldierRageCaptureDuringRage, "Manifest Destiny", "Keep capturing objectives while raging."),
        new(LastToDiePerkKind.SoldierCivilDefenseTurret, "Civil Defense Turret", "Right-click deploys a sentry that destroys bullets, rockets, and mines."),
        new(LastToDiePerkKind.SoldierLuckyBastard, "Lucky Bastard", "Critical damage triggers 5s invulnerability, then revives based on your kill threshold."),
        new(LastToDiePerkKind.SoldierThundergunner, "Thundergunner", "Right-click unleashes a stronger airblast that reflects projectiles and bullets and knocks enemies away."),
        new(LastToDiePerkKind.SoldierBattleborn, "Battleborn", "Gain a damage bonus equal to your current combo meter count."),
        new(LastToDiePerkKind.SoldierFogOfWar, "Fog of War", "Taking damage grants stacking evasion for 3s. Evaded attacks flash Miss! above you."),
    ];

    private static readonly LastToDiePerkDefinition[] LastToDieDemoknightPerkCatalog =
    [
        new(LastToDiePerkKind.DemoknightMeleeRange, "Longinus", "Increase Eyelander range by 50%."),
        new(LastToDiePerkKind.DemoknightLifesteal, "Vampiric", "Restore 60% of dealt sword damage."),
        new(LastToDiePerkKind.DemoknightMoveSpeed, "Zealous", "Gain 30% passive movement speed."),
        new(LastToDiePerkKind.DemoknightKillHeal, "Bloodthirsty", "Restore 75 health on kill."),
        new(LastToDiePerkKind.DemoknightKillInvincibility, "Deity", "Gain 2 seconds of invulnerability after kills."),
        new(LastToDiePerkKind.DemoknightChargeRate, "Vigorous", "Recharge the Demoknight charge meter 80% faster."),
        new(LastToDiePerkKind.DemoknightChargeResistance, "Relentless", "Take 80% less damage while charging."),
        new(LastToDiePerkKind.DemoknightDamageMultiplier, "Butchery", "Increase Eyelander damage by 40%."),
        new(LastToDiePerkKind.DemoknightFullHealOnKill, "Gore Obsessed", "Restore to full health on kill."),
        new(LastToDiePerkKind.DemoknightAttackSpeed, "Typhoon", "Swing the Eyelander 50% faster."),
        new(LastToDiePerkKind.DemoknightPostRageRegeneration, "Meditation", "Regenerate health for 10 seconds after rage."),
        new(LastToDiePerkKind.DemoknightFullControlDuringCharge, "Full Control", "Keep full turn and jump control while charging."),
        new(LastToDiePerkKind.DemoknightGhostDash, "Naught Spectre", "Right-click dashes through danger with a boosted next hit."),
    ];

    private LastToDieRunState? _lastToDieRun;
    private bool _lastToDieSurvivorMenuOpen;
    private int _lastToDieSurvivorHoverIndex = -1;
    private bool _lastToDiePerkMenuOpen;
    private int _lastToDiePerkHoverIndex = -1;
    private bool _lastToDieStageClearOverlayOpen;
    private int _lastToDieStageClearOverlayTicks;
    private bool _lastToDieFailureOverlayOpen;
    private int _lastToDieFailureOverlayTicks;
    private int _lastToDieTimerReductionPopupTicksRemaining;
    private float _lastToDieTimerReductionPopupRise;
    private int _lastToDieTimerReductionPopupSeconds;
    private Texture2D? _lastToDieBuffIconTexture;
    private bool _lastToDieBuffIconLoadAttempted;

    private const string LastToDieHelmetLoadoutItemId = "ltd.accessory.helmet";
    private const string LastToDieDogtagsLoadoutItemId = "ltd.accessory.dogtags";

    private bool IsLastToDieSessionActive => _gameplaySessionKind == GameplaySessionKind.LastToDie;

    private bool IsOfflineBotSessionActive => _gameplaySessionKind is GameplaySessionKind.Practice or GameplaySessionKind.LastToDie;

    private bool ShouldUseLastToDieAccessoryLoadoutColumn(PlayerClass viewedClass)
    {
        return IsLastToDieSessionActive
            && _lastToDieRun is not null
            && _lastToDieRun.SurvivorKind == LastToDieSurvivorKind.Soldier
            && viewedClass == PlayerClass.Soldier;
    }

    private void AddLastToDieAccessoryLoadoutButtons(GameplayLoadoutMenuLayout layout, List<GameplayLoadoutMenuButton> buttons)
    {
        AddButton(LastToDieHelmetLoadoutItemId, 0);
        AddButton(LastToDieDogtagsLoadoutItemId, 1);

        void AddButton(string itemId, int optionIndex)
        {
            buttons.Add(new GameplayLoadoutMenuButton(
                GameplayLoadoutMenuPresentation.GetColumnOptionBounds(layout.LeftColumnBounds, optionIndex),
                () => SetNetworkStatus("Last To Die accessories auto-equip from reward drops."),
                GameplayLoadoutMenuButtonKind.AccessoryOption,
                PlayerClass.Soldier,
                GameplayEquipmentSlot.Secondary,
                itemId));
        }
    }

    private void DrawLastToDieAccessoryLoadoutColumn(
        GameplayLoadoutMenuLayout layout,
        Rectangle columnBounds,
        List<GameplayLoadoutMenuButton> buttons)
    {
        DrawGameplayLoadoutAccessoryColumnBackground(columnBounds);
        DrawLastToDieAccessoryLoadoutOption(
            GameplayLoadoutMenuPresentation.GetColumnOptionBounds(columnBounds, 0),
            _lastToDieRun?.EquippedHelmet,
            _gameplayLoadoutHelmetTexture,
            LastToDieHelmetLoadoutItemId,
            buttons);
        DrawLastToDieAccessoryLoadoutOption(
            GameplayLoadoutMenuPresentation.GetColumnOptionBounds(columnBounds, 1),
            _lastToDieRun?.EquippedDogtags,
            _gameplayLoadoutDogTagsTexture,
            LastToDieDogtagsLoadoutItemId,
            buttons);
    }

    private void DrawGameplayLoadoutAccessoryColumnBackground(Rectangle columnBounds)
    {
        if (_gameplayLoadoutScrollerTexture is not null)
        {
            var frameWidth = _gameplayLoadoutScrollerTexture.Width / 5;
            var source = new Rectangle(0, 0, frameWidth, _gameplayLoadoutScrollerTexture.Height);
            DrawLoadedSpriteFrame(_gameplayLoadoutScrollerTexture, columnBounds.Location.ToVector2(), source, Color.White, 0f, Vector2.Zero, new Vector2(columnBounds.Width / (float)source.Width, columnBounds.Height / (float)source.Height), SpriteEffects.None, 0f);
            return;
        }

        _spriteBatch.Draw(_pixel, columnBounds, new Color(90, 82, 63));
    }

    private void DrawLastToDieAccessoryLoadoutOption(
        Rectangle bounds,
        LastToDieAccessoryDefinition? accessory,
        LoadedSpriteFrame? texture,
        string itemId,
        List<GameplayLoadoutMenuButton> buttons)
    {
        var hovered = _gameplayLoadoutMenuHoverIndex >= 0
            && _gameplayLoadoutMenuHoverIndex < buttons.Count
            && buttons[_gameplayLoadoutMenuHoverIndex].Kind == GameplayLoadoutMenuButtonKind.AccessoryOption
            && string.Equals(buttons[_gameplayLoadoutMenuHoverIndex].ItemId, itemId, StringComparison.Ordinal);

        if (texture is not null)
        {
            var sourceX = accessory.HasValue ? texture.Width / 2 : 0;
            var source = new Rectangle(sourceX, 0, texture.Width / 2, texture.Height);
            DrawLoadedSpriteFrame(texture, bounds.Location.ToVector2(), source, Color.White, 0f, Vector2.Zero, new Vector2(bounds.Width / (float)source.Width, bounds.Height / (float)source.Height), SpriteEffects.None, 0f);
        }
        else
        {
            _spriteBatch.Draw(_pixel, bounds, accessory.HasValue ? new Color(232, 221, 177) : new Color(52, 47, 35));
            DrawCenteredMenuFontText(accessory?.SlotLabel ?? "Empty", bounds, accessory.HasValue ? new Color(51, 40, 31) : new Color(240, 233, 221), 1f, 0.42f);
        }

        if (hovered)
        {
            DrawGameplayLoadoutMenuOutline(bounds, new Color(255, 239, 198), 2);
        }
    }

    private GameplayLoadoutMenuBoardLine[] BuildLastToDieAccessoryLoadoutBoardLines(string? itemId)
    {
        var showHelmet = string.IsNullOrWhiteSpace(itemId)
            || string.Equals(itemId, LastToDieHelmetLoadoutItemId, StringComparison.Ordinal);
        var accessory = showHelmet
            ? _lastToDieRun?.EquippedHelmet
            : _lastToDieRun?.EquippedDogtags;
        var slotLabel = showHelmet ? "Helmet" : "Dogtags";
        if (!accessory.HasValue)
        {
            return
            [
                new GameplayLoadoutMenuBoardLine(slotLabel, new Color(245, 240, 232)),
                new GameplayLoadoutMenuBoardLine("No item equipped.", new Color(186, 186, 186)),
                new GameplayLoadoutMenuBoardLine("Find one from reward drops.", new Color(186, 186, 186)),
            ];
        }

        return
        [
            new GameplayLoadoutMenuBoardLine($"{GetLastToDieAccessoryPrefix(accessory.Value)} {slotLabel}", new Color(255, 214, 82)),
            new GameplayLoadoutMenuBoardLine(GetLastToDieAccessoryDescription(accessory.Value), new Color(186, 186, 186)),
        ];
    }

    private void DrawLastToDieBuffIcon(MouseState mouse)
    {
        if (!IsLastToDieSessionActive || _lastToDieRun is null || _world.LocalPlayerAwaitingJoin)
        {
            return;
        }

        var iconBounds = new Rectangle(96, ViewportHeight - 83, 35, 35);
        var texture = GetLastToDieBuffIconTexture();
        if (texture is not null)
        {
            _spriteBatch.Draw(texture, iconBounds, Color.White);
        }
        else
        {
            _spriteBatch.Draw(_pixel, iconBounds, new Color(76, 26, 26));
            _spriteBatch.Draw(_pixel, new Rectangle(iconBounds.X + 6, iconBounds.Y + 6, iconBounds.Width - 12, iconBounds.Height - 12), new Color(224, 66, 66));
        }

        if (!iconBounds.Contains(mouse.Position))
        {
            return;
        }

        var lines = BuildLastToDieBuffTooltipLines(_lastToDieRun);
        if (lines.Count == 0)
        {
            lines.Add("No stat bonuses");
        }

        const float scale = 0.9f;
        var width = 0f;
        foreach (var line in lines)
        {
            width = MathF.Max(width, MeasureBitmapFontWidth(line, scale));
        }

        var panel = new Rectangle(
            iconBounds.Right + 10,
            Math.Max(12, iconBounds.Y - 13),
            (int)MathF.Ceiling(width + 30),
            Math.Max(43, 23 + (lines.Count * 20)));
        _spriteBatch.Draw(_pixel, panel, new Color(20, 22, 26, 238));
        _spriteBatch.Draw(_pixel, new Rectangle(panel.X, panel.Y, panel.Width, 2), new Color(220, 72, 72));
        var y = panel.Y + 10f;
        foreach (var line in lines)
        {
            DrawBitmapFontText(line, new Vector2(panel.X + 13f, y), new Color(232, 232, 232), scale);
            y += 20f;
        }
    }

    private Texture2D? GetLastToDieBuffIconTexture()
    {
        if (_lastToDieBuffIconLoadAttempted)
        {
            return _lastToDieBuffIconTexture;
        }

        _lastToDieBuffIconLoadAttempted = true;
        try
        {
            var path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), "ltd_buff.png");
            if (File.Exists(path))
            {
                using var stream = File.OpenRead(path);
                _lastToDieBuffIconTexture = Texture2D.FromStream(GraphicsDevice, stream);
            }
        }
        catch
        {
            _lastToDieBuffIconTexture = null;
        }

        return _lastToDieBuffIconTexture;
    }

    private int GetOfflineEnemyBotCount()
    {
        return IsLastToDieSessionActive
            ? _lastToDieRun?.EnemyBotCount ?? 0
            : _practiceEnemyBotCount;
    }

    private int GetOfflineFriendlyBotCount()
    {
        return IsLastToDieSessionActive ? 0 : _practiceFriendlyBotCount;
    }

    private bool ShouldSuspendOfflineGameplaySimulation()
    {
        return IsLastToDieSessionActive
            && (HasOpenGameplayOverlay()
                || _lastToDieSurvivorMenuOpen
                || _lastToDiePerkMenuOpen
                || IsLastToDieStageClearOverlayActive()
                || IsLastToDieFailurePresentationActive());
    }

    private bool IsLastToDieStageClearOverlayActive()
    {
        return _lastToDieStageClearOverlayOpen;
    }

    private bool IsLastToDieFailureOverlayActive()
    {
        return _lastToDieFailureOverlayOpen;
    }

    private int GetLastToDieStageIntroDurationTicks()
    {
        return Math.Max(1, (int)MathF.Round(_config.TicksPerSecond * LastToDieStageIntroDurationSeconds));
    }

    private void ResetLastToDieState()
    {
        StopLastToDieGameOverSound();
        _lastToDieRun = null;
        _lastToDieSurvivorMenuOpen = false;
        _lastToDieSurvivorHoverIndex = -1;
        _lastToDiePerkMenuOpen = false;
        _lastToDiePerkHoverIndex = -1;
        _lastToDieStageClearOverlayOpen = false;
        _lastToDieStageClearOverlayTicks = 0;
        ClearLastToDieDeathFocusPresentation();
        ResetLastToDieBotReactionState();
        ResetLastToDieCombatFeedbackPresentation();
        _lastToDieFailureOverlayOpen = false;
        _lastToDieFailureOverlayTicks = 0;
        _lastToDieTimerReductionPopupTicksRemaining = 0;
        _lastToDieTimerReductionPopupRise = 0f;
        _lastToDieTimerReductionPopupSeconds = 0;
    }

    private void TryStartLastToDieRun()
    {
        _practiceMapEntries = BuildPracticeMapEntries();
        var initialMap = SelectInitialLastToDieMap();
        if (initialMap is null)
        {
            _menuStatusMessage = "No local maps are available for Last To Die.";
            return;
        }

        ResetLastToDieState();
        _lastToDieRun = new LastToDieRunState(initialMap.LevelName)
        {
            AwaitingOpeningSurvivorSelection = true,
            AwaitingOpeningPerkSelection = true,
        };
        if (!BeginLastToDieStage(initialMap.LevelName))
        {
            ResetLastToDieState();
            return;
        }

        OpenLastToDieSurvivorMenu();
    }

    private bool BeginLastToDieStage(string levelName)
    {
        return _gameplaySessionController.BeginLastToDieStage(levelName);
    }

    private static PlayerClass GetLastToDieSurvivorPlayerClass(LastToDieSurvivorKind survivorKind)
    {
        return survivorKind switch
        {
            LastToDieSurvivorKind.Demoknight => PlayerClass.Demoman,
            _ => PlayerClass.Soldier,
        };
    }

    private void ApplySelectedLastToDieSurvivorToCurrentStage()
    {
        if (_lastToDieRun is null || _lastToDieRun.AwaitingOpeningSurvivorSelection)
        {
            return;
        }

        var playerClass = GetLastToDieSurvivorPlayerClass(_lastToDieRun.SurvivorKind);
        _world.PrepareLocalPlayerJoin();
        _world.SetLocalPlayerTeam(PlayerTeam.Red);
        _world.CompleteLocalPlayerJoin(playerClass);
        _world.TryMoveLocalPlayerToControlPointSpawn();
    }

    private bool TryBeginOfflineBotSession(
        string levelName,
        GameplaySessionKind sessionKind,
        int tickRate,
        ExperimentalGameplaySettings experimentalSettings,
        int timeLimitMinutes,
        int capLimit,
        int respawnSeconds,
        bool openJoinMenus,
        string consoleSessionName)
    {
        return _gameplaySessionController.TryBeginOfflineBotSession(
            levelName,
            sessionKind,
            tickRate,
            experimentalSettings,
            timeLimitMinutes,
            capLimit,
            respawnSeconds,
            openJoinMenus,
            consoleSessionName);
    }

    private void AdvanceLastToDieSimulationTick()
    {
        if (!IsLastToDieSessionActive
            || _lastToDieRun is null
            || _lastToDieSurvivorMenuOpen
            || _lastToDiePerkMenuOpen
            || IsLastToDieStageClearOverlayActive()
            || IsLastToDieFailurePresentationActive())
        {
            return;
        }

        if (_lastToDieRun.StageRemainingTicks > 0)
        {
            _lastToDieRun.StageRemainingTicks -= 1;
        }

        if (_lastToDieRun.StageIntroTicksRemaining > 0)
        {
            _lastToDieRun.StageIntroTicksRemaining -= 1;
        }
    }

    private void UpdateLastToDieSession(int clientTicks)
    {
        _ = clientTicks;
        UpdateLastToDieRunStats();

        if (!IsLastToDieSessionActive
            || _lastToDieRun is null
            || _lastToDieSurvivorMenuOpen
            || _lastToDiePerkMenuOpen
            || IsLastToDieStageClearOverlayActive()
            || IsLastToDieFailurePresentationActive())
        {
            return;
        }

        if (!_world.LocalPlayerAwaitingJoin && !_world.LocalPlayer.IsAlive)
        {
            TriggerLastToDieDeathFocusFailure();
            return;
        }

        if (_world.MatchState.IsEnded)
        {
            if (_world.MatchState.WinnerTeam == PlayerTeam.Blue)
            {
                TriggerLastToDieFailure();
                return;
            }

            if (_world.MatchState.WinnerTeam == PlayerTeam.Red)
            {
                HandleLastToDieStageClear();
                return;
            }
        }

        if (_lastToDieRun.StageRemainingTicks <= 0)
        {
            HandleLastToDieStageClear();
        }
    }

    private void HandleLastToDieStageClear()
    {
        if (_lastToDieRun is null
            || _lastToDieSurvivorMenuOpen
            || _lastToDiePerkMenuOpen
            || IsLastToDieStageClearOverlayActive()
            || IsLastToDieFailurePresentationActive())
        {
            return;
        }

        _lastToDieRun.LevelsCompleted += 1;

        if (IsLastToDieFinalStage(_lastToDieRun))
        {
            ReturnToLastToDieMenu("Last To Die cleared.");
            return;
        }

        OpenLastToDieStageClearOverlay();
    }

    private bool TryTriggerLastToDieStageVictoryForTesting()
    {
        if (!IsLastToDieSessionActive || _lastToDieRun is null)
        {
            return false;
        }

        if (IsLastToDieFailurePresentationActive())
        {
            return false;
        }

        if (IsLastToDieStageClearOverlayActive())
        {
            ContinueFromLastToDieStageClearOverlay();
            return true;
        }

        if (_lastToDiePerkMenuOpen)
        {
            return true;
        }

        if (_lastToDieSurvivorMenuOpen)
        {
            return true;
        }

        HandleLastToDieStageClear();
        if (IsLastToDieStageClearOverlayActive())
        {
            ContinueFromLastToDieStageClearOverlay();
        }

        return true;
    }

    private void OpenLastToDieStageClearOverlay()
    {
        StopIngameMusic();
        StopLastToDieIngameMusic();
        CloseInGameMenu();
        if (_lastToDieStageClearOverlayOpen)
        {
            return;
        }

        _lastToDieStageClearOverlayOpen = true;
        _lastToDieStageClearOverlayTicks = 0;
    }

    private void ContinueFromLastToDieStageClearOverlay()
    {
        _lastToDieStageClearOverlayOpen = false;
        _lastToDieStageClearOverlayTicks = 0;
        SuppressPrimaryFireUntilMouseRelease();
        OpenLastToDiePerkMenu();
    }

    private static bool IsLastToDieFinalStage(LastToDieRunState run)
    {
        return run.EnemyBotCount >= LastToDieFinalEnemyBotCount
            && run.StageDurationMinutes >= LastToDieFinalStageMinutes;
    }

    private void OpenLastToDieSurvivorMenu()
    {
        if (_lastToDieRun is null)
        {
            return;
        }

        StopIngameMusic();
        StopLastToDieIngameMusic();
        _lastToDieSurvivorMenuOpen = true;
        _lastToDieSurvivorHoverIndex = -1;
    }

    private void OpenLastToDiePerkMenu()
    {
        if (_lastToDieRun is null)
        {
            return;
        }

        var choices = BuildLastToDiePerkChoices(_lastToDieRun);
        if (choices.Length == 0)
        {
            if (_lastToDieRun.AwaitingOpeningPerkSelection)
            {
                _lastToDieRun.AwaitingOpeningPerkSelection = false;
                _lastToDieRun.PendingRewardChoices = [];
                return;
            }

            AdvanceLastToDieStage();
            return;
        }

        StopIngameMusic();
        StopLastToDieIngameMusic();
        _lastToDieRun.PendingRewardChoices = choices;
        _lastToDiePerkMenuOpen = true;
        _lastToDiePerkHoverIndex = -1;
    }

    private static LastToDieRewardChoice[] BuildLastToDiePerkChoices(LastToDieRunState run)
    {
        var available = GetLastToDiePerkCatalog(run.SurvivorKind)
            .Where(definition => !run.ChosenPerks.Contains(definition.Kind))
            .ToList();
        ShuffleLastToDiePerks(available);
        var choices = available
            .Take(Math.Min(LastToDiePerkChoiceCount, available.Count))
            .Select(perk => new LastToDieRewardChoice(perk, null))
            .ToArray();
        if (choices.Length > 0
            && run.SurvivorKind == LastToDieSurvivorKind.Soldier
            && RandomNumberGenerator.GetInt32(100) < (int)(LastToDieAccessoryChoiceChance * 100f))
        {
            var replaceIndex = RandomNumberGenerator.GetInt32(choices.Length);
            choices[replaceIndex] = new LastToDieRewardChoice(null, CreateRandomLastToDieAccessory());
        }

        return choices;
    }

    private static LastToDieAccessoryDefinition CreateRandomLastToDieAccessory()
    {
        var slot = RandomNumberGenerator.GetInt32(2) == 0
            ? LastToDieAccessorySlot.Helmet
            : LastToDieAccessorySlot.Dogtags;
        var statKinds = Enum.GetValues<LastToDieAccessoryStatKind>();
        var statKind = statKinds[RandomNumberGenerator.GetInt32(statKinds.Length)];
        PlayerClass? targetClass = statKind == LastToDieAccessoryStatKind.DamageToClass
            ? GameplayLoadoutMenuPresentation.ClassStripOrder[RandomNumberGenerator.GetInt32(GameplayLoadoutMenuPresentation.ClassStripOrder.Length)]
            : null;
        return new LastToDieAccessoryDefinition(slot, statKind, RollLastToDieAccessoryValue(statKind), targetClass);
    }

    private static int RollLastToDieAccessoryValue(LastToDieAccessoryStatKind statKind)
    {
        return statKind switch
        {
            LastToDieAccessoryStatKind.JumpHeight => RollLastToDieAccessoryStep(5, 50, 5),
            LastToDieAccessoryStatKind.BonusJumps => RollLastToDieAccessoryStep(1, 4, 1),
            LastToDieAccessoryStatKind.Evasion => RollLastToDieAccessoryStep(10, 60, 5),
            LastToDieAccessoryStatKind.Thorns => RollLastToDieAccessoryStep(10, 100, 10),
            LastToDieAccessoryStatKind.BulletResist
                or LastToDieAccessoryStatKind.ExplosiveResist
                or LastToDieAccessoryStatKind.FireResist
                or LastToDieAccessoryStatKind.DamageResist
                or LastToDieAccessoryStatKind.AcquiredWeaponHealing => RollLastToDieAccessoryStep(10, 60, 5),
            LastToDieAccessoryStatKind.HealthRegeneration => RollLastToDieAccessoryStep(3, 15, 1),
            LastToDieAccessoryStatKind.AcquiredWeaponDamage => RollLastToDieAccessoryStep(10, 90, 5),
            LastToDieAccessoryStatKind.DamageToClass => RollLastToDieAccessoryStep(10, 70, 5),
            LastToDieAccessoryStatKind.ProjectilesPerShot => RollLastToDieAccessoryStep(2, 4, 1),
            _ => RollLastToDieAccessoryStep(10, 80, 5),
        };
    }

    private static int RollLastToDieAccessoryStep(int minValue, int maxValue, int step)
    {
        var stepCount = ((maxValue - minValue) / step) + 1;
        return minValue + (RandomNumberGenerator.GetInt32(stepCount) * step);
    }

    private static LastToDiePerkDefinition[] GetLastToDiePerkCatalog(LastToDieSurvivorKind survivorKind)
    {
        return survivorKind switch
        {
            LastToDieSurvivorKind.Demoknight => LastToDieDemoknightPerkCatalog,
            _ => LastToDieSoldierPerkCatalog,
        };
    }

    private void AdvanceLastToDieStage()
    {
        if (_lastToDieRun is null)
        {
            return;
        }

        _lastToDieRun.StageNumber = Math.Min(LastToDieStageCount, _lastToDieRun.StageNumber + 1);
        _lastToDieRun.EnemyBotCount = Math.Min(LastToDieFinalEnemyBotCount, _lastToDieRun.EnemyBotCount + 1);
        _lastToDieRun.StageDurationMinutes = Math.Min(
            LastToDieFinalStageMinutes,
            _lastToDieRun.StageDurationMinutes + LastToDieStageMinuteIncrement);
        _lastToDieRun.PendingRewardChoices = [];

        var nextMap = SelectRandomLastToDieMap(_lastToDieRun.CurrentLevelName);
        if (nextMap is null || !BeginLastToDieStage(nextMap.LevelName))
        {
            ReturnToLastToDieMenu("Failed to start the next Last To Die stage.");
        }
    }

    private PracticeMapEntry? SelectRandomLastToDieMap(string? excludedLevelName)
    {
        if (_practiceMapEntries.Count == 0)
        {
            _practiceMapEntries = BuildPracticeMapEntries();
        }

        if (_practiceMapEntries.Count == 0)
        {
            return null;
        }

        var rotationEntries = _practiceMapEntries
            .Where(IsEligibleLastToDieRotationMap)
            .ToList();
        if (rotationEntries.Count == 0)
        {
            return null;
        }

        var candidates = string.IsNullOrWhiteSpace(excludedLevelName)
            ? rotationEntries
            : rotationEntries
                .Where(entry => !string.Equals(entry.LevelName, excludedLevelName, StringComparison.OrdinalIgnoreCase))
                .ToList();
        if (candidates.Count == 0)
        {
            candidates = rotationEntries;
        }

        return candidates[RandomNumberGenerator.GetInt32(candidates.Count)];
    }

    private PracticeMapEntry? SelectInitialLastToDieMap()
    {
        if (_practiceMapEntries.Count == 0)
        {
            _practiceMapEntries = BuildPracticeMapEntries();
        }

        return SelectRandomLastToDieMap(excludedLevelName: null);
    }

    private static bool IsEligibleLastToDieRotationMap(PracticeMapEntry entry)
    {
        return entry.Mode == GameModeKind.KingOfTheHill;
    }

    private static void ShuffleLastToDiePerks(List<LastToDiePerkDefinition> definitions)
    {
        for (var index = definitions.Count - 1; index > 0; index -= 1)
        {
            var swapIndex = RandomNumberGenerator.GetInt32(index + 1);
            (definitions[index], definitions[swapIndex]) = (definitions[swapIndex], definitions[index]);
        }
    }

    private static ExperimentalGameplaySettings BuildLastToDieExperimentalGameplaySettings(LastToDieRunState run)
    {
        var settings = new ExperimentalGameplaySettings(
            EnableSoldierFastCapture: run.SurvivorKind == LastToDieSurvivorKind.Soldier,
            EnableDemoknightFastCapture: run.SurvivorKind == LastToDieSurvivorKind.Demoknight,
            EnableDemoknightKit: run.SurvivorKind == LastToDieSurvivorKind.Demoknight,
            EnableCapturedPointHealingAura: true,
            DemoknightSwordBaseDamage: run.SurvivorKind == LastToDieSurvivorKind.Demoknight ? 100 : ExperimentalGameplaySettings.DefaultDemoknightSwordBaseDamage,
            EnableComboTracking: true,
            EnableKillStreakTracking: true,
            EnableRage: true,
            EnableEnemyHealthPackDrops: true,
            EnableEnemyDroppedWeapons: true,
            EnemyHealthPackDropChance: 1f);

        foreach (var perk in run.ChosenPerks)
        {
            settings = perk switch
            {
                LastToDiePerkKind.SoldierShotgun => settings with
                {
                    EnableSoldierShotgunSecondaryWeapon = true,
                    SoldierShotgunPelletMultiplier = 2,
                },
                LastToDiePerkKind.HealOnDamage => settings with { EnableHealOnDamage = true },
                LastToDiePerkKind.HealOnKill => settings with { EnableHealOnKill = true, HealOnKillAmount = 75 },
                LastToDiePerkKind.RateOfFireOnDamage => settings with { EnableRateOfFireMultiplierOnDamage = true },
                LastToDiePerkKind.SoldierInstantReload => settings with { EnableSoldierInstantReload = true },
                LastToDiePerkKind.PassiveHealthRegeneration => settings with { EnablePassiveHealthRegeneration = true, PassiveHealthRegenerationPerSecond = 8f },
                LastToDiePerkKind.InvincibilityOnKill => settings with { EnableGhostPhaseOnKill = true, KillInvincibilityDurationSeconds = 1f },
                LastToDiePerkKind.ProjectileSpeedMultiplier => settings with { EnableProjectileSpeedMultiplier = true, ProjectileSpeedMultiplierValue = 1.6f },
                LastToDiePerkKind.AirshotDamageMultiplier => settings with { EnableAirshotDamageMultiplier = true, AirshotDamageMultiplierValue = 1.5f },
                LastToDiePerkKind.SoldierStingerRockets => settings with { EnableSoldierStingerRockets = true },
                LastToDiePerkKind.SoldierRageExtensionOnKill => settings with { EnableSoldierRageExtensionOnKill = true },
                LastToDiePerkKind.SoldierDangerClose => settings with { EnableSoldierDangerClose = true },
                LastToDiePerkKind.SoldierSelfDamageHealing => settings with { EnableSelfDamageHealing = true },
                LastToDiePerkKind.SoldierReloadSpeedMultiplier => settings with { ReloadSpeedMultiplierValue = 1f / 0.6f },
                LastToDiePerkKind.SoldierAmmoRegeneratesWhileSwappedOut => settings with { EnableSoldierAmmoRegeneratesWhileSwappedOut = true },
                LastToDiePerkKind.SoldierInfiniteAmmoDuringRage => settings with { EnableSoldierInfiniteAmmoDuringRage = true },
                LastToDiePerkKind.SoldierRageCaptureLockout => settings with { EnableSoldierRageCaptureLockout = true },
                LastToDiePerkKind.SoldierRageCaptureDuringRage => settings with { EnableSoldierRageCaptureDuringRage = true },
                LastToDiePerkKind.SoldierNapalmRockets => settings with { EnableSoldierNapalmRockets = true },
                LastToDiePerkKind.SoldierFinalClipRocketBurst => settings with { EnableSoldierFinalClipRocketBurst = true },
                LastToDiePerkKind.SoldierCivilDefenseTurret => settings with { EnableSoldierCivilDefenseTurret = true },
                LastToDiePerkKind.SoldierLuckyBastard => settings with { EnableSoldierLuckyBastard = true },
                LastToDiePerkKind.SoldierThundergunner => settings with { EnableSoldierThundergunner = true },
                LastToDiePerkKind.SoldierBattleborn => settings with { EnableSoldierBattleborn = true },
                LastToDiePerkKind.SoldierFogOfWar => settings with { EnableSoldierFogOfWar = true },
                LastToDiePerkKind.DemoknightMeleeRange => settings with { DemoknightSwordRangeMultiplier = 1.5f },
                LastToDiePerkKind.DemoknightLifesteal => settings with { EnableHealOnDamage = true, HealOnDamageFraction = 0.6f },
                LastToDiePerkKind.DemoknightMoveSpeed => settings with { PassiveMovementSpeedMultiplier = 1.3f },
                LastToDiePerkKind.DemoknightKillHeal => settings with { EnableHealOnKill = true, HealOnKillAmount = 75 },
                LastToDiePerkKind.DemoknightKillInvincibility => settings with { EnableInvincibilityOnKill = true, KillInvincibilityDurationSeconds = 2f },
                LastToDiePerkKind.DemoknightChargeRate => settings with { DemoknightChargeRechargeMultiplier = 1.8f },
                LastToDiePerkKind.DemoknightChargeResistance => settings with { DemoknightChargeDamageTakenMultiplier = 0.2f },
                LastToDiePerkKind.DemoknightDamageMultiplier => settings with { DemoknightSwordDamageMultiplier = 1.4f },
                LastToDiePerkKind.DemoknightFullHealOnKill => settings with { EnableFullHealOnKill = true },
                LastToDiePerkKind.DemoknightAttackSpeed => settings with { DemoknightSwordCooldownMultiplier = 1f / 1.5f },
                LastToDiePerkKind.DemoknightPostRageRegeneration => settings with { EnableDemoknightPostRageRegeneration = true },
                LastToDiePerkKind.DemoknightFullControlDuringCharge => settings with { EnableDemoknightFullControlDuringCharge = true },
                LastToDiePerkKind.DemoknightGhostDash => settings with { EnableDemoknightGhostDash = true },
                _ => settings,
            };
        }

        return ApplyLastToDieAccessorySettings(run, settings);
    }

    private static ExperimentalGameplaySettings ApplyLastToDieAccessorySettings(
        LastToDieRunState run,
        ExperimentalGameplaySettings settings)
    {
        ApplyAccessory(run.EquippedHelmet);
        ApplyAccessory(run.EquippedDogtags);
        return settings;

        void ApplyAccessory(LastToDieAccessoryDefinition? accessory)
        {
            if (!accessory.HasValue)
            {
                return;
            }

            var item = accessory.Value;
            var percentMultiplier = 1f + (item.Value / 100f);
            switch (item.StatKind)
            {
                case LastToDieAccessoryStatKind.ExplosiveDamage:
                case LastToDieAccessoryStatKind.BulletDamage:
                    settings = settings with { PassiveDamageMultiplier = settings.PassiveDamageMultiplier * percentMultiplier };
                    break;
                case LastToDieAccessoryStatKind.MovementSpeed:
                    settings = settings with { PassiveMovementSpeedMultiplier = settings.PassiveMovementSpeedMultiplier * percentMultiplier };
                    break;
                case LastToDieAccessoryStatKind.JumpHeight:
                    settings = settings with { PassiveJumpHeightMultiplier = settings.PassiveJumpHeightMultiplier * percentMultiplier };
                    break;
                case LastToDieAccessoryStatKind.BonusJumps:
                    settings = settings with { PassiveBonusAirJumps = settings.PassiveBonusAirJumps + item.Value };
                    break;
                case LastToDieAccessoryStatKind.Evasion:
                    settings = settings with { PassiveEvasionChance = settings.PassiveEvasionChance + (item.Value / 100f) };
                    break;
                case LastToDieAccessoryStatKind.Thorns:
                    settings = settings with { PassiveThornsFraction = settings.PassiveThornsFraction + (item.Value / 100f) };
                    break;
                case LastToDieAccessoryStatKind.BulletResist:
                case LastToDieAccessoryStatKind.ExplosiveResist:
                case LastToDieAccessoryStatKind.FireResist:
                case LastToDieAccessoryStatKind.DamageResist:
                    settings = settings with { PassiveDamageResistance = CombineLastToDieResistance(settings.PassiveDamageResistance, item.Value / 100f) };
                    break;
                case LastToDieAccessoryStatKind.HealthRegeneration:
                    settings = settings with
                    {
                        EnablePassiveHealthRegeneration = true,
                        PassiveHealthRegenerationPerSecond = settings.PassiveHealthRegenerationPerSecond + item.Value,
                    };
                    break;
                case LastToDieAccessoryStatKind.AcquiredWeaponDamage:
                    settings = settings with { AcquiredWeaponDamageMultiplier = settings.AcquiredWeaponDamageMultiplier * percentMultiplier };
                    break;
                case LastToDieAccessoryStatKind.AcquiredWeaponHealing:
                    settings = settings with { AcquiredWeaponHealingMultiplier = settings.AcquiredWeaponHealingMultiplier * percentMultiplier };
                    break;
                case LastToDieAccessoryStatKind.DamageToClass:
                    settings = settings with { PassiveDamageMultiplier = settings.PassiveDamageMultiplier * percentMultiplier };
                    break;
                case LastToDieAccessoryStatKind.ProjectilesPerShot:
                    settings = settings with { BonusProjectilesPerShot = Math.Max(settings.BonusProjectilesPerShot, item.Value - 1) };
                    break;
            }
        }
    }

    private static float CombineLastToDieResistance(float current, float added)
    {
        return 1f - ((1f - Math.Clamp(current, 0f, 0.95f)) * (1f - Math.Clamp(added, 0f, 0.95f)));
    }

    private void UpdateLastToDieSurvivorMenu(KeyboardState keyboard, MouseState mouse)
    {
        if (!_lastToDieSurvivorMenuOpen || _lastToDieRun is null)
        {
            return;
        }

        var layout = GetLastToDieChoiceMenuLayout(LastToDieSurvivorCatalog.Length);
        _lastToDieSurvivorHoverIndex = GetLastToDieChoiceHoverIndex(mouse.Position, layout);

        if (TryGetLastToDieChoiceHotkeySelection(keyboard, LastToDieSurvivorCatalog.Length, out var selectedIndex))
        {
            ChooseLastToDieSurvivor(selectedIndex);
            return;
        }

        var clickPressed = mouse.LeftButton == ButtonState.Pressed && _previousMouse.LeftButton != ButtonState.Pressed;
        if (!clickPressed || _lastToDieSurvivorHoverIndex < 0)
        {
            return;
        }

        ChooseLastToDieSurvivor(_lastToDieSurvivorHoverIndex);
    }

    private void UpdateLastToDiePerkMenu(KeyboardState keyboard, MouseState mouse)
    {
        if (!_lastToDiePerkMenuOpen || _lastToDieRun is null)
        {
            return;
        }

        var layout = GetLastToDieChoiceMenuLayout(_lastToDieRun.PendingRewardChoices.Length);
        _lastToDiePerkHoverIndex = GetLastToDieChoiceHoverIndex(mouse.Position, layout);

        if (TryGetLastToDieChoiceHotkeySelection(keyboard, _lastToDieRun.PendingRewardChoices.Length, out var selectedIndex))
        {
            ChooseLastToDiePerk(selectedIndex);
            return;
        }

        var clickPressed = mouse.LeftButton == ButtonState.Pressed && _previousMouse.LeftButton != ButtonState.Pressed;
        if (!clickPressed || _lastToDiePerkHoverIndex < 0)
        {
            return;
        }

        ChooseLastToDiePerk(_lastToDiePerkHoverIndex);
    }

    private void UpdateLastToDieStageClearOverlay(KeyboardState keyboard, MouseState mouse)
    {
        if (!_lastToDieStageClearOverlayOpen)
        {
            return;
        }

        _lastToDieStageClearOverlayTicks += 1;
        var readyForContinue = _lastToDieStageClearOverlayTicks >= LastToDieStageClearContinueDelayTicks;
        var clickPressed = mouse.LeftButton == ButtonState.Pressed && _previousMouse.LeftButton != ButtonState.Pressed;
        if (!readyForContinue)
        {
            return;
        }

        if (clickPressed || IsKeyPressed(keyboard, Keys.Enter) || IsKeyPressed(keyboard, Keys.Space))
        {
            ContinueFromLastToDieStageClearOverlay();
        }
    }

    private bool TryGetLastToDieChoiceHotkeySelection(KeyboardState keyboard, int choiceCount, out int selectedIndex)
    {
        selectedIndex = -1;
        Keys[] digitKeys = [Keys.D1, Keys.D2, Keys.D3];
        Keys[] numpadKeys = [Keys.NumPad1, Keys.NumPad2, Keys.NumPad3];
        for (var index = 0; index < Math.Min(choiceCount, digitKeys.Length); index += 1)
        {
            if (IsKeyPressed(keyboard, digitKeys[index]) || IsKeyPressed(keyboard, numpadKeys[index]))
            {
                selectedIndex = index;
                return true;
            }
        }

        return false;
    }

    private void ChooseLastToDieSurvivor(int selectedIndex)
    {
        if (_lastToDieRun is null
            || selectedIndex < 0
            || selectedIndex >= LastToDieSurvivorCatalog.Length)
        {
            return;
        }

        var selected = LastToDieSurvivorCatalog[selectedIndex];
        _lastToDieRun.SurvivorKind = selected.Kind;
        _lastToDieRun.AwaitingOpeningSurvivorSelection = false;
        _lastToDieSurvivorMenuOpen = false;
        _lastToDieSurvivorHoverIndex = -1;
        SuppressPrimaryFireUntilMouseRelease();
        ApplySelectedLastToDieSurvivorToCurrentStage();
        OpenLastToDiePerkMenu();
    }

    private void ChooseLastToDiePerk(int selectedIndex)
    {
        if (_lastToDieRun is null
            || selectedIndex < 0
            || selectedIndex >= _lastToDieRun.PendingRewardChoices.Length)
        {
            return;
        }

        var selected = _lastToDieRun.PendingRewardChoices[selectedIndex];
        if (selected.Accessory.HasValue)
        {
            EquipLastToDieAccessory(_lastToDieRun, selected.Accessory.Value);
        }
        else if (selected.Perk.HasValue)
        {
            _lastToDieRun.ChosenPerks.Add(selected.Perk.Value.Kind);
        }

        _world.ConfigureExperimentalGameplaySettings(BuildLastToDieExperimentalGameplaySettings(_lastToDieRun));
        _lastToDieRun.PendingRewardChoices = [];
        _lastToDiePerkMenuOpen = false;
        _lastToDiePerkHoverIndex = -1;
        SuppressPrimaryFireUntilMouseRelease();

        if (_lastToDieRun.AwaitingOpeningPerkSelection)
        {
            _lastToDieRun.AwaitingOpeningPerkSelection = false;
            if (!BeginLastToDieStage(_lastToDieRun.CurrentLevelName))
            {
                ReturnToLastToDieMenu("Failed to start Last To Die.");
            }
            return;
        }

        AdvanceLastToDieStage();
    }

    private static void EquipLastToDieAccessory(LastToDieRunState run, LastToDieAccessoryDefinition accessory)
    {
        if (accessory.Slot == LastToDieAccessorySlot.Helmet)
        {
            run.EquippedHelmet = accessory;
        }
        else
        {
            run.EquippedDogtags = accessory;
        }
    }

    private static string GetLastToDieRewardChoiceLabel(LastToDieRewardChoice choice)
    {
        if (choice.Accessory.HasValue)
        {
            var accessory = choice.Accessory.Value;
            return $"Item: {GetLastToDieAccessoryPrefix(accessory)} {accessory.SlotLabel}";
        }

        return choice.Perk?.Label ?? string.Empty;
    }

    private static string GetLastToDieRewardChoiceDescription(LastToDieRewardChoice choice)
    {
        if (choice.Accessory.HasValue)
        {
            return GetLastToDieAccessoryDescription(choice.Accessory.Value);
        }

        return choice.Perk?.Description ?? string.Empty;
    }

    private static string GetLastToDieAccessoryPrefix(LastToDieAccessoryDefinition accessory)
    {
        return accessory.StatKind switch
        {
            LastToDieAccessoryStatKind.ExplosiveDamage => "Grenadier's",
            LastToDieAccessoryStatKind.BulletDamage => "Rifleman's",
            LastToDieAccessoryStatKind.MovementSpeed => "Speedy",
            LastToDieAccessoryStatKind.JumpHeight => "Air Superiority",
            LastToDieAccessoryStatKind.BonusJumps => "Hoppy",
            LastToDieAccessoryStatKind.Evasion => "Graceful",
            LastToDieAccessoryStatKind.Thorns => "Pointy",
            LastToDieAccessoryStatKind.BulletResist => "Stubborn",
            LastToDieAccessoryStatKind.ExplosiveResist => "Bomb Squad",
            LastToDieAccessoryStatKind.FireResist => "Flaming",
            LastToDieAccessoryStatKind.HealthRegeneration => "Sturdy",
            LastToDieAccessoryStatKind.DamageResist => "Defensive",
            LastToDieAccessoryStatKind.AcquiredWeaponDamage => "Expert's",
            LastToDieAccessoryStatKind.AcquiredWeaponHealing => "Cunning",
            LastToDieAccessoryStatKind.DamageToClass => "Specialist's",
            LastToDieAccessoryStatKind.ProjectilesPerShot => "Tyrant's",
            _ => "Soldier's",
        };
    }

    private static string GetLastToDieAccessoryDescription(LastToDieAccessoryDefinition accessory)
    {
        return accessory.StatKind switch
        {
            LastToDieAccessoryStatKind.BonusJumps => $"+{accessory.Value} bonus jumps. (Auto equips)",
            LastToDieAccessoryStatKind.HealthRegeneration => $"+{accessory.Value} health regeneration per second. (Auto equips)",
            LastToDieAccessoryStatKind.ProjectilesPerShot => $"Projectiles per shot x{accessory.Value}. (Auto equips)",
            LastToDieAccessoryStatKind.DamageToClass => $"+{accessory.Value}% damage to {accessory.TargetClass?.ToString() ?? "one class"}. (Auto equips)",
            _ => $"+{accessory.Value}% {GetLastToDieAccessoryStatLabel(accessory.StatKind)}. (Auto equips)",
        };
    }

    private static string GetLastToDieAccessoryStatLabel(LastToDieAccessoryStatKind statKind)
    {
        return statKind switch
        {
            LastToDieAccessoryStatKind.ExplosiveDamage => "explosive damage",
            LastToDieAccessoryStatKind.BulletDamage => "bullet damage",
            LastToDieAccessoryStatKind.MovementSpeed => "movement speed",
            LastToDieAccessoryStatKind.JumpHeight => "jump height",
            LastToDieAccessoryStatKind.Evasion => "evasion",
            LastToDieAccessoryStatKind.Thorns => "thorns",
            LastToDieAccessoryStatKind.BulletResist => "bullet resist",
            LastToDieAccessoryStatKind.ExplosiveResist => "explosive resist",
            LastToDieAccessoryStatKind.FireResist => "fire resist",
            LastToDieAccessoryStatKind.DamageResist => "damage resist",
            LastToDieAccessoryStatKind.AcquiredWeaponDamage => "damage with acquired weapons",
            LastToDieAccessoryStatKind.AcquiredWeaponHealing => "healing from acquired weapons",
            _ => statKind.ToString(),
        };
    }

    private static List<string> BuildLastToDieBuffTooltipLines(LastToDieRunState run)
    {
        var lines = new List<string>();
        if (run.EquippedHelmet.HasValue)
        {
            lines.Add(GetLastToDieAccessoryTooltipLine(run.EquippedHelmet.Value));
        }

        if (run.EquippedDogtags.HasValue)
        {
            lines.Add(GetLastToDieAccessoryTooltipLine(run.EquippedDogtags.Value));
        }

        foreach (var perk in run.ChosenPerks.Take(8))
        {
            if (TryGetLastToDiePerkDefinition(run.SurvivorKind, perk, out var definition))
            {
                lines.Add(definition.Label);
            }
        }

        if (run.ChosenPerks.Count > 8)
        {
            lines.Add($"+{run.ChosenPerks.Count - 8} perks");
        }

        return lines;
    }

    private static string GetLastToDieAccessoryTooltipLine(LastToDieAccessoryDefinition accessory)
    {
        return $"{accessory.SlotLabel}: {GetLastToDieAccessoryPrefix(accessory)} +{accessory.Value}";
    }

    private static bool TryGetLastToDiePerkDefinition(
        LastToDieSurvivorKind survivorKind,
        LastToDiePerkKind perkKind,
        out LastToDiePerkDefinition definition)
    {
        foreach (var candidate in GetLastToDiePerkCatalog(survivorKind))
        {
            if (candidate.Kind == perkKind)
            {
                definition = candidate;
                return true;
            }
        }

        definition = default;
        return false;
    }

    private LastToDieChoiceMenuLayout GetLastToDieChoiceMenuLayout(int choiceCount)
    {
        var panelWidth = Math.Min(ViewportWidth - 48, 980);
        var panelHeight = Math.Min(ViewportHeight - 40, 440);
        var panel = new Rectangle(
            (ViewportWidth - panelWidth) / 2,
            (ViewportHeight - panelHeight) / 2,
            panelWidth,
            panelHeight);
        var availableWidth = panel.Width - 64;
        var gap = 18;
        var cardWidth = (availableWidth - (gap * Math.Max(0, choiceCount - 1))) / Math.Max(1, choiceCount);
        var cardHeight = 224;
        var cardTop = panel.Y + 136;
        var cards = new Rectangle[choiceCount];
        var left = panel.X + 32;
        for (var index = 0; index < choiceCount; index += 1)
        {
            cards[index] = new Rectangle(left, cardTop, cardWidth, cardHeight);
            left += cardWidth + gap;
        }

        return new LastToDieChoiceMenuLayout(panel, cards);
    }

    private static int GetLastToDieChoiceHoverIndex(Point mousePosition, LastToDieChoiceMenuLayout layout)
    {
        for (var index = 0; index < layout.CardBounds.Length; index += 1)
        {
            if (layout.CardBounds[index].Contains(mousePosition))
            {
                return index;
            }
        }

        return -1;
    }

    private void DrawLastToDieSurvivorMenu()
    {
        if (!_lastToDieSurvivorMenuOpen || _lastToDieRun is null)
        {
            return;
        }

        var viewportWidth = ViewportWidth;
        var viewportHeight = ViewportHeight;
        _spriteBatch.Draw(_pixel, new Rectangle(0, 0, viewportWidth, viewportHeight), Color.Black * 0.86f);

        var layout = GetLastToDieChoiceMenuLayout(LastToDieSurvivorCatalog.Length);
        _spriteBatch.Draw(_pixel, layout.Panel, new Color(22, 24, 29, 242));
        _spriteBatch.Draw(_pixel, new Rectangle(layout.Panel.X, layout.Panel.Y, layout.Panel.Width, 3), new Color(210, 210, 210));
        _spriteBatch.Draw(_pixel, new Rectangle(layout.Panel.X, layout.Panel.Bottom - 3, layout.Panel.Width, 3), new Color(76, 76, 76));

        DrawBitmapFontText("Choose Survivor", new Vector2(layout.Panel.X + 28f, layout.Panel.Y + 24f), Color.White, 1.22f);
        DrawBitmapFontText("Pick the survivor for this run.", new Vector2(layout.Panel.X + 28f, layout.Panel.Y + 58f), new Color(212, 212, 212), 0.94f);

        for (var index = 0; index < LastToDieSurvivorCatalog.Length; index += 1)
        {
            var choice = LastToDieSurvivorCatalog[index];
            var bounds = layout.CardBounds[index];
            var isHovered = index == _lastToDieSurvivorHoverIndex;
            var backColor = isHovered ? new Color(70, 38, 38, 240) : new Color(34, 37, 43, 232);
            var accentColor = isHovered ? new Color(210, 78, 78) : new Color(118, 126, 140);
            _spriteBatch.Draw(_pixel, bounds, backColor);
            _spriteBatch.Draw(_pixel, new Rectangle(bounds.X, bounds.Y, bounds.Width, 3), accentColor);
            _spriteBatch.Draw(_pixel, new Rectangle(bounds.X, bounds.Bottom - 3, bounds.Width, 3), new Color(14, 16, 19));

            DrawBitmapFontText($"{index + 1}", new Vector2(bounds.X + 14f, bounds.Y + 12f), new Color(236, 224, 198), 1f);
            DrawBitmapFontText(choice.Label, new Vector2(bounds.X + 14f, bounds.Y + 58f), Color.White, 1.3f);
        }
    }

    private void DrawLastToDiePerkMenu()
    {
        if (!_lastToDiePerkMenuOpen || _lastToDieRun is null)
        {
            return;
        }

        var viewportWidth = ViewportWidth;
        var viewportHeight = ViewportHeight;
        _spriteBatch.Draw(_pixel, new Rectangle(0, 0, viewportWidth, viewportHeight), Color.Black * 0.86f);

        var layout = GetLastToDieChoiceMenuLayout(_lastToDieRun.PendingRewardChoices.Length);
        _spriteBatch.Draw(_pixel, layout.Panel, new Color(22, 24, 29, 242));
        _spriteBatch.Draw(_pixel, new Rectangle(layout.Panel.X, layout.Panel.Y, layout.Panel.Width, 3), new Color(210, 210, 210));
        _spriteBatch.Draw(_pixel, new Rectangle(layout.Panel.X, layout.Panel.Bottom - 3, layout.Panel.Width, 3), new Color(76, 76, 76));

        DrawBitmapFontText("Perks", new Vector2(layout.Panel.X + 28f, layout.Panel.Y + 24f), Color.White, 1.22f);
        var subtitle = _lastToDieRun.AwaitingOpeningPerkSelection
            ? "Choose 1 perk."
            : "Choose 1 reward for the next stage.";
        DrawBitmapFontText(subtitle, new Vector2(layout.Panel.X + 28f, layout.Panel.Y + 58f), new Color(212, 212, 212), 0.94f);

        for (var index = 0; index < _lastToDieRun.PendingRewardChoices.Length; index += 1)
        {
            var choice = _lastToDieRun.PendingRewardChoices[index];
            var bounds = layout.CardBounds[index];
            var isHovered = index == _lastToDiePerkHoverIndex;
            var backColor = isHovered ? new Color(70, 38, 38, 240) : new Color(34, 37, 43, 232);
            var accentColor = isHovered ? new Color(210, 78, 78) : new Color(118, 126, 140);
            _spriteBatch.Draw(_pixel, bounds, backColor);
            _spriteBatch.Draw(_pixel, new Rectangle(bounds.X, bounds.Y, bounds.Width, 3), accentColor);
            _spriteBatch.Draw(_pixel, new Rectangle(bounds.X, bounds.Bottom - 3, bounds.Width, 3), new Color(14, 16, 19));

            DrawBitmapFontText($"{index + 1}", new Vector2(bounds.X + 14f, bounds.Y + 12f), new Color(236, 224, 198), 1f);
            var label = GetLastToDieRewardChoiceLabel(choice);
            var description = GetLastToDieRewardChoiceDescription(choice);
            var labelColor = choice.IsAccessory ? new Color(255, 214, 82) : Color.White;
            if (choice.IsAccessory)
            {
                DrawBitmapFontText(label, new Vector2(bounds.X + 13f, bounds.Y + 43f), new Color(255, 160, 28) * 0.55f, 1.02f);
                DrawBitmapFontText(label, new Vector2(bounds.X + 15f, bounds.Y + 45f), new Color(255, 244, 160) * 0.35f, 1.02f);
            }

            DrawBitmapFontText(label, new Vector2(bounds.X + 14f, bounds.Y + 44f), labelColor, 0.98f);

            var descriptionLines = WrapMenuParagraph(description, 28);
            var lineY = bounds.Y + 84f;
            for (var lineIndex = 0; lineIndex < descriptionLines.Length; lineIndex += 1)
            {
                DrawBitmapFontText(descriptionLines[lineIndex], new Vector2(bounds.X + 14f, lineY), new Color(214, 214, 214), 0.88f);
                lineY += 20f;
            }
        }
    }

    private void DrawLastToDieStageClearOverlay()
    {
        if (!_lastToDieStageClearOverlayOpen || _lastToDieRun is null)
        {
            return;
        }

        var viewportWidth = ViewportWidth;
        var viewportHeight = ViewportHeight;
        var alpha = Math.Clamp(_lastToDieStageClearOverlayTicks / (float)LastToDieStageClearFadeTicks, 0f, 1f);
        _spriteBatch.Draw(_pixel, new Rectangle(0, 0, viewportWidth, viewportHeight), Color.Black * (0.8f * alpha));

        DrawHudTextCentered(
            "YOU SURVIVED",
            new Vector2(viewportWidth / 2f, viewportHeight * 0.34f),
            new Color(240, 232, 208) * alpha,
            2.7f);

        var clearedText = $"Stage {_lastToDieRun.StageNumber} cleared.";
        DrawHudTextCentered(
            clearedText,
            new Vector2(viewportWidth / 2f, viewportHeight * 0.44f),
            new Color(216, 206, 176) * alpha,
            1.3f);

        if (_lastToDieStageClearOverlayTicks < LastToDieStageClearContinueDelayTicks)
        {
            return;
        }

        DrawHudTextCentered(
            "Press Enter or click to choose your next perk.",
            new Vector2(viewportWidth / 2f, viewportHeight * 0.55f),
            new Color(214, 214, 214) * alpha,
            1f);
    }

    private void TriggerLastToDieFailure()
    {
        if (_world.LocalPlayer.IsAlive)
        {
            _world.ForceKillLocalPlayer();
            TriggerLastToDieDeathFocusFailure();
            return;
        }

        TriggerLastToDieDeathFocusFailure();
    }

    private void UpdateLastToDieFailureOverlay(KeyboardState keyboard, MouseState mouse)
    {
        _ = mouse;
        if (!_lastToDieFailureOverlayOpen)
        {
            return;
        }

        _lastToDieFailureOverlayTicks += 1;
        var readyForContinue = _lastToDieFailureOverlayTicks >= LastToDieFailureContinueDelayTicks;
        if (!readyForContinue)
        {
            return;
        }

        if (IsKeyPressed(keyboard, Keys.Enter))
        {
            ReturnToLastToDieMenu();
        }
    }

    private void DrawLastToDieFailureOverlay()
    {
        if (!_lastToDieFailureOverlayOpen)
        {
            return;
        }

        var viewportWidth = ViewportWidth;
        var viewportHeight = ViewportHeight;
        var alpha = Math.Clamp(_lastToDieFailureOverlayTicks / (float)LastToDieFailureFadeTicks, 0f, 1f);
        var centerFadeAlpha = MathF.Min(0.9f, alpha * 1.1f);
        _spriteBatch.Draw(_pixel, new Rectangle(0, 0, viewportWidth, viewportHeight), Color.Black * centerFadeAlpha);

        var edgeAlpha = MathF.Min(0.56f, alpha * 0.7f);
        _spriteBatch.Draw(_pixel, new Rectangle(0, 0, viewportWidth, 96), new Color(120, 0, 0) * edgeAlpha);
        _spriteBatch.Draw(_pixel, new Rectangle(0, viewportHeight - 96, viewportWidth, 96), new Color(120, 0, 0) * edgeAlpha);
        _spriteBatch.Draw(_pixel, new Rectangle(0, 0, 96, viewportHeight), new Color(120, 0, 0) * edgeAlpha);
        _spriteBatch.Draw(_pixel, new Rectangle(viewportWidth - 96, 0, 96, viewportHeight), new Color(120, 0, 0) * edgeAlpha);

        if (_lastToDieDeathFocus is not null && alpha >= 0.25f)
        {
            DrawLastToDieFailureCorpse(viewportWidth, viewportHeight, alpha);
        }

        if (_lastToDieRun is null || alpha < 0.2f)
        {
            return;
        }

        DrawHudTextCentered(
            "YOU FAILED YOUR TEAM",
            new Vector2(viewportWidth / 2f, viewportHeight * 0.18f),
            new Color(230, 214, 214) * alpha,
            3f);

        DrawHudTextCentered(
            $"Kills: {_lastToDieRun.TotalKills}",
            new Vector2(viewportWidth / 2f, viewportHeight * 0.74f),
            new Color(220, 214, 214) * alpha,
            1.15f);
        DrawHudTextCentered(
            $"Damage: {_lastToDieRun.TotalDamageDealt}",
            new Vector2(viewportWidth / 2f, viewportHeight * 0.80f),
            new Color(220, 214, 214) * alpha,
            1.15f);
        DrawHudTextCentered(
            $"Matches clutched: {_lastToDieRun.LevelsCompleted}",
            new Vector2(viewportWidth / 2f, viewportHeight * 0.86f),
            new Color(220, 214, 214) * alpha,
            1.15f);

        if (_lastToDieFailureOverlayTicks >= LastToDieFailureContinueDelayTicks)
        {
            DrawHudTextCentered(
                "Press Enter to continue.",
                new Vector2(viewportWidth / 2f, viewportHeight * 0.93f),
                new Color(214, 214, 214) * alpha,
                1f);
        }
    }

    private void UpdateLastToDieRunStats()
    {
        if (!IsLastToDieSessionActive || _lastToDieRun is null || _world.LocalPlayerAwaitingJoin)
        {
            return;
        }

        if (_lastToDieTimerReductionPopupTicksRemaining > 0)
        {
            _lastToDieTimerReductionPopupTicksRemaining -= 1;
            _lastToDieTimerReductionPopupRise += 0.6f;
        }

        var currentKills = Math.Max(0, _world.LocalPlayer.Kills);
        if (currentKills < _lastToDieRun.ObservedStageKills)
        {
            _lastToDieRun.ObservedStageKills = currentKills;
            return;
        }

        if (currentKills > _lastToDieRun.ObservedStageKills)
        {
            var killCountDelta = currentKills - _lastToDieRun.ObservedStageKills;
            _lastToDieRun.TotalKills += killCountDelta;
            _lastToDieRun.ObservedStageKills = currentKills;
            ReduceLastToDieTimerForKills(killCountDelta);
        }
    }

    private void ReduceLastToDieTimerForKills(int killCountDelta)
    {
        if (_lastToDieRun is null || killCountDelta <= 0)
        {
            return;
        }

        var tickReduction = killCountDelta * LastToDieKillTimerReductionSeconds * _config.TicksPerSecond;
        _lastToDieRun.StageRemainingTicks = Math.Max(0, _lastToDieRun.StageRemainingTicks - tickReduction);
        _lastToDieTimerReductionPopupTicksRemaining = Math.Max(_lastToDieTimerReductionPopupTicksRemaining, 42);
        _lastToDieTimerReductionPopupRise = 0f;
        _lastToDieTimerReductionPopupSeconds = killCountDelta * LastToDieKillTimerReductionSeconds;
    }

    private void RegisterLastToDieLocalDamageDealt(int amount)
    {
        if (!IsLastToDieSessionActive || _lastToDieRun is null || amount <= 0)
        {
            return;
        }

        _lastToDieRun.TotalDamageDealt += amount;
    }

    private void DrawLastToDieHud()
    {
        if (!IsLastToDieSessionActive || _lastToDieRun is null)
        {
            return;
        }

        var timerText = FormatHudTimerText(_lastToDieRun.StageRemainingTicks);
        DrawTimerFontTextRightAligned(timerText, new Vector2(ViewportWidth - 18f, 18f), Color.White, 1f);
        if (_lastToDieTimerReductionPopupTicksRemaining > 0)
        {
            var alpha = Math.Clamp(_lastToDieTimerReductionPopupTicksRemaining / 42f, 0f, 1f);
            var popupPosition = new Vector2(ViewportWidth - 88f, 20f - _lastToDieTimerReductionPopupRise);
            var popupText = $"-{_lastToDieTimerReductionPopupSeconds}";
            DrawBitmapFontText(popupText, popupPosition + new Vector2(2f, 2f), Color.Black * alpha, 1f);
            DrawBitmapFontText(popupText, popupPosition, new Color(255, 226, 74) * alpha, 1f);
        }

        var stageLabel = $"Stage {_lastToDieRun.StageNumber}/{LastToDieStageCount}";
        var enemiesLabel = $"{_lastToDieRun.EnemyBotCount} Enemies";
        var stageX = ViewportWidth - MeasureBitmapFontWidth(stageLabel, 0.92f) - 18f;
        var enemiesX = ViewportWidth - MeasureBitmapFontWidth(enemiesLabel, 0.92f) - 18f;
        DrawBitmapFontText(stageLabel, new Vector2(stageX, 44f), new Color(232, 232, 232), 0.92f);
        DrawBitmapFontText(enemiesLabel, new Vector2(enemiesX, 64f), new Color(210, 196, 160), 0.92f);

        if (_lastToDieRun.StageIntroTicksRemaining > 0)
        {
            var introDurationTicks = GetLastToDieStageIntroDurationTicks();
            var introProgress = 1f - (_lastToDieRun.StageIntroTicksRemaining / (float)introDurationTicks);
            var fadeAlpha = introProgress < 0.32f
                ? Math.Clamp(introProgress / 0.32f, 0f, 1f)
                : Math.Clamp(1f - ((introProgress - 0.32f) / 0.68f), 0f, 1f);
            var introColor = new Color(241, 232, 203) * (fadeAlpha * 0.96f);
            DrawHudTextCentered("SURVIVE!", new Vector2(ViewportWidth / 2f, ViewportHeight * 0.2f), introColor, 2.4f);
        }
    }
}
