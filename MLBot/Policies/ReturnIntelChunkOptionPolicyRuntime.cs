using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using OpenGarrison.Core;
using OpenGarrison.MLBot.Contracts;

namespace OpenGarrison.MLBot.Policies;

public sealed class ReturnIntelChunkOptionPolicyRuntime : IMLBotPolicyRuntime, IDisposable
{
    public const string DefaultObservationInputName = "obs";
    public const string DefaultMoveLogitsOutputName = "chunk_move_logits";
    public const string DefaultBinaryLogitsOutputName = "chunk_binary_logits";
    public const string DefaultAimOutputName = "chunk_aim";

    private readonly IMLBotPolicyRuntime _basePolicy;
    private readonly InferenceSession _session;
    private readonly string _observationInputName;
    private readonly string _moveLogitsOutputName;
    private readonly string _binaryLogitsOutputName;
    private readonly string _aimOutputName;
    private readonly MLBotObservationVectorSchema _observationSchema;
    private readonly MLBotTaskPhase _taskPhase;
    private readonly bool _requiresCarryingIntel;
    private readonly bool _latchAcrossChunks;
    private readonly bool? _requiredIsGrounded;
    private readonly float _minEngageDistance;
    private readonly float _engageDistance;
    private readonly int _maxCommitTicks;
    private readonly string? _levelNameFilter;
    private readonly PlayerTeam? _teamFilter;
    private readonly PlayerClass? _classFilter;
    private readonly float? _minObjectiveRelativeX;
    private readonly float? _maxObjectiveRelativeX;
    private readonly float? _minObjectiveRelativeY;
    private readonly float? _maxObjectiveRelativeY;
    private MLBotAction[] _activeChunk = [];
    private int _activeIndex;
    private bool _latched;

    public ReturnIntelChunkOptionPolicyRuntime(
        IMLBotPolicyRuntime basePolicy,
        string modelPath,
        float engageDistance,
        int maxCommitTicks = 0,
        string? levelNameFilter = null,
        PlayerTeam? teamFilter = null,
        PlayerClass? classFilter = null,
        float? minObjectiveRelativeX = null,
        float? maxObjectiveRelativeX = null,
        float? minObjectiveRelativeY = null,
        float? maxObjectiveRelativeY = null,
        MLBotTaskPhase taskPhase = MLBotTaskPhase.ReturnIntel,
        bool requiresCarryingIntel = true,
        bool latchAcrossChunks = true,
        bool? requiredIsGrounded = null,
        float minEngageDistance = 0f,
        string observationInputName = DefaultObservationInputName,
        string moveLogitsOutputName = DefaultMoveLogitsOutputName,
        string binaryLogitsOutputName = DefaultBinaryLogitsOutputName,
        string aimOutputName = DefaultAimOutputName)
    {
        _basePolicy = basePolicy;
        _session = new InferenceSession(modelPath);
        _engageDistance = Math.Max(0f, engageDistance);
        _maxCommitTicks = Math.Max(0, maxCommitTicks);
        _levelNameFilter = string.IsNullOrWhiteSpace(levelNameFilter) ? null : levelNameFilter;
        _teamFilter = teamFilter;
        _classFilter = classFilter;
        _minObjectiveRelativeX = minObjectiveRelativeX;
        _maxObjectiveRelativeX = maxObjectiveRelativeX;
        _minObjectiveRelativeY = minObjectiveRelativeY;
        _maxObjectiveRelativeY = maxObjectiveRelativeY;
        _taskPhase = taskPhase;
        _requiresCarryingIntel = requiresCarryingIntel;
        _latchAcrossChunks = latchAcrossChunks;
        _requiredIsGrounded = requiredIsGrounded;
        _minEngageDistance = Math.Max(0f, minEngageDistance);
        _observationInputName = observationInputName;
        _moveLogitsOutputName = moveLogitsOutputName;
        _binaryLogitsOutputName = binaryLogitsOutputName;
        _aimOutputName = aimOutputName;
        _observationSchema = ResolveObservationSchema();
    }

    public MLBotAction Evaluate(in MLBotObservation observation)
    {
        if (!CanRunChunkOption(observation))
        {
            ResetChunk();
            return _basePolicy.Evaluate(observation);
        }

        if (_activeIndex >= _activeChunk.Length)
        {
            if (!_latchAcrossChunks)
            {
                _latched = false;
            }

            if (!_latched && !ShouldStartChunkOption(observation))
            {
                ResetChunk();
                return _basePolicy.Evaluate(observation);
            }

            _latched = true;
            _activeChunk = PredictChunk(observation);
            _activeIndex = 0;
        }

        if (_activeChunk.Length == 0)
        {
            return _basePolicy.Evaluate(observation);
        }

        var action = _activeChunk[_activeIndex];
        _activeIndex += 1;
        return action;
    }

    public void Dispose()
    {
        _session.Dispose();
        if (_basePolicy is IDisposable disposable)
        {
            disposable.Dispose();
        }
    }

    private bool CanRunChunkOption(in MLBotObservation observation)
    {
        return observation.TaskPhase == _taskPhase
            && (!_requiresCarryingIntel || observation.IsCarryingIntel)
            && observation.Objective.HasObjective;
    }

