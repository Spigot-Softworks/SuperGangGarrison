namespace OpenGarrison.Server;

internal readonly record struct ServerBotRuntimeMetrics(
    bool HasMeasurements,
    int ControlledBotCount,
    int SampleCount,
    double LastBuildInputMilliseconds,
    double AverageBuildInputMilliseconds,
    double MaxBuildInputMilliseconds,
    double LastApplyInputMilliseconds,
    double AverageApplyInputMilliseconds,
    double MaxApplyInputMilliseconds);
