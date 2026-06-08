#nullable enable

using System.Collections.Generic;
using Microsoft.Xna.Framework;
using OpenGarrison.Core;
using OpenGarrison.Protocol;

namespace OpenGarrison.Client;

public partial class Game1
{
    private const int HeavyDashDodgePopupTicks = 72;
    private const float HeavyDashDodgePopupFadeTicks = 24f;
    private const float HeavyDashDodgePopupRisePerTick = 0.32f;
    private const float HeavyDashDodgePopupImageScale = 1.6f;
    private const float HeavyDashDodgePopupTextScale = 1.35f;
    private readonly Dictionary<int, HeavyDashDodgePopupState> _heavyDashDodgePopupsByPlayerId = new();

    private void ObserveHeavyDashDodgeDamageEvent(WorldDamageEvent damageEvent)
    {
        if (!IsHeavyDashDodgeEvent(damageEvent.Flags))
        {
            return;
        }

        TryTriggerHeavyDashDodgePopup(damageEvent.TargetKind, damageEvent.TargetEntityId);
    }

    private void ObserveHeavyDashDodgeDamageEvent(SnapshotDamageEvent damageEvent)
    {
        if (!IsHeavyDashDodgeEvent((DamageEventFlags)damageEvent.Flags))
        {
            return;
        }

        TryTriggerHeavyDashDodgePopup((DamageTargetKind)damageEvent.TargetKind, damageEvent.TargetEntityId);
    }

    private static bool IsHeavyDashDodgeEvent(DamageEventFlags flags)
    {
        return flags.HasFlag(DamageEventFlags.Evaded)
            && flags.HasFlag(DamageEventFlags.GhostDash);
    }

    private void TryTriggerHeavyDashDodgePopup(DamageTargetKind targetKind, int targetEntityId)
    {
        if (targetKind != DamageTargetKind.Player
            || FindPlayerById(targetEntityId) is not { } targetPlayer)
        {
            return;
        }

        if (!_heavyDashDodgePopupsByPlayerId.TryGetValue(targetPlayer.Id, out var popup))
        {
            popup = new HeavyDashDodgePopupState();
            _heavyDashDodgePopupsByPlayerId[targetPlayer.Id] = popup;
        }

        popup.TicksRemaining = HeavyDashDodgePopupTicks;
        popup.Rise = 0f;
    }

    private void UpdateHeavyDashDodgePopup()
    {
        if (_heavyDashDodgePopupsByPlayerId.Count == 0)
        {
            return;
        }

        List<int>? expiredPlayerIds = null;
        foreach (var pair in _heavyDashDodgePopupsByPlayerId)
        {
            var popup = pair.Value;
            popup.TicksRemaining -= 1;
            popup.Rise += HeavyDashDodgePopupRisePerTick;
            if (popup.TicksRemaining <= 0)
            {
                expiredPlayerIds ??= new List<int>();
                expiredPlayerIds.Add(pair.Key);
            }
        }

        if (expiredPlayerIds is null)
        {
            return;
        }

        for (var index = 0; index < expiredPlayerIds.Count; index += 1)
        {
            _heavyDashDodgePopupsByPlayerId.Remove(expiredPlayerIds[index]);
        }
    }

    private void DrawHeavyDashDodgePopup(PlayerEntity player, Vector2 cameraPosition)
    {
        if (!_heavyDashDodgePopupsByPlayerId.TryGetValue(player.Id, out var popup))
        {
            return;
        }

        var alpha = MathHelper.Clamp(popup.TicksRemaining / HeavyDashDodgePopupFadeTicks, 0f, 1f);
        var position = new Vector2(player.X - cameraPosition.X, player.Top - cameraPosition.Y - 34f - popup.Rise);
        if (DrawGameplayMissPopupImage(position, alpha, HeavyDashDodgePopupImageScale))
        {
            return;
        }

        DrawBitmapFontTextCentered("Dodged!", position + new Vector2(2f, 2f), Color.Black * alpha, HeavyDashDodgePopupTextScale);
        DrawBitmapFontTextCentered("Dodged!", position, new Color(255, 230, 64) * alpha, HeavyDashDodgePopupTextScale);
    }

    private void ResetHeavyDashDodgePopups()
    {
        _heavyDashDodgePopupsByPlayerId.Clear();
    }

    private sealed class HeavyDashDodgePopupState
    {
        public int TicksRemaining;
        public float Rise;
    }
}
