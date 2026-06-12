#nullable enable

using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using OpenGarrison.Core;
using OpenGarrison.Protocol;

namespace OpenGarrison.Client;

public partial class Game1
{
    private const int EvasionMissPopupTicks = 72;
    private const float EvasionMissPopupFadeTicks = 24f;
    private const float EvasionMissPopupRisePerTick = 0.32f;
    private const float EvasionMissPopupImageScale = 1.6f;
    private const float EvasionMissPopupTextScale = 1.35f;
    private readonly Dictionary<int, EvasionMissPopupState> _evasionMissPopupsByPlayerId = new();

    private void ObserveEvasionMissDamageEvent(WorldDamageEvent damageEvent)
    {
        if (!IsEvasionMissEvent(damageEvent.Flags, damageEvent.TargetKind))
        {
            return;
        }

        TryTriggerEvasionMissPopup(damageEvent.TargetEntityId);
    }

    private void ObserveEvasionMissDamageEvent(SnapshotDamageEvent damageEvent)
    {
        if (!IsEvasionMissEvent((DamageEventFlags)damageEvent.Flags, (DamageTargetKind)damageEvent.TargetKind))
        {
            return;
        }

        TryTriggerEvasionMissPopup(damageEvent.TargetEntityId);
    }

    private static bool IsEvasionMissEvent(DamageEventFlags flags, DamageTargetKind targetKind)
    {
        return targetKind == DamageTargetKind.Player
            && flags.HasFlag(DamageEventFlags.Evaded)
            && !flags.HasFlag(DamageEventFlags.GhostDash)
            && !flags.HasFlag(DamageEventFlags.CivvieUmbrellaBlock);
    }

    private void TryTriggerEvasionMissPopup(int targetPlayerId)
    {
        if (FindPlayerById(targetPlayerId) is not { } targetPlayer)
        {
            return;
        }

        if (!_evasionMissPopupsByPlayerId.TryGetValue(targetPlayer.Id, out var popup))
        {
            popup = new EvasionMissPopupState();
            _evasionMissPopupsByPlayerId[targetPlayer.Id] = popup;
        }

        popup.TicksRemaining = EvasionMissPopupTicks;
        popup.Rise = 0f;
    }

    private void UpdateEvasionMissPopups()
    {
        if (_evasionMissPopupsByPlayerId.Count == 0)
        {
            return;
        }

        List<int>? expiredPlayerIds = null;
        foreach (var pair in _evasionMissPopupsByPlayerId)
        {
            var popup = pair.Value;
            popup.TicksRemaining -= 1;
            popup.Rise += EvasionMissPopupRisePerTick;
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
            _evasionMissPopupsByPlayerId.Remove(expiredPlayerIds[index]);
        }
    }

    private void DrawEvasionMissPopup(PlayerEntity player, Vector2 cameraPosition)
    {
        if (!_evasionMissPopupsByPlayerId.TryGetValue(player.Id, out var popup))
        {
            return;
        }

        var alpha = Math.Clamp(popup.TicksRemaining / EvasionMissPopupFadeTicks, 0f, 1f);
        var position = new Vector2(player.X - cameraPosition.X, player.Top - cameraPosition.Y - 34f - popup.Rise);
        if (DrawGameplayMissPopupImage(position, alpha, EvasionMissPopupImageScale))
        {
            return;
        }

        DrawBitmapFontTextCentered("MISS!", position + new Vector2(2f, 2f), Color.Black * alpha, EvasionMissPopupTextScale);
        DrawBitmapFontTextCentered("MISS!", position, new Color(255, 230, 64) * alpha, EvasionMissPopupTextScale);
    }

    private void ResetEvasionMissPopups()
    {
        _evasionMissPopupsByPlayerId.Clear();
    }

    private sealed class EvasionMissPopupState
    {
        public int TicksRemaining;
        public float Rise;
    }
}
