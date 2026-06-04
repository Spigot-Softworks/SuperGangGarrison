#nullable enable

using Microsoft.Xna.Framework;
using OpenGarrison.Core;
using OpenGarrison.Protocol;
using System;
using System.Text.Json;

namespace OpenGarrison.Client;

public partial class Game1
{
    private const string VotePresentationSourcePluginId = "chat.voting";
    private const string VotePresentationTargetPluginId = "open.garrison.client";
    private const string VotePresentationMessageType = "vote-event";
    private const int VotePresentationResultTicks = 180;
    private const int VotePresentationFlashTicks = 24;

    private static readonly JsonSerializerOptions VotePresentationJsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private VotePresentationState? _votePresentationState;

    private sealed class VotePresentationState
    {
        public string EventName { get; set; } = string.Empty;
        public string Kind { get; set; } = string.Empty;
        public string MapLabel { get; set; } = string.Empty;
        public string InitiatorName { get; set; } = string.Empty;
        public string ActorName { get; set; } = string.Empty;
        public int YesVotes { get; set; }
        public int NoVotes { get; set; }
        public int RequiredYesVotes { get; set; }
        public int EligibleVotes { get; set; }
        public int RemainingTicks { get; set; }
        public int ResultTicks { get; set; }
        public int FlashTicks { get; set; }
        public bool IsComplete { get; set; }
        public bool Passed { get; set; }
    }

    private sealed class VotePresentationPayload
    {
        public string EventName { get; set; } = string.Empty;
        public string Kind { get; set; } = string.Empty;
        public string MapLabel { get; set; } = string.Empty;
        public string InitiatorName { get; set; } = string.Empty;
        public byte? ActorSlot { get; set; }
        public string ActorName { get; set; } = string.Empty;
        public int YesVotes { get; set; }
        public int NoVotes { get; set; }
        public int RequiredYesVotes { get; set; }
        public int EligibleVotes { get; set; }
        public int SecondsRemaining { get; set; }
    }

