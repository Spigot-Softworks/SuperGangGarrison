#nullable enable

using Microsoft.Xna.Framework.Input;
using OpenGarrison.Core;
using OpenGarrison.MLBot;
using OpenGarrison.MLBot.Contracts;
using System;
using System.Collections.Generic;

namespace OpenGarrison.Client;

public partial class Game1
{
    private enum MLBotDemonstrationRecorderState
    {
        None = 0,
        Armed = 1,
        Recording = 2,
    }

    private const int MLBotDemonstrationRecorderDefaultMaximumTicks = 18000;
    private const int MLBotDemonstrationRecorderShortMaximumTicks = 1200;
    private const int MLBotDemonstrationAttackCompletionGraceTicks = 30;
    private const int MLBotDemonstrationCaptureCompletionGraceTicks = 60;

    private readonly MLBotObservationRuntimeState _mlBotDemonstrationRuntimeState = new();
    private readonly List<MLBotDemonstrationSample> _mlBotDemonstrationSamples = new();
    private MLBotDemonstrationRecorderState _mlBotDemonstrationRecorderState;
    private MLBotTaskPhase _mlBotDemonstrationRequestedPhase;
    private PlayerInputSnapshot _mlBotDemonstrationCaptureInput;
    private PendingMLBotDemonstrationStep? _mlBotDemonstrationPendingStep;
    private string? _mlBotDemonstrationLabel;
    private int _mlBotDemonstrationMaximumTicks = MLBotDemonstrationRecorderDefaultMaximumTicks;
    private bool _mlBotDemonstrationShortCapture;
    private int _mlBotDemonstrationAttackCompletionStopTick = -1;
    private int _mlBotDemonstrationCaptureCompletionStopTick = -1;
    private int _mlBotDemonstrationStartingRedCaps;
    private int _mlBotDemonstrationStartingBlueCaps;
    private PlayerTeam?[] _mlBotDemonstrationStartingControlPointOwners = [];

    private bool HandleMLBotDemonstrationRecorderConsoleCommand(string commandText, string[] parts)
    {
        if (parts.Length < 2)
        {
            PrintMLBotDemonstrationRecorderStatus();
            return true;
        }

        switch (parts[1].ToLowerInvariant())
        {
            case "start":
                if (parts.Length < 3 || !TryParseMLBotDemonstrationPhase(parts[2], out var phase))
                {
                    AddConsoleLine("usage: ml_demo_rec start <attack|return|capture|defend|auto> [label]");
                    return true;
                }

                StartMLBotDemonstrationRecorder(
                    phase,
                    ExtractMLBotDemonstrationTrailingArgument(commandText, parts[1], parts[2]),
                    MLBotDemonstrationRecorderDefaultMaximumTicks,
                    shortCapture: false);
                return true;

            case "short":
                if (parts.Length < 3 || !TryParseMLBotDemonstrationPhase(parts[2], out phase))
                {
                    AddConsoleLine("usage: ml_demo_rec short <attack|return|capture|defend|auto> [max_ticks] [label]");
                    return true;
                }

                ParseMLBotDemonstrationShortArguments(
                    ExtractMLBotDemonstrationTrailingArgument(commandText, parts[1], parts[2]),
                    out var maximumTicks,
                    out var label);
                StartMLBotDemonstrationRecorder(
                    phase,
                    label,
                    maximumTicks,
                    shortCapture: true);
                return true;

            case "stop":
            case "save":
                StopAndSaveMLBotDemonstrationRecorder(
                    ExtractMLBotDemonstrationTrailingArgument(commandText, parts[1]));
                return true;

            case "cancel":
            case "clear":
                CancelMLBotDemonstrationRecorder("ml demo recording canceled");
                return true;

            case "status":
                PrintMLBotDemonstrationRecorderStatus();
                return true;

            default:
                AddConsoleLine("usage: ml_demo_rec <start|short|stop|save|cancel|status> ...");
                return true;
        }
    }

