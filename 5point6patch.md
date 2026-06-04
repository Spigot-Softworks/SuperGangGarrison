# 5.6 Patch Blueprint

## Scope

This patch focuses on presentation polish and client-side feedback:

- Append `(Alpha)` to the visible build/version label everywhere it is shown.
- Add a small loading overlay for connecting, replay/demo loading, and custom map download/sync.
- Relocate and wire the vote sound/graphic assets from the repository root.
- Add gib/player `Splat` feedback and corpse ground-impact sound feedback.
- Add client-side dynamic music events, with a settings-menu toggle to disable the system.

## Asset Placement

Runtime assets belong under `Core/Content`, because the desktop client copies that tree to `Content/**` and the browser project mirrors it to `wwwroot/Content/**`.

Sounds should live in `Core/Content/Sounds` with matching XML metadata, except looped music tracks which should live in `Core/Content/Sounds/Music`.

Voting graphics should live under a HUD sprite folder such as `Core/Content/Sprites/HUDs/Voting`, with GameMaker-style sprite XML plus `.images/image 0.png` payloads.

## Version Label

`GetApplicationVersionLabel()` is shared by the corner overlay and settings menu. The suffix should be appended there as display formatting only, so updater/version parsing behavior stays unchanged.

Target behavior:

- `1.2.3` displays as `1.2.3 (Alpha)`.
- Existing labels that already end in `(Alpha)` are not double-suffixed.
- Development fallback displays as `dev (Alpha)`.

## Loading Overlay

The current client mostly reports joining/map sync status through `_menuStatusMessage`. Add a dedicated bottom-right overlay using the Faucet updater visual language:

- dark background, thin border, white title/message text, segmented progress bar.
- title text changes from updater language to client activity language, such as `Smoke - Loading`.
- progress supports determinate `0..1` and indeterminate animated segments.

Integration points:

- connection start: show `Connecting...`.
- replay/demo start: show `Loading replay...` / `Loading demo...`.
- custom map sync: show `Downloading custom map...`, `Verifying custom map...`, `Loading map...`.
- clear on gameplay entry or failure.

Desktop custom-map sync currently blocks through the synchronous path. Convert it to the async path used by browser, and add progress reporting in `CustomMapSyncService` so the overlay updates during downloads.

## Voting Presentation

The server chat voting plugin currently broadcasts only system text. Add a structured vote event path so the client can show graphics and play sounds without parsing chat strings.

Events:

- `started`: play `voteengage`, show panel.
- `yes`: play `voteyes`, update yes count.
- `no`: play `voteno`, update no count.
- `passed`: play `votesuccess`, fade success panel.
- `failed`, `expired`, `canceled`: play `votefail`, fade failure panel.

Implementation preference:

- Update the server voting plugin to broadcast plugin messages using the existing server-plugin message transport.
- Add a built-in client handler that consumes `chat.voting` messages and drives a native HUD/presentation controller.
- Keep system chat messages for compatibility.

## Gore Feedback

`Splat` already exists under `Core/Content/Sounds`; `ImpactSnd` needs to be added.

Gibs:

- During `AdvancePlayerGibs`, detect overlap with alive players.
- Apply a short impulse in the overlapping player's movement direction.
- Emit `Splat` with a per-gib/player cooldown to prevent sound spam.

Corpses:

- Have `DeadBodyEntity.Advance` report vertical landing impact.
- Emit `ImpactSnd` from `AdvanceDeadBodies` when downward impact speed exceeds a threshold.
- Use a one-shot or cooldown guard so resting corpses do not repeat the impact sound.

## Dynamic Music

Add a client-side dynamic music controller that fades between normal in-game music and event loops.

Settings:

- Add `Dynamic Music` to the Audio settings tab.
- Persist it in client preferences.
- When disabled, stop event loops and keep normal in-game music behavior.

States and priority:

1. Nearby uber: loop `uber_common`.
2. Intelligence carried: loop `menumusic4`.
3. Local player enemy combat: loop `actionmusic`.
4. Normal: regular in-game music.

Combat rules:

- Dealing damage to an enemy or taking damage from an enemy activates combat music.
- Self damage does not count.
- Same-team damage does not count.
- Combat state expires after roughly 8 seconds without qualifying damage.

Music behavior:

- Keep event tracks looped and fade volumes smoothly.
- Fade normal in-game music down while an event track is active, then fade it back up afterward.
- Respect global mute and in-game music volume.
- Stop event music on menu, round end, match end, or when in-game music is disabled.

## Validation

Recommended test coverage:

- Asset import smoke tests for new sound/sprite metadata.
- Version label suffix tests.
- Dynamic music state resolver tests.
- Custom map sync progress callback tests where practical.
- Simulation tests for corpse impact threshold/cooldown and gib splat cooldown/impulse.

Manual verification:

- Desktop and browser custom map download/loading overlay.
- In-game vote panel and sounds.
- Gib splat behavior near moving players.
- Corpse impact sound on landing.
- Dynamic music fades, priority, expiration, and settings toggle.
