using System;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using OpenGarrison.Core;
using OpenGarrison.Protocol;

namespace OpenGarrison.Client;

public partial class Game1
{
    private VoiceChatSettings _voiceSettings = VoiceChatSettings.Load();
    private VoiceChatClient? _voiceChat;
    private string _voiceStartupError = "";
    private bool _jukeboxWasAudible;
    private uint _voiceConnectionGeneration;
    private double VoiceClockSeconds => _networkInterpolationClock.Elapsed.TotalSeconds;
    private bool IsJukeboxPresent => _voiceChat?.ServerState?.JukeboxPlaying == true;
    private bool IsJukeboxAudible => IsJukeboxPresent && _voiceChat?.ServerState?.JukeboxPaused == false
        && !_voiceSettings.JukeboxMuted && _voiceSettings.JukeboxVolumePercent > 0
        && _voiceChat.IsSpeaking(0, VoiceClockSeconds);

    private void InitializeVoiceSettings()
    {
        if (OperatingSystem.IsBrowser() && InputBindingsSettings.TryParseBinding(_voiceSettings.PushToTalkBinding, out var binding))
            _inputBindings.PushToTalk = binding;
    }

    private VoiceChatClient? EnsureVoiceChat()
    {
        if (_voiceConnectionGeneration != _networkClient.ConnectionGeneration)
        {
            ResetVoiceChat();
            _voiceConnectionGeneration = _networkClient.ConnectionGeneration;
        }
        if (_voiceChat is not null) return _voiceChat;
        try
        {
            IVoiceAudioDevice? device = VoiceAudioPlatform.CreateDevice?.Invoke();
#if !BROWSER_KNI
            device ??= new DesktopVoiceAudioDevice();
#endif
            if (device is not null) _voiceChat = new VoiceChatClient(device, _voiceSettings, _networkClient.SendVoice, _networkClient.SendVoiceChannelMembership);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException) { _voiceStartupError = "Voice unavailable: " + ex.Message; }
        return _voiceChat;
    }

    private void UpdateVoiceChat(KeyboardState keyboard, MouseState mouse, bool windowActive)
    {
        _localJukebox?.Tick();
        if ((!_networkClient.IsConnected && _localJukebox is null) || _networkClient.IsReplayConnection)
        {
            if (_voiceChat?.ServerState is not null) ResetVoiceChat();
            return;
        }
        var voice = EnsureVoiceChat();
        if (voice is null) return;
        var inLastToDieLobby = _networkClient.LastToDieState.Snapshot?.Phase == LastToDieWirePhase.Lobby;
        var allowed = windowActive && !_networkClient.IsAwaitingWelcome && (!_mainMenuOpen || inLastToDieLobby)
            && (!_inGameMenuOpen || IsHostedLastToDieActive())
            && !_optionsMenuOpen && !_controlsMenuOpen && !_chatOpen && !_consoleOpen && !_passwordPromptOpen
            && !_teamSelectOpen && !_classSelectOpen && !_loadingOverlayVisible;
        var held = InputBindingInput.IsDown(_inputBindings.PushToTalk, keyboard, mouse);
        voice.Update(VoiceClockSeconds, allowed, held, _audioMuted ? 0 : GetNonLinearVolumeScale(_masterVolumePercent), IsScoreboardSlotMuted, GetVoiceSpeakerMix);
        if (_jukeboxWasAudible != IsJukeboxAudible)
        {
            _jukeboxWasAudible = IsJukeboxAudible;
            UpdateCurrentMusicInstanceVolumes();
        }
    }

    private string GetVoiceChannelActionLabel() => _voiceChat?.IsVoiceChannelJoined == true ? "Leave voice" : "Join voice";
    private string GetVoiceMuteActionLabel() => _voiceSettings.VoiceMuted ? "Unmute all voice" : "Mute all voice";
    private void ToggleVoiceMute()
    {
        var muted = !_voiceSettings.VoiceMuted;
        if (_voiceChat is { } voice) voice.SetVoiceMuted(muted);
        else _voiceSettings.VoiceMuted = muted;
        SaveVoiceSettings();
    }

