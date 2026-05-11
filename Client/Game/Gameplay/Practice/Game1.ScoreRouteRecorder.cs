#nullable enable

using Microsoft.Xna.Framework.Input;
using OpenGarrison.Core;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace OpenGarrison.Client;

public partial class Game1
{
    private const int BotBrainCorridorRecorderSampleStrideTicks = 6;
    private const float BotBrainCorridorRecorderSampleDistance = 48f;

    private static readonly JsonSerializerOptions BotBrainCorridorRecorderJsonOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly List<BotBrainCorridorRecorderSample> _botBrainCorridorRecorderSamples = [];
    private readonly List<BotBrainCorridorRecorderMark> _botBrainCorridorRecorderMarks = [];
    private PlayerInputSnapshot _scoreRouteRecorderLastInput;
    private BotBrainCorridorRecorderSample? _botBrainCorridorRecorderLastSample;
    private bool _botBrainCorridorRecorderActive;
    private int _botBrainCorridorRecorderStartRedCaps;
    private int _botBrainCorridorRecorderStartBlueCaps;
    private long _botBrainCorridorRecorderStartFrame;
    private bool _botBrainCorridorRecorderLastGrounded;
    private bool _botBrainCorridorRecorderLastCarryingIntel;
    private float _botBrainCorridorRecorderLastMoveDirection;

    private bool HandleScoreRouteRecorderConsoleCommand(string commandText, string[] parts)
    {
        AddConsoleLine("BotBrain recorder uses function keys: F6 start/stop, F7 lane mark, F8 objective mark, F9 branch mark, F10 cancel.");
        return true;
    }

    private void UpdateBotBrainCorridorRecorderHotkeys(KeyboardState keyboard)
    {
        if (_consoleOpen || _mainMenuOpen)
        {
            return;
        }

        if (IsKeyPressed(keyboard, Keys.F6))
        {
            if (_botBrainCorridorRecorderActive)
            {
                StopBotBrainCorridorRecording(save: true);
            }
            else
            {
                StartBotBrainCorridorRecording();
            }
        }

        if (IsKeyPressed(keyboard, Keys.F7))
        {
            AddBotBrainCorridorRecorderMark("Lane");
        }

        if (IsKeyPressed(keyboard, Keys.F8))
        {
            AddBotBrainCorridorRecorderMark("Objective");
        }

        if (IsKeyPressed(keyboard, Keys.F9))
        {
            AddBotBrainCorridorRecorderMark("Branch");
        }

        if (IsKeyPressed(keyboard, Keys.F10))
        {
            StopBotBrainCorridorRecording(save: false);
        }
    }

    private void SetScoreRouteRecorderCaptureInput(PlayerInputSnapshot gameplayInput)
    {
        _scoreRouteRecorderLastInput = gameplayInput;
    }

    private void OnScoreRouteRecorderBeforeTick()
    {
        _ = _scoreRouteRecorderLastInput;
    }

    private void OnScoreRouteRecorderAfterTick()
    {
        if (!_botBrainCorridorRecorderActive || _world.LocalPlayerAwaitingJoin)
        {
            return;
        }

        var player = _world.LocalPlayer;
        if (!player.IsAlive)
        {
            AddBotBrainCorridorRecorderSample("Death");
            return;
        }

        var moveDirection = ResolveRecordedMoveDirection(_scoreRouteRecorderLastInput);
        var reason = ResolveBotBrainCorridorSampleReason(player, moveDirection);
        if (reason is null)
        {
            return;
        }

        AddBotBrainCorridorRecorderSample(reason);
    }

