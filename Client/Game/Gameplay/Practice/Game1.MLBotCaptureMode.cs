#nullable enable

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using OpenGarrison.MLBot;
using OpenGarrison.MLBot.Contracts;
using System;
using System.Collections.Generic;

namespace OpenGarrison.Client;

public partial class Game1
{
    private const string MLBotCaptureMaximumTicksEnvironmentVariable = "OG_MLBOT_CAPTURE_MAX_TICKS";
    private static readonly MLBotControllerMode MLBotConfiguredMode = MLBotModeResolver.Resolve();
    private readonly List<string> _mlBotCaptureOverlayLines = new();

    private static bool IsMLBotCaptureModeEnabled()
    {
        return MLBotConfiguredMode == MLBotControllerMode.Capture;
    }

    private void HandleMLBotCaptureModeHotkeys(KeyboardState keyboard)
    {
        if (!IsMLBotCaptureModeEnabled() || _navEditorEnabled)
        {
            return;
        }

        if (IsKeyPressed(keyboard, Keys.F9))
        {
            ToggleMLBotCaptureShortDemo(MLBotTaskPhase.AttackIntel, grantEnemyIntel: false);
            return;
        }

        if (IsKeyPressed(keyboard, Keys.F6))
        {
            ToggleMLBotCaptureShortDemo(MLBotTaskPhase.CaptureObjective, grantEnemyIntel: false);
            return;
        }

        if (IsKeyPressed(keyboard, Keys.F10))
        {
            ToggleMLBotCaptureShortDemo(MLBotTaskPhase.ReturnIntel, grantEnemyIntel: true);
            return;
        }

        if (IsKeyPressed(keyboard, Keys.F7))
        {
            ToggleMLBotCaptureDaggerAssist(MLBotTaskPhase.AttackIntel, grantEnemyIntel: false);
            return;
        }

        if (IsKeyPressed(keyboard, Keys.F5))
        {
            ToggleMLBotCaptureDaggerAssist(MLBotTaskPhase.CaptureObjective, grantEnemyIntel: false);
            return;
        }

        if (IsKeyPressed(keyboard, Keys.F8))
        {
            ToggleMLBotCaptureDaggerAssist(MLBotTaskPhase.ReturnIntel, grantEnemyIntel: true);
            return;
        }

        if (IsKeyPressed(keyboard, Keys.F11))
        {
            ToggleMLBotCaptureDaggerAssist(MLBotTaskPhase.None, grantEnemyIntel: false);
            return;
        }

        if (IsKeyPressed(keyboard, Keys.F3))
        {
            StopOrPrintMLBotCaptureModeStatus();
        }
    }

    private void ToggleMLBotCaptureShortDemo(MLBotTaskPhase phase, bool grantEnemyIntel)
    {
        if (IsMLBotDaggerAssistActive())
        {
            AddConsoleLine("stop ml_dagger before starting an ml capture clip.");
            return;
        }

        if (IsMLBotDemonstrationRecorderActive())
        {
            if (_mlBotDemonstrationRequestedPhase == phase && _mlBotDemonstrationShortCapture)
            {
                StopAndSaveMLBotDemonstrationRecorder(requestedLabel: null);
                return;
            }

            AddConsoleLine($"ml capture already active for {DescribeMLBotDemonstrationPhase(_mlBotDemonstrationRequestedPhase)}. press that same hotkey again to save.");
            return;
        }

        if (grantEnemyIntel && !_world.LocalPlayer.IsCarryingIntel && !_world.ForceGiveEnemyIntelToLocalPlayer())
        {
            AddConsoleLine("ml capture could not grant enemy intel for a return clip.");
            return;
        }

        StartMLBotDemonstrationRecorder(
            phase,
            label: null,
            GetMLBotCaptureMaximumTicks(),
            shortCapture: true);
    }

