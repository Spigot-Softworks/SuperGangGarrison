#nullable enable

using Microsoft.Xna.Framework.Input;
using OpenGarrison.Core;
using System;

namespace OpenGarrison.Client;

public partial class Game1
{
    public static readonly TimeSpan CrtUnlockSequenceTimeout = TimeSpan.FromSeconds(3);
    public static readonly Keys[] CrtUnlockAlphabet =
    [
        Keys.A, Keys.B, Keys.C, Keys.D, Keys.E, Keys.F, Keys.G, Keys.H, Keys.I, Keys.J,
        Keys.K, Keys.L, Keys.M, Keys.N, Keys.O, Keys.P, Keys.Q, Keys.R, Keys.S, Keys.T,
        Keys.U, Keys.V, Keys.W, Keys.X, Keys.Y, Keys.Z,
    ];

    public bool _crtSettingsUnlockedForSession;
    public int _crtUnlockSequenceProgress;
    public TimeSpan _crtUnlockLastInputAt;

    public bool IsCrtSettingsUnlockedForSession => !OperatingSystem.IsBrowser() && _crtSettingsUnlockedForSession;

    public void UpdateCrtUnlockSequence(KeyboardState keyboard, TimeSpan totalGameTime)
    {
        if (!CanListenForCrtUnlockSequence())
        {
            _crtUnlockSequenceProgress = 0;
            return;
        }

        if (_crtUnlockSequenceProgress > 0
            && totalGameTime - _crtUnlockLastInputAt > CrtUnlockSequenceTimeout)
        {
            _crtUnlockSequenceProgress = 0;
        }

        var pressedLetter = Keys.None;
        var pressedLetterCount = 0;
        foreach (var key in CrtUnlockAlphabet)
        {
            if (keyboard.IsKeyDown(key) && !_previousKeyboard.IsKeyDown(key))
            {
                pressedLetter = key;
                pressedLetterCount += 1;
                if (pressedLetterCount > 1)
                {
                    break;
                }
            }
        }

        if (pressedLetterCount == 0)
        {
            return;
        }

        _crtUnlockLastInputAt = totalGameTime;
        if (pressedLetterCount > 1)
        {
            _crtUnlockSequenceProgress = 0;
            return;
        }

        var expectedKey = _crtUnlockSequenceProgress switch
        {
            1 => Keys.R,
            2 => Keys.T,
            _ => Keys.C,
        };
        if (pressedLetter == expectedKey)
        {
            _crtUnlockSequenceProgress += 1;
            if (_crtUnlockSequenceProgress == 3)
            {
                UnlockCrtSettingsForSession();
            }

            return;
        }

        // A new C starts over; any other letter clears the partial sequence.
        _crtUnlockSequenceProgress = pressedLetter == Keys.C ? 1 : 0;
    }

    public bool CanListenForCrtUnlockSequence()
    {
        return !OperatingSystem.IsBrowser()
            && !_crtSettingsUnlockedForSession
            && IsWindowInputActive
            && _mainMenuOpen
            && !_startupSplashOpen
            && !_loadingOverlayState.Visible
            && !_mainMenuChromeHidden
            && !_builderEditorEnabled
            && _mainMenuPage == MainMenuPage.Root
            && _menuManager.MainMenuOverlay.GetActiveOverlay() == MainMenuOverlayKind.None
            && _activeDevMessagePopup is null
            && !_quitPromptOpen
            && !_passwordPromptOpen
            && !_chatOpen
            && !_consoleOpen
            && !_accountDialogOpen
            && !_editingPlayerName
            && !_editingFriendCode
            && !_editingFriendNickname
            && !_editingConnectHost
            && !_editingConnectPort
            && _hostSetupEditField == HostSetupEditField.None;
    }

    public void UnlockCrtSettingsForSession()
    {
        _crtSettingsUnlockedForSession = true;
        _crtUnlockSequenceProgress = 0;
        if (string.IsNullOrWhiteSpace(_menuStatusMessage))
        {
            _menuStatusMessage = "CRT options unlocked in Graphics.";
        }
    }

    public string GetCrtPresetLabel()
    {
        return OpenGarrisonPreferencesDocument.NormalizeCrtPreset(_clientSettings.CrtPreset) switch
        {
            CrtPresetKind.PcMonitor => "PC CRT",
            CrtPresetKind.StudioRgb => "Studio RGB",
            CrtPresetKind.ArcadeRgb => "Arcade RGB",
            CrtPresetKind.HomeTvRgb => "Home TV RGB",
            _ => "Off",
        };
    }

