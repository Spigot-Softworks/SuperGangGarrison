using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using OpenGarrison.MLBot.Contracts;

namespace OpenGarrison.MLBot.Policies;

public sealed class ReturnIntelChunkSelectorPolicyRuntime : IMLBotPolicyRuntime, IDisposable
{
    public const string DefaultObservationInputName = "obs";
    public const string DefaultOptionLogitsOutputName = "option_logits";
    public const string DefaultMoveLogitsOutputName = "chunk_move_logits";
    public const string DefaultBinaryLogitsOutputName = "chunk_binary_logits";
    public const string DefaultAimOutputName = "chunk_aim";

    private readonly IMLBotPolicyRuntime _basePolicy;
    private readonly InferenceSession _selectorSession;
    private readonly List<ChunkOptionSession> _chunkOptions;
    private readonly string _observationInputName;
    private readonly string _optionLogitsOutputName;
    private readonly MLBotObservationVectorSchema _observationSchema;
    private MLBotAction[] _activeChunk = [];
    private int _activeIndex;
    private int _activeOptionIndex;
    private int _latchedOptionIndex;

    public ReturnIntelChunkSelectorPolicyRuntime(
        IMLBotPolicyRuntime basePolicy,
        string selectorModelPath,
        IReadOnlyList<(string ModelPath, int CommitTicks)> chunkOptions,
        string observationInputName = DefaultObservationInputName,
        string optionLogitsOutputName = DefaultOptionLogitsOutputName)
    {
        _basePolicy = basePolicy;
        _selectorSession = new InferenceSession(selectorModelPath);
        _chunkOptions = chunkOptions
            .Where(option => File.Exists(option.ModelPath))
            .Select(option => new ChunkOptionSession(option.ModelPath, option.CommitTicks))
            .ToList();
        _observationInputName = observationInputName;
        _optionLogitsOutputName = optionLogitsOutputName;
        _observationSchema = ResolveObservationSchema(_selectorSession, _observationInputName);
    }

    public MLBotAction Evaluate(in MLBotObservation observation)
    {
        if (!CanRunSelector(observation))
        {
            ResetChunk();
            return _basePolicy.Evaluate(observation);
        }

        var selectedOption = SelectOption(observation);
        if (selectedOption > _latchedOptionIndex)
        {
            _latchedOptionIndex = selectedOption;
            ResetActiveChunk();
        }

        var desiredOption = _latchedOptionIndex > 0 ? _latchedOptionIndex : selectedOption;
        if (desiredOption <= 0 || desiredOption > _chunkOptions.Count)
        {
            ResetChunk();
            return _basePolicy.Evaluate(observation);
        }

        if (_activeOptionIndex == desiredOption && _activeIndex < _activeChunk.Length)
        {
            var activeAction = _activeChunk[_activeIndex];
            _activeIndex += 1;
            return activeAction;
        }

        _activeOptionIndex = desiredOption;
        _activeChunk = _chunkOptions[desiredOption - 1].PredictChunk(observation, _observationSchema);
        _activeIndex = 0;
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
        _selectorSession.Dispose();
        foreach (var option in _chunkOptions)
        {
            option.Dispose();
        }

        if (_basePolicy is IDisposable disposable)
        {
            disposable.Dispose();
        }
    }

    private static bool CanRunSelector(in MLBotObservation observation)
    {
        return observation.TaskPhase == MLBotTaskPhase.ReturnIntel
            && observation.IsCarryingIntel
            && observation.Objective.HasObjective;
    }

    private int SelectOption(in MLBotObservation observation)
    {
        var featureVector = MLBotFeatureVectorizer.Vectorize(observation, _observationSchema);
        var observationTensor = new DenseTensor<float>(featureVector, [1, featureVector.Length]);
        var inputs = new List<NamedOnnxValue>
        {
            NamedOnnxValue.CreateFromTensor(_observationInputName, observationTensor),
        };

        using var results = _selectorSession.Run(inputs);
        var logits = results.First(result => result.Name == _optionLogitsOutputName).AsEnumerable<float>().ToArray();
        if (logits.Length == 0)
        {
            return 0;
        }

        return ArgMax(logits, 0, logits.Length);
    }

    private void ResetChunk()
    {
        ResetActiveChunk();
        _latchedOptionIndex = 0;
    }

    private void ResetActiveChunk()
    {
        _activeChunk = [];
        _activeIndex = 0;
        _activeOptionIndex = 0;
    }

    private static MLBotObservationVectorSchema ResolveObservationSchema(InferenceSession session, string observationInputName)
    {
        if (session.InputMetadata.TryGetValue(observationInputName, out var metadata))
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

    private sealed class ChunkOptionSession : IDisposable
    {
        private readonly InferenceSession _session;
        private readonly int _maxCommitTicks;

        public ChunkOptionSession(string modelPath, int maxCommitTicks)
        {
            _session = new InferenceSession(modelPath);
            _maxCommitTicks = Math.Max(0, maxCommitTicks);
        }

        public MLBotAction[] PredictChunk(in MLBotObservation observation, MLBotObservationVectorSchema observationSchema)
        {
            var featureVector = MLBotFeatureVectorizer.Vectorize(observation, observationSchema);
            var observationTensor = new DenseTensor<float>(featureVector, [1, featureVector.Length]);
            var inputs = new List<NamedOnnxValue>
            {
                NamedOnnxValue.CreateFromTensor(DefaultObservationInputName, observationTensor),
            };

            using var results = _session.Run(inputs);
            var moveLogits = results.First(result => result.Name == DefaultMoveLogitsOutputName).AsEnumerable<float>().ToArray();
            var binaryLogits = results.First(result => result.Name == DefaultBinaryLogitsOutputName).AsEnumerable<float>().ToArray();
            var aim = results.First(result => result.Name == DefaultAimOutputName).AsEnumerable<float>().ToArray();
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

        public void Dispose()
        {
            _session.Dispose();
        }
    }
}