    private bool TryHandleBuiltInVotePresentationMessage(ServerPluginMessage message)
    {
        if (!string.Equals(message.SourcePluginId, VotePresentationSourcePluginId, StringComparison.OrdinalIgnoreCase)
            || !string.Equals(message.TargetPluginId, VotePresentationTargetPluginId, StringComparison.OrdinalIgnoreCase)
            || !string.Equals(message.MessageTypeName, VotePresentationMessageType, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        try
        {
            var payload = JsonSerializer.Deserialize<VotePresentationPayload>(message.Payload, VotePresentationJsonOptions);
            if (payload is null || string.IsNullOrWhiteSpace(payload.EventName))
            {
                return true;
            }

            HandleVotePresentationPayload(payload);
        }
        catch (JsonException ex)
        {
            AddConsoleLine($"vote presentation payload rejected: {ex.Message}");
        }

        return true;
    }

    private void HandleVotePresentationPayload(VotePresentationPayload payload)
    {
        var eventName = payload.EventName.Trim().ToLowerInvariant();
        var state = _votePresentationState ??= new VotePresentationState();
        state.EventName = eventName;
        state.Kind = payload.Kind;
        state.MapLabel = payload.MapLabel;
        state.InitiatorName = payload.InitiatorName;
        state.ActorName = payload.ActorName;
        state.YesVotes = Math.Max(0, payload.YesVotes);
        state.NoVotes = Math.Max(0, payload.NoVotes);
        state.RequiredYesVotes = Math.Max(1, payload.RequiredYesVotes);
        state.EligibleVotes = Math.Max(0, payload.EligibleVotes);
        state.RemainingTicks = Math.Max(0, payload.SecondsRemaining * SimulationConfig.DefaultTicksPerSecond);
        state.FlashTicks = VotePresentationFlashTicks;

        switch (eventName)
        {
            case "started":
                state.IsComplete = false;
                state.Passed = false;
                state.ResultTicks = 0;
                PlayVotePresentationSound("VoteEngageSnd");
                break;
            case "yes":
                state.IsComplete = false;
                PlayVotePresentationSound("VoteYesSnd");
                break;
            case "no":
                state.IsComplete = false;
                PlayVotePresentationSound("VoteNoSnd");
                break;
            case "passed":
                state.IsComplete = true;
                state.Passed = true;
                state.ResultTicks = VotePresentationResultTicks;
                PlayVotePresentationSound("VoteSuccessSnd");
                break;
            case "failed":
            case "expired":
            case "canceled":
                state.IsComplete = true;
                state.Passed = false;
                state.ResultTicks = VotePresentationResultTicks;
                PlayVotePresentationSound("VoteFailSnd");
                break;
        }
    }

    private void UpdateVotePresentation(int clientTicks)
    {
        var state = _votePresentationState;
        if (state is null)
        {
            return;
        }

        var ticks = Math.Max(0, clientTicks);
        if (ticks == 0)
        {
            return;
        }

        state.FlashTicks = Math.Max(0, state.FlashTicks - ticks);
        if (!state.IsComplete)
        {
            state.RemainingTicks = Math.Max(0, state.RemainingTicks - ticks);
            return;
        }

        state.ResultTicks = Math.Max(0, state.ResultTicks - ticks);
        if (state.ResultTicks <= 0)
        {
            _votePresentationState = null;
        }
    }

    private void DrawVotePresentationOverlay()
    {
        var state = _votePresentationState;
        if (state is null)
        {
            return;
        }

        var width = Math.Min(330, Math.Max(240, ViewportWidth - 24));
        var height = 104;
        var bounds = new Rectangle(
            Math.Max(12, ViewportWidth - width - 12),
            58,
            width,
            height);
        var alpha = state.IsComplete
            ? Math.Clamp(state.ResultTicks / 45f, 0.35f, 1f)
            : 1f;
        var fill = state.Passed
            ? new Color(42, 62, 48) * (0.94f * alpha)
            : state.IsComplete
                ? new Color(66, 42, 42) * (0.94f * alpha)
                : new Color(54, 51, 50) * (0.94f * alpha);
        var outline = state.FlashTicks > 0
            ? new Color(255, 242, 190) * alpha
            : new Color(213, 205, 188) * (0.86f * alpha);

        DrawRoundedRectangleOutline(bounds, fill, outline, outlineThickness: 2, radius: 6);

        var title = state.IsComplete
            ? state.Passed ? "Vote passed" : GetVoteFailureTitle(state.EventName)
            : "Vote in progress";
        DrawBitmapFontText(title, new Vector2(bounds.X + 12f, bounds.Y + 9f), Color.White * alpha, 1f);

        var mapText = TrimBitmapMenuText(state.MapLabel, bounds.Width - 24f, 0.86f);
        DrawBitmapFontText(mapText, new Vector2(bounds.X + 12f, bounds.Y + 31f), new Color(230, 220, 180) * alpha, 0.86f);

        var subtitle = state.IsComplete
            ? $"{state.YesVotes} yes / {state.NoVotes} no"
            : $"{Math.Max(0, state.RemainingTicks / SimulationConfig.DefaultTicksPerSecond)}s remaining";
        DrawBitmapFontText(subtitle, new Vector2(bounds.X + 12f, bounds.Y + 50f), new Color(210, 210, 210) * alpha, 0.78f);

        DrawVoteCountRow(bounds, state, alpha);
    }

    private void DrawVoteCountRow(Rectangle bounds, VotePresentationState state, float alpha)
    {
        var rowY = bounds.Bottom - 31f;
        var yesX = bounds.X + 16f;
        var noX = bounds.X + 128f;
        TryDrawScreenSprite("VoteYesS", 0, new Vector2(yesX + 8f, rowY + 8f), Color.White * alpha, new Vector2(2f, 2f));
        TryDrawScreenSprite("VoteNoS", 0, new Vector2(noX + 8f, rowY + 8f), Color.White * alpha, new Vector2(2f, 2f));
        DrawBitmapFontText($"YES {state.YesVotes}", new Vector2(yesX + 28f, rowY + 1f), Color.White * alpha, 0.86f);
        DrawBitmapFontText($"NO {state.NoVotes}", new Vector2(noX + 28f, rowY + 1f), Color.White * alpha, 0.86f);

        var required = state.IsComplete
            ? string.Empty
            : $"Need {state.RequiredYesVotes}";
        if (!string.IsNullOrWhiteSpace(required))
        {
            DrawBitmapFontText(required, new Vector2(bounds.Right - 86f, rowY + 1f), new Color(230, 220, 180) * alpha, 0.78f);
        }
    }

    private static string GetVoteFailureTitle(string eventName)
    {
        return eventName switch
        {
            "expired" => "Vote expired",
            "canceled" => "Vote canceled",
            _ => "Vote failed",
        };
    }

    private void PlayVotePresentationSound(string soundName)
    {
        if (!_audioAvailable || _runtimeAssets is null)
        {
            return;
        }

        TryPlaySound(_runtimeAssets.GetSound(soundName), 0.88f, 0f, 0f);
    }
}
