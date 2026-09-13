using System;
using System.Collections.Generic;

namespace OpenGarrison.Client;

/// <summary>Device lifetime is owned by one game. Browser installs its Web Audio implementation before constructing Game1.</summary>
public interface IVoiceAudioDevice : IDisposable
{
    bool UsesGameMasterVolume { get; }
    bool CaptureActive { get; }
    string Status { get; }
    IReadOnlyList<string> MicrophoneNames { get; }
    void SetCapture(bool active, string microphoneName);
    bool TryReadCapture(out float[] samples, out int sampleRate);
    void Play(byte speakerSlot, short[] samples, int channels, float volume, float pan);
    void SetVolume(byte speakerSlot, float volume);
    void StopPlayback(byte speakerSlot);
}

public static class VoiceAudioPlatform
{
    public static Func<IVoiceAudioDevice>? CreateDevice { get; set; }
    public static string? BrowserSettingsJson { get; set; }
    public static Action<string>? SaveBrowserSettings { get; set; }
}