    private void HandleMLBotDemonstrationRecorderHotkeys(KeyboardState keyboard)
    {
        if (IsMLBotCaptureModeEnabled())
        {
            HandleMLBotCaptureModeHotkeys(keyboard);
            return;
        }

        if (!IsKeyPressed(keyboard, Keys.F9) || _navEditorEnabled)
        {
            return;
        }

        if (IsMLBotDemonstrationRecorderActive())
        {
            AddConsoleLine("ml demo recorder is already active.");
            return;
        }

        StartMLBotDemonstrationRecorder(
            MLBotTaskPhase.None,
            label: null,
            MLBotDemonstrationRecorderDefaultMaximumTicks,
            shortCapture: false);
    }

    private void SetMLBotDemonstrationCaptureInput(PlayerInputSnapshot gameplayInput)
    {
        _mlBotDemonstrationCaptureInput = gameplayInput;
    }

    private void OnMLBotDemonstrationRecorderBeforeTick()
    {
        if (!IsMLBotDemonstrationRecorderActive())
        {
            return;
        }

        if (!_world.LocalPlayer.IsAlive)
        {
            CancelMLBotDemonstrationRecorder("ml demo recording failed: local player died");
            return;
        }

        var capturedAction = MLBotActionEncoder.Encode(_mlBotDemonstrationCaptureInput);
        if (_mlBotDemonstrationRecorderState == MLBotDemonstrationRecorderState.Armed
            && !HasAnyAction(capturedAction))
        {
            return;
        }

        if (_mlBotDemonstrationRecorderState == MLBotDemonstrationRecorderState.Armed)
        {
            _mlBotDemonstrationRecorderState = MLBotDemonstrationRecorderState.Recording;
            _mlBotDemonstrationSamples.Clear();
            MLBotObservationRuntimeStateTracker.Reset(_mlBotDemonstrationRuntimeState);
            _mlBotDemonstrationAttackCompletionStopTick = -1;
            _mlBotDemonstrationCaptureCompletionStopTick = -1;
            _mlBotDemonstrationStartingRedCaps = _world.RedCaps;
            _mlBotDemonstrationStartingBlueCaps = _world.BlueCaps;
            _mlBotDemonstrationStartingControlPointOwners = CaptureMLBotDemonstrationControlPointOwners();
            AddConsoleLine(
                $"ml demo recording started: {_world.Level.Name} {_world.LocalPlayer.Team} {_world.LocalPlayer.ClassId} {DescribeMLBotDemonstrationPhase(_mlBotDemonstrationRequestedPhase)}");
        }

        var resolvedPhase = ResolveMLBotDemonstrationPhase();
        var observation = MLBotObservationBuilder.Build(
            _world,
            SimulationWorld.LocalPlayerSlot,
            _world.LocalPlayer,
            resolvedPhase,
            _mlBotDemonstrationRuntimeState);
        MLBotObservationRuntimeStateTracker.Update(_mlBotDemonstrationRuntimeState, observation, _world.LocalPlayer);
        _mlBotDemonstrationPendingStep = new PendingMLBotDemonstrationStep(
            _mlBotDemonstrationSamples.Count,
            resolvedPhase,
            observation,
            capturedAction,
            _world.LocalPlayer.IsCarryingIntel,
            _world.RedCaps,
            _world.BlueCaps);
    }

