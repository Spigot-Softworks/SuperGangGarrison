#nullable enable

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using System;
using System.Collections.Generic;
using System.Globalization;
using OpenGarrison.Client.Plugins;
using OpenGarrison.Core;

namespace OpenGarrison.Client;

public partial class Game1
{
    private static OfflineBotControllerMode GetNextBotMode(OfflineBotControllerMode botMode)
    {
        return OfflineBotControllerMode.BotBrain;
    }

    private static MusicMode GetNextMusicMode(MusicMode musicMode)
    {
        return musicMode switch
        {
            MusicMode.None => MusicMode.MenuOnly,
            MusicMode.MenuOnly => MusicMode.InGameOnly,
            MusicMode.InGameOnly => MusicMode.MenuAndInGame,
            _ => MusicMode.None,
        };
    }

    private static string GetFrameRateLimitLabel(int frameRateLimit)
    {
        return frameRateLimit switch
        {
            0 => "Off",
            30 => "30 FPS",
            60 => "60 FPS",
            75 => "75 FPS",
            120 => "120 FPS",
            _ => "Off",
        };
    }

    private void BeginEditingPlayerName()
    {
        _editingPlayerName = true;
        _playerNameEditBuffer = _world.LocalPlayer.DisplayName;
        InitializePlayerNameEditCursor();
    }

    private void ToggleFullscreenSetting()
    {
        if (OperatingSystem.IsBrowser())
        {
            return;
        }

        _clientSettings.Fullscreen = !_clientSettings.Fullscreen;
        ApplyGraphicsSettings();

        if (!OperatingSystem.IsBrowser())
        {
            Window.AllowUserResizing = !_graphics.IsFullScreen;
        }
    }

    private void ResetWindowSize()
    {
        if (_graphics.IsFullScreen || OperatingSystem.IsBrowser())
        {
            return;
        }

        var defaultDimensions = GetPreferredBackBufferDimensions(fullscreen: false, _ingameResolution);
        _graphics.PreferredBackBufferWidth = defaultDimensions.X;
        _graphics.PreferredBackBufferHeight = defaultDimensions.Y;
        _graphics.ApplyChanges();
    }

    private void CycleMusicModeSetting()
    {
        _musicMode = GetNextMusicMode(_musicMode);
        StopMenuMusic();
        StopFaucetMusic();
        StopIngameMusic();
        PersistClientSettings();
    }

    private void CycleBotModeSetting()
    {
        _clientSettings.BotMode = GetNextBotMode(_clientSettings.BotMode);
        PersistClientSettings();
        ApplyConfiguredPracticeBotController(respawnActiveBots: true);
    }

    private void CycleIngameResolutionSetting()
    {
        _clientSettings.IngameResolution = GetNextIngameResolution(_clientSettings.IngameResolution);
        ApplyGraphicsSettings();
    }

    private void CycleFrameRateLimitSetting()
    {
        var current = NormalizeFrameRateLimit(_frameRateLimit);
        var next = current switch
        {
            0 => 30,
            30 => 60,
            60 => 75,
            75 => 120,
            _ => 0,
        };

        _frameRateLimit = next;
        PersistClientSettings();
    }

    private void CycleParticleModeSetting()
    {
        _particleMode = (_particleMode + 2) % 3;
        PersistClientSettings();
    }

    private void CycleFlameRenderModeSetting()
    {
        _flameRenderMode = (_flameRenderMode + 1) % 2;
        PersistClientSettings();
    }

    private void CycleMenuBackgroundModeSetting()
    {
        _menuBackgroundMode = _menuBackgroundMode switch
        {
            MenuBackgroundMode.Static => MenuBackgroundMode.DefaultMaps,
            MenuBackgroundMode.DefaultMaps => MenuBackgroundMode.AllMaps,
            MenuBackgroundMode.AllMaps => MenuBackgroundMode.Static,
            _ => MenuBackgroundMode.Static
        };

        // Initialize or reset the animated background controller based on new mode
        if (_menuBackgroundMode != MenuBackgroundMode.Static && _mainMenuOpen)
        {
            _animatedMenuBackgroundController.Initialize(_menuBackgroundMode);
        }
        else if (_menuBackgroundMode == MenuBackgroundMode.Static)
        {
            _animatedMenuBackgroundController.Reset();
        }

        PersistClientSettings();
    }

    private void CycleGibLevelSetting()
    {
        _gibLevel = _gibLevel switch
        {
            0 => 1,
            1 => 2,
            2 => 3,
            _ => 0,
        };
        PersistClientSettings();
    }

    private void CycleCorpseDurationSetting()
    {
        _corpseDurationMode = _corpseDurationMode == ClientSettings.CorpseDurationInfinite
            ? ClientSettings.CorpseDurationDefault
            : ClientSettings.CorpseDurationInfinite;
        if (_corpseDurationMode != ClientSettings.CorpseDurationInfinite)
        {
            ResetRetainedDeadBodies();
        }

        PersistClientSettings();
    }

    private void ToggleHealerRadarSetting()
    {
        _healerRadarEnabled = !_healerRadarEnabled;
        PersistClientSettings();
    }

