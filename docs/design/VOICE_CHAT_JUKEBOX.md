# Voice chat and server Jukebox

## Player controls

Voice defaults to **Push to Talk**, with **Shift** as the default key. Either Shift key works with the default binding. `Options > Audio` has expandable **Game** and **Chat** groups, with Game open initially. Game contains global volume/mute, sound effects, game music and Jukebox controls. Chat contains incoming voice mute/volume, microphone mode/device/gain, the push-to-talk key, team/all-player channel and voice status. The key can be rebound under `Options > Controls > Keyboard & Mouse`. Existing explicit bindings are preserved; old control files without a scoreboard binding fall back to Tab.

Push to Talk captures only while held during connected, focused gameplay. Open Microphone is an explicit alternative. Disabled turns off microphone transmission. Options, text entry, the console, loading, replay playback, disconnection and loss of focus stop capture. Incoming voice remains available when microphone transmission is disabled. A server gag blocks transmission while preserving listening.

**Last To Die co-op voice is opt-in.** Click **Join voice channel** in the lobby before speaking or listening to other players. The button changes to **Leave voice channel**. The same toggle is available in the in-match Escape menu. Joined players can hold Shift to talk in the lobby, during gameplay, and in the Escape menu. Leaving immediately stops microphone capture and discards queued player audio. Membership lasts through stage changes and returning to the same lobby, but disconnecting/reconnecting requires joining again. Jukebox playback remains independent of voice membership. Normal multiplayer voice retains its existing default participation.

Speaking participants appear at the right edge of the HUD in a vertically centered stack; active players also have a speaking marker on the scoreboard. There is no persistent push-to-talk hint on the HUD. The multiplayer Escape menu has **Mute all voice**, which changes to **Unmute all voice** while muted; the same toggle is in Audio > Chat. It immediately stops incoming player audio and discards queued audio, preserves individual mute settings and voice volume, and persists across sessions. Microphone transmission, Last To Die channel membership and Jukebox playback are independent of this toggle. A player's existing scoreboard mute suppresses their voice and chat. Jukebox has its own persistent mute and volume. While music is playing, **Jukebox** appears as a participant in both the HUD and scoreboard with the track title; click its scoreboard row to mute/unmute. Paused music keeps the participant visible with a Paused status. Stopping removes it.

Jukebox is an audio participant, using reserved audio identity 0. It does not join a team, consume a player/spectator slot, spawn an entity, affect balancing, gain stats, or enter voting. Ordinary in-game music is silenced while Jukebox is audible and restored when it stops, pauses or is muted. Master volume and global audio mute apply to voice and Jukebox.

Client settings persist in `config/voice-chat.json` under the application's user-data root. Desktop key bindings also use the existing controls configuration. Browser settings persist in local storage under `voice-chat-v1`.

## Server setup

1. Start the server once to create `config/server-audio.json` under its user-data root.
2. Run `jukebox list` in the server console to create the music directory and `playlist.txt`. Add audio files there.
3. Edit `playlist.txt` with one audio filename per line, in the order you want to hear them.
4. Run `jukebox play`. Tracks play in their listed order, without alphabetical sorting.

On a default Windows install the playlist is `%LOCALAPPDATA%\OpenGarrison\config\jukebox\playlist.txt`. `jukebox status` prints its resolved path on every platform. `--user-data-root <directory>` or `OPENGARRISON_USER_DATA_ROOT` selects a separate user-data root, including when hosting multiple servers.

Supported files are mono/stereo **WAV, OGG Vorbis and MP3**, sampled at 8–192 kHz. WAV supports PCM 8/16/24/32-bit and IEEE float32. WAV extensible, multichannel files, Ogg Opus, URLs and DRM-protected media are not supported. Decode failures are logged and skipped; an entirely invalid/empty playlist stops. Audio is resampled to 48 kHz stereo and streamed to listeners; clients do not download or retain the source file.

The playlist contains up to 1,024 supported entries and preserves duplicate entries. Blank lines and lines beginning with `#` are ignored. Missing/unsupported files, directory paths, and titles exceeding 160 UTF-8 bytes are skipped without reordering the other entries. Filenames with spaces need no quotes inside the playlist. Files absent from the playlist are not played. `list` and `play` reread the file; an active queue, including automatic advancement, `next`, and looping, keeps the order loaded by `play`. Select a track by its displayed number or exact filename. Filename lookup on disk follows the host filesystem's case sensitivity.

If `playlist.txt` does not exist, the first `list` or `play` creates it from existing audio files in directory enumeration order, without sorting. After that, the file is authoritative; add new filenames to it yourself. For example, this plays Z before A and then repeats Z:

```text
# My server playlist
Z song.mp3
A song.ogg
Z song.mp3
```

```text
jukebox list
jukebox play
jukebox play 2
jukebox play "My song.mp3"
jukebox next
jukebox pause
jukebox resume
jukebox stop
jukebox status
```

The console/admin command requires `ManageServerConfiguration`. Existing server command, admin and RCON routing enforce that permission. Players cannot inject Jukebox packets or execute playlist commands through the voice protocol.

Default `server-audio.json`:

```json
{
  "VoiceEnabled": true,
  "VoiceTeamOnly": false,
  "JukeboxEnabled": true,
  "JukeboxAutoPlay": false,
  "JukeboxLoop": true,
  "JukeboxDirectory": "jukebox"
}
```

`JukeboxDirectory` is relative to the server's config directory, or an absolute path. Changing it requires a server restart. These runtime cvars update and persist the other settings:

