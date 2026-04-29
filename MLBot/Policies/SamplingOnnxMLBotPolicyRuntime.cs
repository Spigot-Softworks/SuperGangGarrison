using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using OpenGarrison.MLBot.Contracts;

namespace OpenGarrison.MLBot.Policies;

public sealed class SamplingOnnxMLBotPolicyRuntime : IMLBotPolicyRuntime, IDisposable
{
    private readonly InferenceSession _session;
    private readonly Random _random;
    private readonly float _temperature;
    private readonly string _observationInputName;
    private readonly string _moveLogitsOutputName;
    private readonly string _binaryLogitsOutputName;
    private readonly string _aimOutputName;
    private readonly MLBotObservationVectorSchema _observationSchema;
    private readonly bool _enablePolicyOverrides;
    private bool _returnIntelRecoveryCommitted;
    private bool _returnIntelFinalBackoffCommitted;

    public SamplingOnnxMLBotPolicyRuntime(
        string modelPath,
        int seed,
        float temperature = 1f,
        string observationInputName = OnnxMLBotPolicyRuntime.DefaultObservationInputName,
        string moveLogitsOutputName = OnnxMLBotPolicyRuntime.DefaultMoveLogitsOutputName,
        string binaryLogitsOutputName = OnnxMLBotPolicyRuntime.DefaultBinaryLogitsOutputName,
        string aimOutputName = OnnxMLBotPolicyRuntime.DefaultAimOutputName,
        bool enablePolicyOverrides = true)
    {
        _session = new InferenceSession(modelPath);
        _random = new Random(seed);
        _temperature = Math.Max(0.05f, temperature);
        _observationInputName = observationInputName;
        _moveLogitsOutputName = moveLogitsOutputName;
        _binaryLogitsOutputName = binaryLogitsOutputName;
        _aimOutputName = aimOutputName;
        _enablePolicyOverrides = enablePolicyOverrides;
        _observationSchema = ResolveObservationSchema();
    }

    public MLBotAction Evaluate(in MLBotObservation observation)
    {
        var featureVector = MLBotFeatureVectorizer.Vectorize(observation, _observationSchema);
        var observationTensor = new DenseTensor<float>(featureVector, [1, featureVector.Length]);
        var inputs = new List<NamedOnnxValue>
        {
            NamedOnnxValue.CreateFromTensor(_observationInputName, observationTensor),
        };

        using IDisposableReadOnlyCollection<DisposableNamedOnnxValue> results = _session.Run(inputs);
        var moveLogits = results.First(result => result.Name == _moveLogitsOutputName).AsEnumerable<float>().ToArray();
        var binaryLogits = results.First(result => result.Name == _binaryLogitsOutputName).AsEnumerable<float>().ToArray();
        var aim = results.First(result => result.Name == _aimOutputName).AsEnumerable<float>().ToArray();

        var moveIndex = SampleCategorical(moveLogits);
        var moveDirection = moveIndex switch
        {
            0 => -1,
            2 => 1,
            _ => 0,
        };
        var returnIntelWallRecoveryDirection = _enablePolicyOverrides
            ? GetReturnIntelWallRecoveryDirection(observation)
            : 0;
        if (!_enablePolicyOverrides)
        {
            _returnIntelRecoveryCommitted = false;
            _returnIntelFinalBackoffCommitted = false;
        }

        if (returnIntelWallRecoveryDirection != 0)
        {
            moveDirection = returnIntelWallRecoveryDirection;
        }

        var aimOffsetX = aim.Length > 0 ? Math.Clamp(aim[0], -1f, 1f) : 0f;
        var aimOffsetY = aim.Length > 1 ? Math.Clamp(aim[1], -1f, 1f) : 0f;
        var crouch = SampleBernoulli(binaryLogits, 1);
        var jump = SampleBernoulli(binaryLogits, 0)
            || (_enablePolicyOverrides
                && (ShouldForceRecoveryJump(observation, moveDirection, crouch)
                    || ShouldForceObjectiveClimbJump(observation, moveDirection, crouch)
                    || ShouldForceReturnIntelWallRecoveryJump(observation, returnIntelWallRecoveryDirection, crouch)));
        return new MLBotAction(
            MoveDirection: moveDirection,
            Jump: jump,
            Crouch: crouch,
            FirePrimary: SampleBernoulli(binaryLogits, 2),
            FireSecondary: SampleBernoulli(binaryLogits, 3),
            DropIntel: SampleBernoulli(binaryLogits, 4),
            AimWorldX: observation.BotX + (aimOffsetX * MLBotFeatureVectorizer.DistanceScale),
            AimWorldY: observation.BotY + (aimOffsetY * MLBotFeatureVectorizer.DistanceScale));
    }

