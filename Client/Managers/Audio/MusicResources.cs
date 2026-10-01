#nullable enable

using Microsoft.Xna.Framework.Audio;

namespace OpenGarrison.Client;

public sealed class MusicResources
{
    public SoundEffect? MenuMusic;
    public SoundEffectInstance? MenuMusicInstance;
    public SoundEffect? LastToDieMenuMusic;
    public SoundEffectInstance? LastToDieMenuMusicInstance;
    public SoundEffect? FaucetMusic;
    public SoundEffectInstance? FaucetMusicInstance;
    public SoundEffect? IngameMusic;
    public SoundEffectInstance? IngameMusicInstance;
    public SoundEffect? IngameCombatMusic;
    public SoundEffectInstance? IngameCombatMusicInstance;
    public SoundEffect? LastToDieIngameMusic;
    public SoundEffectInstance? LastToDieIngameMusicInstance;
    public SoundEffect? LastToDieGameOverSound;
    public SoundEffectInstance? LastToDieGameOverSoundInstance;
}
