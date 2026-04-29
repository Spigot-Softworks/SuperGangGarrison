using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using OpenGarrison.MLBot.Contracts;

namespace OpenGarrison.MLBot.Policies;

public sealed class ReturnIntelHierarchicalChunkPolicyRuntime : IMLBotPolicyRuntime, IDisposable
{
    public const string DefaultObservationInputName = "obs";
    public const string DefaultOptionLogitsOutputName = "option_logits";
    public const string DefaultMoveLogitsOutputName = "option_chunk_move_logits";
    public const string DefaultBinaryLogitsOutputName = "option_chunk_binary_logits";
    public const string DefaultAimOutputName = "option_chunk_aim";

    private readonly IMLBotPolicyRuntime _basePolicy;
    private readonly InferenceSession _session;
    private readonly int[] _commitTicksByOption;
    private readonly MLBotObservationVectorSchema _observationSchema;
    private MLBotAction[] _activeChunk = [];
    private int _activeIndex;
    private int _activeOptionIndex;
    private int _latchedOptionIndex;

    public ReturnIntelHierarchicalChunkPolicyRuntime(
        IMLBotPolicyRuntime basePolicy,
        string modelPath,
        IReadOnlyList<int> commitTicksByOption)
    {
        _basePolicy = basePolicy;
        _session = new InferenceSession(modelPath);
        _commitTicksByOption = commitTicksByOption.Select(commitTicks => Math.Max(0, commitTicks)).ToArray();
        _observationSchema = ResolveObservationSchema(_session);
    }

    public MLBotAction Evaluate(in MLBotObservation observation)
    {
        if (!CanRunOption(observation) || _commitTicksByOption.Length == 0)
        {
            ResetChunk();
            return _basePolicy.Evaluate(observation);
        }

        using var results = RunModel(observation, out var selectedOption);
        if (selectedOption > _latchedOptionIndex)
        {
            _latchedOptionIndex = selectedOption;
            ResetActiveChunk();
        }

        var desiredOption = _latchedOptionIndex > 0 ? _latchedOptionIndex : selectedOption;
        if (desiredOption <= 0 || desiredOption > _commitTicksByOption.Length)
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
        _activeChunk = PredictChunk(observation, results, desiredOption - 1);
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
        _session.Dispose();
        if (_basePolicy is IDisposable disposable)
        {
            disposable.Dispose();
        }
    }

    private static bool CanRunOption(in MLBotObservation observation)
    {
        return observation.TaskPhase == MLBotTaskPhase.ReturnIntel
            && observation.IsCarryingIntel
            && observation.Objective.HasObjective;
    }

    private IDisposableReadOnlyCollection<DisposableNamedOnnxValue> RunModel(
        in MLBotObservation observation,
        out int selectedOption)
    {
        var featureVector = MLBotFeatureVectorizer.Vectorize(observation, _observationSchema);
        var observationTensor = new DenseTensor<float>(featureVector, [1, featureVector.Length]);
        var inputs = new List<NamedOnnxValue>
        {
            NamedOnnxValue.CreateFromTensor(DefaultObservationInputName, observationTensor),
        };

        var results = _session.Run(inputs);
        var logits = results.First(result => result.Name == DefaultOptionLogitsOutputName).AsEnumerable<float>().ToArray();
        selectedOption = logits.Length == 0 ? 0 : ArgMax(logits, 0, logits.Length);
        return results;
    }

    private MLBotAction[] PredictChunk(
        in MLBotObservation observation,
        IDisposableReadOnlyCollection<DisposableNamedOnnxValue> results,
        int optionIndex)
    {
        var moveLogits = results.First(result => result.Name == DefaultMoveLogitsOutputName).AsEnumerable<float>().ToArray();
        var binaryLogits = results.First(result => result.Name == DefaultBinaryLogitsOutputName).AsEnumerable<float>().ToArray();
        var aim = results.First(result => result.Name == DefaultAimOutputName).AsEnumerable<float>().ToArray();
        var optionCount = _commitTicksByOption.Length;
        var horizon = Math.Min(
            moveLogits.Length / Math.Max(1, optionCount * 3),
            Math.Min(
                binaryLogits.Length / Math.Max(1, optionCount * 5),
                aim.Length / Math.Max(1, optionCount * 2)));
        var commitTicks = _commitTicksByOption[optionIndex];
        var chunkLength = commitTicks > 0 ? Math.Min(horizon, commitTicks) : horizon;
        if (chunkLength <= 0)
        {
            return [];
        }

        var actions = new MLBotAction[chunkLength];
        for (var frameIndex = 0; frameIndex < chunkLength; frameIndex += 1)
        {
            var moveOffset = ((optionIndex * horizon) + frameIndex) * 3;
            var binaryOffset = ((optionIndex * horizon) + frameIndex) * 5;
            var aimOffset = ((optionIndex * horizon) + frameIndex) * 2;
            var moveIndex = ArgMax(moveLogits, moveOffset, 3);
            var moveDirection = moveIndex switch
            {
                0 => -1,
                2 => 1,
                _ => 0,
            };
            var aimOffsetX = Math.Clamp(aim[aimOffset + 0], -1f, 1f);
            var aimOffsetY = Math.Clamp(aim[aimOffset + 1], -1f, 1f);
            actions[frameIndex] = new MLBotAction(
                MoveDirection: moveDirection,
                Jump: ReadBinary(binaryLogits, binaryOffset + 0),
                Crouch: ReadBinary(binaryLogits, binaryOffset + 1),
                FirePrimary: ReadBinary(binaryLogits, binaryOffset + 2),
                FireSecondary: ReadBinary(binaryLogits, binaryOffset + 3),
                DropIntel: ReadBinary(binaryLogits, binaryOffset + 4),
                AimWorldX: observation.BotX + (aimOffsetX * MLBotFeatureVectorizer.DistanceScale),
                AimWorldY: observation.BotY + (aimOffsetY * MLBotFeatureVectorizer.DistanceScale));
        }

        return actions;
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

    private static MLBotObservationVectorSchema ResolveObservationSchema(InferenceSession session)
    {
        if (session.InputMetadata.TryGetValue(DefaultObservationInputName, out var metadata))
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

    private static bool ReadBinary(float[] logits, int index)
    {
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
}
