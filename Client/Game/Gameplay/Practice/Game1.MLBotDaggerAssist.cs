#nullable enable

using Microsoft.Xna.Framework.Input;
using OpenGarrison.Core;
using OpenGarrison.MLBot;
using OpenGarrison.MLBot.Contracts;
using OpenGarrison.MLBot.Policies;
using System;
using System.Collections.Generic;

namespace OpenGarrison.Client;

public partial class Game1
{
    private enum MLBotDaggerAssistState
    {
        None = 0,
        Recording = 1,
    }

    private const int MLBotDaggerAssistMaximumTicks = 18000;
    private const int MLBotDaggerAttackCompletionGraceTicks = 30;

    private readonly MLBotObservationRuntimeState _mlBotDaggerRuntimeState = new();
    private readonly List<MLBotDemonstrationSample> _mlBotDaggerSamples = new();
    private MLBotDaggerAssistState _mlBotDaggerAssistState;
    private MLBotTaskPhase _mlBotDaggerRequestedPhase;
    private int _mlBotDaggerAttackCompletionStopTick = -1;
    private PlayerInputSnapshot _mlBotDaggerHumanInput;
    private PlayerInputSnapshot _mlBotDaggerAppliedInput;
    private MLBotAction _mlBotDaggerSuggestedAction;
    private bool _mlBotDaggerUsedHumanOverride;
    private PendingMLBotDaggerStep? _mlBotDaggerPendingStep;
    private string? _mlBotDaggerLabel;
    private string? _mlBotDaggerPolicyPath;
    private IMLBotPolicyRuntime? _mlBotDaggerPolicy;
    private MLBotObservation _mlBotDaggerCurrentObservation;
    private bool _mlBotDaggerHasCurrentObservation;
    private int _mlBotDaggerStartingRedCaps;
    private int _mlBotDaggerStartingBlueCaps;
    private bool _mlBotDaggerUsingAutoResolvedModel;

    private bool HandleMLBotDaggerAssistConsoleCommand(string commandText, string[] parts)
    {
        if (parts.Length < 2)
        {
            PrintMLBotDaggerAssistStatus();
            return true;
        }

        switch (parts[1].ToLowerInvariant())
        {
            case "start":
                if (parts.Length < 3 || !TryParseMLBotDemonstrationPhase(parts[2], out var phase))
                {
                    AddConsoleLine("usage: ml_dagger <start|stop|cancel|status> <attack|return|capture|defend|auto> [label]");
                    return true;
                }

                StartMLBotDaggerAssist(
                    phase,
                    ExtractMLBotDaggerTrailingArgument(commandText, parts[1], parts[2]));
                return true;

            case "stop":
            case "save":
                StopAndSaveMLBotDaggerAssist(
                    ExtractMLBotDaggerTrailingArgument(commandText, parts[1]));
                return true;

            case "cancel":
            case "clear":
                CancelMLBotDaggerAssist("ml dagger assist canceled");
                return true;

            case "status":
                PrintMLBotDaggerAssistStatus();
                return true;

            default:
                AddConsoleLine("usage: ml_dagger <start|stop|save|cancel|status> ...");
                return true;
        }
    }

    private void HandleMLBotDaggerAssistHotkeys(KeyboardState keyboard)
    {
        if (IsMLBotCaptureModeEnabled())
        {
            return;
        }

        if (!IsKeyPressed(keyboard, Keys.F10) || _navEditorEnabled)
        {
            return;
        }

        if (IsMLBotDaggerAssistActive())
        {
            StopAndSaveMLBotDaggerAssist(requestedLabel: null);
            return;
        }

        StartMLBotDaggerAssist(MLBotTaskPhase.None, label: null);
    }

    private void SetMLBotDaggerHumanInput(PlayerInputSnapshot gameplayInput)
    {
        _mlBotDaggerHumanInput = gameplayInput;
    }

