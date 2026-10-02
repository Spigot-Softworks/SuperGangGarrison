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
        return ClientSettings.NormalizePlayerCardSizeMode(_hudManager.RuntimeSettings.PlayerCardSizeMode) switch
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

        var path = RefreshBubbleWheelPluginConfigPathCache();
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

        var path = GetCachedBubbleWheelPluginConfigPath();
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

    private string GetCachedBubbleWheelPluginConfigPath()
    {
        var configuredUserDataRoot = Environment.GetEnvironmentVariable(RuntimePaths.UserDataRootEnvironmentVariable);
        var pathComparison = OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;
        if (_bubbleWheelPluginConfigPath is null
            || !string.Equals(_bubbleWheelPluginConfigPathUserDataRootOverride, configuredUserDataRoot, pathComparison))
        {
            RefreshBubbleWheelPluginConfigPathCache(configuredUserDataRoot);
        }

        return _bubbleWheelPluginConfigPath!;
    }

    private string RefreshBubbleWheelPluginConfigPathCache(string? configuredUserDataRoot = null)
    {
        configuredUserDataRoot ??= Environment.GetEnvironmentVariable(RuntimePaths.UserDataRootEnvironmentVariable);
        _bubbleWheelPluginConfigPath = GetBubbleWheelPluginConfigPath();
        _bubbleWheelPluginConfigPathUserDataRootOverride = configuredUserDataRoot;
        _bubbleWheelPluginConfigLastWriteUtc = default;
        return _bubbleWheelPluginConfigPath;
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

        var currentMode = OpenGarrisonPreferencesDocument.NormalizeDisplayMode(_menuManager.DisplaySettings.DisplayMode);
        _clientSettings.DisplayMode = currentMode == DisplayModeKind.Fullscreen
            ? DisplayModeKind.Windowed
            : DisplayModeKind.Fullscreen;
        ApplyGraphicsSettings();
    }

    public void ResetWindowSize()
    {
        if (IsScreenFillingDisplayMode(_menuManager.DisplaySettings.DisplayMode) || OperatingSystem.IsBrowser())
        {
            return;
        }

        var defaultDimensions = GetWindowDimensions(DisplayModeKind.Windowed, _menuManager.DisplaySettings.IngameResolution, _menuManager.DisplaySettings.WindowSize);
        _graphics.PreferredBackBufferWidth = defaultDimensions.X;
        _graphics.PreferredBackBufferHeight = defaultDimensions.Y;
        _graphics.ApplyChanges();
    }

    public void CycleMusicModeSetting()
    {
        _audioManager.RuntimeSettings.MusicMode = GetNextMusicMode(_audioManager.RuntimeSettings.MusicMode);
        StopMenuMusic();
        StopFaucetMusic();
        StopIngameMusic();
        ResetDynamicMusicPlayback();
        PersistClientSettings();
    }

    public void ToggleDynamicMusicSetting()
    {
        _audioManager.RuntimeSettings.DynamicMusicEnabled = !_audioManager.RuntimeSettings.DynamicMusicEnabled;
        if (!_audioManager.RuntimeSettings.DynamicMusicEnabled)
        {
            ResetDynamicMusicPlayback();
        }

        ApplyAudioVolumeState();
        PersistClientSettings();
    }


    public void TogglePositionSmoothingSetting()
    {
        _gameplayManager.RuntimeSettings.PositionSmoothingEnabled = !_gameplayManager.RuntimeSettings.PositionSmoothingEnabled;
        PersistClientSettings();
    }

    public void ToggleCameraPanningSetting()
    {
        _gameplayManager.RuntimeSettings.CameraPanningEnabled = !_gameplayManager.RuntimeSettings.CameraPanningEnabled;
        ResetCameraPanningState();
        ResetSmoothCameraState();
        PersistClientSettings();
    }

    public void TogglePredictionSetting()
    {
        _gameplayManager.RuntimeSettings.EnablePrediction = !_gameplayManager.RuntimeSettings.EnablePrediction;
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
        var current = NormalizeFrameRateLimit(_menuManager.DisplaySettings.FrameRateLimit);
        var next = current switch
        {
            0 => 30,
            30 => 60,
            60 => 75,
            75 => 120,
            _ => 0,
        };

        _menuManager.DisplaySettings.FrameRateLimit = next;
        PersistClientSettings();
    }

    public void CycleParticleModeSetting()
    {
        _gameplayManager.RuntimeSettings.ParticleMode = (_gameplayManager.RuntimeSettings.ParticleMode + 2) % 3;
        PersistClientSettings();
    }


    public void CyclePlayerCardSizeSetting()
    {
        _hudManager.RuntimeSettings.PlayerCardSizeMode = ClientSettings.NormalizePlayerCardSizeMode(_hudManager.RuntimeSettings.PlayerCardSizeMode) switch
        {
            ClientSettings.PlayerCardSizeSmall => ClientSettings.PlayerCardSizeMedium,
            ClientSettings.PlayerCardSizeMedium => ClientSettings.PlayerCardSizeLarge,
            _ => ClientSettings.PlayerCardSizeSmall,
        };

        PersistClientSettings();
    }

    public void CycleCursorSizeSetting()
    {
        var current = ClientSettings.NormalizeCursorSizePercent(_hudManager.RuntimeSettings.CursorSizePercent);
        _hudManager.RuntimeSettings.CursorSizePercent = current >= ClientSettings.CursorSizeMaxPercent
            ? ClientSettings.CursorSizeMinPercent
            : current + ClientSettings.CursorSizeStepPercent;

        PersistClientSettings();
    }

    public void AdjustCursorSizeSetting(int step)
    {
        var current = ClientSettings.NormalizeCursorSizePercent(_hudManager.RuntimeSettings.CursorSizePercent);
        _hudManager.RuntimeSettings.CursorSizePercent = Math.Clamp(
            current + (step * ClientSettings.CursorSizeStepPercent),
            ClientSettings.CursorSizeMinPercent,
            ClientSettings.CursorSizeMaxPercent);

        PersistClientSettings();
    }

    public void CycleLowHealthColorModeSetting()
    {
        _hudManager.RuntimeSettings.LowHealthColorMode = ClientSettings.NormalizeLowHealthColorMode(_hudManager.RuntimeSettings.LowHealthColorMode) switch
        {
            LowHealthColorMode.Red => LowHealthColorMode.None,
            _ => LowHealthColorMode.Red,
        };

        PersistClientSettings();
    }

    public void CycleDamageVignetteIntensitySetting()
    {
        var current = ClientSettings.NormalizeDamageVignetteIntensityPercent(_hudManager.RuntimeSettings.DamageVignetteIntensityPercent);
        _hudManager.RuntimeSettings.DamageVignetteIntensityPercent = current <= 0
            ? ClientSettings.DefaultDamageVignetteIntensityPercent
            : current - 10;
        PersistClientSettings();
    }

    public void CycleFlameRenderModeSetting()
    {
        _gameplayManager.RuntimeSettings.FlameRenderMode = (_gameplayManager.RuntimeSettings.FlameRenderMode + 1) % 2;
        PersistClientSettings();
    }

    public void CycleBloodRenderModeSetting()
    {
        _gameplayManager.RuntimeSettings.BloodRenderMode = (_gameplayManager.RuntimeSettings.BloodRenderMode + 1) % 2;
        if (_gameplayManager.RuntimeSettings.BloodRenderMode != 0)
        {
            _gameplayManager.GoreEffects.ResetBloodSquibEffects();
        }

        PersistClientSettings();
    }

    public void ToggleDynamicRagdollSetting()
    {
        _gameplayManager.RuntimeSettings.DynamicRagdollEnabled = !_gameplayManager.RuntimeSettings.DynamicRagdollEnabled;
        if (!_gameplayManager.RuntimeSettings.DynamicRagdollEnabled)
        {
            ResetDynamicRagdollEffects();
        }

        PersistClientSettings();
    }

    public void ToggleBurnCharredCorpsesSetting()
    {
        _gameplayManager.RuntimeSettings.BurnCharredCorpsesEnabled = !_gameplayManager.RuntimeSettings.BurnCharredCorpsesEnabled;
        if (!_gameplayManager.RuntimeSettings.BurnCharredCorpsesEnabled)
        {
            ResetBurnCharredCorpses();
        }

        PersistClientSettings();
    }

    public void AdjustBloodPersistenceSeconds(int step)
    {
        _gameplayManager.RuntimeSettings.BloodPersistenceSeconds = Math.Clamp(_gameplayManager.RuntimeSettings.BloodPersistenceSeconds + step, 1, 120);
        ApplyBloodPresentationSettingsToWorld();
        PersistClientSettings();
    }

    public void CycleCorpseFadeModeSetting()
    {
        _gameplayManager.RuntimeSettings.CorpseFadeMode = (_gameplayManager.RuntimeSettings.CorpseFadeMode + 1) % 2;
        PersistClientSettings();
    }

    public void CycleMenuBackgroundModeSetting()
    {
        _gameplayManager.RuntimeSettings.MenuBackgroundMode = _gameplayManager.RuntimeSettings.MenuBackgroundMode switch
        {
            MenuBackgroundMode.Static => MenuBackgroundMode.DefaultMaps,
            MenuBackgroundMode.DefaultMaps => MenuBackgroundMode.AllMaps,
            MenuBackgroundMode.AllMaps => MenuBackgroundMode.Static,
            _ => MenuBackgroundMode.Static
        };

        // Initialize or reset the animated background controller based on new mode
        if (_gameplayManager.RuntimeSettings.MenuBackgroundMode != MenuBackgroundMode.Static && _mainMenuOpen)
        {
            _menuManager.AnimatedMenuBackground.Initialize(_gameplayManager.RuntimeSettings.MenuBackgroundMode);
        }
        else if (_gameplayManager.RuntimeSettings.MenuBackgroundMode == MenuBackgroundMode.Static)
        {
            _menuManager.AnimatedMenuBackground.Reset();
        }

        PersistClientSettings();
    }

    public void CycleGibLevelSetting()
    {
        _gameplayManager.RuntimeSettings.GibLevel = _gameplayManager.RuntimeSettings.GibLevel switch
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
        _gameplayManager.RuntimeSettings.BloodAmountLevel = _gameplayManager.RuntimeSettings.BloodAmountLevel >= 5 ? 1 : _gameplayManager.RuntimeSettings.BloodAmountLevel + 1;
        PersistClientSettings();
    }

    public void CycleCorpseDurationSetting()
    {
        _gameplayManager.RuntimeSettings.CorpseDurationMode = _gameplayManager.RuntimeSettings.CorpseDurationMode == ClientSettings.CorpseDurationInfinite
            ? ClientSettings.CorpseDurationDefault
            : ClientSettings.CorpseDurationInfinite;
        if (_gameplayManager.RuntimeSettings.CorpseDurationMode != ClientSettings.CorpseDurationInfinite)
        {
            ResetRetainedDeadBodies();
        }

        PersistClientSettings();
    }

    public void ToggleHealerRadarSetting()
    {
        _hudManager.RuntimeSettings.HealerRadarEnabled = !_hudManager.RuntimeSettings.HealerRadarEnabled;
        PersistClientSettings();
    }

    public void ToggleShowHealerSetting()
    {
        _hudManager.RuntimeSettings.ShowHealerEnabled = !_hudManager.RuntimeSettings.ShowHealerEnabled;
        PersistClientSettings();
    }

    public void ToggleShowHealingSetting()
    {
        _hudManager.RuntimeSettings.ShowHealingEnabled = !_hudManager.RuntimeSettings.ShowHealingEnabled;
        PersistClientSettings();
    }

    public void ToggleShowHealthBarSetting()
    {
        _hudManager.RuntimeSettings.ShowHealthBarEnabled = !_hudManager.RuntimeSettings.ShowHealthBarEnabled;
        PersistClientSettings();
    }

    public void ToggleShowShieldBarSetting()
    {
        _hudManager.RuntimeSettings.ShowShieldBarEnabled = !_hudManager.RuntimeSettings.ShowShieldBarEnabled;
        PersistClientSettings();
    }

    public void ToggleHudWeaponDisplayModeSetting()
    {
        _hudManager.RuntimeSettings.HudShowOnlyActiveWeapon = !_hudManager.RuntimeSettings.HudShowOnlyActiveWeapon;
        PersistClientSettings();
    }

    public void ToggleOverheadChatSetting()
    {
        _hudManager.RuntimeSettings.OverheadChatEnabled = !_hudManager.RuntimeSettings.OverheadChatEnabled;
        if (!_hudManager.RuntimeSettings.OverheadChatEnabled)
        {
            _localOverheadChatMessage = null;
            _overheadChatMessagesBySlot.Clear();
        }

        PersistClientSettings();
    }

    public void TogglePortraitRumbleSetting()
    {
        _hudManager.RuntimeSettings.PortraitRumbleEnabled = !_hudManager.RuntimeSettings.PortraitRumbleEnabled;
        if (!_hudManager.RuntimeSettings.PortraitRumbleEnabled)
        {
            _hudManager.LocalStatus.ResetPortraitRumble();
        }

        PersistClientSettings();
    }

    public void TogglePostGameMvpArtSetting()
    {
        _hudManager.RuntimeSettings.PostGameMvpArtEnabled = !_hudManager.RuntimeSettings.PostGameMvpArtEnabled;
        _postGameMvpArtHidden = false;
        if (!_hudManager.RuntimeSettings.PostGameMvpArtEnabled)
        {
            _postGameMvpArtFrameSelections.Clear();
        }

        PersistClientSettings();
    }

    public void ToggleDamageVignetteSetting()
    {
        _hudManager.RuntimeSettings.DamageVignetteEnabled = !_hudManager.RuntimeSettings.DamageVignetteEnabled;
        if (!_hudManager.RuntimeSettings.DamageVignetteEnabled)
        {
            _hudManager.LocalStatus.ResetDamageVignette();
        }

        PersistClientSettings();
    }

    public void TogglePersistentSelfNameSetting()
    {
        _hudManager.RuntimeSettings.ShowPersistentSelfNameEnabled = !_hudManager.RuntimeSettings.ShowPersistentSelfNameEnabled;
        PersistClientSettings();
    }

    public void ToggleShowPlayerNamesSetting()
    {
        _hudManager.RuntimeSettings.ShowPlayerNamesEnabled = !_hudManager.RuntimeSettings.ShowPlayerNamesEnabled;
        PersistClientSettings();
    }

    public void ToggleSpriteDropShadowSetting()
    {
        _gameplayManager.RuntimeSettings.SpriteDropShadowEnabled = !_gameplayManager.RuntimeSettings.SpriteDropShadowEnabled;
        PersistClientSettings();
    }

    public void ToggleStuckArrowsSetting()
    {
        _gameplayManager.RuntimeSettings.StuckArrowsEnabled = !_gameplayManager.RuntimeSettings.StuckArrowsEnabled;
        if (!_gameplayManager.RuntimeSettings.StuckArrowsEnabled)
        {
            _gameplayManager.ImpactEffects.ClearStuckArrows();
        }

        PersistClientSettings();
    }

    public void CycleWeaponBobSetting()
    {
        _gameplayManager.RuntimeSettings.WeaponBobMode = OpenGarrisonPreferencesDocument.NormalizeWeaponBobMode(_gameplayManager.RuntimeSettings.WeaponBobMode) == WeaponBobMode.Enabled
            ? WeaponBobMode.Disabled
            : WeaponBobMode.Enabled;
        PersistClientSettings();
    }

    public void ToggleWeaponRotationStyleSetting()
    {
        _gameplayManager.RuntimeSettings.PixelPerfectWeaponRotation = !_gameplayManager.RuntimeSettings.PixelPerfectWeaponRotation;
        PersistClientSettings();
    }

    public void ToggleWeaponRotationSourceSetting()
    {
        _gameplayManager.RuntimeSettings.UseLocalWeaponRotation = !_gameplayManager.RuntimeSettings.UseLocalWeaponRotation;
        PersistClientSettings();
    }

    public void ToggleAudioMuteSetting()
    {
        _audioManager.RuntimeSettings.AudioMuted = !_audioManager.RuntimeSettings.AudioMuted;
        ApplyAudioVolumeState();
        PersistClientSettings();
    }

    public void AdjustMasterVolume(int deltaPercent)
    {
        _audioManager.RuntimeSettings.MasterVolumePercent = Math.Clamp(_audioManager.RuntimeSettings.MasterVolumePercent + deltaPercent, 0, 100);
        ApplyAudioVolumeState();
        PersistClientSettings();
    }

    public void AdjustMenuMusicVolume(int deltaPercent)
    {
        _audioManager.RuntimeSettings.MenuMusicVolumePercent = Math.Clamp(_audioManager.RuntimeSettings.MenuMusicVolumePercent + deltaPercent, 0, 100);
        ApplyAudioVolumeState();
        PersistClientSettings();
    }

    public void AdjustIngameMusicVolume(int deltaPercent)
    {
        _audioManager.RuntimeSettings.IngameMusicVolumePercent = Math.Clamp(_audioManager.RuntimeSettings.IngameMusicVolumePercent + deltaPercent, 0, 100);
        ApplyAudioVolumeState();
        PersistClientSettings();
    }

    public void AdjustCombatMusicVolume(int deltaPercent)
    {
        _audioManager.RuntimeSettings.CombatMusicVolumePercent = Math.Clamp(
            _audioManager.RuntimeSettings.CombatMusicVolumePercent + deltaPercent,
            0,
            OpenGarrisonPreferencesDocument.MaxCombatMusicVolumePercent);
        ApplyAudioVolumeState();
        PersistClientSettings();
    }

    public void AdjustSoundEffectsVolume(int deltaPercent)
    {
        _audioManager.RuntimeSettings.SoundEffectsVolumePercent = Math.Clamp(_audioManager.RuntimeSettings.SoundEffectsVolumePercent + deltaPercent, 0, 100);
        PersistClientSettings();
    }

    public void ToggleUberOutlinesSetting()
    {
        _gameplayManager.RuntimeSettings.ShowUberOutlinesEnabled = !_gameplayManager.RuntimeSettings.ShowUberOutlinesEnabled;
        PersistClientSettings();
    }

    public void ToggleProjectileTeamTintSetting()
    {
        _gameplayManager.RuntimeSettings.ProjectileTeamTintEnabled = !_gameplayManager.RuntimeSettings.ProjectileTeamTintEnabled;
        PersistClientSettings();
    }

    public void ToggleKillCamSetting()
    {
        _gameplayManager.RuntimeSettings.KillCamEnabled = !_gameplayManager.RuntimeSettings.KillCamEnabled;
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
