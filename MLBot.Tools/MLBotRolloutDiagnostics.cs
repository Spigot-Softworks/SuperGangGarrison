using OpenGarrison.Core;
using OpenGarrison.MLBot.Contracts;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace OpenGarrison.MLBot.Tools;

internal static class MLBotRolloutDiagnostics
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };

    public static int AnalyzeRollout(string inputPath, string? outputPath, int stallWindow = 60, float improvementThreshold = 16f)
    {
        if (!File.Exists(inputPath))
        {
            Console.Error.WriteLine($"rollout file does not exist: {inputPath}");
            return 1;
        }

        var document = JsonSerializer.Deserialize<MLBotRolloutDocument>(File.ReadAllText(inputPath), JsonOptions);
        if (document is null || document.Steps.Length == 0)
        {
            Console.Error.WriteLine($"rollout file contained no steps: {inputPath}");
            return 1;
        }

        var summary = BuildSummary(document, stallWindow, improvementThreshold);
        Console.WriteLine(
            $"ticks={summary.TicksElapsed} success={summary.Success} terminal_reason={summary.TerminalReason} best_distance={summary.BestObjectiveDistance:0.0} best_tick={summary.BestObjectiveDistanceTick} stall_tick={(summary.StallTick?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "none")} likely_issue={summary.LikelyIssue}");

        if (!string.IsNullOrWhiteSpace(outputPath))
        {
            var directory = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllText(outputPath, JsonSerializer.Serialize(summary, JsonOptions));
            Console.WriteLine($"saved summary={outputPath}");
        }

        return 0;
    }

    private static MLBotRolloutDiagnosticSummary BuildSummary(MLBotRolloutDocument document, int stallWindow, float improvementThreshold)
    {
        var bestDistance = float.MaxValue;
        var bestTick = 0;
        var bestIndex = 0;
        var lastImprovementIndex = 0;
        var runningBest = float.MaxValue;
        int? stallIndex = null;

        for (var index = 0; index < document.Steps.Length; index += 1)
        {
            var distance = document.Steps[index].Observation.ObjectiveDistance;
            if (distance < bestDistance)
            {
                bestDistance = distance;
                bestTick = document.Steps[index].Tick;
                bestIndex = index;
            }

            if (distance < runningBest - improvementThreshold)
            {
                runningBest = distance;
                lastImprovementIndex = index;
            }

            if (index - lastImprovementIndex >= stallWindow)
            {
                stallIndex = lastImprovementIndex;
                break;
            }
        }

        var referenceIndex = stallIndex ?? bestIndex;
        var windowStart = Math.Max(0, referenceIndex);
        var windowEnd = Math.Min(document.Steps.Length, referenceIndex + stallWindow);
        var window = document.Steps[windowStart..windowEnd];
        var anchor = document.Steps[referenceIndex];

        var moveCounts = new Dictionary<int, int>();
        var jumpCount = 0;
        var crouchCount = 0;
        var moveChanges = 0;
        var zeroVelocityTicks = 0;
        var previousMove = window[0].Action.MoveDirection;
        var minX = float.MaxValue;
        var maxX = float.MinValue;
        var minY = float.MaxValue;
        var maxY = float.MinValue;

        foreach (var step in window)
        {
            moveCounts.TryGetValue(step.Action.MoveDirection, out var moveCount);
            moveCounts[step.Action.MoveDirection] = moveCount + 1;
            if (step.Action.Jump)
            {
                jumpCount += 1;
            }

            if (step.Action.Crouch)
            {
                crouchCount += 1;
            }

            if (Math.Abs(step.Observation.VelocityX) < 0.01f && Math.Abs(step.Observation.VelocityY) < 0.01f)
            {
                zeroVelocityTicks += 1;
            }

            if (step.Action.MoveDirection != previousMove)
            {
                moveChanges += 1;
                previousMove = step.Action.MoveDirection;
            }

            minX = MathF.Min(minX, step.Observation.BotX);
            maxX = MathF.Max(maxX, step.Observation.BotX);
            minY = MathF.Min(minY, step.Observation.BotY);
            maxY = MathF.Max(maxY, step.Observation.BotY);
        }

        var moveDirection = moveCounts
            .OrderByDescending(static pair => pair.Value)
            .ThenBy(static pair => pair.Key)
            .First().Key;

        var positionSpan = MathF.Sqrt(((maxX - minX) * (maxX - minX)) + ((maxY - minY) * (maxY - minY)));
        var likelyIssue = InferLikelyIssue(
            anchor,
            moveDirection,
            moveCounts,
            jumpCount,
            zeroVelocityTicks,
            moveChanges,
            positionSpan,
            window.Length);
        return new MLBotRolloutDiagnosticSummary
        {
            LevelName = document.LevelName,
            Team = document.Team,
            ClassId = document.ClassId,
            TaskPhase = document.TaskPhase,
            Success = document.Success,
            TerminalReason = document.TerminalReason,
            TicksElapsed = document.TicksElapsed,
            TotalReward = document.TotalReward,
            BestObjectiveDistance = bestDistance,
            BestObjectiveDistanceTick = bestTick,
            StallTick = stallIndex is null ? null : anchor.Tick,
            LikelyIssue = likelyIssue,
            StallObservation = anchor.Observation,
            StallAction = anchor.Action,
            StallWindowLength = window.Length,
            StallWindowMoveCounts = moveCounts,
            StallWindowJumpCount = jumpCount,
            StallWindowCrouchCount = crouchCount,
            StallWindowMoveChanges = moveChanges,
            StallWindowZeroVelocityTicks = zeroVelocityTicks,
            StallWindowPositionSpan = positionSpan,
        };
    }

    private static string InferLikelyIssue(
        MLBotRolloutStep anchor,
        int dominantMoveDirection,
        Dictionary<int, int> moveCounts,
        int jumpCount,
        int zeroVelocityTicks,
        int moveChanges,
        float positionSpan,
        int windowLength)
    {
        var probes = anchor.Observation.Probes;
        var mostlyStationary = zeroVelocityTicks >= Math.Max(10, (int)(windowLength * 0.7f));
        var barelyJumping = jumpCount <= Math.Max(1, windowLength / 20);
        var jumpSpamming = jumpCount >= Math.Max(8, windowLength / 3);
        var pushingIntoFootObstacle = dominantMoveDirection != 0 && probes.ForwardFootObstacleDistance <= 8f;
        var pushingIntoHeadObstacle = dominantMoveDirection != 0 && probes.ForwardHeadObstacleDistance <= 12f;
        var touchingWall = probes.TouchingLeftWall || probes.TouchingRightWall;
        var sameLocalPocket = positionSpan <= 128f;
        var broadLocalPocket = positionSpan <= 256f;
        var headRoomAvailable = probes.ForwardHeadObstacleDistance >= 24f;
        moveCounts.TryGetValue(0, out var idleCount);
        var mostlyIdle = idleCount >= Math.Max(10, (int)(windowLength * 0.6f));

        if (!anchor.Observation.IsGrounded && broadLocalPocket && mostlyIdle && jumpSpamming)
        {
            return "airborne_idle_jump_indecision";
        }

        if (sameLocalPocket && jumpSpamming && (pushingIntoFootObstacle || touchingWall))
        {
            return "wall_jump_loop_without_progress";
        }

        if (sameLocalPocket && jumpSpamming && pushingIntoHeadObstacle)
        {
            return "head_obstacle_jump_loop_without_progress";
        }

        if (sameLocalPocket && moveChanges >= Math.Max(6, windowLength / 8))
        {
            return "local_loop_without_progress";
        }

        if (anchor.Observation.IsGrounded && mostlyStationary && pushingIntoFootObstacle && headRoomAvailable && barelyJumping)
        {
            return "blocked_at_low_obstacle_without_jump";
        }

        if (mostlyStationary && mostlyIdle)
        {
            return "idle_after_progress_stall";
        }

        if (anchor.Observation.StuckTicks >= 30 && mostlyStationary)
        {
            return "repeated_input_without_progress";
        }

        return "no_clear_heuristic";
    }
}

internal sealed class MLBotRolloutDiagnosticSummary
{
    public string LevelName { get; set; } = string.Empty;

    public PlayerTeam Team { get; set; }

    public PlayerClass ClassId { get; set; }

    public MLBotTaskPhase TaskPhase { get; set; }

    public bool Success { get; set; }

    public string TerminalReason { get; set; } = string.Empty;

    public int TicksElapsed { get; set; }

    public float TotalReward { get; set; }

    public float BestObjectiveDistance { get; set; }

    public int BestObjectiveDistanceTick { get; set; }

    public int? StallTick { get; set; }

    public string LikelyIssue { get; set; } = string.Empty;

    public MLBotObservation StallObservation { get; set; }

    public MLBotAction StallAction { get; set; }

    public int StallWindowLength { get; set; }

    public Dictionary<int, int> StallWindowMoveCounts { get; set; } = [];

    public int StallWindowJumpCount { get; set; }

    public int StallWindowCrouchCount { get; set; }

    public int StallWindowMoveChanges { get; set; }

    public int StallWindowZeroVelocityTicks { get; set; }

    public float StallWindowPositionSpan { get; set; }
}