    private PlayerInputSnapshot ResolveMLBotDaggerGameplayInput(PlayerInputSnapshot gameplayInput)
    {
        if (!IsMLBotDaggerAssistActive()
            || _mlBotDaggerPolicy is null
            || _consoleOpen
            || _networkClient.IsConnected
            || !IsPracticeSessionActive
            || IsGameplayInputBlocked()
            || !_world.LocalPlayer.IsAlive)
        {
            _mlBotDaggerHasCurrentObservation = false;
            return gameplayInput;
        }

        var resolvedPhase = ResolveMLBotDaggerPhase();
        var observation = MLBotObservationBuilder.Build(
            _world,
            SimulationWorld.LocalPlayerSlot,
            _world.LocalPlayer,
            resolvedPhase,
            _mlBotDaggerRuntimeState);
        var suggestedAction = _mlBotDaggerPolicy.Evaluate(observation);
        var suggestedInput = MLBotActionDecoder.Decode(suggestedAction);
        var usedHumanOverride = HasMeaningfulHumanOverride(gameplayInput);
        var appliedInput = usedHumanOverride ? gameplayInput : suggestedInput;

        _mlBotDaggerCurrentObservation = observation;
        _mlBotDaggerHasCurrentObservation = true;
        _mlBotDaggerSuggestedAction = suggestedAction;
        _mlBotDaggerAppliedInput = appliedInput;
        _mlBotDaggerUsedHumanOverride = usedHumanOverride;
        return appliedInput;
    }

    private void OnMLBotDaggerAssistBeforeTick()
    {
        if (!IsMLBotDaggerAssistActive())
        {
            return;
        }

        if (!_world.LocalPlayer.IsAlive)
        {
            CancelMLBotDaggerAssist("ml dagger assist failed: local player died");
            return;
        }

        if (!_mlBotDaggerHasCurrentObservation)
        {
            return;
        }

        MLBotObservationRuntimeStateTracker.Update(_mlBotDaggerRuntimeState, _mlBotDaggerCurrentObservation, _world.LocalPlayer);

        _mlBotDaggerStartingRedCaps = _mlBotDaggerSamples.Count == 0 ? _world.RedCaps : _mlBotDaggerStartingRedCaps;
        _mlBotDaggerStartingBlueCaps = _mlBotDaggerSamples.Count == 0 ? _world.BlueCaps : _mlBotDaggerStartingBlueCaps;

        _mlBotDaggerPendingStep = new PendingMLBotDaggerStep(
            _mlBotDaggerSamples.Count,
            ResolveMLBotDaggerPhase(),
            _mlBotDaggerCurrentObservation,
            MLBotActionEncoder.Encode(_mlBotDaggerAppliedInput),
            MLBotActionEncoder.Encode(_mlBotDaggerHumanInput),
            _mlBotDaggerSuggestedAction,
            _mlBotDaggerUsedHumanOverride,
            _world.LocalPlayer.IsCarryingIntel,
            _world.RedCaps,
            _world.BlueCaps);
    }

    private void OnMLBotDaggerAssistAfterTick()
    {
        if (!IsMLBotDaggerAssistActive() || _mlBotDaggerPendingStep is not { } pendingStep)
        {
            return;
        }

        var nextResolvedPhase = ResolveMLBotDaggerPhase();
        var nextObservation = MLBotObservationBuilder.Build(
            _world,
            SimulationWorld.LocalPlayerSlot,
            _world.LocalPlayer,
            nextResolvedPhase,
            _mlBotDaggerRuntimeState);

        var pickedUpIntel = !pendingStep.WasCarryingIntel && _world.LocalPlayer.IsCarryingIntel;
        var scoredIntel = pendingStep.WasCarryingIntel
            && !_world.LocalPlayer.IsCarryingIntel
            && ((_world.RedCaps > pendingStep.RedCapsBefore) || (_world.BlueCaps > pendingStep.BlueCapsBefore));
        var died = !_world.LocalPlayer.IsAlive;
        var episodeEnded = died || scoredIntel;

        _mlBotDaggerSamples.Add(new MLBotDemonstrationSample
        {
            Tick = pendingStep.Tick,
            ResolvedPhase = pendingStep.ResolvedPhase,
            Observation = pendingStep.Observation,
            Action = pendingStep.Action,
            HumanAction = pendingStep.HumanAction,
            SuggestedAction = pendingStep.SuggestedAction,
            UsedHumanOverride = pendingStep.UsedHumanOverride,
            NextObservation = nextObservation,
            PickedUpIntel = pickedUpIntel,
            ScoredIntel = scoredIntel,
            Died = died,
            EpisodeEnded = episodeEnded,
        });

        if (_mlBotDaggerRequestedPhase == MLBotTaskPhase.AttackIntel
            && pickedUpIntel
            && _mlBotDaggerAttackCompletionStopTick < 0)
        {
            _mlBotDaggerAttackCompletionStopTick = _mlBotDaggerSamples.Count + MLBotDaggerAttackCompletionGraceTicks;
            AddConsoleLine($"ml dagger attack objective reached; saving in {MLBotDaggerAttackCompletionGraceTicks} ticks.");
        }

        _mlBotDaggerPendingStep = null;
        _mlBotDaggerHasCurrentObservation = false;

        if (episodeEnded && _mlBotDaggerRequestedPhase == MLBotTaskPhase.None)
        {
            StopAndSaveMLBotDaggerAssist(requestedLabel: null);
            return;
        }

        if (_mlBotDaggerRequestedPhase == MLBotTaskPhase.ReturnIntel && scoredIntel)
        {
            StopAndSaveMLBotDaggerAssist(requestedLabel: null);
            return;
        }

        if (_mlBotDaggerRequestedPhase == MLBotTaskPhase.AttackIntel
            && _mlBotDaggerAttackCompletionStopTick >= 0
            && _mlBotDaggerSamples.Count >= _mlBotDaggerAttackCompletionStopTick)
        {
            StopAndSaveMLBotDaggerAssist(
                requestedLabel: null,
                chainIntoReturn: _world.LocalPlayer.IsAlive && _world.LocalPlayer.IsCarryingIntel);
            return;
        }

        if (_mlBotDaggerSamples.Count >= MLBotDaggerAssistMaximumTicks)
        {
            StopAndSaveMLBotDaggerAssist(requestedLabel: null);
        }
    }

