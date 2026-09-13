#nullable enable
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using OpenGarrison.ClientShared;
using OpenGarrison.Core;
using OpenGarrison.Protocol;
using OpenGarrison.SessionRuntime;

namespace OpenGarrison.Client;

public partial class Game1
{
    private bool _jukeboxMenuOpen;
    private SessionJukeboxPlayer? _localJukebox;
    private Task? _jukeboxLibraryLoad;
    private bool _jukeboxLibraryDirty = true;
    private string[] _jukeboxTracks = [];
    private int _jukeboxTrackIndex;
    private static string JukeboxDirectory => Path.Combine(RuntimePaths.ConfigDirectory, "jukebox");
    private bool CanControlSessionJukebox => IsEmbeddedSessionOwner || IsPracticeSessionActive && !_networkClient.IsConnected;

    private void ManageJukeboxLibrary()
    {
        if (OperatingSystem.IsBrowser()) { _jukeboxLibraryDirty = true; BrowserJukeboxStore.ManageLibrary?.Invoke(); return; }
        try
        {
            Directory.CreateDirectory(JukeboxDirectory);
            Process.Start(new ProcessStartInfo(JukeboxDirectory) { UseShellExecute = true });
        }
        catch (Exception ex) { _menuStatusMessage = "Could not open your music folder: " + ex.Message; }
    }
    private void OpenSessionJukebox()
    {
        _jukeboxMenuOpen = true; _inGameMenuHoverIndex = 0;
        if (OperatingSystem.IsBrowser() && CanControlSessionJukebox && _jukeboxLibraryDirty
            && _jukeboxLibraryLoad is null && BrowserJukeboxStore.RestoreLibrary is { } restore)
        {
            ExecuteSessionJukebox("stop");
            _jukeboxLibraryLoad = restore();
        }
        else RefreshSessionJukeboxTracks();
    }
    private IReadOnlyList<string> ExecuteSessionJukebox(string command)
    {
        if (_embeddedSessionHost is { } host) return host.ExecuteJukeboxCommand(command);
        if (!IsPracticeSessionActive || _networkClient.IsConnected) return ["The host controls the room's music."];
        _localJukebox ??= new(_world, () => VoiceClockSeconds, message =>
        {
            if (message is ServerAudioStateMessage state) EnsureVoiceChat()?.ApplyState(state);
            else if (message is AudioRelayMessage audio) EnsureVoiceChat()?.Receive(audio, VoiceClockSeconds);
        });
        return _localJukebox.Execute(command);
    }
    private void RefreshSessionJukeboxTracks()
    {
        if (!CanControlSessionJukebox) return;
        try
        {
            Directory.CreateDirectory(JukeboxDirectory);
            var playlist = Path.Combine(JukeboxDirectory, "playlist.txt");
            var existing = File.Exists(playlist) ? File.ReadAllLines(playlist) : [];
            var known = existing.Select(line => line.Trim()).ToHashSet(StringComparer.OrdinalIgnoreCase);
            var added = Directory.EnumerateFiles(JukeboxDirectory).Select(Path.GetFileName)
                .Where(name => name is not null && Path.GetExtension(name).ToLowerInvariant() is ".wav" or ".mp3" or ".ogg")
                .Where(name => !known.Contains(name!)).OrderBy(name => name, StringComparer.OrdinalIgnoreCase).ToArray();
            if (added.Length > 0) File.AppendAllLines(playlist, added!);
        }
        catch (IOException ex) { _menuStatusMessage = "Could not refresh music: " + ex.Message; }
        _jukeboxTracks = ExecuteSessionJukebox("list").Where(line => line.Length > 0 && char.IsAsciiDigit(line[0])).ToArray();
        _jukeboxTrackIndex = Math.Clamp(_jukeboxTrackIndex, 0, Math.Max(0, _jukeboxTracks.Length - 1));
    }
    private List<MenuPageAction> GetSessionJukeboxActions()
    {
        if (_jukeboxLibraryLoad is { IsCompleted: true } load)
        {
            _jukeboxLibraryLoad = null;
            if (load.IsFaulted) _menuStatusMessage = "Could not load your saved tracks: " + load.Exception!.GetBaseException().Message;
            else { _jukeboxLibraryDirty = false; RefreshSessionJukeboxTracks(); }
        }
        return BuildSessionJukeboxActions(ManageJukeboxLibrary);
    }

    private List<MenuPageAction> BuildSessionJukeboxActions(Action manageLibrary)
    {
        var actions = new List<MenuPageAction>();
        if (CanControlSessionJukebox)
        {
            var title = _jukeboxLibraryLoad is not null ? "Loading tracks..." : _jukeboxTracks.Length == 0 ? "No tracks - open music library" : _jukeboxTracks[_jukeboxTrackIndex];
            if (title.Length > 36) title = title[..36];
            actions.Add(new(title, () =>
            {
                if (_jukeboxLibraryLoad is not null) return;
                if (_jukeboxTracks.Length == 0) manageLibrary();
                else _jukeboxTrackIndex = (_jukeboxTrackIndex + 1) % _jukeboxTracks.Length;
            }));
            actions.Add(new("Play Selected", () => _menuStatusMessage = string.Join(" ", ExecuteSessionJukebox("play " + (_jukeboxTrackIndex + 1)))));
            actions.Add(new(_voiceChat?.ServerState?.JukeboxPaused == true ? "Resume Music" : "Pause Music",
                () => ExecuteSessionJukebox(_voiceChat?.ServerState?.JukeboxPaused == true ? "resume" : "pause")));
            actions.Add(new("Next Track", () => ExecuteSessionJukebox("next")));
            actions.Add(new("Stop Music", () => ExecuteSessionJukebox("stop")));
            actions.Add(new("Music Library", manageLibrary));
            actions.Add(new("Refresh Tracks", () => { _jukeboxLibraryDirty = true; OpenSessionJukebox(); }));
        }
        actions.Add(new(_voiceSettings.JukeboxMuted ? "Unmute Music" : "Mute Music", ToggleJukeboxMute));
        actions.Add(new("Back", () => { _jukeboxMenuOpen = false; _inGameMenuHoverIndex = 0; }));
        return actions;
    }
    private void StopLocalJukebox()
    {
        _localJukebox?.Dispose(); _localJukebox = null;
        _jukeboxMenuOpen = false;
    }
}