    private string GetVoiceStatusLabel()
    {
        var error = string.IsNullOrEmpty(_voiceStartupError) ? _voiceChat?.Status : _voiceStartupError;
        if (!string.IsNullOrEmpty(error)) return error;
        if (_voiceChat?.ServerState is { VoiceChannelRequiresJoin: true } && !_voiceChat.IsVoiceChannelJoined) return "Not joined";
        if (_voiceChat is { IsVoiceChannelJoined: true, CanUseVoiceChannel: false }) return "Joining voice channel...";
        if (_voiceChat?.ServerState?.VoiceEnabled == false) return "Transmission disabled by server";
        return "Ready";
    }
    private void ToggleVoiceChannelMembership()
    {
        var voice = EnsureVoiceChat();
        if (voice is not null) voice.SetVoiceChannelJoined(!voice.IsVoiceChannelJoined);
    }

    private void ResetVoiceChat()
    {
        _voiceChat?.Reset();
        if (_jukeboxWasAudible) { _jukeboxWasAudible = false; UpdateCurrentMusicInstanceVolumes(); }
    }
    private void SaveVoiceSettings()
    {
        try { _voiceSettings.Save(); }
        catch (Exception ex) when (ex is System.IO.IOException or UnauthorizedAccessException) { _voiceStartupError = "Could not save voice settings: " + ex.Message; }
    }
    private string GetVoiceModeLabel() => _voiceSettings.Mode switch
    {
        VoiceTransmitMode.OpenMicrophone => "Open Microphone",
        VoiceTransmitMode.Disabled => "Disabled",
        _ => "Push to Talk",
    };
    private void CycleVoiceMode() { _voiceSettings.Mode = (VoiceTransmitMode)(((int)_voiceSettings.Mode + 1) % 3); SaveVoiceSettings(); }
    private void ToggleVoiceTeamOnly() { _voiceSettings.TeamOnly = !_voiceSettings.TeamOnly; SaveVoiceSettings(); }
    private string GetVoiceChannelLabel() => _voiceChat?.ServerState?.TeamOnly == true ? "Team (Server)" : _voiceSettings.TeamOnly ? "Team" : "All Players";
    private void ToggleJukeboxMute() { _voiceSettings.JukeboxMuted = !_voiceSettings.JukeboxMuted; SaveVoiceSettings(); }
    private void AdjustVoiceSetting(string setting, int amount)
    {
        switch (setting)
        {
            case "gain": _voiceSettings.MicrophoneGainPercent = Math.Clamp(_voiceSettings.MicrophoneGainPercent + amount, 0, 200); break;
            case "voice": _voiceSettings.VoiceVolumePercent = Math.Clamp(_voiceSettings.VoiceVolumePercent + amount, 0, 300); break;
            case "jukebox": _voiceSettings.JukeboxVolumePercent = Math.Clamp(_voiceSettings.JukeboxVolumePercent + amount, 0, 100); break;
        }
        SaveVoiceSettings();
    }

    private void ToggleSpatialVoice() { _voiceSettings.SpatialVoice = !_voiceSettings.SpatialVoice; SaveVoiceSettings(); }

    private bool TryGetVoiceSpeakerPlayer(byte slot, out PlayerEntity player)
    {
        player = null!;
        if (slot == 0)
        {
            return false;
        }

        var localSlot = _networkClient.IsConnected && _networkClient.LocalPlayerSlot != 0
            ? _networkClient.LocalPlayerSlot
            : SimulationWorld.LocalPlayerSlot;
        if (slot == localSlot)
        {
            player = _world.LocalPlayer;
            return true;
        }

        // RemoteSnapshotPlayers is the current rendered snapshot. Resolve its
        // authoritative slot through the world's mapping instead of treating
        // the local simulation slot or list index as a network slot.
        foreach (var candidate in _world.RemoteSnapshotPlayers)
        {
            if (_world.TryGetPlayerNetworkSlot(candidate, out var candidateSlot) && candidateSlot == slot)
            {
                player = candidate;
                return true;
            }
        }

        return false;
    }