    private void ToggleMLBotCaptureDaggerAssist(MLBotTaskPhase phase, bool grantEnemyIntel)
    {
        if (IsMLBotDemonstrationRecorderActive())
        {
            AddConsoleLine("stop ml_demo_rec before starting ml_dagger.");
            return;
        }

        if (IsMLBotDaggerAssistActive())
        {
            StopAndSaveMLBotDaggerAssist(requestedLabel: null);
            return;
        }

        if (grantEnemyIntel && !_world.LocalPlayer.IsCarryingIntel && !_world.ForceGiveEnemyIntelToLocalPlayer())
        {
            AddConsoleLine("ml dagger could not grant enemy intel for a return assist.");
            return;
        }

        StartMLBotDaggerAssist(phase, label: null);
    }

    private static int GetMLBotCaptureMaximumTicks()
    {
        var configured = Environment.GetEnvironmentVariable(MLBotCaptureMaximumTicksEnvironmentVariable);
        return int.TryParse(configured, out var maximumTicks)
            ? Math.Clamp(maximumTicks, 1, 18000)
            : 1200;
    }

    private void StopOrPrintMLBotCaptureModeStatus()
    {
        if (IsMLBotDemonstrationRecorderActive())
        {
            StopAndSaveMLBotDemonstrationRecorder(requestedLabel: null);
            return;
        }

        if (IsMLBotDaggerAssistActive())
        {
            StopAndSaveMLBotDaggerAssist(requestedLabel: null);
            return;
        }

        PrintMLBotCaptureModeStatus();
    }

    private void PrintMLBotCaptureModeStatus()
    {
        AddConsoleLine($"ml capture mode: {_world.Level.Name} {_world.LocalPlayer.Team} {_world.LocalPlayer.ClassId} short_ticks={GetMLBotCaptureMaximumTicks()}");
        AddConsoleLine("F3 stop/status | F6 capture short clip | F9 attack short clip (auto-chains to return after pickup) | F10 return short clip (+intel)");
        AddConsoleLine("F5 capture dagger | F7 attack dagger (auto-chains to return after pickup) | F8 return dagger (+intel) | F11 auto dagger");
        if (_world.KothUnlockTicksRemaining > 0)
        {
            AddConsoleLine($"KOTH point unlocks in {FormatMLBotCaptureModeSeconds(_world.KothUnlockTicksRemaining)}s; keep recording through approach, wait, cap, and brief hold.");
        }

        AddConsoleLine("class inference is automatic from the local player; labels are optional.");
        PrintMLBotDemonstrationRecorderStatus();
        PrintMLBotDaggerAssistStatus();
    }