    private void OnMLBotDemonstrationRecorderAfterTick()
    {
        if (!IsMLBotDemonstrationRecorderActive() || _mlBotDemonstrationRecorderState != MLBotDemonstrationRecorderState.Recording)
        {
            return;
        }

        if (_mlBotDemonstrationPendingStep is not { } pendingStep)
        {
            return;
        }

        var nextResolvedPhase = ResolveMLBotDemonstrationPhase();
        var nextObservation = MLBotObservationBuilder.Build(
            _world,
            SimulationWorld.LocalPlayerSlot,
            _world.LocalPlayer,
            nextResolvedPhase,
            _mlBotDemonstrationRuntimeState);

        var pickedUpIntel = !pendingStep.WasCarryingIntel && _world.LocalPlayer.IsCarryingIntel;
        var scoredIntel = pendingStep.WasCarryingIntel
            && !_world.LocalPlayer.IsCarryingIntel
            && ((_world.RedCaps > pendingStep.RedCapsBefore) || (_world.BlueCaps > pendingStep.BlueCapsBefore));
        var died = !_world.LocalPlayer.IsAlive;
        var episodeEnded = died || scoredIntel;

        _mlBotDemonstrationSamples.Add(new MLBotDemonstrationSample
        {
            Tick = pendingStep.Tick,
            ResolvedPhase = pendingStep.ResolvedPhase,
            Observation = pendingStep.Observation,
            Action = pendingStep.Action,
            NextObservation = nextObservation,
            PickedUpIntel = pickedUpIntel,
            ScoredIntel = scoredIntel,
            Died = died,
            EpisodeEnded = episodeEnded,
        });

        if (_mlBotDemonstrationRequestedPhase == MLBotTaskPhase.AttackIntel
            && pickedUpIntel
            && _mlBotDemonstrationAttackCompletionStopTick < 0)
        {
            _mlBotDemonstrationAttackCompletionStopTick = _mlBotDemonstrationSamples.Count + MLBotDemonstrationAttackCompletionGraceTicks;
            AddConsoleLine($"ml demo attack objective reached; saving in {MLBotDemonstrationAttackCompletionGraceTicks} ticks.");
        }

        if (_mlBotDemonstrationRequestedPhase == MLBotTaskPhase.CaptureObjective
            && HasFriendlyControlPointCapturedSinceStart()
            && _mlBotDemonstrationCaptureCompletionStopTick < 0)
        {
            _mlBotDemonstrationCaptureCompletionStopTick = _mlBotDemonstrationSamples.Count + MLBotDemonstrationCaptureCompletionGraceTicks;
            AddConsoleLine($"ml demo capture objective reached; saving in {MLBotDemonstrationCaptureCompletionGraceTicks} ticks.");
        }

        _mlBotDemonstrationPendingStep = null;

        if (episodeEnded && _mlBotDemonstrationRequestedPhase == MLBotTaskPhase.None)
        {
            StopAndSaveMLBotDemonstrationRecorder(requestedLabel: null);
            return;
        }

        if (_mlBotDemonstrationRequestedPhase == MLBotTaskPhase.ReturnIntel && scoredIntel)
        {
            StopAndSaveMLBotDemonstrationRecorder(requestedLabel: null);
            return;
        }

        if (_mlBotDemonstrationRequestedPhase == MLBotTaskPhase.AttackIntel
            && _mlBotDemonstrationAttackCompletionStopTick >= 0
            && _mlBotDemonstrationSamples.Count >= _mlBotDemonstrationAttackCompletionStopTick)
        {
            StopAndSaveMLBotDemonstrationRecorder(
                requestedLabel: null,
                chainIntoReturn: _world.LocalPlayer.IsAlive && _world.LocalPlayer.IsCarryingIntel);
            return;
        }

        if (_mlBotDemonstrationRequestedPhase == MLBotTaskPhase.CaptureObjective
            && _mlBotDemonstrationCaptureCompletionStopTick >= 0
            && _mlBotDemonstrationSamples.Count >= _mlBotDemonstrationCaptureCompletionStopTick)
        {
            StopAndSaveMLBotDemonstrationRecorder(requestedLabel: null);
            return;
        }

        if (_mlBotDemonstrationSamples.Count >= _mlBotDemonstrationMaximumTicks)
        {
            StopAndSaveMLBotDemonstrationRecorder(requestedLabel: null);
        }
    }