    private void StartMLBotDaggerAssist(MLBotTaskPhase requestedPhase, string? label)
    {
        if (!IsPracticeSessionActive)
        {
            AddConsoleLine("ml dagger assist is practice-only.");
            return;
        }

        if (_networkClient.IsConnected)
        {
            AddConsoleLine("ml dagger assist is offline-only.");
            return;
        }

        if (OperatingSystem.IsBrowser())
        {
            AddConsoleLine("ml dagger assist currently requires the desktop runtime.");
            return;
        }

        if (IsMLBotDaggerAssistActive())
        {
            AddConsoleLine("ml dagger assist is already active.");
            return;
        }

        if (IsMLBotDemonstrationRecorderActive())
        {
            AddConsoleLine("stop ml_demo_rec before starting ml_dagger.");
            return;
        }

        var modelPath = ResolveMLBotDaggerModelPath(requestedPhase, out var autoResolvedModel);
        if (string.IsNullOrWhiteSpace(modelPath) || !File.Exists(modelPath))
        {
            AddConsoleLine("ml dagger could not resolve a model for the current class/team/phase.");
            AddConsoleLine($"set {MLBotCaptureModelResolver.CaptureModelRootEnvironmentVariable} to a valid model root or set {MLBotPolicyRuntimeFactory.ModelPathEnvironmentVariable} explicitly.");
            return;
        }

        try
        {
            _mlBotDaggerPolicy = MLBotPolicyRuntimeFactory.CreateConfigured(modelPath);
        }
        catch (Exception exception)
        {
            AddConsoleLine($"ml dagger assist could not load model: {exception.Message}");
            if (_mlBotDaggerPolicy is IDisposable disposablePolicy)
            {
                disposablePolicy.Dispose();
            }
            _mlBotDaggerPolicy = null;
            return;
        }

        _mlBotDaggerAssistState = MLBotDaggerAssistState.Recording;
        _mlBotDaggerRequestedPhase = requestedPhase;
        _mlBotDaggerLabel = string.IsNullOrWhiteSpace(label) ? null : label.Trim();
        _mlBotDaggerPolicyPath = modelPath;
        _mlBotDaggerUsingAutoResolvedModel = autoResolvedModel;
        _mlBotDaggerHumanInput = default;
        _mlBotDaggerAppliedInput = default;
        _mlBotDaggerSuggestedAction = default;
        _mlBotDaggerUsedHumanOverride = false;
        _mlBotDaggerPendingStep = null;
        MLBotObservationRuntimeStateTracker.Reset(_mlBotDaggerRuntimeState);
        _mlBotDaggerAttackCompletionStopTick = -1;
        _mlBotDaggerStartingRedCaps = _world.RedCaps;
        _mlBotDaggerStartingBlueCaps = _world.BlueCaps;
        _mlBotDaggerSamples.Clear();
        _mlBotDaggerHasCurrentObservation = false;

        AddConsoleLine(
            $"ml dagger assist started: {_world.Level.Name} {_world.LocalPlayer.Team} {_world.LocalPlayer.ClassId} {DescribeMLBotDemonstrationPhase(requestedPhase)}");
        AddConsoleLine(autoResolvedModel
            ? "the phase-matched capture model is driving the local player. press movement/jump/fire inputs to correct it, then use the same hotkey or F3 to save."
            : "the model now drives the local player. press movement/jump/fire inputs to correct it, then use the same hotkey or F3 to save.");
        AddConsoleLine($"ml dagger policy: {MLBotPolicyRuntimeFactory.DescribeEnvironmentPolicyConfiguration(modelPath)}");
    }

