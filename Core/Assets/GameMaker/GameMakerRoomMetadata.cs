using System.Collections.Generic;

namespace OpenGarrison.Core;

public sealed record GameMakerRoomMetadata(
    string Name,
    WorldBounds Bounds,
    string PrimaryBackgroundAssetName,
    IReadOnlyList<SpawnPoint> RedSpawns,
    IReadOnlyList<SpawnPoint> BlueSpawns,
    IReadOnlyList<IntelBaseMarker> IntelBases,
    IReadOnlyList<RoomObjectMarker> RoomObjects,
    IReadOnlyList<float> AreaBoundaries)
{
    public IReadOnlyList<AreaTransitionMarker> AreaTransitionMarkers { get; init; } = Array.Empty<AreaTransitionMarker>();

    public IReadOnlyList<string> UnsupportedEntities { get; init; } = Array.Empty<string>();

    public CustomMapVisualMetadata CustomMapVisuals { get; init; } = CustomMapVisualMetadata.Empty;

    public CustomMapControlPointSettings ControlPointSettings { get; init; } = CustomMapControlPointSettings.Default;

    public MapLogicGraph LogicGraph { get; init; } = MapLogicGraph.Empty;

    public MapLogicActivatorSet LogicActivators { get; init; } = MapLogicActivatorSet.Empty;

    public CustomMapScrSettings ScrSettings { get; init; } = CustomMapScrSettings.Default;

    public bool ShowControlPoints { get; init; }

    public MapLogicScoreTriggerSet LogicScoreTriggers { get; init; } = MapLogicScoreTriggerSet.Empty;

    public SpritesheetPlaybackSet SpritesheetPlaybackSet { get; init; } = SpritesheetPlaybackSet.Empty;

    public GameModeKind? ExplicitGameMode { get; init; }
}