    private (float Volume, float Pan) GetVoiceSpeakerMix(byte slot)
    {
        if (!_voiceSettings.SpatialVoice
            || IsLocalSpectatorPresentationActive()
            || !TryGetVoiceSpeakerPlayer(slot, out var player)
            || !player.IsAlive
            || player.IsSpyCloaked && player.Team != _world.LocalPlayer.Team)
        {
            return (1f, 0f);
        }

        var listener = GetWorldSoundListenerPosition();
        var dx = player.X - listener.X;
        var dy = player.Y - listener.Y;
        var distance = MathF.Sqrt((dx * dx) + (dy * dy));
        var volume = distance <= 300f ? 1f : Math.Clamp(1f - ((distance - 300f) / 1200f), 0f, 1f);
        return (volume, Math.Clamp(dx / 400f, -1f, 1f));
    }

    private bool TryGetVoiceSpeakerScreenPosition(byte slot, out Vector2 position)
    {
        position = default;
        if (!TryGetVoiceSpeakerPlayer(slot, out var player) || !player.IsAlive)
        {
            return false;
        }

        if (player.IsSpyCloaked && player.Team != _world.LocalPlayer.Team)
        {
            return false;
        }

        var camera = _hasGameplayCameraTopLeft ? _gameplayCameraTopLeft : Vector2.Zero;
        position = GetWorldScreenPosition(player.X, player.Y, camera);
        return position.X >= 8f && position.X <= ViewportWidth - 8f
            && position.Y >= 8f && position.Y <= ViewportHeight - 8f;
    }
    private void CycleVoiceMicrophone()
    {
        var microphones = EnsureVoiceChat()?.MicrophoneNames.ToList() ?? [];
        microphones.Insert(0, "");
        var index = microphones.IndexOf(_voiceSettings.MicrophoneName);
        _voiceSettings.MicrophoneName = microphones[(index + 1) % microphones.Count];
        SaveVoiceSettings();
    }

    private bool IsScoreboardPlayerSpeaking(PlayerEntity player) => ReferenceEquals(player, _world.LocalPlayer)
        ? _voiceChat?.IsTransmitting(VoiceClockSeconds) == true
        : !_voiceSettings.VoiceMuted && TryGetScoreboardPlayerNetworkSlot(player, out var slot) && _voiceChat?.IsSpeaking(slot, VoiceClockSeconds) == true && !IsScoreboardSlotMuted(slot);

    private void DrawVoiceParticipants()
    {
        if (_gameplayHudHidden || !_networkClient.IsConnected || _networkClient.IsReplayConnection || _voiceChat is not { } voice) return;
        const int width = 192;
        const int rowStride = 32;
        var now = VoiceClockSeconds;
        var transmitting = voice.IsTransmitting(now);
        var speaking = voice.Speakers.Where(speaker => speaker.Slot != 0 && now - speaker.Buffer.LastReceivedAt < 0.25)
            .OrderBy(speaker => speaker.Slot).Take(5).ToArray();
        var speakers = speaking.Where(speaker => !TryGetVoiceSpeakerScreenPosition(speaker.Slot, out _)).ToArray();
        foreach (var speaker in speaking)
        {
            if (TryGetVoiceSpeakerScreenPosition(speaker.Slot, out var markerPosition)
                && !_voiceSettings.VoiceMuted && !IsScoreboardSlotMuted(speaker.Slot))
            {
                var markerColor = speaker.Team == 1 ? new Color(225, 110, 103) : new Color(94, 170, 255);
                _spriteBatch.Draw(_pixel, new Rectangle((int)markerPosition.X - 3, (int)markerPosition.Y - 3, 6, 6), markerColor);
            }
        }
        var rowCount = (IsJukeboxPresent ? 1 : 0) + (transmitting ? 1 : 0) + speakers.Length;
        if (rowCount == 0) return;
        var x = ViewportWidth - width - 10;
        var y = (ViewportHeight - (rowCount * rowStride - 4)) / 2;
        if (IsJukeboxPresent)
        {
            var muted = _voiceSettings.JukeboxMuted || _voiceSettings.JukeboxVolumePercent == 0;
            var state = voice.ServerState!;
            DrawVoiceParticipantRow(x, y, width, "Jukebox", state.JukeboxPaused ? "Paused" : muted ? "Muted" : state.TrackName,
                muted ? Color.Gray : new Color(239, 201, 104));
            y += rowStride;
        }
        if (transmitting)
        {
            DrawVoiceParticipantRow(x, y, width, _world.LocalPlayer.DisplayName, "Transmitting", new Color(116, 230, 144));
            y += rowStride;
        }
        foreach (var speaker in speakers)
        {
            var muted = _voiceSettings.VoiceMuted || IsScoreboardSlotMuted(speaker.Slot);
            var color = muted ? Color.Gray : speaker.Team == 1 ? new Color(225, 110, 103) : new Color(94, 170, 255);
            var spatialMix = GetVoiceSpeakerMix(speaker.Slot);
            var status = muted ? "Muted" : _voiceSettings.SpatialVoice && spatialMix.Volume <= 0f ? "Out of range" : "Speaking";
            DrawVoiceParticipantRow(x, y, width, speaker.Name, status, color);
            y += rowStride;
        }
    }

