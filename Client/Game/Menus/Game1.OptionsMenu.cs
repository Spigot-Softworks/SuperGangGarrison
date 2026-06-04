#nullable enable

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Reflection;
using OpenGarrison.Client.Plugins;
using OpenGarrison.ClientShared;
using OpenGarrison.Core;

namespace OpenGarrison.Client;

public partial class Game1
{
    private const string ApplicationVersionFileName = "version.txt";
    private const string DevelopmentVersionLabel = "dev";
    private const string AlphaVersionSuffix = " (Alpha)";

    private static string? _cachedApplicationVersionLabel;

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

    private static string GetHudWeaponDisplayModeLabel(bool showOnlyActiveWeapon)
    {
        return showOnlyActiveWeapon
            ? "Show Only Active Weapon"
            : "Show All Weapons";
    }

    private static string GetDamageVignetteIntensityLabel(int percent)
    {
        return $"{ClientSettings.NormalizeDamageVignetteIntensityPercent(percent)}%";
    }

    private static string GetControllerInputModeLabel(ControllerInputMode mode)
    {
        return OpenGarrisonPreferencesDocument.NormalizeControllerInputMode(mode) switch
        {
            ControllerInputMode.Off => "Off",
            ControllerInputMode.On => "On",
            _ => "Auto",
        };
    }

    private static string GetControllerReticleModeLabel(ControllerReticleMode mode)
    {
        return OpenGarrisonPreferencesDocument.NormalizeControllerReticleMode(mode) switch
        {
            ControllerReticleMode.AimLine => "Aim Line",
            _ => "Cursor",
        };
    }

    private static string GetControllerPercentLabel(float value)
    {
        return $"{MathF.Round(value * 100f)}%";
    }

    private static string GetControllerPixelsLabel(float value)
    {
        return $"{MathF.Round(value)} px";
    }

    private static string GetControllerSpeedLabel(float value)
    {
        return $"{MathF.Round(value)} px/s";
    }

    private static readonly ControllerButtonBinding[] ControllerButtonBindingCycle =
    [
        ControllerButtonBinding.None,
        ControllerButtonBinding.A,
        ControllerButtonBinding.B,
        ControllerButtonBinding.X,
        ControllerButtonBinding.Y,
        ControllerButtonBinding.LeftShoulder,
        ControllerButtonBinding.RightShoulder,
        ControllerButtonBinding.LeftTrigger,
        ControllerButtonBinding.RightTrigger,
        ControllerButtonBinding.Back,
        ControllerButtonBinding.Start,
        ControllerButtonBinding.LeftStick,
        ControllerButtonBinding.RightStick,
        ControllerButtonBinding.DPadUp,
        ControllerButtonBinding.DPadDown,
        ControllerButtonBinding.DPadLeft,
        ControllerButtonBinding.DPadRight,
    ];

    private static string GetControllerButtonBindingLabel(ControllerButtonBinding binding)
    {
        return OpenGarrisonPreferencesDocument.NormalizeControllerButtonBinding(binding) switch
        {
            ControllerButtonBinding.None => "Unbound",
            ControllerButtonBinding.A => "Cross / A",
            ControllerButtonBinding.B => "Circle / B",
            ControllerButtonBinding.X => "Square / X",
            ControllerButtonBinding.Y => "Triangle / Y",
            ControllerButtonBinding.LeftShoulder => "L1 / LB",
            ControllerButtonBinding.RightShoulder => "R1 / RB",
            ControllerButtonBinding.LeftTrigger => "L2 / LT",
            ControllerButtonBinding.RightTrigger => "R2 / RT",
            ControllerButtonBinding.Back => "Share / Back",
            ControllerButtonBinding.Start => "Options / Start",
            ControllerButtonBinding.LeftStick => "L3",
            ControllerButtonBinding.RightStick => "R3",
            ControllerButtonBinding.DPadUp => "D-Pad Up",
            ControllerButtonBinding.DPadDown => "D-Pad Down",
            ControllerButtonBinding.DPadLeft => "D-Pad Left",
            ControllerButtonBinding.DPadRight => "D-Pad Right",
            var normalized => normalized.ToString(),
        };
    }

    private static string GetApplicationVersionLabel()
    {
        return _cachedApplicationVersionLabel ??= FormatApplicationVersionDisplayLabel(LoadApplicationVersionLabel());
    }

    private static string LoadApplicationVersionLabel()
    {
        foreach (var candidate in EnumerateApplicationVersionCandidates())
        {
            if (TryNormalizeApplicationVersionLabel(candidate, out var version)
                && !IsDefaultSdkVersionLabel(version))
            {
                return version;
            }
        }

        return DevelopmentVersionLabel;
    }