    private void StartMLBotDemonstrationRecorder(MLBotTaskPhase requestedPhase, string? label, int maximumTicks, bool shortCapture)
    {
        if (!IsPracticeSessionActive)
        {
            AddConsoleLine("ml demo recorder is practice-only.");
            return;
        }

        if (_networkClient.IsConnected)
        {
            AddConsoleLine("ml demo recorder is offline-only.");
            return;
        }

        if (OperatingSystem.IsBrowser())
        {
            AddConsoleLine("ml demo recorder currently saves desktop runtime files only.");
            return;
        }

        if (IsMLBotDemonstrationRecorderActive())
        {
            AddConsoleLine("ml demo recorder is already active.");
            return;
        }

        _mlBotDemonstrationRequestedPhase = requestedPhase;
        _mlBotDemonstrationLabel = string.IsNullOrWhiteSpace(label) ? null : label.Trim();
        _mlBotDemonstrationMaximumTicks = Math.Clamp(maximumTicks, 1, MLBotDemonstrationRecorderDefaultMaximumTicks);
        _mlBotDemonstrationShortCapture = shortCapture;
        _mlBotDemonstrationCaptureInput = default;
        _mlBotDemonstrationPendingStep = null;
        _mlBotDemonstrationStartingControlPointOwners = CaptureMLBotDemonstrationControlPointOwners();
        MLBotObservationRuntimeStateTracker.Reset(_mlBotDemonstrationRuntimeState);
        _mlBotDemonstrationAttackCompletionStopTick = -1;
        _mlBotDemonstrationCaptureCompletionStopTick = -1;
        _mlBotDemonstrationSamples.Clear();
        _mlBotDemonstrationRecorderState = MLBotDemonstrationRecorderState.Armed;
        AddConsoleLine(
            $"ml demo recorder armed: {_world.Level.Name} {_world.LocalPlayer.Team} {_world.LocalPlayer.ClassId} {DescribeMLBotDemonstrationPhase(requestedPhase)} max_ticks={_mlBotDemonstrationMaximumTicks}");
        AddConsoleLine(IsMLBotCaptureModeEnabled()
            ? "move to begin recording. class inference comes from the local player, and the save path already carries map/team/class/phase."
            : "move your player to begin recording, then run ml_demo_rec stop when the demo is complete.");
    }

    private void StopAndSaveMLBotDemonstrationRecorder(string? requestedLabel, bool chainIntoReturn = false)
    {
        if (!IsMLBotDemonstrationRecorderActive())
        {
            AddConsoleLine("ml demo recorder is not active.");
            return;
        }

        if (_mlBotDemonstrationSamples.Count == 0)
        {
            CancelMLBotDemonstrationRecorder("ml demo recording canceled: no samples were captured");
            return;
        }

        var metadata = BuildMLBotDemonstrationMetadata(requestedLabel);
        var document = new MLBotDemonstrationDocument
        {
            Metadata = metadata,
            Samples = _mlBotDemonstrationSamples.ToArray(),
        };
        var outputPath = MLBotDemonstrationStore.ResolveWritablePath(metadata);
        var continueIntoReturn = chainIntoReturn
            && metadata.RequestedPhase == MLBotTaskPhase.AttackIntel
            && _world.LocalPlayer.IsAlive
            && _world.LocalPlayer.IsCarryingIntel;
        var nextMaximumTicks = _mlBotDemonstrationMaximumTicks;
        var nextShortCapture = _mlBotDemonstrationShortCapture;
        MLBotDemonstrationStore.Save(document);
        ClearMLBotDemonstrationRecorderState();

        AddConsoleLine(
            $"ml demo saved: {metadata.Team} {metadata.ClassId} {DescribeMLBotDemonstrationPhase(metadata.RequestedPhase)} ticks={metadata.TickCount} success={metadata.Success}");
        AddConsoleLine($"ml demo path: {outputPath}");

        if (continueIntoReturn)
        {
            AddConsoleLine("ml demo chaining into return phase.");
            StartMLBotDemonstrationRecorder(
                MLBotTaskPhase.ReturnIntel,
                label: null,
                nextMaximumTicks,
                nextShortCapture);
        }
    }

