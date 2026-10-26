# Map scripting alongside nodes

Open **Map → Map script… (Lua)** in Garrison Builder. The editor supports typing,
multiline paste, selection/copy/cut, undo, line numbers, scrolling, syntax checks,
and an example. **Apply / Ctrl+S** stores the script in the editing document;
save the map normally afterwards. **Esc / Cancel** discards un-applied script
edits. Scripts travel with `.ogmap`, exported PNG maps, and map packages.

Every object's Properties now has **Name** and **Groups**. Names identify one
object and must be unique within the map. Groups are comma-separated, reusable
collections; an object may belong to several. Multi-select objects, right-click,
and use **Set groups…** to assign the same memberships to the selection. Cloning
a named object gives its copy a unique name and retains its groups. Internal IDs
remain stable behind the scenes and no longer appear as editable properties.

## Capture example

Assign the affected spawns, cabinets, barriers, and sprites to `red_spawn` and
`blue_spawn` as appropriate. This script replaces the equivalent capture wiring:

```lua
local red_spawn = group("red_spawn")
local blue_spawn = group("blue_spawn")

function on_start()
    red_spawn:enable()
    blue_spawn:disable()
end

function on_capture(point, team)
    if point == 1 and team == BLU then
        red_spawn:disable()
        blue_spawn:enable()
    elseif point == 1 and team == RED then
        red_spawn:enable()
        blue_spawn:disable()
    end
end
```

Control point numbers match the editor and start at **1**. `RED`, `BLU`/`BLUE`,
and `NEUTRAL` are string constants (`"red"`, `"blue"`, `"neutral"`). Scripts use
Lua functions, conditions, loops, local variables, tables, and persistent state
between events. Each round creates a fresh script environment.

## Events

| Function | When it runs |
| --- | --- |
| `on_start()` | Once when a map/round starts, after node initialization. |
| `on_capture(point, team)` | After an actual control point capture and its node evaluation. |
| `on_tick(dt)` | Every simulation tick; `dt` is the step in seconds. |
| `on_signal(name, active)` | Queued named-node output changes, delivered during a simulation tick. |

Top-level code runs before `on_start`, so it can create aliases and initialize
state. Local variables captured by event functions persist for the round.

## Objects, groups, and nodes

`object("door")` selects one named runtime object or spawn.
`group("spawn_blue")` selects every runtime object/spawn in that group. Both
lookups are case-insensitive and reject unknown names with a useful error.
Overlapping objects remain independent.

| Selection operation | Meaning |
| --- | --- |
| `:enable()` | Enable/show the selected objects; allow selected spawns. |
| `:disable()` | Disable/hide the selected objects; exclude selected spawns. |
| `:toggle()` | Invert each selected object's current active state. |
| `:clear()` | Remove the script override and return control to normal node/spawn rules. |
| `:is_enabled()` | True when every selected object is currently enabled. |
| `.count` | Number of selected runtime objects/spawns. |

A script override persists until another script action or `:clear()`. This makes
ownership explicit: a node cannot silently undo a script's disable on the next
tick. Existing nodes continue to evaluate; objects without script overrides use
the existing behavior. Disabling all spawn points defers respawning until a
spawn becomes available.

Give logic nodes names too. `map.pulse("open_door")` pulses a named node (raw
logic keys also work); `map.signal("open_door")` reads its output. The editor's
symbol list inserts `object(...)`, `group(...)`, or `map.pulse(...)` expressions.

```lua
function on_signal(name, active)
    if name == "alarm" and active then
        object("door"):disable()
        map.after(3, function()
            object("door"):clear()
        end)
    end
end
```

Other map functions:

- `map.owner(point)` returns `RED`, `BLU`, or `NEUTRAL`.
- `map.after(seconds, function)` schedules a one-shot callback in simulation time.
  A zero delay runs on a later tick; at most 128 timers may be pending.
- `map.score(team, amount)` changes a team's score (bounded to ±1000 per call).
- `map.end_round(team)` ends the round; omit the team for a draw.
- `map.log(...)` / `print(...)` records a bounded map-local diagnostic log.

Use named sprites, zones, barriers, cabinets, and spawn points for selection
actions. Other editor-only entities are controlled through their node signals.

## Errors, multiplayer, and limits

**Check syntax / Ctrl+Enter** compiles without running the script. Publishing
rejects syntax errors and duplicate object names. Quick test checks runtime
lookups/events; errors include `map.lua` line context, appear in the game console,
and are shown when reopening the script editor. An error stops that script for
the round; existing node logic continues.

Only the authoritative simulation runs Lua. Connected clients receive complete
scripted-object override state in snapshots, including late joiners and reset to
node control. Protocol **115** requires matching updated client/server builds.

The map API exposes map operations, not the filesystem, operating system, network,
or arbitrary CLR objects. Source is limited to 65,536 characters. Startup and
callbacks have instruction/time budgets. Native `table.sort`, `string.gsub`, and
`string.rep` are omitted from this map sandbox. The implementation uses
[MoonSharp's sandbox modules](https://www.moonsharp.org/sandbox.html) and
[preemptive coroutine execution](https://www.moonsharp.org/coroutines.html).
