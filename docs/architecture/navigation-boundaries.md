# Navigation Boundaries

These are target ownership rules for bot navigation.

## Provider-only dependency

- **Rule:** `BotBrainController` and `BotBrainPracticeBotController` depend only on `INavigationGraphProvider` for navigation graphs.
- **Rationale:** Controllers should consume navigation capability without knowing how graphs are built, stored, or selected.
- **Violation:** A controller constructing an `Og2NavigationGraphBuilder` or opening an `Og2NavigationGraphCache` itself.

## Provider-owned resolution

- **Rule:** Graph builder selection, cache lookup, version/fingerprint compatibility, and resolution-source knowledge live behind `INavigationGraphProvider`.
- **Rationale:** Centralizing resolution policy keeps cache and format evolution out of bot behavior code.
- **Violation:** A controller comparing map fingerprints, checking cache format versions, or choosing between cached and freshly built graphs.

## Hidden implementation details

- **Rule:** Controllers must not reference `Og2NavigationGraphBuilder`, `Og2NavigationGraphCache`, fingerprints, or cache format versions.
- **Rationale:** Removing implementation knowledge lets the provider change storage and compatibility rules without controller changes.
- **Violation:** Adding a controller field or conditional for `Og2NavigationGraphCache.CurrentFormatVersion`.