    private void StartBotBrainCorridorRecording()
    {
        _botBrainCorridorRecorderSamples.Clear();
        _botBrainCorridorRecorderMarks.Clear();
        _botBrainCorridorRecorderLastSample = null;
        _botBrainCorridorRecorderActive = true;
        _botBrainCorridorRecorderStartFrame = _world.Frame;
        _botBrainCorridorRecorderStartRedCaps = _world.RedCaps;
        _botBrainCorridorRecorderStartBlueCaps = _world.BlueCaps;
        _botBrainCorridorRecorderLastGrounded = _world.LocalPlayer.IsGrounded;
        _botBrainCorridorRecorderLastCarryingIntel = _world.LocalPlayer.IsCarryingIntel;
        _botBrainCorridorRecorderLastMoveDirection = ResolveRecordedMoveDirection(_scoreRouteRecorderLastInput);
        AddBotBrainCorridorRecorderSample("Start");
        AddConsoleLine("BotBrain recorder started. F6 save/stop, F7 lane, F8 objective, F9 branch, F10 cancel.");
    }

    private void StopBotBrainCorridorRecording(bool save)
    {
        if (!_botBrainCorridorRecorderActive)
        {
            return;
        }

        AddBotBrainCorridorRecorderSample(save ? "Stop" : "Cancel");
        _botBrainCorridorRecorderActive = false;
        if (!save)
        {
            AddConsoleLine("BotBrain recorder canceled.");
            return;
        }

        var path = SaveBotBrainCorridorRecording();
        AddConsoleLine($"BotBrain recorder saved {Path.GetFileName(path)} samples={_botBrainCorridorRecorderSamples.Count} marks={_botBrainCorridorRecorderMarks.Count}");
    }

    private void AddBotBrainCorridorRecorderMark(string kind)
    {
        if (!_botBrainCorridorRecorderActive)
        {
            AddConsoleLine("BotBrain recorder is not active. Press F6 to start.");
            return;
        }

        var player = _world.LocalPlayer;
        _botBrainCorridorRecorderMarks.Add(new BotBrainCorridorRecorderMark(
            Kind: kind,
            Frame: _world.Frame,
            Tick: (int)(_world.Frame - _botBrainCorridorRecorderStartFrame),
            X: player.X,
            Y: player.Y,
            Bottom: player.Bottom,
            IsGrounded: player.IsGrounded,
            IsCarryingIntel: player.IsCarryingIntel));
        AddBotBrainCorridorRecorderSample($"Mark:{kind}");
        AddConsoleLine($"BotBrain recorder mark {kind} at ({player.X:0},{player.Y:0}).");
    }

    private string? ResolveBotBrainCorridorSampleReason(PlayerEntity player, float moveDirection)
    {
        if (_botBrainCorridorRecorderLastSample is null)
        {
            return "Start";
        }

        if (player.IsGrounded != _botBrainCorridorRecorderLastGrounded)
        {
            return player.IsGrounded ? "Land" : "Airborne";
        }

        if (player.IsCarryingIntel != _botBrainCorridorRecorderLastCarryingIntel)
        {
            return player.IsCarryingIntel ? "IntelPickup" : "IntelDropOrCap";
        }

        if (moveDirection != 0f
            && _botBrainCorridorRecorderLastMoveDirection != 0f
            && moveDirection != _botBrainCorridorRecorderLastMoveDirection)
        {
            return "DirectionChange";
        }

        if (_world.Frame - _botBrainCorridorRecorderLastSample.Frame >= BotBrainCorridorRecorderSampleStrideTicks)
        {
            return "Stride";
        }

        var dx = player.X - _botBrainCorridorRecorderLastSample.X;
        var dy = player.Y - _botBrainCorridorRecorderLastSample.Y;
        return (dx * dx) + (dy * dy) >= BotBrainCorridorRecorderSampleDistance * BotBrainCorridorRecorderSampleDistance
            ? "Distance"
            : null;
    }