    private void DrawVoiceParticipantRow(int x, int y, int width, string name, string status, Color color)
    {
        _spriteBatch.Draw(_pixel, new Rectangle(x, y, width, 28), new Color(26, 29, 34) * 0.85f);
        _spriteBatch.Draw(_pixel, new Rectangle(x + 5, y + 8, 3, 10), color);
        _spriteBatch.Draw(_pixel, new Rectangle(x + 10, y + 5, 3, 16), color);
        DrawBitmapFontText(TrimBitmapMenuText(SanitizeScoreboardText(name), width - 24, 1f), new Vector2(x + 19, y + 3), color, 1f);
        DrawBitmapFontText(TrimBitmapMenuText(SanitizeScoreboardText(status), width - 24, 0.75f), new Vector2(x + 19, y + 16), Color.White * 0.8f, 0.75f);
    }

    private Rectangle GetJukeboxScoreboardBounds()
    {
        var layout = GetScoreboardLayout();
        return new Rectangle((int)layout.XOffset + 5, (int)GetScoreboardSpectatorY(layout.YOffset), 535, 20);
    }
    private void DrawJukeboxScoreboardRow(float alpha)
    {
        var bounds = GetJukeboxScoreboardBounds();
        var color = _voiceSettings.JukeboxMuted ? Color.Gray : new Color(239, 201, 104);
        _spriteBatch.Draw(_pixel, bounds, new Color(26, 29, 34) * alpha);
        DrawBitmapFontText("Jukebox", new Vector2(bounds.X + 6, bounds.Y + 4), color * alpha, 1f);
        var label = _voiceChat?.ServerState?.JukeboxPaused == true ? "Paused" : _voiceChat?.ServerState?.TrackName ?? "";
        DrawBitmapFontText(TrimBitmapMenuText(SanitizeScoreboardText(label), 315, 0.75f), new Vector2(bounds.X + 95, bounds.Y + 5), Color.White * alpha, 0.75f);
        DrawBitmapFontText(_voiceSettings.JukeboxMuted ? "[Unmute]" : "[Mute]", new Vector2(bounds.Right - 74, bounds.Y + 4), color * alpha, 1f);
    }
    private bool TryHandleJukeboxScoreboardClick(MouseState mouse)
    {
        if (!IsJukeboxPresent || !GetJukeboxScoreboardBounds().Contains(mouse.Position)) return false;
        var pressed = mouse.LeftButton == ButtonState.Pressed && _previousMouse.LeftButton != ButtonState.Pressed
            || mouse.RightButton == ButtonState.Pressed && _previousMouse.RightButton != ButtonState.Pressed;
        if (!pressed) return false;
        ToggleJukeboxMute();
        _suppressPrimaryFireUntilMouseRelease = true;
        _suppressSecondaryFireUntilMouseRelease = true;
        CloseScoreboardContextMenu();
        return true;
    }
}
