#nullable enable

using Microsoft.Xna.Framework;
using OpenGarrison.Protocol;
using System;
using System.Text.Json;

namespace OpenGarrison.Client;

public partial class Game1
{
    private const string VipPresentationSourcePluginId = "vip.mode";
    private const string VipPresentationTargetPluginId = "open.garrison.client";
    private const string VipPresentationMessageType = "vip-event";
    private const int VipPresentationDefaultTicks = 180;

    private static readonly JsonSerializerOptions VipPresentationJsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private VipPresentationState? _vipPresentationState;

    private sealed class VipPresentationState
    {
        public string Text { get; set; } = string.Empty;
        public int DurationTicks { get; set; }
        public int TicksRemaining { get; set; }
    }

    private sealed class VipPresentationPayload
    {
        public string Text { get; set; } = string.Empty;
        public int DurationTicks { get; set; }
    }

    private bool TryHandleBuiltInVipPresentationMessage(ServerPluginMessage message)
    {
        if (!string.Equals(message.SourcePluginId, VipPresentationSourcePluginId, StringComparison.OrdinalIgnoreCase)
            || !string.Equals(message.TargetPluginId, VipPresentationTargetPluginId, StringComparison.OrdinalIgnoreCase)
            || !string.Equals(message.MessageTypeName, VipPresentationMessageType, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        try
        {
            var payload = JsonSerializer.Deserialize<VipPresentationPayload>(message.Payload, VipPresentationJsonOptions);
            if (payload is null || string.IsNullOrWhiteSpace(payload.Text))
            {
                return true;
            }

            ShowVipPresentation(payload.Text, payload.DurationTicks);
        }
        catch (JsonException ex)
        {
            AddConsoleLine($"vip presentation payload rejected: {ex.Message}");
        }

        return true;
    }

    private void ShowVipPresentation(string text, int durationTicks)
    {
        var ticks = durationTicks <= 0 ? VipPresentationDefaultTicks : durationTicks;
        _vipPresentationState = new VipPresentationState
        {
            Text = text.Trim(),
            DurationTicks = Math.Max(1, ticks),
            TicksRemaining = Math.Max(1, ticks),
        };

        if (_audioAvailable && _runtimeAssets is not null)
        {
            TryPlaySound(_runtimeAssets.GetSound("NoticeSnd"), 0.82f, 0f, 0f);
        }
    }

    private void UpdateVipPresentation(int clientTicks)
    {
        if (_vipPresentationState is null)
        {
            return;
        }

        _vipPresentationState.TicksRemaining = Math.Max(0, _vipPresentationState.TicksRemaining - Math.Max(0, clientTicks));
        if (_vipPresentationState.TicksRemaining <= 0)
        {
            _vipPresentationState = null;
        }
    }

    private void DrawVipPresentationOverlay()
    {
        var state = _vipPresentationState;
        if (state is null)
        {
            return;
        }

        var progress = 1f - (state.TicksRemaining / (float)Math.Max(1, state.DurationTicks));
        var fadeIn = Math.Clamp(progress / 0.16f, 0f, 1f);
        var fadeOut = Math.Clamp(state.TicksRemaining / 42f, 0f, 1f);
        var alpha = Math.Min(fadeIn, fadeOut) * 0.96f;
        if (alpha <= 0f)
        {
            return;
        }

        var text = state.Text.ToUpperInvariant();
        var scale = text.Length > 22 ? 1.7f : 2.1f;
        var position = new Vector2(ViewportWidth / 2f, ViewportHeight * 0.2f);
        DrawHudTextCentered(text, position + new Vector2(2f, 2f), Color.Black * (alpha * 0.75f), scale);
        DrawHudTextCentered(text, position, new Color(241, 232, 203) * alpha, scale);
    }
}