    private void ToggleShowHealerSetting()
    {
        _showHealerEnabled = !_showHealerEnabled;
        PersistClientSettings();
    }

    private void ToggleShowHealingSetting()
    {
        _showHealingEnabled = !_showHealingEnabled;
        PersistClientSettings();
    }

    private void ToggleShowHealthBarSetting()
    {
        _showHealthBarEnabled = !_showHealthBarEnabled;
        PersistClientSettings();
    }

    private void TogglePersistentSelfNameSetting()
    {
        _showPersistentSelfNameEnabled = !_showPersistentSelfNameEnabled;
        PersistClientSettings();
    }

    private void ToggleSpriteDropShadowSetting()
    {
        _spriteDropShadowEnabled = !_spriteDropShadowEnabled;
        PersistClientSettings();
    }

    private void ToggleAudioMuteSetting()
    {
        _audioMuted = !_audioMuted;
        ApplyAudioVolumeState();
        PersistClientSettings();
    }

    private void AdjustMenuMusicVolume(int deltaPercent)
    {
        _menuMusicVolumePercent = Math.Clamp(_menuMusicVolumePercent + deltaPercent, 0, 100);
        ApplyAudioVolumeState();
        PersistClientSettings();
    }

    private void AdjustIngameMusicVolume(int deltaPercent)
    {
        _ingameMusicVolumePercent = Math.Clamp(_ingameMusicVolumePercent + deltaPercent, 0, 100);
        ApplyAudioVolumeState();
        PersistClientSettings();
    }

    private void AdjustSoundEffectsVolume(int deltaPercent)
    {
        _soundEffectsVolumePercent = Math.Clamp(_soundEffectsVolumePercent + deltaPercent, 0, 100);
        PersistClientSettings();
    }

    private void ToggleUberOutlinesSetting()
    {
        _uberOutlineEnabled = !_uberOutlineEnabled;
        PersistClientSettings();
    }

    private void ToggleProjectileTeamTintSetting()
    {
        _projectileTeamTintEnabled = !_projectileTeamTintEnabled;
        PersistClientSettings();
    }

    private void ToggleKillCamSetting()
    {
        _killCamEnabled = !_killCamEnabled;
        PersistClientSettings();
    }

    private void ToggleVSyncSetting()
    {
        _clientSettings.VSync = !_clientSettings.VSync;
        ApplyGraphicsSettings();
    }

    private void GetOptionsMenuLayout(int rowCount, out float xbegin, out float ybegin, out float spacing, out float width, out float valueX)
    {
        xbegin = 40f;
        valueX = 240f;
        width = 320f;
        if (ViewportHeight < 540)
        {
            spacing = 26f;
            width = 340f;
            valueX = 232f;
        }
        else
        {
            spacing = 30f;
        }

        var compactLayout = ViewportHeight < 540;
        var defaultY = compactLayout ? 104f : 170f;
        var minY = compactLayout ? 24f : 40f;
        var bottomPadding = compactLayout ? 18f : 40f;
        var estimatedTextHeight = compactLayout ? 18f : 22f;
        var totalHeight = Math.Max(0, rowCount - 1) * spacing + estimatedTextHeight;
        ybegin = MathF.Min(defaultY, MathF.Max(minY, ViewportHeight - bottomPadding - totalHeight));
    }

    private void DrawMenuPanelBackdrop(Rectangle rectangle, float alpha)
    {
        if (rectangle.Width <= 0 || rectangle.Height <= 0)
        {
            return;
        }

        DrawInsetHudPanel(
            rectangle,
            new Color(184, 178, 160) * (alpha * 0.35f),
            new Color(24, 27, 32) * (alpha * 0.85f));
    }

    private void DrawMenuPlaqueRows(Vector2 position, int rowCount, float spacing, float width, float alpha)
    {
        for (var index = 0; index < rowCount; index += 1)
        {
            DrawMenuPlaque(position.X - 6f, position.Y + (index * spacing) - 4f, width, alpha);
        }
    }

    private void DrawMenuPlaque(float x, float y, float width, float alpha)
    {
        if (width <= 0f)
        {
            return;
        }

        var tint = Color.White * alpha;
        const float plaqueSpriteWidth = 17f;
        if (!TryDrawScreenSprite("gbMenuLayoutS", 0, new Vector2(x, y), tint, Vector2.One))
        {
            _spriteBatch.Draw(
                _pixel,
                new Rectangle((int)MathF.Round(x), (int)MathF.Round(y), Math.Max(1, (int)MathF.Round(width)), 17),
                new Color(70, 74, 82) * (alpha * 0.55f));
            return;
        }

        var middleScaleX = Math.Max(1f, (width / (plaqueSpriteWidth - 1f)) - 2f);
        TryDrawScreenSprite("gbMenuLayoutS", 1, new Vector2(x + plaqueSpriteWidth, y), tint, new Vector2(middleScaleX, 1f));
        TryDrawScreenSprite("gbMenuLayoutS", 2, new Vector2(x - plaqueSpriteWidth + width + 1f, y), tint, Vector2.One);
    }
}