    public void Dispose()
    {
        _session.Dispose();
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

        return MLBotObservationVectorSchema.V2;
    }

    private bool SampleBernoulli(float[] logits, int index)
    {
        if (index >= logits.Length)
        {
            return false;
        }

        var scaledLogit = logits[index] / _temperature;
        var probability = 1f / (1f + MathF.Exp(-scaledLogit));
        return _random.NextSingle() < probability;
    }

    private static bool ShouldForceRecoveryJump(in MLBotObservation observation, int moveDirection, bool crouch)
    {
        return moveDirection != 0
            && !crouch
            && observation.IsGrounded
            && observation.StuckTicks >= 12f
            && observation.Probes.ForwardFootObstacleDistance <= 8f
            && observation.Probes.GroundAheadDistance <= 4f;
    }

    private static bool ShouldForceObjectiveClimbJump(in MLBotObservation observation, int moveDirection, bool crouch)
    {
        if (moveDirection == 0 || crouch || !observation.IsGrounded || !observation.Objective.HasObjective)
        {
            return false;
        }

        if (observation.Objective.RelativeY > -96f
            || observation.Probes.ForwardFootObstacleDistance > 8f
            || observation.Probes.GroundAheadDistance > 4f)
        {
            return false;
        }

        var horizontalDirectionToObjective = MathF.Sign(observation.Objective.RelativeX);
        var atOuterLip = observation.ObjectiveDistance is >= 300f and <= 450f
            && MathF.Abs(observation.Objective.RelativeX) >= 220f
            && (horizontalDirectionToObjective == 0 || horizontalDirectionToObjective == moveDirection);
        var atInnerPin = observation.ObjectiveDistance <= 256f
            && MathF.Abs(observation.VelocityX) <= 1f;
        return atOuterLip || atInnerPin;
    }

    private int GetReturnIntelWallRecoveryDirection(in MLBotObservation observation)
    {
        if (observation.TaskPhase != MLBotTaskPhase.ReturnIntel
            || observation.Team != OpenGarrison.Core.PlayerTeam.Red
            || !observation.IsCarryingIntel
            || !observation.Objective.HasObjective)
        {
            _returnIntelRecoveryCommitted = false;
            _returnIntelFinalBackoffCommitted = false;
            return 0;
        }

        if (observation.ObjectiveDistance < 120f || observation.Objective.RelativeY > -40f)
        {
            _returnIntelRecoveryCommitted = false;
            _returnIntelFinalBackoffCommitted = false;
            return 0;
        }

        var relativeX = MathF.Abs(observation.Objective.RelativeX);
        var farWallRecovery = observation.ObjectiveDistance is >= 230f and <= 450f
            && relativeX >= 220f
            && observation.Objective.RelativeY <= -120f;
        var finalDropSwitch = observation.ObjectiveDistance is >= 230f and <= 270f
            && relativeX >= 205f
            && observation.Objective.RelativeY is >= -150f and <= -110f;
        var finalLaneCommit = observation.ObjectiveDistance is >= 180f and <= 270f
            && relativeX >= 170f
            && observation.Objective.RelativeY is >= -110f and <= -60f;
        var finalSetupRelease = _returnIntelFinalBackoffCommitted
            && observation.ObjectiveDistance is >= 220f and <= 270f
            && relativeX >= 160f
            && observation.Objective.RelativeY is >= -190f and <= -145f;

        if (finalDropSwitch || finalLaneCommit || finalSetupRelease)
        {
            _returnIntelFinalBackoffCommitted = false;
            _returnIntelRecoveryCommitted = true;
        }

        var finalApexBackoff = observation.ObjectiveDistance is >= 190f and <= 230f
            && relativeX is >= 90f and <= 130f
            && observation.Objective.RelativeY is >= -190f and <= -150f
            && observation.VelocityX * MathF.Sign(observation.Objective.RelativeX) > 100f;
        if (finalApexBackoff)
        {
            _returnIntelFinalBackoffCommitted = true;
            return -MathF.Sign(observation.Objective.RelativeX);
        }

        var finalWallBackoff = observation.ObjectiveDistance is >= 150f and <= 230f
            && relativeX is >= 60f and <= 130f
            && observation.Objective.RelativeY <= -120f
            && observation.Probes.ForwardFootObstacleDistance <= 8f
            && MathF.Abs(observation.VelocityX) <= 1f;
        if (finalWallBackoff)
        {
            _returnIntelFinalBackoffCommitted = true;
            return -MathF.Sign(observation.Objective.RelativeX);
        }

        if (_returnIntelFinalBackoffCommitted)
        {
            return -MathF.Sign(observation.Objective.RelativeX);
        }

        var committedFinalRecovery = _returnIntelRecoveryCommitted
            && observation.ObjectiveDistance is >= 120f and <= 280f
            && observation.Objective.RelativeY is >= -220f and <= -40f;

        return farWallRecovery || committedFinalRecovery
            ? MathF.Sign(observation.Objective.RelativeX)
            : 0;
    }

