namespace OpenGarrison.Server;

internal readonly record struct ServerBotRuntimeMetrics(
    bool HasMeasurements,
    int ControlledBotCount,
    int ActiveInputCount,
    int ZeroInputCount,
    int RefreshedInputCount,
    int ReusedInputCount,
    int BotBrainActiveControllerCount,
    int BotBrainNavigationLoadedCount,
    int BotBrainNavigationMissingCount,
    int BotBrainObjectiveTapeLoadedCount,
    int BotBrainActivePathCount,
    int SampleCount,
    double LastBuildInputMilliseconds,
    double AverageBuildInputMilliseconds,
    double MaxBuildInputMilliseconds,
    double LastApplyInputMilliseconds,
    double AverageApplyInputMilliseconds,
    double MaxApplyInputMilliseconds);
