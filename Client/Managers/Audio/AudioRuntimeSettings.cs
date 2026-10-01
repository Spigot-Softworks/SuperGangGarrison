#nullable enable

using OpenGarrison.Core;

namespace OpenGarrison.Client;

public sealed class AudioRuntimeSettings
{
    public bool AudioMuted { get; set; }

    public int MasterVolumePercent { get; set; } = 100;

    public int MenuMusicVolumePercent { get; set; } = 70;

    public int IngameMusicVolumePercent { get; set; } = 70;

    public int CombatMusicVolumePercent { get; set; } = OpenGarrisonPreferencesDocument.DefaultCombatMusicVolumePercent;

    public int SoundEffectsVolumePercent { get; set; } = 70;

    public bool DynamicMusicEnabled { get; set; } = true;

    public MusicMode MusicMode { get; set; } = MusicMode.MenuAndInGame;
}
