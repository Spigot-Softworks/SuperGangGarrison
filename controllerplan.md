# Controller Support Plan (XInput)

Scope: ignore `Modern`; implement controller support in the existing MonoGame/KNI client path.

## Current Input Shape

- Gameplay input is currently built in `Client/Input/KeyboardInputMapper.cs`.
- `PlayerInputSnapshot` already carries `AimWorldX` and `AimWorldY`, so the simulation and network protocol can accept controller aim without new protocol fields.
- HUD crosshair/sniper charge rendering currently reads `MouseState`, so controller support needs an effective aim position shared by update and draw.
- `GamePad.GetState(PlayerIndex.One)` is only used for pause today.
- Menu controllers mostly use mouse hover/click, but they already store selected/hover indices that controller navigation can reuse.

## Design Goals

- Keep keyboard/mouse behavior unchanged.
- Treat controller aim as a client-side input translation into existing `PlayerInputSnapshot`.
- Use XInput-style defaults through MonoGame `GamePad` first.
- Avoid server/protocol changes.
- Add a controller-specific reticle mode: cursor or fading aim line.
- Add mild unscoped aim assist for direct joystick aim.
- Add standard controller menu navigation: analog stick/D-pad selection, confirm, back.

## Gameplay Mapping

Initial default mapping:

- Left stick/D-pad: movement.
- `A`: jump.
- `RT`: primary fire.
- `LT`: secondary fire/scope.
- `RB`: utility ability.
- `Y`: swap weapon.
- `X`: interact weapon.
- `Back`: scoreboard.
- `Start`: pause.
- `R3`: aim distance tier cycle.

## Aim Model

### Direct Aim

For non-scoped gameplay:

- Right stick vector maps directly to aim direction.
- The crosshair/aim endpoint stays at a fixed distance from the local player.
- Neutral right stick preserves the last valid aim direction.
- This matches virtual-joystick shooter behavior: stick direction chooses aim direction; it does not rotate the crosshair incrementally.

### R3 Distance Tiers

- `R3` cycles through three distance tiers: `0 -> 1 -> 2 -> 0`.
- Each tier increases the fixed aim endpoint distance.
- Longer distance gives finer angular control because the same screen-space offset covers less angle.
- Initial tunable values: 96, 160, 240 source pixels.

### Sniper

Unscoped sniper:

- Uses the same direct right-stick aim as other classes.
- Aim assist applies.

Scoped sniper:

- Uses precision aim mode.
- Keep an aim line.
- Horizontal facing is based on the last direct aim direction.
- Right stick vertical movement adjusts the line up/down like mouse-emulation precision control.
- Aim endpoint is computed from player position, horizontal scoped distance, and vertical offset.
- R3 distance tiers still affect precision sensitivity.

## Aim Assist

Apply only when:

- Controller mode is active.
- Right stick is engaged.
- Player is alive.
- Aim is direct/unscoped.

Behavior:

- Scan visible enemy players.
- Ignore dead players, allies, hidden spies, and blocked line-of-sight candidates.
- Pick the nearest candidate inside a small angular cone.
- Blend aim direction toward the candidate instead of snapping.
- Do not fire automatically.
- Do not apply to scoped sniper precision mode by default.

## Reticle Rendering

- Replace mouse-only crosshair drawing with an effective aim screen position.
- Controller reticle mode:
  - `Cursor`: draw existing crosshair at the controller aim endpoint.
  - `AimLine`: draw a white line from player to aim endpoint, fading near the end.
- Scoped sniper always uses the line, matching the requested precision behavior.
- Sniper charge HUD should follow the effective aim endpoint rather than mouse coordinates.

## Menu Navigation

Add reusable controller navigation primitives:

- D-pad and left stick up/down/left/right.
- `A` confirm.
- `B` back.
- Optional shoulder buttons for tab switching.
- One selection move per D-pad/stick press; stick must return through neutral before another slot change.

Apply first to:

- Main menu.
- In-game pause menu.
- Quit prompt.
- Options menu.
- Controls menu.
- Team select.
- Class select.
- Gameplay loadout menu.
- Plugin options.
- Lobby browser and manual connect.
- Practice setup and client powers.
- Last To Die and Jump setup.
- Friends menu basics.

## Settings

Persist through existing settings files:

- Controller mode: `Off`, `Auto`, `On`.
- Controller reticle mode: `Cursor`, `AimLine`.
- Aim assist enabled.
- Aim assist strength.
- Right-stick deadzone.
- Scoped precision vertical speed.
- Aim distance tier values.
- Rebindable gameplay controller buttons for jump, primary fire, secondary fire, utility ability, interact, swap weapon, scoreboard, pause, aim-distance cycle, change team, and change class. These belong on the Controller page of the Controls menu, not the Gameplay options page.

## Verification

Automated:

- Settings load/save.
- Controller aim math.
- R3 distance tier cycling.
- Deadzone behavior.
- Aim assist candidate choice.

Manual:

- Practice mode with every class.
- Sniper scoped and unscoped.
- Spy backstab direction.
- Demoman detonation.
- Pyro airblast.
- Medic healing beam.
- Online prediction edge behavior.
- Main menu, in-game menu, options, team/class selection, loadout.

## Execution Slices

1. Add controller settings, polling, and state helpers.
2. Add effective aim state and controller gameplay mapper.
3. Route gameplay input and local aim state through effective aim.
4. Add cursor/aim-line rendering.
5. Add minimal menu navigation.
6. Add aim assist.
7. Add options UI and tests.

## Execution Progress

Implemented:

- Controller settings and INI persistence.
- MonoGame `GamePad` polling with `Off` / `Auto` / `On` modes.
- Controller polling now scans all four MonoGame player slots and selects the active connected pad, so DS4/DualSense devices mapped by SDL/Steam Input/DS4Windows are not limited to `PlayerIndex.One`.
- Gameplay mapping for left stick/D-pad movement, `A` jump, `RT` primary, `LT` secondary/scope, `RB` utility, `X` interact, `Y` swap, `Back` scoreboard, `Start` pause.
- Right-stick direct aim with fixed distance tiers cycled by `R3`.
- Direct right-stick aim now uses magnitude-aware steering so tiny stick deflections no longer snap immediately to a full new direction.
- Right-stick aim now has a post-deadzone noise floor and hysteresis so minor stick noise does not jitter the cursor/aim line.
- Scoped sniper precision aim using vertical stick movement and controller-framed scoped camera.
- Stronger unscoped controller aim assist against visible enemies in line of sight, with a wider assist cone and more noticeable default strength.
- Effective aim routing for local weapon rendering, sniper HUD, crosshair, and fading aim line.
- Scout utility ability also follows the current controller swap-weapon binding, matching the requested scout special bind.
- Gameplay options rows for controller mode, reticle mode, aim assist, aim assist strength, right-stick deadzone, scoped precision speed, and all three aim-distance tiers.
- Controls menu now has Keyboard and Controller pages. Controller bindings use capture-style rebinding instead of left/right cyclic rebinding rows.
- Controller binding labels show PlayStation names alongside normalized MonoGame/Xbox names, such as `Cross / A`, `Square / X`, `L2 / LT`, and `Options / Start`.
- Baseline controller menu navigation for main menu, in-game menu, quit prompt, options, controls, plugin options, team select, class select, gameplay loadout, credits, Last To Die, Jump setup, manual connect, lobby browser, practice setup, client powers, debug menu, password prompt cancel, host setup back action, and basic friends menu navigation.
- Controller menus accept the primary-fire binding as confirm and the secondary-fire binding as back/no, so default RT confirms and default LT backs out while A/B still work.
- Menu navigation now advances one slot per press instead of repeating while held.
- In-game pause menu exposes Change Team and Change Class for controller users.
- Online team selection keeps the user in class selection instead of closing the selection UI while the team change is pending.
- In-game pause menu controller confirm is consumed after opening team/class selection so selecting class no longer immediately picks Scout.
- `L3` performs a very short down-and-slightly-behind aim flick and then returns to the previous controller aim direction.
- `L3+R3` sends taunt input and cancels any active L3 aim flick; the chord also suppresses the normal R3 aim-distance cycle while held.
- Right-stick opposite-direction flick can instantly reorient the controller crosshair when enabled, without requiring the stick to fully return to neutral between direction changes.
- Added `Flick to change directions` as a persisted controller gameplay setting.
- Settings/options, controls/plugin-options, host setup, practice setup, manual connect, and lobby browser overlays use a darker full-screen tint and draw over the main menu page so the underlying buttons/options dim behind the submenu.
- Focused controller settings tests for save/load and value normalization.

Still remaining:

- Automated coverage for controller aim math, R3 distance tier cycling, deadzone behavior, and aim-assist target selection. These need either extracted pure helpers or a controllable `GamePadState`/world harness.
- Manual XInput QA across all classes, especially scoped sniper, spy facing/backstab direction, medic beam behavior, pyro airblast, demoman detonation, and online prediction edges.
- Manual QA for the new L3 aim flick, L3+R3 taunt chord, and right-stick opposite-direction flick on physical XInput controllers.
- Manual menu QA with a physical controller. Current pass wires the primary menus and common overlays; bespoke editors such as the custom bubble pixel editor remain keyboard/mouse-first.
- Confirm whether the default button mapping should expose taunt/call medic/bubble menu shortcuts on controller, or keep the first pass limited to combat/menu essentials.
- Manual visual QA is still needed for the settings, practice, host, lobby browser, and manual connect overlay tint on both static and animated menu backgrounds.

## Verification Results

Passed:

- `dotnet build Client\OpenGarrison.Client.csproj --no-restore`
- `dotnet test Tests\OpenGarrison.PluginHost.Tests\OpenGarrison.PluginHost.Tests.csproj --no-restore --filter ControllerInputSettingsTests`

Notes:

- Build and test warnings are pre-existing analyzer/nullability warnings in unrelated projects/files.
- No `Modern` folder investigation or edits were performed.
