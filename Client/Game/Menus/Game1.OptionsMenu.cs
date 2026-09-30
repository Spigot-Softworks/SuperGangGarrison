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
    public const string DevelopmentVersionLabel = "dev";

    public static string? _cachedApplicationVersionLabel;

    public static MusicMode GetNextMusicMode(MusicMode musicMode)
    {
        return musicMode switch
        {
            MusicMode.None => MusicMode.MenuOnly,
            MusicMode.MenuOnly => MusicMode.InGameOnly,
            MusicMode.InGameOnly => MusicMode.MenuAndInGame,
            _ => MusicMode.None,
        };
    }

    public static string GetFrameRateLimitLabel(int frameRateLimit)
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

    public static float NormalizeSmoothCameraMultiplier(float multiplier)
    {
        if (float.IsNaN(multiplier) || float.IsInfinity(multiplier))
        {
            return ClientSettings.DefaultSmoothCameraMultiplier;
        }

        return Math.Clamp(multiplier, 0f, 1f);
    }


    public static string GetPlayerCardSizeLabel(int sizeMode)
    {
        return ClientSettings.NormalizePlayerCardSizeMode(sizeMode) switch
        {
            ClientSettings.PlayerCardSizeMedium => "Medium",
            ClientSettings.PlayerCardSizeLarge => "Large",
            _ => "Small",
        };
    }

    public static string GetCursorSizeLabel(int cursorSizePercent)
    {
        return $"{ClientSettings.NormalizeCursorSizePercent(cursorSizePercent)}%";
    }

    public static string GetLowHealthColorModeLabel(LowHealthColorMode mode)
    {
        return ClientSettings.NormalizeLowHealthColorMode(mode) switch
        {
            LowHealthColorMode.None => "None",
            _ => "Red",
        };
    }

    public static string GetHudWeaponDisplayModeLabel(bool showOnlyActiveWeapon)
    {
        return showOnlyActiveWeapon
            ? "Show Only Active Weapon"
            : "Show All Weapons";
    }

    public static string GetBuildMenuStyleLabel(BuildMenuStyle style)
    {
        return OpenGarrisonPreferencesDocument.NormalizeBuildMenuStyle(style) == BuildMenuStyle.Wheel
            ? "Wheel"
            : "List";
    }

    public static string GetDamageVignetteIntensityLabel(int percent)
    {
        return $"{ClientSettings.NormalizeDamageVignetteIntensityPercent(percent)}%";
    }

    public static string GetControllerInputModeLabel(ControllerInputMode mode)
    {
        return OpenGarrisonPreferencesDocument.NormalizeControllerInputMode(mode) switch
        {
            ControllerInputMode.Off => "Off",
            ControllerInputMode.On => "On",
            _ => "Auto",
        };
    }

    public static string GetControllerReticleModeLabel(ControllerReticleMode mode)
    {
        return OpenGarrisonPreferencesDocument.NormalizeControllerReticleMode(mode) switch
        {
            ControllerReticleMode.AimLine => "Aim Line",
            _ => "Cursor",
        };
    }

    public static string GetControllerPercentLabel(float value)
    {
        return $"{MathF.Round(value * 100f)}%";
    }

    public static string GetControllerPixelsLabel(float value)
    {
        return $"{MathF.Round(value)} px";
    }

    public static string GetControllerSpeedLabel(float value)
    {
        return $"{MathF.Round(value)} px/s";
    }

    public static readonly ControllerButtonBinding[] ControllerButtonBindingCycle =
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

    public static string GetControllerButtonBindingLabel(ControllerButtonBinding binding)
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

    public static string GetApplicationVersionLabel()
    {
        if (OperatingSystem.IsBrowser() && IsRestrictedBrowserEdition)
        {
            return "v0.8.4";
        }

        return _cachedApplicationVersionLabel ??= FormatApplicationVersionDisplayLabel(LoadApplicationVersionLabel());
    }

    public static string LoadApplicationVersionLabel()
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

    public static IEnumerable<string> EnumerateApplicationVersionCandidates()
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

    public static IEnumerable<string> EnumerateBrowserApplicationVersionCandidates()
    {
        foreach (var relativePath in new[] { ApplicationBuildInfo.VersionFileName, $"Content/{ApplicationBuildInfo.VersionFileName}" })
        {
            if (BrowserContentCatalog.TryGetText(relativePath, out var version))
            {
                yield return version;
            }
        }
    }

    public static IEnumerable<string> EnumerateApplicationVersionFilePaths()
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
                yield return Path.Combine(current, ApplicationBuildInfo.VersionFileName);
                current = Directory.GetParent(current)?.FullName ?? string.Empty;
            }
        }
    }

    public static void AddVersionProbeDirectory(List<string> directories, string? directory)
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

    public static bool TryReadVersionFile(string path, out string version)
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

    public static bool TryNormalizeApplicationVersionLabel(string? rawVersion, out string version)
    {
        version = rawVersion?.Trim() ?? string.Empty;
        return !string.IsNullOrWhiteSpace(version);
    }

    public static string FormatApplicationVersionDisplayLabel(string version)
    {
        return version.Trim();
    }

    public static bool IsDefaultSdkVersionLabel(string version)
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

    public float GetPlayerCardSizeScale()
    {
        return ClientSettings.NormalizePlayerCardSizeMode(_playerCardSizeMode) switch
        {
            ClientSettings.PlayerCardSizeMedium => 0.75f,
            ClientSettings.PlayerCardSizeLarge => 1f,
            _ => 0.55f,
        };
    }

    public void BeginEditingPlayerName()
    {
        _editingPlayerName = true;
        _playerNameEditBuffer = _world.LocalPlayer.DisplayName;
        InitializePlayerNameEditCursor();
    }

    public void ApplyLoadedBubbleWheelPluginSettings()
    {
        _bubbleWheelBehavior = OpenGarrisonPreferencesDocument.NormalizeBubbleWheelBehavior(_clientSettings.BubbleWheelBehavior);
        if (OperatingSystem.IsBrowser())
        {
            return;
        }

        var path = GetBubbleWheelPluginConfigPath();
        var config = BubbleWheelPluginConfig.LoadOrCreate(path, _bubbleWheelBehavior);
        _bubbleWheelBehavior = config.Behavior;
        _bubbleWheelPluginConfigLastWriteUtc = GetFileLastWriteUtcOrDefault(path);
    }

    public BubbleWheelBehavior GetBubbleWheelBehaviorSetting()
    {
        if (OperatingSystem.IsBrowser())
        {
            return OpenGarrisonPreferencesDocument.NormalizeBubbleWheelBehavior(_bubbleWheelBehavior);
        }

        var path = GetBubbleWheelPluginConfigPath();
        var lastWriteUtc = GetFileLastWriteUtcOrDefault(path);
        if (lastWriteUtc != default && lastWriteUtc != _bubbleWheelPluginConfigLastWriteUtc)
        {
            _bubbleWheelBehavior = BubbleWheelPluginConfig.Load(path).Behavior;
            _bubbleWheelPluginConfigLastWriteUtc = GetFileLastWriteUtcOrDefault(path);
        }

        return OpenGarrisonPreferencesDocument.NormalizeBubbleWheelBehavior(_bubbleWheelBehavior);
    }

    public static string GetBubbleWheelPluginConfigPath()
    {
        return Path.Combine(RuntimePaths.ConfigDirectory, "plugins", "client", "bubblewheel", BubbleWheelPluginConfig.DefaultFileName);
    }

    public static DateTime GetFileLastWriteUtcOrDefault(string path)
    {
        try
        {
            return File.Exists(path) ? File.GetLastWriteTimeUtc(path) : default;
        }
        catch (IOException)
        {
            return default;
        }
        catch (UnauthorizedAccessException)
        {
            return default;
        }
    }

    public void CycleDisplayModeSetting()
    {
        if (OperatingSystem.IsBrowser())
        {
            return;
        }

        _clientSettings.DisplayMode = GetNextDisplayMode(_clientSettings.DisplayMode);
        ApplyGraphicsSettings();
    }

    public void ToggleFullscreenHotkey()
    {
        if (OperatingSystem.IsBrowser())
        {
            return;
        }

        var currentMode = OpenGarrisonPreferencesDocument.NormalizeDisplayMode(_displayMode);
        _clientSettings.DisplayMode = currentMode == DisplayModeKind.Fullscreen
            ? DisplayModeKind.Windowed
            : DisplayModeKind.Fullscreen;
        ApplyGraphicsSettings();
    }

    public void ResetWindowSize()
    {
        if (IsScreenFillingDisplayMode(_displayMode) || OperatingSystem.IsBrowser())
        {
            return;
        }

        var defaultDimensions = GetWindowDimensions(DisplayModeKind.Windowed, _ingameResolution, _windowSize);
        _graphics.PreferredBackBufferWidth = defaultDimensions.X;
        _graphics.PreferredBackBufferHeight = defaultDimensions.Y;
        _graphics.ApplyChanges();
    }

    public void CycleMusicModeSetting()
    {
        _musicMode = GetNextMusicMode(_musicMode);
        StopMenuMusic();
        StopFaucetMusic();
        StopIngameMusic();
        ResetDynamicMusicPlayback();
        PersistClientSettings();
    }

    public void ToggleDynamicMusicSetting()
    {
        _dynamicMusicEnabled = !_dynamicMusicEnabled;
        if (!_dynamicMusicEnabled)
        {
            ResetDynamicMusicPlayback();
        }

        ApplyAudioVolumeState();
        PersistClientSettings();
    }


    public void TogglePositionSmoothingSetting()
    {
        _positionSmoothingEnabled = !_positionSmoothingEnabled;
        PersistClientSettings();
    }

    public void ToggleCameraPanningSetting()
    {
        _cameraPanningEnabled = !_cameraPanningEnabled;
        ResetCameraPanningState();
        ResetSmoothCameraState();
        PersistClientSettings();
    }

    public void TogglePredictionSetting()
    {
        _enablePrediction = !_enablePrediction;
        ResetLocalPredictionForAuthorityTransition();
        PersistClientSettings();
    }

    public void CycleSwapWeaponsBindingSetting()
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

    public void ToggleScrollWheelWeaponSwapSetting()
    {
        _inputBindings.ScrollWheelWeaponSwapEnabled = !_inputBindings.ScrollWheelWeaponSwapEnabled;
        PersistInputBindings();
    }

    public void CycleBuildMenuStyleSetting()
    {
        _clientSettings.BuildMenuStyle = OpenGarrisonPreferencesDocument.NormalizeBuildMenuStyle(_clientSettings.BuildMenuStyle) == BuildMenuStyle.List
            ? BuildMenuStyle.Wheel
            : BuildMenuStyle.List;
        BeginClosingBuildMenu();
        PersistClientSettings();
    }

    public void CycleControllerInputModeSetting()
    {
        _clientSettings.ControllerInputMode = OpenGarrisonPreferencesDocument.NormalizeControllerInputMode(_clientSettings.ControllerInputMode) switch
        {
            ControllerInputMode.Auto => ControllerInputMode.On,
            ControllerInputMode.On => ControllerInputMode.Off,
            _ => ControllerInputMode.Auto,
        };
        PersistClientSettings();
    }

    public void CycleControllerReticleModeSetting()
    {
        _clientSettings.ControllerReticleMode = OpenGarrisonPreferencesDocument.NormalizeControllerReticleMode(_clientSettings.ControllerReticleMode) switch
        {
            ControllerReticleMode.Cursor => ControllerReticleMode.AimLine,
            _ => ControllerReticleMode.Cursor,
        };
        PersistClientSettings();
    }

    public void ToggleControllerAimAssistSetting()
    {
        _clientSettings.ControllerAimAssistEnabled = !_clientSettings.ControllerAimAssistEnabled;
        PersistClientSettings();
    }

    public void ToggleControllerFlickToChangeDirectionsSetting()
    {
        _clientSettings.ControllerFlickToChangeDirections = !_clientSettings.ControllerFlickToChangeDirections;
        PersistClientSettings();
    }

    public void CycleControllerAimAssistStrengthSetting()
    {
        AdjustControllerAimAssistStrengthSetting(0.1f);
    }

    public void AdjustControllerAimAssistStrengthSetting(float delta)
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

    public void CycleControllerAimDeadzoneSetting()
    {
        AdjustControllerAimDeadzoneSetting(0.05f);
    }

    public void AdjustControllerAimDeadzoneSetting(float delta)
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

    public void CycleControllerScopedPrecisionSpeedSetting()
    {
        AdjustControllerScopedPrecisionSpeedSetting(30f);
    }

    public void AdjustControllerScopedPrecisionSpeedSetting(float delta)
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

    public void CycleControllerAimDistanceTier1Setting()
    {
        AdjustControllerAimDistanceTier1Setting(16f);
    }

    public void AdjustControllerAimDistanceTier1Setting(float delta)
    {
        _clientSettings.ControllerAimDistanceTier1 = AdjustControllerAimDistance(
            _clientSettings.ControllerAimDistanceTier1,
            OpenGarrisonPreferencesDocument.DefaultControllerAimDistanceTier1,
            delta);
        PersistClientSettings();
    }

    public void CycleControllerAimDistanceTier2Setting()
    {
        AdjustControllerAimDistanceTier2Setting(16f);
    }

    public void AdjustControllerAimDistanceTier2Setting(float delta)
    {
        _clientSettings.ControllerAimDistanceTier2 = AdjustControllerAimDistance(
            _clientSettings.ControllerAimDistanceTier2,
            OpenGarrisonPreferencesDocument.DefaultControllerAimDistanceTier2,
            delta);
        PersistClientSettings();
    }

    public void CycleControllerAimDistanceTier3Setting()
    {
        AdjustControllerAimDistanceTier3Setting(16f);
    }

    public void AdjustControllerAimDistanceTier3Setting(float delta)
    {
        _clientSettings.ControllerAimDistanceTier3 = AdjustControllerAimDistance(
            _clientSettings.ControllerAimDistanceTier3,
            OpenGarrisonPreferencesDocument.DefaultControllerAimDistanceTier3,
            delta);
        PersistClientSettings();
    }

    public static float AdjustControllerAimDistance(float current, float fallback, float delta)
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

    public string GetSwapWeaponsBindingLabel()
    {
        return InputBindingsSettings.NormalizeSwapWeaponsBinding(_inputBindings.SwapWeaponsBinding) switch
        {
            WeaponSwapBindingMode.Space => "Space",
            WeaponSwapBindingMode.MouseSecondary => "M2",
            WeaponSwapBindingMode.Q => "Q",
            WeaponSwapBindingMode.Custom => $"Custom ({GetBindingDisplayName(_inputBindings.SwapWeaponsCustomKey)})",
            _ => "Q",
        };
    }

    public void CycleIngameResolutionSetting()
    {
        _clientSettings.IngameResolution = GetNextIngameResolution(_clientSettings.IngameResolution);
        ApplyGraphicsSettings();
    }

    public void CycleWindowSizeSetting()
    {
        if (OperatingSystem.IsBrowser())
        {
            return;
        }

        _clientSettings.WindowSize = GetNextWindowSize(_clientSettings.WindowSize);
        ApplyGraphicsSettings();
    }


    public void CycleFrameRateLimitSetting()
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

    public void CycleParticleModeSetting()
    {
        _particleMode = (_particleMode + 2) % 3;
        PersistClientSettings();
    }


    public void CyclePlayerCardSizeSetting()
    {
        _playerCardSizeMode = ClientSettings.NormalizePlayerCardSizeMode(_playerCardSizeMode) switch
        {
            ClientSettings.PlayerCardSizeSmall => ClientSettings.PlayerCardSizeMedium,
            ClientSettings.PlayerCardSizeMedium => ClientSettings.PlayerCardSizeLarge,
            _ => ClientSettings.PlayerCardSizeSmall,
        };

        PersistClientSettings();
    }

    public void CycleCursorSizeSetting()
    {
        var current = ClientSettings.NormalizeCursorSizePercent(_cursorSizePercent);
        _cursorSizePercent = current >= ClientSettings.CursorSizeMaxPercent
            ? ClientSettings.CursorSizeMinPercent
            : current + ClientSettings.CursorSizeStepPercent;

        PersistClientSettings();
    }

    public void AdjustCursorSizeSetting(int step)
    {
        var current = ClientSettings.NormalizeCursorSizePercent(_cursorSizePercent);
        _cursorSizePercent = Math.Clamp(
            current + (step * ClientSettings.CursorSizeStepPercent),
            ClientSettings.CursorSizeMinPercent,
            ClientSettings.CursorSizeMaxPercent);

        PersistClientSettings();
    }

    public void CycleLowHealthColorModeSetting()
    {
        _lowHealthColorMode = ClientSettings.NormalizeLowHealthColorMode(_lowHealthColorMode) switch
        {
            LowHealthColorMode.Red => LowHealthColorMode.None,
            _ => LowHealthColorMode.Red,
        };

        PersistClientSettings();
    }

    public void CycleDamageVignetteIntensitySetting()
    {
        var current = ClientSettings.NormalizeDamageVignetteIntensityPercent(_damageVignetteIntensityPercent);
        _damageVignetteIntensityPercent = current <= 0
            ? ClientSettings.DefaultDamageVignetteIntensityPercent
            : current - 10;
        PersistClientSettings();
    }

    public void CycleFlameRenderModeSetting()
    {
        _flameRenderMode = (_flameRenderMode + 1) % 2;
        PersistClientSettings();
    }

    public void CycleBloodRenderModeSetting()
    {
        _bloodRenderMode = (_bloodRenderMode + 1) % 2;
        if (_bloodRenderMode != 0)
        {
            _gameplayManager.GoreEffects.ResetBloodSquibEffects();
        }

        PersistClientSettings();
    }

    public void ToggleDynamicRagdollSetting()
    {
        _dynamicRagdollEnabled = !_dynamicRagdollEnabled;
        if (!_dynamicRagdollEnabled)
        {
            ResetDynamicRagdollEffects();
        }

        PersistClientSettings();
    }

    public void ToggleBurnCharredCorpsesSetting()
    {
        _burnCharredCorpsesEnabled = !_burnCharredCorpsesEnabled;
        if (!_burnCharredCorpsesEnabled)
        {
            ResetBurnCharredCorpses();
        }

        PersistClientSettings();
    }

    public void AdjustBloodPersistenceSeconds(int step)
    {
        _bloodPersistenceSeconds = Math.Clamp(_bloodPersistenceSeconds + step, 1, 120);
        ApplyBloodPresentationSettingsToWorld();
        PersistClientSettings();
    }

    public void CycleCorpseFadeModeSetting()
    {
        _corpseFadeMode = (_corpseFadeMode + 1) % 2;
        PersistClientSettings();
    }

    public void CycleMenuBackgroundModeSetting()
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
            _menuManager.AnimatedMenuBackground.Initialize(_menuBackgroundMode);
        }
        else if (_menuBackgroundMode == MenuBackgroundMode.Static)
        {
            _menuManager.AnimatedMenuBackground.Reset();
        }

        PersistClientSettings();
    }

    public void CycleGibLevelSetting()
    {
        _gibLevel = _gibLevel switch
        {
            0 => 1,
            1 => 2,
            2 => 3,
            _ => 0,
        };
        if (!AreBloodVisualsEnabled)
        {
            _gameplayManager.GoreEffects.ResetBloodSquibEffects();
        }

        PersistClientSettings();
    }

    public void CycleBloodAmountSetting()
    {
        _bloodAmountLevel = _bloodAmountLevel >= 5 ? 1 : _bloodAmountLevel + 1;
        PersistClientSettings();
    }

    public void CycleCorpseDurationSetting()
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

    public void ToggleHealerRadarSetting()
    {
        _healerRadarEnabled = !_healerRadarEnabled;
        PersistClientSettings();
    }

    public void ToggleShowHealerSetting()
    {
        _showHealerEnabled = !_showHealerEnabled;
        PersistClientSettings();
    }

    public void ToggleShowHealingSetting()
    {
        _showHealingEnabled = !_showHealingEnabled;
        PersistClientSettings();
    }

    public void ToggleShowHealthBarSetting()
    {
        _showHealthBarEnabled = !_showHealthBarEnabled;
        PersistClientSettings();
    }

    public void ToggleShowShieldBarSetting()
    {
        _showShieldBarEnabled = !_showShieldBarEnabled;
        PersistClientSettings();
    }

    public void ToggleHudWeaponDisplayModeSetting()
    {
        _hudShowOnlyActiveWeapon = !_hudShowOnlyActiveWeapon;
        PersistClientSettings();
    }

    public void ToggleOverheadChatSetting()
    {
        _overheadChatEnabled = !_overheadChatEnabled;
        if (!_overheadChatEnabled)
        {
            _localOverheadChatMessage = null;
            _overheadChatMessagesBySlot.Clear();
        }

        PersistClientSettings();
    }

    public void TogglePortraitRumbleSetting()
    {
        _portraitRumbleEnabled = !_portraitRumbleEnabled;
        if (!_portraitRumbleEnabled)
        {
            _portraitRumbleRemainingSeconds = 0f;
            _portraitRumbleIntensity = 0f;
        }

        PersistClientSettings();
    }

    public void TogglePostGameMvpArtSetting()
    {
        _postGameMvpArtEnabled = !_postGameMvpArtEnabled;
        _postGameMvpArtHidden = false;
        if (!_postGameMvpArtEnabled)
        {
            _postGameMvpArtFrameSelections.Clear();
        }

        PersistClientSettings();
    }

    public void ToggleDamageVignetteSetting()
    {
        _damageVignetteEnabled = !_damageVignetteEnabled;
        if (!_damageVignetteEnabled)
        {
            _damageVignetteIntensity = 0f;
            _damageVignetteFlashIntensity = 0f;
        }

        PersistClientSettings();
    }

    public void TogglePersistentSelfNameSetting()
    {
        _showPersistentSelfNameEnabled = !_showPersistentSelfNameEnabled;
        PersistClientSettings();
    }

    public void ToggleShowPlayerNamesSetting()
    {
        _showPlayerNamesEnabled = !_showPlayerNamesEnabled;
        PersistClientSettings();
    }

    public void ToggleSpriteDropShadowSetting()
    {
        _spriteDropShadowEnabled = !_spriteDropShadowEnabled;
        PersistClientSettings();
    }

    public void ToggleStuckArrowsSetting()
    {
        _stuckArrowsEnabled = !_stuckArrowsEnabled;
        if (!_stuckArrowsEnabled)
        {
            _stuckArrowVisuals.Clear();
        }

        PersistClientSettings();
    }

    public void CycleWeaponBobSetting()
    {
        _weaponBobMode = OpenGarrisonPreferencesDocument.NormalizeWeaponBobMode(_weaponBobMode) == WeaponBobMode.Enabled
            ? WeaponBobMode.Disabled
            : WeaponBobMode.Enabled;
        PersistClientSettings();
    }

    public void ToggleWeaponRotationStyleSetting()
    {
        _pixelPerfectWeaponRotation = !_pixelPerfectWeaponRotation;
        PersistClientSettings();
    }

    public void ToggleWeaponRotationSourceSetting()
    {
        _useLocalWeaponRotation = !_useLocalWeaponRotation;
        PersistClientSettings();
    }

    public void ToggleAudioMuteSetting()
    {
        _audioMuted = !_audioMuted;
        ApplyAudioVolumeState();
        PersistClientSettings();
    }

    public void AdjustMasterVolume(int deltaPercent)
    {
        _masterVolumePercent = Math.Clamp(_masterVolumePercent + deltaPercent, 0, 100);
        ApplyAudioVolumeState();
        PersistClientSettings();
    }

    public void AdjustMenuMusicVolume(int deltaPercent)
    {
        _menuMusicVolumePercent = Math.Clamp(_menuMusicVolumePercent + deltaPercent, 0, 100);
        ApplyAudioVolumeState();
        PersistClientSettings();
    }

    public void AdjustIngameMusicVolume(int deltaPercent)
    {
        _ingameMusicVolumePercent = Math.Clamp(_ingameMusicVolumePercent + deltaPercent, 0, 100);
        ApplyAudioVolumeState();
        PersistClientSettings();
    }

    public void AdjustCombatMusicVolume(int deltaPercent)
    {
        _combatMusicVolumePercent = Math.Clamp(
            _combatMusicVolumePercent + deltaPercent,
            0,
            OpenGarrisonPreferencesDocument.MaxCombatMusicVolumePercent);
        ApplyAudioVolumeState();
        PersistClientSettings();
    }

    public void AdjustSoundEffectsVolume(int deltaPercent)
    {
        _soundEffectsVolumePercent = Math.Clamp(_soundEffectsVolumePercent + deltaPercent, 0, 100);
        PersistClientSettings();
    }

    public void ToggleUberOutlinesSetting()
    {
        _uberOutlineEnabled = !_uberOutlineEnabled;
        PersistClientSettings();
    }

    public void ToggleProjectileTeamTintSetting()
    {
        _projectileTeamTintEnabled = !_projectileTeamTintEnabled;
        PersistClientSettings();
    }

    public void ToggleKillCamSetting()
    {
        _killCamEnabled = !_killCamEnabled;
        PersistClientSettings();
    }

    public void ToggleVSyncSetting()
    {
        _clientSettings.VSync = !_clientSettings.VSync;
        ApplyGraphicsSettings();
    }


    public void DrawMenuPanelBackdrop(Rectangle rectangle, float alpha)
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


}
