using OpenGarrison.Core;
using OpenGarrison.MLBot.Contracts;

namespace OpenGarrison.MLBot.Policies;

public sealed class FilteredTaskOptionPolicyRuntime : IMLBotPolicyRuntime, IDisposable
{
    private readonly IMLBotPolicyRuntime _basePolicy;
    private readonly IMLBotPolicyRuntime _optionPolicy;
    private readonly MLBotTaskPhase _taskPhase;
    private readonly float _engageDistance;
    private readonly string? _levelNameFilter;
    private readonly PlayerTeam? _teamFilter;
    private readonly PlayerClass? _classFilter;
    private readonly float? _minObjectiveRelativeX;
    private readonly float? _maxObjectiveRelativeX;
    private readonly float? _minObjectiveRelativeY;
    private readonly float? _maxObjectiveRelativeY;
    private bool _committed;

    public FilteredTaskOptionPolicyRuntime(
        IMLBotPolicyRuntime basePolicy,
        IMLBotPolicyRuntime optionPolicy,
        MLBotTaskPhase taskPhase,
        float engageDistance = 0f,
        string? levelNameFilter = null,
        PlayerTeam? teamFilter = null,
        PlayerClass? classFilter = null,
        float? minObjectiveRelativeX = null,
        float? maxObjectiveRelativeX = null,
        float? minObjectiveRelativeY = null,
        float? maxObjectiveRelativeY = null)
    {
        _basePolicy = basePolicy;
        _optionPolicy = optionPolicy;
        _taskPhase = taskPhase;
        _engageDistance = Math.Max(0f, engageDistance);
        _levelNameFilter = string.IsNullOrWhiteSpace(levelNameFilter) ? null : levelNameFilter;
        _teamFilter = teamFilter;
        _classFilter = classFilter;
        _minObjectiveRelativeX = minObjectiveRelativeX;
        _maxObjectiveRelativeX = maxObjectiveRelativeX;
        _minObjectiveRelativeY = minObjectiveRelativeY;
        _maxObjectiveRelativeY = maxObjectiveRelativeY;
    }

    public MLBotAction Evaluate(in MLBotObservation observation)
    {
        if (!CanUseOption(observation))
        {
            _committed = false;
            return _basePolicy.Evaluate(observation);
        }

        if (!_committed && (_engageDistance <= 0f || observation.ObjectiveDistance <= _engageDistance))
        {
            _committed = true;
        }

        return _committed
            ? _optionPolicy.Evaluate(observation)
            : _basePolicy.Evaluate(observation);
    }

    public void Dispose()
    {
        if (_basePolicy is IDisposable baseDisposable)
        {
            baseDisposable.Dispose();
        }

        if (_optionPolicy is IDisposable optionDisposable)
        {
            optionDisposable.Dispose();
        }
    }

    private bool CanUseOption(in MLBotObservation observation)
    {
        return observation.TaskPhase == _taskPhase
            && observation.Objective.HasObjective
            && (_levelNameFilter is null || string.Equals(observation.LevelName, _levelNameFilter, StringComparison.OrdinalIgnoreCase))
            && (!_teamFilter.HasValue || observation.Team == _teamFilter.Value)
            && (_classFilter is null || observation.ClassId == _classFilter.Value)
            && (!_minObjectiveRelativeX.HasValue || observation.Objective.RelativeX >= _minObjectiveRelativeX.Value)
            && (!_maxObjectiveRelativeX.HasValue || observation.Objective.RelativeX <= _maxObjectiveRelativeX.Value)
            && (!_minObjectiveRelativeY.HasValue || observation.Objective.RelativeY >= _minObjectiveRelativeY.Value)
            && (!_maxObjectiveRelativeY.HasValue || observation.Objective.RelativeY <= _maxObjectiveRelativeY.Value);
    }
}