    private MLBotDemonstrationMetadata BuildMLBotDemonstrationMetadata(string? requestedLabel)
    {
        var label = string.IsNullOrWhiteSpace(requestedLabel)
            ? _mlBotDemonstrationLabel ?? string.Empty
            : requestedLabel.Trim();
        var success = HasMLBotDemonstrationSucceeded();
        return new MLBotDemonstrationMetadata
        {
            LevelName = _world.Level.Name,
            MapAreaIndex = _world.Level.MapAreaIndex,
            Mode = _world.MatchRules.Mode,
            Team = _world.LocalPlayer.Team,
            ClassId = _world.LocalPlayer.ClassId,
            RequestedPhase = _mlBotDemonstrationRequestedPhase,
            CaptureKind = MLBotDemonstrationCaptureKind.Demonstration,
            Label = label,
            RecordedAtUtc = DateTimeOffset.UtcNow,
            TickCount = _mlBotDemonstrationSamples.Count,
            CaptureMaxTicks = _mlBotDemonstrationMaximumTicks,
            ShortCapture = _mlBotDemonstrationShortCapture,
            Success = success,
            Outcome = success ? "success" : "manual_stop",
        };
    }

    private bool HasMLBotDemonstrationSucceeded()
    {
        for (var index = 0; index < _mlBotDemonstrationSamples.Count; index += 1)
        {
            if (_mlBotDemonstrationSamples[index].ScoredIntel)
            {
                return true;
            }
        }

        return _mlBotDemonstrationRequestedPhase switch
        {
            MLBotTaskPhase.AttackIntel => _world.LocalPlayer.IsCarryingIntel,
            MLBotTaskPhase.ReturnIntel => (_world.RedCaps > _mlBotDemonstrationStartingRedCaps)
                || (_world.BlueCaps > _mlBotDemonstrationStartingBlueCaps),
            MLBotTaskPhase.CaptureObjective => HasFriendlyControlPointCapturedSinceStart(),
            _ => false,
        };
    }

    private PlayerTeam?[] CaptureMLBotDemonstrationControlPointOwners()
    {
        var owners = new PlayerTeam?[_world.ControlPoints.Count];
        for (var index = 0; index < _world.ControlPoints.Count; index += 1)
        {
            owners[index] = _world.ControlPoints[index].Team;
        }

        return owners;
    }

    private bool HasFriendlyControlPointCapturedSinceStart()
    {
        for (var index = 0; index < _world.ControlPoints.Count; index += 1)
        {
            var point = _world.ControlPoints[index];
            var startingOwner = index < _mlBotDemonstrationStartingControlPointOwners.Length
                ? _mlBotDemonstrationStartingControlPointOwners[index]
                : null;
            if (point.Team == _world.LocalPlayer.Team && startingOwner != _world.LocalPlayer.Team)
            {
                return true;
            }
        }

        return false;
    }

    private bool IsFriendlyControlPointActive()
    {
        for (var index = 0; index < _world.ControlPoints.Count; index += 1)
        {
            var point = _world.ControlPoints[index];
            if (point.Team == _world.LocalPlayer.Team
                || point.CappingTeam == _world.LocalPlayer.Team)
            {
                return true;
            }
        }

        return false;
    }

    private MLBotTaskPhase ResolveMLBotDemonstrationPhase()
    {
        return _mlBotDemonstrationRequestedPhase == MLBotTaskPhase.None
            ? MLBotTaskStateResolver.Resolve(_world, _world.LocalPlayer)
            : _mlBotDemonstrationRequestedPhase;
    }

    private void PrintMLBotDemonstrationRecorderStatus()
    {
        if (!IsMLBotDemonstrationRecorderActive())
        {
            AddConsoleLine("ml demo recorder: idle");
            return;
        }

        AddConsoleLine(
            $"ml demo recorder: {_mlBotDemonstrationRecorderState.ToString().ToLowerInvariant()} {DescribeMLBotDemonstrationPhase(_mlBotDemonstrationRequestedPhase)} ticks={_mlBotDemonstrationSamples.Count}/{_mlBotDemonstrationMaximumTicks} short={_mlBotDemonstrationShortCapture}");
    }

    private void CancelMLBotDemonstrationRecorder(string reason)
    {
        if (!IsMLBotDemonstrationRecorderActive())
        {
            AddConsoleLine("ml demo recorder: idle");
            return;
        }

        ClearMLBotDemonstrationRecorderState();
        AddConsoleLine(reason);
    }

