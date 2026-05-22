#nullable enable

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using OpenGarrison.Client.Plugins;
using OpenGarrison.ClientShared;
using OpenGarrison.Core;

namespace OpenGarrison.Client;

public partial class Game1
{
    private static OfflineBotControllerMode GetNextBotMode(OfflineBotControllerMode botMode)
    {
        return OfflineBotControllerMode.BotBrain;
    }

    private static string GetBotModeLabel(OfflineBotControllerMode botMode)
    {
        return "BotBrain";
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

    private static float NormalizeSmoothCameraMultiplier(float multiplier)
    {
        if (float.IsNaN(multiplier) || float.IsInfinity(multiplier))
        {
            return ClientSettings.DefaultSmoothCameraMultiplier;
        }

        return Math.Clamp(multiplier, 0f, 1f);
    }

    private static string GetSmoothCameraMultiplierLabel(float multiplier)
    {
        var normalized = NormalizeSmoothCameraMultiplier(multiplier);
        return normalized <= 0.01f
            ? "Off"
            : $"{MathF.Round(normalized * 100f)}%";
    }

    private static string GetPlayerCardSizeLabel(int sizeMode)
    {
        return ClientSettings.NormalizePlayerCardSizeMode(sizeMode) switch
        {
            ClientSettings.PlayerCardSizeMedium => "Medium",
            ClientSettings.PlayerCardSizeLarge => "Large",
            _ => "Small",
        };
    }

    private static string GetLowHealthColorModeLabel(LowHealthColorMode mode)
    {
        return ClientSettings.NormalizeLowHealthColorMode(mode) switch
        {
            LowHealthColorMode.None => "None",
            _ => "Red",
        };
    }

    private static string GetDamageVignetteIntensityLabel(int percent)
    {
        return $"{ClientSettings.NormalizeDamageVignetteIntensityPercent(percent)}%";
    }

    private static string GetApplicationVersionLabel()
    {
        var versionPath = Path.Combine(AppContext.BaseDirectory, "version.txt");
        if (File.Exists(versionPath))
        {
            var version = File.ReadAllText(versionPath).Trim();
            if (!string.IsNullOrWhiteSpace(version))
            {
                return version;
            }
        }

        var informationalVersion = typeof(Game1).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion;
        if (!string.IsNullOrWhiteSpace(informationalVersion))
        {
            return informationalVersion;
        }

        return typeof(Game1).Assembly.GetName().Version?.ToString() ?? "dev";
    }

    private float GetPlayerCardSizeScale()
    {
        return ClientSettings.NormalizePlayerCardSizeMode(_playerCardSizeMode) switch
        {
            ClientSettings.PlayerCardSizeMedium => 0.75f,
            ClientSettings.PlayerCardSizeLarge => 1f,
            _ => 0.55f,
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

    private void CycleSwapWeaponsBindingSetting()
    {
        _inputBindings.SwapWeaponsBinding = InputBindingsSettings.NormalizeSwapWeaponsBinding(_inputBindings.SwapWeaponsBinding) switch
        {
            WeaponSwapBindingMode.Space => WeaponSwapBindingMode.MouseSecondary,
            WeaponSwapBindingMode.MouseSecondary => WeaponSwapBindingMode.Q,
            WeaponSwapBindingMode.Q => WeaponSwapBindingMode.Custom,
            _ => WeaponSwapBindingMode.Space,
        };
        PersistInputBindings();
    }

    private string GetSwapWeaponsBindingLabel()
    {
        return InputBindingsSettings.NormalizeSwapWeaponsBinding(_inputBindings.SwapWeaponsBinding) switch
        {
            WeaponSwapBindingMode.Space => "Space",
            WeaponSwapBindingMode.MouseSecondary => "M2",
            WeaponSwapBindingMode.Q => "Q",
            WeaponSwapBindingMode.Custom => $"Custom ({GetBindingDisplayName(_inputBindings.SwapWeaponsCustomKey)})",
            _ => "Space",
        };
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

    private void CycleSmoothCameraMultiplierSetting()
    {
        var current = NormalizeSmoothCameraMultiplier(_smoothCameraMultiplier);
        _smoothCameraMultiplier = current switch
        {
            <= 0.01f => ClientSettings.DefaultSmoothCameraMultiplier,
            < 0.5f => 0.6f,
            < 0.8f => 0.85f,
            < 0.95f => 1f,
            _ => 0f,
        };
        if (_smoothCameraMultiplier <= 0f)
        {
            _hasSmoothCameraY = false;
            _hasGameplayCameraTopLeft = false;
        }

        PersistClientSettings();
    }

    private void CyclePlayerCardSizeSetting()
    {
        _playerCardSizeMode = ClientSettings.NormalizePlayerCardSizeMode(_playerCardSizeMode) switch
        {
            ClientSettings.PlayerCardSizeSmall => ClientSettings.PlayerCardSizeMedium,
            ClientSettings.PlayerCardSizeMedium => ClientSettings.PlayerCardSizeLarge,
            _ => ClientSettings.PlayerCardSizeSmall,
        };

        PersistClientSettings();
    }

    private void CycleLowHealthColorModeSetting()
    {
        _lowHealthColorMode = ClientSettings.NormalizeLowHealthColorMode(_lowHealthColorMode) switch
        {
            LowHealthColorMode.Red => LowHealthColorMode.None,
            _ => LowHealthColorMode.Red,
        };

        PersistClientSettings();
    }

    private void CycleDamageVignetteIntensitySetting()
    {
        var current = ClientSettings.NormalizeDamageVignetteIntensityPercent(_damageVignetteIntensityPercent);
        _damageVignetteIntensityPercent = current <= 0
            ? ClientSettings.DefaultDamageVignetteIntensityPercent
            : current - 10;
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

    private void ToggleOverheadChatSetting()
    {
        _overheadChatEnabled = !_overheadChatEnabled;
        if (!_overheadChatEnabled)
        {
            _localOverheadChatMessage = null;
            _overheadChatMessagesBySlot.Clear();
        }

        PersistClientSettings();
    }

    private void TogglePortraitRumbleSetting()
    {
        _portraitRumbleEnabled = !_portraitRumbleEnabled;
        if (!_portraitRumbleEnabled)
        {
            _portraitRumbleRemainingSeconds = 0f;
            _portraitRumbleIntensity = 0f;
        }

        PersistClientSettings();
    }

    private void ToggleDamageVignetteSetting()
    {
        _damageVignetteEnabled = !_damageVignetteEnabled;
        if (!_damageVignetteEnabled)
        {
            _damageVignetteIntensity = 0f;
            _damageVignetteFlashIntensity = 0f;
        }

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