    private void StopAndSaveMLBotDaggerAssist(string? requestedLabel, bool chainIntoReturn = false)
    {
        if (!IsMLBotDaggerAssistActive())
        {
            AddConsoleLine("ml dagger assist is not active.");
            return;
        }

        if (_mlBotDaggerSamples.Count == 0)
        {
            CancelMLBotDaggerAssist("ml dagger assist canceled: no samples were captured");
            return;
        }

        var metadata = BuildMLBotDaggerMetadata(requestedLabel);
        var document = new MLBotDemonstrationDocument
        {
            Metadata = metadata,
            Samples = _mlBotDaggerSamples.ToArray(),
        };
        var outputPath = MLBotCorrectionDemonstrationStore.ResolveWritablePath(metadata);
        var continueIntoReturn = chainIntoReturn
            && metadata.RequestedPhase == MLBotTaskPhase.AttackIntel
            && _world.LocalPlayer.IsAlive
            && _world.LocalPlayer.IsCarryingIntel;
        MLBotCorrectionDemonstrationStore.Save(document);
        ClearMLBotDaggerAssistState();

        AddConsoleLine(
            $"ml dagger saved: {metadata.Team} {metadata.ClassId} {DescribeMLBotDemonstrationPhase(metadata.RequestedPhase)} ticks={metadata.TickCount} success={metadata.Success}");
        AddConsoleLine($"ml dagger path: {outputPath}");

        if (continueIntoReturn)
        {
            AddConsoleLine("ml dagger chaining into return phase.");
            StartMLBotDaggerAssist(MLBotTaskPhase.ReturnIntel, label: null);
        }
    }

    private MLBotDemonstrationMetadata BuildMLBotDaggerMetadata(string? requestedLabel)
    {
        var label = string.IsNullOrWhiteSpace(requestedLabel)
            ? _mlBotDaggerLabel ?? string.Empty
            : requestedLabel.Trim();
        var success = HasMLBotDaggerSucceeded();
        return new MLBotDemonstrationMetadata
        {
            LevelName = _world.Level.Name,
            MapAreaIndex = _world.Level.MapAreaIndex,
            Mode = _world.MatchRules.Mode,
            Team = _world.LocalPlayer.Team,
            ClassId = _world.LocalPlayer.ClassId,
            RequestedPhase = _mlBotDaggerRequestedPhase,
            CaptureKind = MLBotDemonstrationCaptureKind.DaggerAssist,
            PolicyModelPath = _mlBotDaggerPolicyPath ?? string.Empty,
            Label = label,
            RecordedAtUtc = DateTimeOffset.UtcNow,
            TickCount = _mlBotDaggerSamples.Count,
            CaptureMaxTicks = MLBotDaggerAssistMaximumTicks,
            ShortCapture = false,
            Success = success,
            Outcome = success ? "success" : "manual_stop",
        };
    }

    private bool HasMLBotDaggerSucceeded()
    {
        for (var index = 0; index < _mlBotDaggerSamples.Count; index += 1)
        {
            if (_mlBotDaggerSamples[index].ScoredIntel)
            {
                return true;
            }
        }

        return _mlBotDaggerRequestedPhase switch
        {
            MLBotTaskPhase.AttackIntel => _world.LocalPlayer.IsCarryingIntel,
            MLBotTaskPhase.ReturnIntel => (_world.RedCaps > _mlBotDaggerStartingRedCaps)
                || (_world.BlueCaps > _mlBotDaggerStartingBlueCaps),
            MLBotTaskPhase.CaptureObjective => IsFriendlyControlPointActive(),
            _ => false,
        };
    }

    private MLBotTaskPhase ResolveMLBotDaggerPhase()
    {
        return _mlBotDaggerRequestedPhase == MLBotTaskPhase.None
            ? MLBotTaskStateResolver.Resolve(_world, _world.LocalPlayer)
            : _mlBotDaggerRequestedPhase;
    }