    private void ClearMLBotDemonstrationRecorderState()
    {
        _mlBotDemonstrationRecorderState = MLBotDemonstrationRecorderState.None;
        _mlBotDemonstrationRequestedPhase = MLBotTaskPhase.None;
        _mlBotDemonstrationCaptureInput = default;
        _mlBotDemonstrationPendingStep = null;
        _mlBotDemonstrationLabel = null;
        _mlBotDemonstrationMaximumTicks = MLBotDemonstrationRecorderDefaultMaximumTicks;
        _mlBotDemonstrationShortCapture = false;
        MLBotObservationRuntimeStateTracker.Reset(_mlBotDemonstrationRuntimeState);
        _mlBotDemonstrationAttackCompletionStopTick = -1;
        _mlBotDemonstrationCaptureCompletionStopTick = -1;
        _mlBotDemonstrationStartingControlPointOwners = [];
        _mlBotDemonstrationSamples.Clear();
    }

    private bool IsMLBotDemonstrationRecorderActive()
    {
        return _mlBotDemonstrationRecorderState != MLBotDemonstrationRecorderState.None;
    }

    private static bool TryParseMLBotDemonstrationPhase(string value, out MLBotTaskPhase phase)
    {
        switch (value.ToLowerInvariant())
        {
            case "attack":
            case "attackintel":
            case "intel":
            case "pickup":
                phase = MLBotTaskPhase.AttackIntel;
                return true;
            case "return":
            case "score":
            case "home":
                phase = MLBotTaskPhase.ReturnIntel;
                return true;
            case "capture":
            case "cap":
            case "objective":
                phase = MLBotTaskPhase.CaptureObjective;
                return true;
            case "defend":
                phase = MLBotTaskPhase.DefendObjective;
                return true;
            case "auto":
                phase = MLBotTaskPhase.None;
                return true;
            default:
                phase = MLBotTaskPhase.None;
                return false;
        }
    }

    private static string DescribeMLBotDemonstrationPhase(MLBotTaskPhase phase)
    {
        return phase switch
        {
            MLBotTaskPhase.AttackIntel => "attack",
            MLBotTaskPhase.ReturnIntel => "return",
            MLBotTaskPhase.CaptureObjective => "capture",
            MLBotTaskPhase.DefendObjective => "defend",
            _ => "auto",
        };
    }

    private static bool HasAnyAction(MLBotAction action)
    {
        return action.MoveDirection != 0
            || action.Jump
            || action.Crouch
            || action.FirePrimary
            || action.FireSecondary
            || action.DropIntel;
    }

    private static string? ExtractMLBotDemonstrationTrailingArgument(string commandText, string subcommand, string? phase = null)
    {
        var prefix = phase is null
            ? $"ml_demo_rec {subcommand}"
            : $"ml_demo_rec {subcommand} {phase}";
        if (!commandText.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var trailing = commandText[prefix.Length..].Trim();
        return string.IsNullOrWhiteSpace(trailing) ? null : trailing;
    }

    private static void ParseMLBotDemonstrationShortArguments(string? trailing, out int maximumTicks, out string? label)
    {
        maximumTicks = MLBotDemonstrationRecorderShortMaximumTicks;
        label = null;

        if (string.IsNullOrWhiteSpace(trailing))
        {
            return;
        }

        var trimmed = trailing.Trim();
        var pieces = trimmed.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
        if (pieces.Length > 0 && int.TryParse(pieces[0], out var parsedTicks))
        {
            maximumTicks = Math.Clamp(parsedTicks, 1, MLBotDemonstrationRecorderDefaultMaximumTicks);
            label = pieces.Length > 1 ? pieces[1].Trim() : null;
            return;
        }

        label = trimmed;
    }

    private readonly record struct PendingMLBotDemonstrationStep(
        int Tick,
        MLBotTaskPhase ResolvedPhase,
        MLBotObservation Observation,
        MLBotAction Action,
        bool WasCarryingIntel,
        int RedCapsBefore,
        int BlueCapsBefore);
}