    private void AddBotBrainCorridorRecorderSample(string reason)
    {
        var player = _world.LocalPlayer;
        var moveDirection = ResolveRecordedMoveDirection(_scoreRouteRecorderLastInput);
        var sample = new BotBrainCorridorRecorderSample(
            Frame: _world.Frame,
            Tick: (int)(_world.Frame - _botBrainCorridorRecorderStartFrame),
            Reason: reason,
            X: player.X,
            Y: player.Y,
            Bottom: player.Bottom,
            HorizontalSpeed: player.HorizontalSpeed,
            VerticalSpeed: player.VerticalSpeed,
            IsGrounded: player.IsGrounded,
            RemainingAirJumps: player.RemainingAirJumps,
            MoveDirection: moveDirection,
            Jump: _scoreRouteRecorderLastInput.Up,
            DropDown: _scoreRouteRecorderLastInput.Down,
            IsCarryingIntel: player.IsCarryingIntel,
            RedCaps: _world.RedCaps,
            BlueCaps: _world.BlueCaps);
        _botBrainCorridorRecorderSamples.Add(sample);
        _botBrainCorridorRecorderLastSample = sample;
        _botBrainCorridorRecorderLastGrounded = player.IsGrounded;
        _botBrainCorridorRecorderLastCarryingIntel = player.IsCarryingIntel;
        _botBrainCorridorRecorderLastMoveDirection = moveDirection;
    }

    private string SaveBotBrainCorridorRecording()
    {
        Directory.CreateDirectory(Path.Combine("artifacts", "botbrain-recordings"));
        var player = _world.LocalPlayer;
        var recording = new BotBrainCorridorRecording(
            FormatVersion: 1,
            LevelName: _world.Level.Name,
            MapAreaIndex: _world.Level.MapAreaIndex,
            MapScale: _world.Level.MapScale,
            Mode: _world.MatchRules.Mode.ToString(),
            Team: _world.LocalPlayerTeam,
            PlayerClass: player.ClassId,
            StartFrame: _botBrainCorridorRecorderStartFrame,
            EndFrame: _world.Frame,
            StartRedCaps: _botBrainCorridorRecorderStartRedCaps,
            StartBlueCaps: _botBrainCorridorRecorderStartBlueCaps,
            EndRedCaps: _world.RedCaps,
            EndBlueCaps: _world.BlueCaps,
            Samples: _botBrainCorridorRecorderSamples.ToArray(),
            Marks: _botBrainCorridorRecorderMarks.ToArray());
        var timestamp = DateTimeOffset.Now.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);
        var fileName = $"{SanitizeRecordingToken(_world.Level.Name)}.a{_world.Level.MapAreaIndex}.{_world.LocalPlayerTeam}.{player.ClassId}.{timestamp}.botbrain-corridor.json";
        var path = Path.Combine("artifacts", "botbrain-recordings", fileName);
        File.WriteAllText(path, JsonSerializer.Serialize(recording, BotBrainCorridorRecorderJsonOptions));
        return path;
    }

    private static float ResolveRecordedMoveDirection(PlayerInputSnapshot input)
    {
        if (input.Left == input.Right)
        {
            return 0f;
        }

        return input.Left ? -1f : 1f;
    }

    private static string SanitizeRecordingToken(string value)
    {
        var chars = value.ToCharArray();
        for (var i = 0; i < chars.Length; i += 1)
        {
            if (!char.IsLetterOrDigit(chars[i]) && chars[i] != '-' && chars[i] != '_')
            {
                chars[i] = '_';
            }
        }

        return new string(chars);
    }

    private sealed record BotBrainCorridorRecording(
        int FormatVersion,
        string LevelName,
        int MapAreaIndex,
        float MapScale,
        string Mode,
        PlayerTeam Team,
        PlayerClass PlayerClass,
        long StartFrame,
        long EndFrame,
        int StartRedCaps,
        int StartBlueCaps,
        int EndRedCaps,
        int EndBlueCaps,
        BotBrainCorridorRecorderSample[] Samples,
        BotBrainCorridorRecorderMark[] Marks);

    private sealed record BotBrainCorridorRecorderSample(
        long Frame,
        int Tick,
        string Reason,
        float X,
        float Y,
        float Bottom,
        float HorizontalSpeed,
        float VerticalSpeed,
        bool IsGrounded,
        int RemainingAirJumps,
        float MoveDirection,
        bool Jump,
        bool DropDown,
        bool IsCarryingIntel,
        int RedCaps,
        int BlueCaps);

    private sealed record BotBrainCorridorRecorderMark(
        string Kind,
        long Frame,
        int Tick,
        float X,
        float Y,
        float Bottom,
        bool IsGrounded,
        bool IsCarryingIntel);
}