    private void PrintMLBotDaggerAssistStatus()
    {
        if (!IsMLBotDaggerAssistActive())
        {
            AddConsoleLine("ml dagger assist: idle");
            return;
        }

        AddConsoleLine(
            $"ml dagger assist: {_mlBotDaggerAssistState.ToString().ToLowerInvariant()} {DescribeMLBotDemonstrationPhase(_mlBotDaggerRequestedPhase)} ticks={_mlBotDaggerSamples.Count} model={_mlBotDaggerPolicyPath} auto={_mlBotDaggerUsingAutoResolvedModel}");
    }

    private void CancelMLBotDaggerAssist(string reason)
    {
        if (!IsMLBotDaggerAssistActive())
        {
            AddConsoleLine("ml dagger assist: idle");
            return;
        }

        ClearMLBotDaggerAssistState();
        AddConsoleLine(reason);
    }

    private void ClearMLBotDaggerAssistState()
    {
        _mlBotDaggerAssistState = MLBotDaggerAssistState.None;
        _mlBotDaggerRequestedPhase = MLBotTaskPhase.None;
        _mlBotDaggerHumanInput = default;
        _mlBotDaggerAppliedInput = default;
        _mlBotDaggerSuggestedAction = default;
        _mlBotDaggerUsedHumanOverride = false;
        _mlBotDaggerPendingStep = null;
        _mlBotDaggerLabel = null;
        _mlBotDaggerPolicyPath = null;
        _mlBotDaggerUsingAutoResolvedModel = false;
        _mlBotDaggerHasCurrentObservation = false;
        MLBotObservationRuntimeStateTracker.Reset(_mlBotDaggerRuntimeState);
        _mlBotDaggerAttackCompletionStopTick = -1;
        _mlBotDaggerSamples.Clear();
        if (_mlBotDaggerPolicy is IDisposable disposablePolicy)
        {
            disposablePolicy.Dispose();
        }
        _mlBotDaggerPolicy = null;
    }

    private bool IsMLBotDaggerAssistActive()
    {
        return _mlBotDaggerAssistState != MLBotDaggerAssistState.None;
    }

    private static bool HasMeaningfulHumanOverride(PlayerInputSnapshot input)
    {
        return input.Left
            || input.Right
            || input.Up
            || input.Down
            || input.FirePrimary
            || input.FireSecondary
            || input.DropIntel;
    }

    private static string? ExtractMLBotDaggerTrailingArgument(string commandText, string subcommand, string? phase = null)
    {
        var prefix = phase is null
            ? $"ml_dagger {subcommand}"
            : $"ml_dagger {subcommand} {phase}";
        if (!commandText.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var trailing = commandText[prefix.Length..].Trim();
        return string.IsNullOrWhiteSpace(trailing) ? null : trailing;
    }

    private string? ResolveMLBotDaggerModelPath(MLBotTaskPhase requestedPhase, out bool autoResolvedModel)
    {
        autoResolvedModel = false;
        if (IsMLBotCaptureModeEnabled())
        {
            var capturePhase = requestedPhase == MLBotTaskPhase.None
                ? MLBotTaskStateResolver.Resolve(_world, _world.LocalPlayer)
                : requestedPhase;
            var resolvedModelPath = MLBotCaptureModelResolver.ResolveBestModelPath(
                _world.Level.Name,
                _world.LocalPlayer.Team,
                _world.LocalPlayer.ClassId,
                capturePhase);
            if (!string.IsNullOrWhiteSpace(resolvedModelPath))
            {
                autoResolvedModel = true;
                return resolvedModelPath;
            }
        }

        var configuredModelPath = Environment.GetEnvironmentVariable(MLBotPolicyRuntimeFactory.ModelPathEnvironmentVariable);
        return !string.IsNullOrWhiteSpace(configuredModelPath) && File.Exists(configuredModelPath)
            ? configuredModelPath
            : null;
    }

    private readonly record struct PendingMLBotDaggerStep(
        int Tick,
        MLBotTaskPhase ResolvedPhase,
        MLBotObservation Observation,
        MLBotAction Action,
        MLBotAction HumanAction,
        MLBotAction SuggestedAction,
        bool UsedHumanOverride,
        bool WasCarryingIntel,
        int RedCapsBefore,
        int BlueCapsBefore);
}