    private static bool ShouldForceReturnIntelWallRecoveryJump(
        in MLBotObservation observation,
        int recoveryDirection,
        bool crouch)
    {
        return recoveryDirection != 0
            && !crouch
            && ((observation.IsGrounded && observation.Probes.GroundAheadDistance <= 4f)
                || ShouldForceReturnIntelAirRecoveryJump(observation, recoveryDirection));
    }

    private static bool ShouldForceReturnIntelAirRecoveryJump(in MLBotObservation observation, int recoveryDirection)
    {
        if (observation.TaskPhase != MLBotTaskPhase.ReturnIntel
            || observation.Team != OpenGarrison.Core.PlayerTeam.Red
            || !observation.IsCarryingIntel
            || !observation.Objective.HasObjective
            || observation.IsGrounded)
        {
            return false;
        }

        var relativeX = MathF.Abs(observation.Objective.RelativeX);
        if (observation.ClassId != OpenGarrison.Core.PlayerClass.Scout)
        {
            return false;
        }

        var towardObjective = recoveryDirection == MathF.Sign(observation.Objective.RelativeX);
        var finalLaneCommitJump = towardObjective
            && observation.ObjectiveDistance is >= 180f and <= 285f
            && relativeX >= 170f
            && observation.Objective.RelativeY is >= -115f and <= -60f
            && observation.VelocityY >= -220f;
        var finalSetupCommitJump = towardObjective
            && observation.ObjectiveDistance is >= 220f and <= 300f
            && relativeX >= 155f
            && observation.Objective.RelativeY is >= -190f and <= -140f
            && observation.VelocityY >= -240f;
        var finalApexBackoffJump = !towardObjective
            && observation.ObjectiveDistance is >= 180f and <= 260f
            && relativeX is >= 90f and <= 170f
            && observation.Objective.RelativeY is >= -215f and <= -130f
            && observation.VelocityY >= -240f;
        return finalLaneCommitJump || finalSetupCommitJump || finalApexBackoffJump;
    }

    private int SampleCategorical(float[] logits)
    {
        if (logits.Length == 0)
        {
            return 1;
        }

        var maxLogit = logits.Max();
        var total = 0f;
        Span<float> probabilities = stackalloc float[logits.Length];
        for (var index = 0; index < logits.Length; index += 1)
        {
            var value = MathF.Exp((logits[index] - maxLogit) / _temperature);
            probabilities[index] = value;
            total += value;
        }

        var sample = _random.NextSingle() * total;
        var cumulative = 0f;
        for (var index = 0; index < probabilities.Length; index += 1)
        {
            cumulative += probabilities[index];
            if (sample <= cumulative)
            {
                return index;
            }
        }

        return probabilities.Length - 1;
    }
}
