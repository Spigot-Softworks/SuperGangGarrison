using OpenGarrison.MLBot.Contracts;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace OpenGarrison.MLBot.Policies;

public sealed class ReturnIntelReplayOptionPolicyRuntime : IMLBotPolicyRuntime, IDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly IMLBotPolicyRuntime _basePolicy;
    private readonly ReplaySequence[] _sequences;
    private readonly float _engageDistance;
    private readonly float _maxSelectionScore;
    private readonly MLBotTaskPhase _taskPhase;
    private readonly bool _requiresCarryingIntel;
    private int _activeSequenceIndex = -1;
    private int _activeFrameIndex;

    public ReturnIntelReplayOptionPolicyRuntime(
        IMLBotPolicyRuntime basePolicy,
        string replayBankPath,
        float engageDistance,
        float maxSelectionScore = 0f)
        : this(
            basePolicy,
            replayBankPath,
            engageDistance,
            maxSelectionScore,
            MLBotTaskPhase.ReturnIntel,
            requiresCarryingIntel: true)
    {
    }

    public ReturnIntelReplayOptionPolicyRuntime(
        IMLBotPolicyRuntime basePolicy,
        string replayBankPath,
        float engageDistance,
        float maxSelectionScore,
        MLBotTaskPhase taskPhase,
        bool requiresCarryingIntel)
    {
        _basePolicy = basePolicy;
        _engageDistance = Math.Max(0f, engageDistance);
        _maxSelectionScore = Math.Max(0f, maxSelectionScore);
        _taskPhase = taskPhase;
        _requiresCarryingIntel = requiresCarryingIntel;
        _sequences = LoadSequences(replayBankPath, _engageDistance, _taskPhase, _requiresCarryingIntel).ToArray();
    }

    public MLBotAction Evaluate(in MLBotObservation observation)
    {
        if (_sequences.Length == 0
            || observation.TaskPhase != _taskPhase
            || (_requiresCarryingIntel && !observation.IsCarryingIntel)
            || !observation.Objective.HasObjective)
        {
            _activeSequenceIndex = -1;
            _activeFrameIndex = 0;
            return _basePolicy.Evaluate(observation);
        }

        if (_activeSequenceIndex < 0 && observation.ObjectiveDistance > _engageDistance)
        {
            return _basePolicy.Evaluate(observation);
        }

        if (_activeSequenceIndex < 0
            || _activeSequenceIndex >= _sequences.Length
            || _activeFrameIndex >= _sequences[_activeSequenceIndex].Frames.Length
            || !MatchesContext(observation, _sequences[_activeSequenceIndex].Context))
        {
            if (!TrySelectNearestFrame(observation, out _activeSequenceIndex, out _activeFrameIndex, out var selectionScore)
                || (_maxSelectionScore > 0f && selectionScore > _maxSelectionScore))
            {
                _activeSequenceIndex = -1;
                _activeFrameIndex = 0;
                return _basePolicy.Evaluate(observation);
            }
        }

        var activeSequence = _sequences[_activeSequenceIndex];
        var activeFrame = activeSequence.Frames[_activeFrameIndex];
        _activeFrameIndex += 1;

        var action = activeFrame.Action;
        return action with
        {
            AimWorldX = observation.Objective.WorldX,
            AimWorldY = observation.Objective.WorldY,
        };
    }

    public void Dispose()
    {
        if (_basePolicy is IDisposable disposable)
        {
            disposable.Dispose();
        }
    }

    private bool TrySelectNearestFrame(
        in MLBotObservation observation,
        out int sequenceIndex,
        out int frameIndex,
        out float selectionScore)
    {
        sequenceIndex = -1;
        frameIndex = 0;
        selectionScore = float.MaxValue;
        var bestDistance = float.MaxValue;

        for (var currentSequenceIndex = 0; currentSequenceIndex < _sequences.Length; currentSequenceIndex += 1)
        {
            var sequence = _sequences[currentSequenceIndex];
            if (!MatchesContext(observation, sequence.Context))
            {
                continue;
            }

            for (var currentFrameIndex = 0; currentFrameIndex < sequence.Frames.Length; currentFrameIndex += 1)
            {
                var frame = sequence.Frames[currentFrameIndex];
                var distance = ScoreDistance(observation, frame.Observation);
                if (distance >= bestDistance)
                {
                    continue;
                }

                sequenceIndex = currentSequenceIndex;
                frameIndex = currentFrameIndex;
                selectionScore = distance;
                bestDistance = distance;
            }
        }

        return sequenceIndex >= 0;
    }

    private static IEnumerable<ReplaySequence> LoadSequences(
        string replayBankPath,
        float engageDistance,
        MLBotTaskPhase taskPhase,
        bool requiresCarryingIntel)
    {
        var paths = ResolveReplayPaths(replayBankPath);
        foreach (var path in paths)
        {
            ReplayDocument? document;
            try
            {
                document = JsonSerializer.Deserialize<ReplayDocument>(File.ReadAllText(path), JsonOptions);
            }
            catch (JsonException)
            {
                continue;
            }
            catch (IOException)
            {
                continue;
            }

            if (document is null)
            {
                continue;
            }

            if (document.Steps.Length > 0 && document.Success)
            {
                foreach (var sequence in BuildSequencesFromSteps(document.Steps, engageDistance, taskPhase, requiresCarryingIntel))
                {
                    yield return sequence;
                }
            }

            if (document.Samples.Length > 0 && document.Metadata?.Success == true)
            {
                foreach (var sequence in BuildSequencesFromSamples(document.Samples, engageDistance, taskPhase, requiresCarryingIntel))
                {
                    yield return sequence;
                }
            }
        }
    }

    private static IEnumerable<ReplaySequence> BuildSequencesFromSteps(
        ReplayStep[] steps,
        float engageDistance,
        MLBotTaskPhase taskPhase,
        bool requiresCarryingIntel)
    {
        var frames = new List<ReplayFrame>();
        MLBotObservation? context = null;
        foreach (var step in steps)
        {
            AddFrame(step.Observation, step.Action, engageDistance, taskPhase, requiresCarryingIntel, frames, ref context);
        }

        if (frames.Count > 0 && context.HasValue)
        {
            yield return new ReplaySequence(context.Value, frames.ToArray());
        }
    }

    private static IEnumerable<ReplaySequence> BuildSequencesFromSamples(
        ReplaySample[] samples,
        float engageDistance,
        MLBotTaskPhase taskPhase,
        bool requiresCarryingIntel)
    {
        var frames = new List<ReplayFrame>();
        MLBotObservation? context = null;
        foreach (var sample in samples)
        {
            AddFrame(sample.Observation, sample.Action, engageDistance, taskPhase, requiresCarryingIntel, frames, ref context);
        }

        if (frames.Count > 0 && context.HasValue)
        {
            yield return new ReplaySequence(context.Value, frames.ToArray());
        }
    }

    private static void AddFrame(
        in MLBotObservation observation,
        in MLBotAction action,
        float engageDistance,
        MLBotTaskPhase taskPhase,
        bool requiresCarryingIntel,
        List<ReplayFrame> frames,
        ref MLBotObservation? context)
    {
        if (observation.TaskPhase != taskPhase
            || (requiresCarryingIntel && !observation.IsCarryingIntel)
            || !observation.Objective.HasObjective
            || observation.ObjectiveDistance > engageDistance)
        {
            return;
        }

        context ??= observation;
        frames.Add(new ReplayFrame(observation, action));
    }

    private static IEnumerable<string> ResolveReplayPaths(string replayBankPath)
    {
        if (File.Exists(replayBankPath))
        {
            yield return replayBankPath;
            yield break;
        }

        if (!Directory.Exists(replayBankPath))
        {
            yield break;
        }

        foreach (var path in Directory.EnumerateFiles(replayBankPath, "*.json", SearchOption.AllDirectories))
        {
            yield return path;
        }
    }

    private static float ScoreDistance(in MLBotObservation current, in MLBotObservation candidate)
    {
        var distance = 0f;
        distance += Squared((current.Objective.RelativeX - candidate.Objective.RelativeX) / 512f) * 4f;
        distance += Squared((current.Objective.RelativeY - candidate.Objective.RelativeY) / 512f) * 4f;
        distance += Squared((current.ObjectiveDistance - candidate.ObjectiveDistance) / 512f) * 2f;
        distance += Squared((current.BotX - candidate.BotX) / 512f) * 2f;
        distance += Squared((current.VelocityX - candidate.VelocityX) / 512f);
        distance += Squared((current.VelocityY - candidate.VelocityY) / 512f);
        distance += Squared((current.BotY - candidate.BotY) / 512f);
        distance += current.IsGrounded == candidate.IsGrounded ? 0f : 0.5f;
        distance += current.Probes.TouchingLeftWall == candidate.Probes.TouchingLeftWall ? 0f : 0.25f;
        distance += current.Probes.TouchingRightWall == candidate.Probes.TouchingRightWall ? 0f : 0.25f;
        distance += current.Probes.TouchingCeiling == candidate.Probes.TouchingCeiling ? 0f : 0.25f;
        distance += Squared((current.Probes.LeftFootObstacleDistance - candidate.Probes.LeftFootObstacleDistance) / 64f) * 0.5f;
        distance += Squared((current.Probes.RightFootObstacleDistance - candidate.Probes.RightFootObstacleDistance) / 64f) * 0.5f;
        distance += Squared((current.Probes.LeftGroundDistance - candidate.Probes.LeftGroundDistance) / 64f) * 0.25f;
        distance += Squared((current.Probes.RightGroundDistance - candidate.Probes.RightGroundDistance) / 64f) * 0.25f;
        return distance;
    }

    private static float Squared(float value) => value * value;

    private static bool MatchesContext(in MLBotObservation current, in MLBotObservation candidate)
    {
        return string.Equals(current.LevelName, candidate.LevelName, StringComparison.OrdinalIgnoreCase)
            && current.Team == candidate.Team
            && current.ClassId == candidate.ClassId;
    }

    private readonly record struct ReplaySequence(MLBotObservation Context, ReplayFrame[] Frames);

    private readonly record struct ReplayFrame(MLBotObservation Observation, MLBotAction Action);

    private sealed class ReplayDocument
    {
        public bool Success { get; set; }

        public ReplayMetadata? Metadata { get; set; }

        public ReplayStep[] Steps { get; set; } = [];

        public ReplaySample[] Samples { get; set; } = [];
    }

    private sealed class ReplayMetadata
    {
        public bool Success { get; set; }
    }

    private sealed class ReplayStep
    {
        public MLBotObservation Observation { get; set; }

        public MLBotAction Action { get; set; }
    }

    private sealed class ReplaySample
    {
        public MLBotObservation Observation { get; set; }

        public MLBotAction Action { get; set; }
    }
}
