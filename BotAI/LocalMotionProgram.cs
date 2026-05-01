namespace OpenGarrison.BotAI;

public enum LocalMotionStepKind
{
    TraverseToNode = 0,
    Idle = 1,
    Run = 2,
    Jump = 3,
    Drop = 4,
    Fall = 5,
}

public sealed record LocalMotionGoalWindow(
    float X,
    float Y,
    float RadiusX,
    float RadiusY,
    string Label = "");

public sealed record LocalMotionStep(
    LocalMotionStepKind Kind,
    int Direction = 0,
    int Ticks = 1,
    int TargetPointId = -1,
    float TargetX = 0f,
    float TargetY = 0f,
    string Label = "");

public sealed record LocalMotionProgram(
    string Label,
    IReadOnlyList<LocalMotionStep> Steps,
    int DurationTicks,
    int NoProgressTicks,
    int CooldownTicks = 0,
    bool AllowInertFastForward = false,
    bool ClearQueuedRouteOnFail = false,
    LocalMotionGoalWindow? GoalWindow = null);
