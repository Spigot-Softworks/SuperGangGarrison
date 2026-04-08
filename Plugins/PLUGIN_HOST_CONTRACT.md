# Plugin Host Contract

This document defines the rules the OpenGarrison plugin hosts are expected to enforce.

It is written for engine contributors who are adding new seams, extending existing ones, or reviewing whether a plugin capability belongs in Lua, CLR, or the engine itself.

## Goals

- Keep the client and server stable when plugin code misbehaves.
- Make Lua the normal plugin path without giving scripts raw ownership of engine internals.
- Keep plugin APIs reusable, bounded, and consistent across features.
- Preserve frame-time and tick-time budgets as engine-owned constraints.

## Non-Goals

- Plugins are not trusted engine peers.
- Lua is not a license to expose arbitrary internal objects.
- The host does not promise that every engine feature can or should become a plugin seam.

## Core Rules

### 1. The engine owns execution budgets

- Plugin callbacks run on engine-owned budgets, not plugin-owned budgets.
- Draw hooks, query hooks, and per-frame or per-tick hooks must stay small.
- Long-running plugin work must be split across multiple update callbacks or moved behind a different engine-managed workflow.
- If a plugin exceeds its callback budget or repeatedly faults, the host may disable it for the rest of the session.

### 2. The engine owns lifecycle and phase safety

- The host defines which operations are legal during initialize, lifecycle, update, draw, query, and shutdown phases.
- Expensive or stateful resource creation must only be allowed in phases that are explicitly safe for it.
- Draw and query callbacks are for presentation and read-mostly work, not opportunistic loading or engine mutation.

### 3. The host exposes services, not engine internals

- Prefer callbacks, DTOs, validated commands, and narrow helper services.
- Do not expose raw engine object graphs, renderer internals, simulation internals, or mutable state containers directly to Lua.
- If a seam would require Lua to understand unstable engine ownership rules, the seam is not ready.

### 4. File access stays inside declared plugin roots

- Plugin config APIs must stay inside the plugin config directory.
- Plugin asset and file enumeration APIs must stay inside the plugin directory.
- A plugin must not be able to escape those roots through relative path tricks.

### 5. Resource ownership stays with the host

- The host is responsible for creating, tracking, and disposing plugin-owned resources.
- If an API creates textures, sounds, atlases, or similar assets, the host must define who owns them and when they are cleaned up.
- Replacement registration must not leak the old resource.

### 6. New seams should be reusable and engine-shaped

- Add a seam when the capability is likely to serve more than one plugin.
- Add a seam when the host can describe the capability in stable gameplay, rendering, UI, audio, state, or messaging terms.
- Do not add a seam that only mirrors one internal call site or one plugin's private workaround.

## Callback Design Rules

When adding a new callback:

- Define whether it is lifecycle, update, draw, query, input, or interaction.
- Decide its budget before exposing it.
- Decide whether it is allowed to allocate resources, load assets, or mutate host state.
- Define what happens if it throws or times out.
- Add a test that exercises both the intended path and the forbidden path.

Recommended bias:

- Lifecycle callbacks: setup and teardown only.
- Update callbacks: incremental work and lightweight state changes.
- Draw callbacks: drawing only.
- Query callbacks: fast reads only.
- Menu or options interactions: bounded host-mediated commands only.

## Asset And Rendering Rules

- Plugin asset registration should go through host-managed registries.
- GPU resource creation must not be legal from arbitrary draw callbacks.
- If a plugin needs preload behavior, add a preload-safe host path instead of telling plugin authors to load lazily during rendering.
- Legacy compatibility seams should be held to the same safety rules as newer seams.

## Error Handling Rules

- A plugin callback failure should be logged with the plugin id, callback name, and phase.
- Repeatedly running a known-bad plugin callback is not a valid recovery strategy.
- If a plugin crosses an execution budget or fails in a way that leaves the session unsafe, disable it and continue running the host.
- Host logs should explain why an operation was rejected so plugin authors can fix it without digging through engine code.

## Messaging And Compatibility Rules

- Plugin messaging uses a host-owned compatibility header, not ad hoc plugin-defined negotiation.
- The host-owned header is the engine-controlled tuple of source plugin id, target plugin id, message type, payload format, and schema version.
- Plugins should treat that header as the canonical compatibility surface for routing and version checks.
- A plugin message contract should be described in terms of target plugin id, message type, payload format, and supported schema range.
- Hosts should reject malformed or unsupported compatibility headers before a plugin callback is invoked.
- Hosts should route addressed plugin messages only to the intended loaded plugin, not broadcast them across the plugin stack.
- Client-originated source plugin ids are useful for compatibility and diagnostics, but they are not security claims by themselves.
- If richer cross-plugin traffic is needed later, extend the host-owned compatibility header first instead of inventing plugin-specific negotiation patterns.

## Lua-Specific Rules

- Lua is the default plugin language.
- Lua seams should be bounded enough that plugin authors do not need engine-source knowledge just to use them safely.
- Do not solve missing Lua capability by reflexively approving a CLR plugin.
- If Lua needs a new power, prefer a reusable host API over direct raw exposure.

## CLR Exception Path

A CLR seam or CLR-only plugin is justified only when one of the following is true:

- The capability depends on engine-private ownership that should not be surfaced to Lua.
- The capability requires platform or native interop.
- The capability is performance-critical in a way that a bounded Lua seam cannot satisfy.
- The capability would materially damage the clarity or safety of the Lua host if exposed there.

"Lua cannot do it yet" is not enough by itself.

## Checklist For New Seams

Before merging a new seam, confirm all of the following:

- The seam has a clear owner in the engine code.
- The seam has a defined callback phase or service boundary.
- Illegal call timing is rejected by the host.
- File and asset paths are contained.
- Resource lifetime is defined.
- Faults are logged clearly.
- Timeout or budget behavior is understood.
- At least one regression test covers the intended use.
- At least one regression test covers the forbidden or failure case.

## Current Practical Limit

The current Lua hosts are in-process hosts, not out-of-process sandboxes.

That means the host can budget and quarantine Lua execution, but it still cannot preempt arbitrary long-running native or CLR work once a callback has entered it. New seams should be designed with that limit in mind.

There are two different kinds of “bad plugin behavior”:

Bad pure Lua behavior
Example: an infinite Lua loop, or a Lua callback doing way too much work.
We now have decent protection here, because MoonSharp can be forced to yield and the host can time-budget/quarantine it.

Bad CLR/native behavior reached from Lua
Example: Lua calls into a host API, and that host API blocks in file I/O, GPU upload, texture creation, a driver call, a native library call, or some future heavy engine routine.
Once execution has crossed into that CLR/native code, MoonSharp’s coroutine budget cannot interrupt it. The engine thread is just stuck there until that call returns.