    private void DrawMLBotCaptureOverlay()
    {
        if (!IsMLBotCaptureModeEnabled() || !IsPracticeSessionActive || _networkClient.IsConnected)
        {
            return;
        }

        _mlBotCaptureOverlayLines.Clear();
        _mlBotCaptureOverlayLines.Add($"ML {_world.Level.Name} {_world.LocalPlayer.Team} {_world.LocalPlayer.ClassId}");
        _mlBotCaptureOverlayLines.Add("F3 stop | F5/F7/F8 dagger | F11 auto");
        if (_world.KothUnlockTicksRemaining > 0)
        {
            _mlBotCaptureOverlayLines.Add($"KOTH unlock {FormatMLBotCaptureModeSeconds(_world.KothUnlockTicksRemaining)}s");
        }

        if (IsMLBotDemonstrationRecorderActive())
        {
            _mlBotCaptureOverlayLines.Add(
                $"rec {_mlBotDemonstrationRecorderState.ToString().ToLowerInvariant()} {DescribeMLBotDemonstrationPhase(_mlBotDemonstrationRequestedPhase)} {_mlBotDemonstrationSamples.Count}/{_mlBotDemonstrationMaximumTicks}");
        }
        else
        {
            _mlBotCaptureOverlayLines.Add("rec idle");
        }

        if (IsMLBotDaggerAssistActive())
        {
            _mlBotCaptureOverlayLines.Add($"dagger {DescribeMLBotDemonstrationPhase(_mlBotDaggerRequestedPhase)} t={_mlBotDaggerSamples.Count} {FormatMLBotCaptureOverlayModelName(_mlBotDaggerPolicyPath)}");
            if (_mlBotDaggerHasCurrentObservation)
            {
                _mlBotCaptureOverlayLines.Add($"{DescribeMLBotDemonstrationPhase(_mlBotDaggerCurrentObservation.TaskPhase)} d={_mlBotDaggerCurrentObservation.ObjectiveDistance:0} dd={_mlBotDaggerCurrentObservation.ObjectiveDistanceDelta:+0;-0;0} stuck={_mlBotDaggerCurrentObservation.StuckTicks:0}");
                _mlBotCaptureOverlayLines.Add($"obj {_mlBotDaggerCurrentObservation.Objective.RelativeX:0},{_mlBotDaggerCurrentObservation.Objective.RelativeY:0} carry={(_mlBotDaggerCurrentObservation.IsCarryingIntel ? "Y" : "N")}");
                _mlBotCaptureOverlayLines.Add($"{FormatMLBotCaptureOverlayAction(_mlBotDaggerSuggestedAction)} {(_mlBotDaggerUsedHumanOverride ? "human" : "model")}");
            }
        }
        else
        {
            _mlBotCaptureOverlayLines.Add("dagger idle");
        }

        const float textScale = 0.72f;
        var lineHeight = 14;
        var padding = 7;
        var width = Math.Min(340, Math.Max(260, ViewportWidth - 24));
        var height = (_mlBotCaptureOverlayLines.Count * lineHeight) + (padding * 2);
        var x = 12;
        var y = Math.Min(Math.Max(96, ViewportHeight - height - 12), 112);
        var rectangle = new Rectangle(x, y, width, height);
        _spriteBatch.Draw(_pixel, rectangle, new Color(18, 20, 24, 178));
        _spriteBatch.Draw(_pixel, new Rectangle(rectangle.X, rectangle.Y, rectangle.Width, 1), new Color(120, 214, 255, 210));

        var position = new Vector2(rectangle.X + padding, rectangle.Y + padding);
        for (var index = 0; index < _mlBotCaptureOverlayLines.Count; index += 1)
        {
            _spriteBatch.DrawString(
                _consoleFont,
                TrimMLBotCaptureOverlayLine(_mlBotCaptureOverlayLines[index], width - (padding * 2), textScale),
                position,
                new Color(236, 238, 242),
                0f,
                Vector2.Zero,
                textScale,
                Microsoft.Xna.Framework.Graphics.SpriteEffects.None,
                0f);
            position.Y += lineHeight;
        }
    }

    private int FormatMLBotCaptureModeSeconds(int ticks)
    {
        return Math.Max(0, (int)MathF.Ceiling(ticks / (float)Math.Max(1, _world.Config.TicksPerSecond)));
    }

    private static string FormatMLBotCaptureOverlayModelName(string? modelPath)
    {
        if (string.IsNullOrWhiteSpace(modelPath))
        {
            return "model=unset";
        }

        var name = System.IO.Path.GetFileNameWithoutExtension(modelPath);
        return string.IsNullOrWhiteSpace(name) ? "model=loaded" : $"model={name}";
    }

    private string TrimMLBotCaptureOverlayLine(string line, int maximumWidth, float scale)
    {
        if (_consoleFont.MeasureString(line).X * scale <= maximumWidth)
        {
            return line;
        }

        const string suffix = "...";
        var trimmed = line;
        while (trimmed.Length > suffix.Length && _consoleFont.MeasureString(trimmed + suffix).X * scale > maximumWidth)
        {
            trimmed = trimmed[..^1];
        }

        return trimmed + suffix;
    }

    private static string FormatMLBotCaptureOverlayAction(MLBotAction action)
    {
        var move = action.MoveDirection < 0
            ? "L"
            : action.MoveDirection > 0
                ? "R"
                : "-";
        return $"act m={move} j={(action.Jump ? "1" : "0")} c={(action.Crouch ? "1" : "0")} f={(action.FirePrimary ? "1" : "0")}/{(action.FireSecondary ? "1" : "0")} d={(action.DropIntel ? "1" : "0")}";
    }
}