    private static IEnumerable<string> EnumerateApplicationVersionCandidates()
    {
        if (OperatingSystem.IsBrowser())
        {
            foreach (var version in EnumerateBrowserApplicationVersionCandidates())
            {
                yield return version;
            }
        }
        else
        {
            foreach (var versionPath in EnumerateApplicationVersionFilePaths())
            {
                if (TryReadVersionFile(versionPath, out var version))
                {
                    yield return version;
                }
            }

            var processPath = Environment.ProcessPath;
            if (!string.IsNullOrWhiteSpace(processPath))
            {
                var productVersion = FileVersionInfo.GetVersionInfo(processPath).ProductVersion;
                if (!string.IsNullOrWhiteSpace(productVersion))
                {
                    yield return productVersion;
                }
            }
        }

        var assembly = typeof(Game1).Assembly;
        var informationalVersion = assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion;
        if (!string.IsNullOrWhiteSpace(informationalVersion))
        {
            yield return informationalVersion;
        }

        var assemblyVersion = assembly.GetName().Version?.ToString();
        if (!string.IsNullOrWhiteSpace(assemblyVersion))
        {
            yield return assemblyVersion;
        }
    }

    private static IEnumerable<string> EnumerateBrowserApplicationVersionCandidates()
    {
        foreach (var relativePath in new[] { ApplicationVersionFileName, $"Content/{ApplicationVersionFileName}" })
        {
            if (BrowserContentCatalog.TryGetText(relativePath, out var version))
            {
                yield return version;
            }
        }
    }

    private static IEnumerable<string> EnumerateApplicationVersionFilePaths()
    {
        var directories = new List<string>();
        AddVersionProbeDirectory(directories, AppContext.BaseDirectory);
        AddVersionProbeDirectory(directories, RuntimePaths.ApplicationRoot);
        AddVersionProbeDirectory(directories, Path.GetDirectoryName(Environment.ProcessPath));
        AddVersionProbeDirectory(directories, Directory.GetCurrentDirectory());

        foreach (var directory in directories)
        {
            var current = directory;
            for (var depth = 0; depth < 4 && !string.IsNullOrWhiteSpace(current); depth += 1)
            {
                yield return Path.Combine(current, ApplicationVersionFileName);
                current = Directory.GetParent(current)?.FullName ?? string.Empty;
            }
        }
    }

    private static void AddVersionProbeDirectory(List<string> directories, string? directory)
    {
        if (string.IsNullOrWhiteSpace(directory))
        {
            return;
        }

        try
        {
            var fullPath = Path.GetFullPath(directory);
            if (!directories.Contains(fullPath, StringComparer.OrdinalIgnoreCase))
            {
                directories.Add(fullPath);
            }
        }
        catch (ArgumentException)
        {
        }
        catch (NotSupportedException)
        {
        }
    }

