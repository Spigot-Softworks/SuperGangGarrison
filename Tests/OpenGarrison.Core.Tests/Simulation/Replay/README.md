# Deterministic simulation replay harness

This harness drives `SimulationWorld` with fixed, headless input scripts and
captures a complete protocol `SnapshotMessage` after every simulation tick. It
proves that the simulation and snapshot conversion path are deterministic, and
that the serialized protocol bytes remain stable enough to compare a
pre-refactor recording with a refactored replay.

## RECORD and VERIFY modes

Normal test runs are VERIFY mode: the three scenarios compare their per-tick
SHA-256 hashes with the checked-in files in `Goldens/`. To record or replace
the files, run:

```sh
REPLAY_RECORD=1 dotnet test --filter "FullyQualifiedName~Replay"
```

The tests always run each scenario twice in-process before the golden compare,
and the Movement scenario also checks the first sixteen snapshots with a
serialize/deserialize/serialize byte round-trip.

## Golden lifecycle

Goldens are platform-independent: Core simulation math goes through
`DeterministicMath` instead of `MathF`, whose transcendental functions defer to
the OS C runtime and differ by an ulp between Windows and Linux. Record on any
machine with `REPLAY_RECORD=1`; CI on Linux verifies the same files. A
mismatch means behavior or snapshot serialization changed; the first differing
tick in the failure localizes the investigation.

If a gameplay or protocol change intentionally changes the replay, record new
goldens, review the diff, and commit the updated files with that change.

## SGG-Cray0nn porting note

On `SGG-Cray0nn`, `SnapshotCapture.cs` changes its static
`ServerHelpers.ToSnapshotX(...)` calls to `world.Snapshots.ToSnapshotX(...)`
instance calls, following the signatures in
`Core/Simulation/Systems/SnapshotSystem.cs`. The Server project reference can
then be removed and the Server `InternalsVisibleTo("OpenGarrison.Core.Tests")`
attribute reverted. The scenarios, replay tests, goldens, and CI workflow
remain otherwise unchanged.
