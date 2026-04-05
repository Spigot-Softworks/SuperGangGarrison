# Contributing

## General Guidelines

- Keep changes focused and reviewable.
- Do not commit generated `bin/`, `obj/`, or `dist/` output.
- Preserve existing packaging behavior unless a change explicitly targets distribution.

## Architecture Priorities

- `Core` owns gameplay rules and simulation behavior.
- `Client` should consume and present simulation state rather than redefining gameplay rules.
- `Server` should remain the authoritative multiplayer runtime around the shared simulation.
- `Protocol` changes are high-impact and should be coordinated with both client and server behavior.

## Build and Verification

Typical local verification:

```powershell
dotnet build .\OpenGarrison.sln -c Debug --nologo
```

If a change touches packaging or distribution behavior, also validate the relevant packaging command from [packaging/DISTRO_QUICKSTART.txt](/Users/level/Desktop/OpenGarrison%20Active/OpenGarrison-Fork/packaging/DISTRO_QUICKSTART.txt).

## Release and Packaging

- Be cautious around cross-platform launch and archive behavior in `scripts/package.ps1` and `scripts/build.sh`.
- Preserve the packaged layout expected by the current release process.