    private static bool TryReadVersionFile(string path, out string version)
    {
        version = string.Empty;
        try
        {
            if (!File.Exists(path))
            {
                return false;
            }

            version = File.ReadAllText(path);
            return !string.IsNullOrWhiteSpace(version);
        }
        catch (IOException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
    }

    private static bool TryNormalizeApplicationVersionLabel(string? rawVersion, out string version)
    {
        version = rawVersion?.Trim() ?? string.Empty;
        return !string.IsNullOrWhiteSpace(version);
    }

    private static string FormatApplicationVersionDisplayLabel(string version)
    {
        var trimmed = version.Trim();
        return trimmed.EndsWith(AlphaVersionSuffix, StringComparison.OrdinalIgnoreCase)
            ? trimmed
            : trimmed + AlphaVersionSuffix;
    }

    private static bool IsDefaultSdkVersionLabel(string version)
    {
        var comparable = version.Trim();
        if (comparable.StartsWith('v') || comparable.StartsWith('V'))
        {
            comparable = comparable[1..];
        }

        comparable = comparable.Split(['-', '+'], 2)[0];
        return string.Equals(comparable, "1.0.0", StringComparison.OrdinalIgnoreCase)
            || string.Equals(comparable, "1.0.0.0", StringComparison.OrdinalIgnoreCase);
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
        ResetDynamicMusicPlayback();
        PersistClientSettings();
    }

    private void ToggleDynamicMusicSetting()
    {
        _dynamicMusicEnabled = !_dynamicMusicEnabled;
        if (!_dynamicMusicEnabled)
        {
            ResetDynamicMusicPlayback();
        }

        ApplyAudioVolumeState();
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

    private void CycleControllerInputModeSetting()
    {
        _clientSettings.ControllerInputMode = OpenGarrisonPreferencesDocument.NormalizeControllerInputMode(_clientSettings.ControllerInputMode) switch
        {
            ControllerInputMode.Auto => ControllerInputMode.On,
            ControllerInputMode.On => ControllerInputMode.Off,
            _ => ControllerInputMode.Auto,
        };
        PersistClientSettings();
    }

    private void CycleControllerReticleModeSetting()
    {
        _clientSettings.ControllerReticleMode = OpenGarrisonPreferencesDocument.NormalizeControllerReticleMode(_clientSettings.ControllerReticleMode) switch
        {
            ControllerReticleMode.Cursor => ControllerReticleMode.AimLine,
            _ => ControllerReticleMode.Cursor,
        };
        PersistClientSettings();
    }

    private void ToggleControllerAimAssistSetting()
    {
        _clientSettings.ControllerAimAssistEnabled = !_clientSettings.ControllerAimAssistEnabled;
        PersistClientSettings();
    }

    private void ToggleControllerFlickToChangeDirectionsSetting()
    {
        _clientSettings.ControllerFlickToChangeDirections = !_clientSettings.ControllerFlickToChangeDirections;
        PersistClientSettings();
    }

    private void CycleControllerAimAssistStrengthSetting()
    {
        AdjustControllerAimAssistStrengthSetting(0.1f);
    }

    private void AdjustControllerAimAssistStrengthSetting(float delta)
    {
        var current = OpenGarrisonPreferencesDocument.NormalizeControllerAimAssistStrength(_clientSettings.ControllerAimAssistStrength);
        var next = current + delta;
        if (next > 1f)
        {
            next = 0f;
        }
        else if (next < 0f)
        {
            next = 1f;
        }

        _clientSettings.ControllerAimAssistStrength = OpenGarrisonPreferencesDocument.NormalizeControllerAimAssistStrength(next);
        PersistClientSettings();
    }

    private void CycleControllerAimDeadzoneSetting()
    {
        AdjustControllerAimDeadzoneSetting(0.05f);
    }

    private void AdjustControllerAimDeadzoneSetting(float delta)
    {
        var current = OpenGarrisonPreferencesDocument.NormalizeControllerAimDeadzone(_clientSettings.ControllerAimDeadzone);
        var next = current + delta;
        if (next > 0.5f)
        {
            next = 0.05f;
        }
        else if (next < 0.05f)
        {
            next = 0.5f;
        }

        _clientSettings.ControllerAimDeadzone = OpenGarrisonPreferencesDocument.NormalizeControllerAimDeadzone(next);
        PersistClientSettings();
    }

    private void CycleControllerScopedPrecisionSpeedSetting()
    {
        AdjustControllerScopedPrecisionSpeedSetting(30f);
    }

    private void AdjustControllerScopedPrecisionSpeedSetting(float delta)
    {
        var current = OpenGarrisonPreferencesDocument.NormalizeControllerScopedPrecisionSpeed(_clientSettings.ControllerScopedPrecisionSpeed);
        var next = current + delta;
        if (next > 420f)
        {
            next = 60f;
        }
        else if (next < 60f)
        {
            next = 420f;
        }

        _clientSettings.ControllerScopedPrecisionSpeed = OpenGarrisonPreferencesDocument.NormalizeControllerScopedPrecisionSpeed(next);
        PersistClientSettings();
    }

    private void CycleControllerAimDistanceTier1Setting()
    {
        AdjustControllerAimDistanceTier1Setting(16f);
    }

    private void AdjustControllerAimDistanceTier1Setting(float delta)
    {
        _clientSettings.ControllerAimDistanceTier1 = AdjustControllerAimDistance(
            _clientSettings.ControllerAimDistanceTier1,
            OpenGarrisonPreferencesDocument.DefaultControllerAimDistanceTier1,
            delta);
        PersistClientSettings();
    }

    private void CycleControllerAimDistanceTier2Setting()
    {
        AdjustControllerAimDistanceTier2Setting(16f);
    }

    private void AdjustControllerAimDistanceTier2Setting(float delta)
    {
        _clientSettings.ControllerAimDistanceTier2 = AdjustControllerAimDistance(
            _clientSettings.ControllerAimDistanceTier2,
            OpenGarrisonPreferencesDocument.DefaultControllerAimDistanceTier2,
            delta);
        PersistClientSettings();
    }

    private void CycleControllerAimDistanceTier3Setting()
    {
        AdjustControllerAimDistanceTier3Setting(16f);
    }

    private void AdjustControllerAimDistanceTier3Setting(float delta)
    {
        _clientSettings.ControllerAimDistanceTier3 = AdjustControllerAimDistance(
            _clientSettings.ControllerAimDistanceTier3,
            OpenGarrisonPreferencesDocument.DefaultControllerAimDistanceTier3,
            delta);
        PersistClientSettings();
    }

    private static float AdjustControllerAimDistance(float current, float fallback, float delta)
    {
        var normalized = OpenGarrisonPreferencesDocument.NormalizeControllerAimDistance(current, fallback);
        var next = normalized + delta;
        if (next > 320f)
        {
            next = 48f;
        }
        else if (next < 48f)
        {
            next = 320f;
        }

        return OpenGarrisonPreferencesDocument.NormalizeControllerAimDistance(next, fallback);
    }

    private void AdjustControllerButtonBindingSetting(
        ControllerButtonBinding current,
        Action<ControllerButtonBinding> apply,
        int step)
    {
        if (step == 0)
        {
            return;
        }

        var normalized = OpenGarrisonPreferencesDocument.NormalizeControllerButtonBinding(current);
        var currentIndex = Array.IndexOf(ControllerButtonBindingCycle, normalized);
        if (currentIndex < 0)
        {
            currentIndex = 0;
        }

        var nextIndex = (currentIndex + step) % ControllerButtonBindingCycle.Length;
        if (nextIndex < 0)
        {
            nextIndex += ControllerButtonBindingCycle.Length;
        }

        apply(ControllerButtonBindingCycle[nextIndex]);
        PersistClientSettings();
    }

    private void AdjustControllerJumpButtonSetting(int step)
    {
        AdjustControllerButtonBindingSetting(_clientSettings.ControllerJumpButton, value => _clientSettings.ControllerJumpButton = value, step);
    }

    private void AdjustControllerPrimaryFireButtonSetting(int step)
    {
        AdjustControllerButtonBindingSetting(_clientSettings.ControllerPrimaryFireButton, value => _clientSettings.ControllerPrimaryFireButton = value, step);
    }

    private void AdjustControllerSecondaryFireButtonSetting(int step)
    {
        AdjustControllerButtonBindingSetting(_clientSettings.ControllerSecondaryFireButton, value => _clientSettings.ControllerSecondaryFireButton = value, step);
    }

    private void AdjustControllerUseAbilityButtonSetting(int step)
    {
        AdjustControllerButtonBindingSetting(_clientSettings.ControllerUseAbilityButton, value => _clientSettings.ControllerUseAbilityButton = value, step);
    }

    private void AdjustControllerInteractButtonSetting(int step)
    {
        AdjustControllerButtonBindingSetting(_clientSettings.ControllerInteractButton, value => _clientSettings.ControllerInteractButton = value, step);
    }

    private void AdjustControllerSwapWeaponButtonSetting(int step)
    {
        AdjustControllerButtonBindingSetting(_clientSettings.ControllerSwapWeaponButton, value => _clientSettings.ControllerSwapWeaponButton = value, step);
    }

    private void AdjustControllerScoreboardButtonSetting(int step)
    {
        AdjustControllerButtonBindingSetting(_clientSettings.ControllerScoreboardButton, value => _clientSettings.ControllerScoreboardButton = value, step);
    }

    private void AdjustControllerPauseButtonSetting(int step)
    {
        AdjustControllerButtonBindingSetting(_clientSettings.ControllerPauseButton, value => _clientSettings.ControllerPauseButton = value, step);
    }

    private void AdjustControllerAimDistanceButtonSetting(int step)
    {
        AdjustControllerButtonBindingSetting(_clientSettings.ControllerAimDistanceButton, value => _clientSettings.ControllerAimDistanceButton = value, step);
    }

    private void AdjustControllerChangeTeamButtonSetting(int step)
    {
        AdjustControllerButtonBindingSetting(_clientSettings.ControllerChangeTeamButton, value => _clientSettings.ControllerChangeTeamButton = value, step);
    }

    private void AdjustControllerChangeClassButtonSetting(int step)
    {
        AdjustControllerButtonBindingSetting(_clientSettings.ControllerChangeClassButton, value => _clientSettings.ControllerChangeClassButton = value, step);
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

    private void ToggleHudWeaponDisplayModeSetting()
    {
        _hudShowOnlyActiveWeapon = !_hudShowOnlyActiveWeapon;
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

    private void TogglePostGameMvpArtSetting()
    {
        _postGameMvpArtEnabled = !_postGameMvpArtEnabled;
        _postGameMvpArtHidden = false;
        if (!_postGameMvpArtEnabled)
        {
            _postGameMvpArtFrameSelections.Clear();
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

    private void ToggleWeaponRotationStyleSetting()
    {
        _pixelPerfectWeaponRotation = !_pixelPerfectWeaponRotation;
        PersistClientSettings();
    }

    private void ToggleWeaponRotationSourceSetting()
    {
        _useLocalWeaponRotation = !_useLocalWeaponRotation;
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

    private void AdjustCombatMusicVolume(int deltaPercent)
    {
        _combatMusicVolumePercent = Math.Clamp(
            _combatMusicVolumePercent + deltaPercent,
            0,
            OpenGarrisonPreferencesDocument.MaxCombatMusicVolumePercent);
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
