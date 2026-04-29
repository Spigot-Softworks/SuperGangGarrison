using OpenGarrison.Core;
using OpenGarrison.MLBot.Contracts;

namespace OpenGarrison.MLBot.Policies;

public sealed class ReturnIntelObjectiveOptionPolicyRuntime : IMLBotPolicyRuntime, IDisposable
{
    private readonly IMLBotPolicyRuntime _basePolicy;
    private readonly IMLBotPolicyRuntime _finalizerPolicy;
    private readonly float _engageDistance;
    private readonly string? _levelNameFilter;
    private readonly PlayerTeam? _teamFilter;
    private readonly PlayerClass? _classFilter;
    private bool _committed;

    public ReturnIntelObjectiveOptionPolicyRuntime(
        IMLBotPolicyRuntime basePolicy,
        IMLBotPolicyRuntime finalizerPolicy,
        float engageDistance,
        string? levelNameFilter = null,
        PlayerTeam? teamFilter = null,
        PlayerClass? classFilter = null)
    {
        _basePolicy = basePolicy;
        _finalizerPolicy = finalizerPolicy;
        _engageDistance = Math.Max(0f, engageDistance);
        _levelNameFilter = string.IsNullOrWhiteSpace(levelNameFilter) ? null : levelNameFilter;
        _teamFilter = teamFilter;
        _classFilter = classFilter;
    }

    public MLBotAction Evaluate(in MLBotObservation observation)
    {
        if (!CanUseFinalizer(observation))
        {
            _committed = false;
            return _basePolicy.Evaluate(observation);
        }

        if (!_committed && observation.ObjectiveDistance <= _engageDistance)
        {
            _committed = true;
        }

        return _committed
            ? _finalizerPolicy.Evaluate(observation)
            : _basePolicy.Evaluate(observation);
    }

    public void Dispose()
    {
        if (_basePolicy is IDisposable baseDisposable)
        {
            baseDisposable.Dispose();
        }

        if (_finalizerPolicy is IDisposable finalizerDisposable)
        {
            finalizerDisposable.Dispose();
        }
    }

    private bool CanUseFinalizer(in MLBotObservation observation)
    {
        return observation.TaskPhase == MLBotTaskPhase.ReturnIntel
            && observation.IsCarryingIntel
            && observation.Objective.HasObjective
            && (_levelNameFilter is null || string.Equals(observation.LevelName, _levelNameFilter, StringComparison.OrdinalIgnoreCase))
            && (!_teamFilter.HasValue || observation.Team == _teamFilter.Value)
            && (_classFilter is null || observation.ClassId == _classFilter.Value);
    }
}
