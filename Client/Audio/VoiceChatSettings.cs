using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using OpenGarrison.Core;

namespace OpenGarrison.Client;

[JsonConverter(typeof(JsonStringEnumConverter<VoiceTransmitMode>))]
public enum VoiceTransmitMode { PushToTalk, OpenMicrophone, Disabled }

public sealed class VoiceChatSettings
{
    public VoiceTransmitMode Mode { get; set; } = VoiceTransmitMode.PushToTalk;
    public string PushToTalkBinding { get; set; } = "V";
    public string MicrophoneName { get; set; } = "";
    public int MicrophoneGainPercent { get; set; } = 100;
    public int VoiceVolumePercent { get; set; } = 100;
    public bool SpatialVoice { get; set; }
    public bool VoiceMuted { get; set; }
    public int JukeboxVolumePercent { get; set; } = 70;
    public bool JukeboxMuted { get; set; }
    public bool TeamOnly { get; set; }
    private static string FilePath => RuntimePaths.GetConfigPath("voice-chat.json");

    public static VoiceChatSettings FromJson(string? json)
    {
        try
        {
            var result = string.IsNullOrWhiteSpace(json) ? new() : JsonSerializer.Deserialize(json, VoiceSettingsJsonContext.Default.VoiceChatSettings) ?? new();
            result.Normalize();
            return result;
        }
        catch (JsonException) { return new(); }
    }

    public static VoiceChatSettings Load()
    {
        try { return FromJson(OperatingSystem.IsBrowser() ? VoiceAudioPlatform.BrowserSettingsJson : File.Exists(FilePath) ? File.ReadAllText(FilePath) : null); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { return new(); }
    }

    public void Normalize()
    {
        if (!Enum.IsDefined(Mode)) Mode = VoiceTransmitMode.PushToTalk;
        MicrophoneGainPercent = Math.Clamp(MicrophoneGainPercent, 0, 200);
        VoiceVolumePercent = Math.Clamp(VoiceVolumePercent, 0, 300);
        JukeboxVolumePercent = Math.Clamp(JukeboxVolumePercent, 0, 100);
        MicrophoneName ??= "";
        if (!InputBindingsSettings.TryParseBinding(PushToTalkBinding, out _)) PushToTalkBinding = "V";
    }

    public void Save()
    {
        Normalize();
        var json = JsonSerializer.Serialize(this, VoiceSettingsJsonContext.Default.VoiceChatSettings);
        if (OperatingSystem.IsBrowser()) VoiceAudioPlatform.SaveBrowserSettings?.Invoke(json);
        else
        {
            Directory.CreateDirectory(RuntimePaths.ConfigDirectory);
            File.WriteAllText(FilePath, json);
        }
    }
}

[JsonSerializable(typeof(VoiceChatSettings))]
[JsonSourceGenerationOptions(WriteIndented = true)]
internal partial class VoiceSettingsJsonContext : JsonSerializerContext;