| Cvar | Default | Behavior |
| --- | --- | --- |
| `sv_voice_enabled` | true | Allow player microphone transmission |
| `sv_voice_teamonly` | false | Force team routing; spectators are isolated together |
| `sv_jukebox_enabled` | true | Allow Jukebox; disabling immediately stops it |
| `sv_jukebox_loop` | true | Repeat the playlist after its last track |
| `sv_jukebox_autoplay` | false | Start the playlist on subsequent server starts |

Voice and Jukebox are independent: disabling voice does not disable server music. Team voice follows the authoritative player's team; either the sender or server may restrict delivery. All authorized listeners receive Jukebox, including spectators and late joiners.

## Runtime implementation

Source paths below are relative to the repository root.

| Layer | Entry points |
| --- | --- |
| Wire contracts | `Protocol/AudioMessages.cs`, `Protocol/ProtocolCodec.Audio.cs`, `Protocol/Protocol64AudioSchemas.cs` |
| Codec, resampling and playout | `Core/Audio/StreamingOpus.cs`, `Core/Audio/StreamingPcmResampler.cs` |
| Server ownership and routing | `Server/Audio/ServerAudioService.cs`, `Server/Runtime/GameServer.Audio.cs` |
| File decoding | `Server/Audio/JukeboxTrackReader.cs` |
| Client stream lifecycle | `Client/Audio/VoiceChatClient.cs` |
| Desktop capture/output | `Client/Audio/DesktopVoiceAudioDevice.cs` |
| Browser capture/output | `Client.Browser/Services/BrowserVoiceAudioDevice.cs`, `Client.Browser/wwwroot/voice-audio.js`, `Client.Browser/wwwroot/voice-capture-worklet.js` |
| Controls, HUD and music integration | `Client/Game/Audio/Game1.VoiceChat.cs`, existing controls/options/scoreboard/audio partials |

Both legacy framing and protocol 64 carry client `VoiceSubmit` and `VoiceChannelMembership`, server `AudioRelay`, and server `ServerAudioState`. Client and server need matching builds; see [ProtocolVersion.cs](../../Protocol/ProtocolVersion.cs) for the compatibility version. No extra audio service or port is needed: audio follows the existing UDP, WebSocket or QUIC connection. The server never trusts submitted identity; it derives speaker slot, name and team from the authenticated session. Per-session sequence checks, a 55-packet/s token bucket (10 packet burst), fixed frame duration and bounded payloads constrain relaying.

Last To Die servers enforce membership for both senders and recipients. Membership requests have increasing revisions and retry every 250 ms until acknowledged in the personalized server state. Older requests cannot undo a newer leave; periodic state delivery repairs lost acknowledgments. The client blocks capture/listening until its join is acknowledged and blocks both immediately on leave, including while a leave request is in flight. Membership belongs to the authenticated connection, not the player slot, survivor, stage or saved audio preferences.

Managed Concentus Opus runs on desktop, browser and dedicated servers. Voice uses 48 kHz mono, 24 kbps; music uses 48 kHz stereo, 96 kbps. Frames are 20 ms with a maximum encoded size of 320 bytes. Each packet carries up to the latest three frames, recovering short losses and normal coalescing. The receiver rejects stale/duplicate packets, conceals missing frames, and bounds its jitter queue to 12 frames. Device queues are bounded separately and recover near live time after stalls.

Protocol-64 audio uses its own LastWins channel, with replacement keys per speaker and a separate state key. Desktop/browser WebSocket microphone sends drop under backpressure; QUIC scheduling coalesces by source. Periodic state messages repair state loss and initialize late joiners. Stream IDs distinguish reconnects and track changes. Playlist decoding and encoding run off the authoritative server loop with a bounded read-ahead queue. Track completion leaves time for receiver buffers to drain; pause, stop, mute and disconnect flush playback promptly.

Desktop audio uses MonoGame's capture API and `DynamicSoundEffectInstance`; it needs the same working platform audio backend as game sound. Browser capture uses `getUserMedia` and an AudioWorklet. It requires HTTPS or localhost, microphone permission and a user gesture to unlock audio. Permission failures appear in Audio > Chat > Voice Status. Late permission grants after PTT release close their tracks, and blur/page hide release capture. Browser microphone labels are available after permission is granted.

## Testing

`VoiceChatTests` covers defaults/rebinding, settings normalization, capture gating, both wire containers and payload bounds, malformed input, loss recovery, sequence wrap/stalls, resampling, server authentication/team/gag/rate policy, and real Opus from synthetic capture to playback/mute/reset. It also exercises Last To Die opt-in, loss/retry/acknowledgment, stale membership changes, immediate leave, reconnect isolation, and independent Jukebox playback. `JukeboxTests` covers explicit line order, duplicates, skipped/unlisted entries, playlist edits, commands, late joining, pause/resume/skip/stop, WAV/MP3/OGG decoding, invalid playlists, automatic advancement and end-of-track draining.

```powershell
dotnet test Tests/OpenGarrison.PluginHost.Tests/OpenGarrison.PluginHost.Tests.csproj --filter 'FullyQualifiedName~VoiceChatTests|FullyQualifiedName~JukeboxTests|FullyQualifiedName~Protocol64MessageSchemaTests'
dotnet test Tests/OpenGarrison.Networking.Tests/OpenGarrison.Networking.Tests.csproj
cd Tests/BrowserSmoke
npm ci
npx playwright install chromium
npm run voice
```

The browser voice test uses Chromium's synthetic microphone and verifies real AudioWorklet capture, release, mono/stereo playback, volume, delayed permission, permission denial and cleanup. It does not record the operator's microphone. For release listening checks, use two clients on separate devices and verify intelligibility, device selection, focus loss, PTT release, team isolation and Jukebox transitions over each deployed transport.