    public string GetCrtSignalModeLabel()
    {
        return OpenGarrisonPreferencesDocument.NormalizeCrtSignalMode(_clientSettings.CrtSignalMode) switch
        {
            CrtSignalModeKind.Native => "Native",
            CrtSignalModeKind.Classic => "Classic (half)",
            _ => "Preset",
        };
    }

    public void CycleCrtPresetSetting()
    {
        _clientSettings.CrtPreset = OpenGarrisonPreferencesDocument.NormalizeCrtPreset(_clientSettings.CrtPreset) switch
        {
            CrtPresetKind.Off => CrtPresetKind.PcMonitor,
            CrtPresetKind.PcMonitor => CrtPresetKind.StudioRgb,
            CrtPresetKind.StudioRgb => CrtPresetKind.ArcadeRgb,
            CrtPresetKind.ArcadeRgb => CrtPresetKind.HomeTvRgb,
            _ => CrtPresetKind.Off,
        };
        CommitCrtSettingsChange();
    }

    public void CycleCrtQualitySetting()
    {
        _clientSettings.CrtQuality = OpenGarrisonPreferencesDocument.NormalizeCrtQuality(_clientSettings.CrtQuality) switch
        {
            CrtQualityKind.Auto => CrtQualityKind.Balanced,
            CrtQualityKind.Balanced => CrtQualityKind.High,
            _ => CrtQualityKind.Auto,
        };
        CommitCrtSettingsChange();
    }

    public void CycleCrtSignalModeSetting()
    {
        _clientSettings.CrtSignalMode = OpenGarrisonPreferencesDocument.NormalizeCrtSignalMode(_clientSettings.CrtSignalMode) switch
        {
            CrtSignalModeKind.Preset => CrtSignalModeKind.Native,
            CrtSignalModeKind.Native => CrtSignalModeKind.Classic,
            _ => CrtSignalModeKind.Preset,
        };
        CommitCrtSettingsChange();
    }

    public void ToggleCrtCurvatureSetting()
    {
        _clientSettings.CrtCurvatureEnabled = !_clientSettings.CrtCurvatureEnabled;
        CommitCrtSettingsChange();
    }

    public void CycleCrtBrightnessSetting()
    {
        AdjustCrtBrightnessSetting(5);
    }

    public void AdjustCrtBrightnessSetting(int delta)
    {
        var current = OpenGarrisonPreferencesDocument.NormalizeCrtBrightnessPercent(_clientSettings.CrtBrightnessPercent);
        var next = OpenGarrisonPreferencesDocument.NormalizeCrtBrightnessPercent(current + delta);
        if (next == current)
        {
            return;
        }

        _clientSettings.CrtBrightnessPercent = next;
        CommitCrtSettingsChange();
    }

    public void ResetCrtSettings()
    {
        var hasChanges = _clientSettings.CrtPreset != OpenGarrisonPreferencesDocument.DefaultCrtPreset
            || _clientSettings.CrtQuality != OpenGarrisonPreferencesDocument.DefaultCrtQuality
            || _clientSettings.CrtSignalMode != OpenGarrisonPreferencesDocument.DefaultCrtSignalMode
            || _clientSettings.CrtCurvatureEnabled != OpenGarrisonPreferencesDocument.DefaultCrtCurvatureEnabled
            || _clientSettings.CrtBrightnessPercent != OpenGarrisonPreferencesDocument.DefaultCrtBrightnessPercent;
        if (!hasChanges)
        {
            return;
        }

        _clientSettings.CrtPreset = OpenGarrisonPreferencesDocument.DefaultCrtPreset;
        _clientSettings.CrtQuality = OpenGarrisonPreferencesDocument.DefaultCrtQuality;
        _clientSettings.CrtSignalMode = OpenGarrisonPreferencesDocument.DefaultCrtSignalMode;
        _clientSettings.CrtCurvatureEnabled = OpenGarrisonPreferencesDocument.DefaultCrtCurvatureEnabled;
        _clientSettings.CrtBrightnessPercent = OpenGarrisonPreferencesDocument.DefaultCrtBrightnessPercent;
        CommitCrtSettingsChange();
    }

    private void CommitCrtSettingsChange()
    {
        OnCrtSettingsChanged();
        PersistClientSettings();
    }
}