    private bool ShouldStartChunkOption(in MLBotObservation observation)
    {
        return CanRunChunkOption(observation)
            && (_levelNameFilter is null || string.Equals(observation.LevelName, _levelNameFilter, StringComparison.OrdinalIgnoreCase))
            && (!_teamFilter.HasValue || observation.Team == _teamFilter.Value)
            && (!_classFilter.HasValue || observation.ClassId == _classFilter.Value)
            && (!_minObjectiveRelativeX.HasValue || observation.Objective.RelativeX >= _minObjectiveRelativeX.Value)
            && (!_maxObjectiveRelativeX.HasValue || observation.Objective.RelativeX <= _maxObjectiveRelativeX.Value)
            && (!_minObjectiveRelativeY.HasValue || observation.Objective.RelativeY >= _minObjectiveRelativeY.Value)
            && (!_maxObjectiveRelativeY.HasValue || observation.Objective.RelativeY <= _maxObjectiveRelativeY.Value)
            && (!_requiredIsGrounded.HasValue || observation.IsGrounded == _requiredIsGrounded.Value)
            && (_minEngageDistance <= 0f || observation.ObjectiveDistance >= _minEngageDistance)
            && (_engageDistance <= 0f || observation.ObjectiveDistance <= _engageDistance);
    }

    private void ResetChunk()
    {
        _activeChunk = [];
        _activeIndex = 0;
        _latched = false;
    }

    private MLBotAction[] PredictChunk(in MLBotObservation observation)
    {
        var featureVector = MLBotFeatureVectorizer.Vectorize(observation, _observationSchema);
        var observationTensor = new DenseTensor<float>(featureVector, [1, featureVector.Length]);
        var inputs = new List<NamedOnnxValue>
        {
            NamedOnnxValue.CreateFromTensor(_observationInputName, observationTensor),
        };

        using var results = _session.Run(inputs);
        var outputs = results.ToDictionary(static result => result.Name, StringComparer.OrdinalIgnoreCase);
        var moveLogits = ReadOutput(outputs, _moveLogitsOutputName, "move_logits").AsEnumerable<float>().ToArray();
        var binaryLogits = ReadOutput(outputs, _binaryLogitsOutputName, "binary_logits").AsEnumerable<float>().ToArray();
        var aim = ReadOutput(outputs, _aimOutputName, "aim").AsEnumerable<float>().ToArray();
        var chunkLength = Math.Min(moveLogits.Length / 3, Math.Min(binaryLogits.Length / 5, aim.Length / 2));
        if (_maxCommitTicks > 0)
        {
            chunkLength = Math.Min(chunkLength, _maxCommitTicks);
        }

        if (chunkLength <= 0)
        {
            return [];
        }

        var actions = new MLBotAction[chunkLength];
        for (var index = 0; index < chunkLength; index += 1)
        {
            var moveIndex = ArgMax(moveLogits, index * 3, 3);
            var moveDirection = moveIndex switch
            {
                0 => -1,
                2 => 1,
                _ => 0,
            };
            var aimOffsetX = Math.Clamp(aim[(index * 2) + 0], -1f, 1f);
            var aimOffsetY = Math.Clamp(aim[(index * 2) + 1], -1f, 1f);
            actions[index] = new MLBotAction(
                MoveDirection: moveDirection,
                Jump: ReadBinary(binaryLogits, index, 0),
                Crouch: ReadBinary(binaryLogits, index, 1),
                FirePrimary: ReadBinary(binaryLogits, index, 2),
                FireSecondary: ReadBinary(binaryLogits, index, 3),
                DropIntel: ReadBinary(binaryLogits, index, 4),
                AimWorldX: observation.BotX + (aimOffsetX * MLBotFeatureVectorizer.DistanceScale),
                AimWorldY: observation.BotY + (aimOffsetY * MLBotFeatureVectorizer.DistanceScale));
        }

        return actions;
    }

    private MLBotObservationVectorSchema ResolveObservationSchema()
    {
        if (_session.InputMetadata.TryGetValue(_observationInputName, out var metadata))
        {
            var dimensions = metadata.Dimensions.ToArray();
            if (dimensions.Length >= 2
                && dimensions[^1] > 0
                && MLBotFeatureVectorizer.TryResolveSchema(dimensions[^1], out var schema))
            {
                return schema;
            }
        }

        return MLBotObservationVectorSchema.V6;
    }

    private static bool ReadBinary(float[] logits, int frameIndex, int binaryIndex)
    {
        var index = (frameIndex * 5) + binaryIndex;
        return index < logits.Length && logits[index] > 0f;
    }

    private static DisposableNamedOnnxValue ReadOutput(
        IReadOnlyDictionary<string, DisposableNamedOnnxValue> outputs,
        string primaryName,
        string fallbackName)
    {
        if (outputs.TryGetValue(primaryName, out var primaryOutput))
        {
            return primaryOutput;
        }

        if (outputs.TryGetValue(fallbackName, out var fallbackOutput))
        {
            return fallbackOutput;
        }

        throw new InvalidOperationException(
            $"Chunk model did not expose '{primaryName}' or '{fallbackName}'. Available outputs: {string.Join(", ", outputs.Keys)}");
    }

    private static int ArgMax(float[] values, int start, int count)
    {
        var bestIndex = 0;
        var bestValue = values[start];
        for (var index = 1; index < count; index += 1)
        {
            var value = values[start + index];
            if (value <= bestValue)
            {
                continue;
            }

            bestValue = value;
            bestIndex = index;
        }

        return bestIndex;
    }
}
