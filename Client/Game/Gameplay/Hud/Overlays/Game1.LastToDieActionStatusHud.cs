#nullable enable

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Microsoft.Xna.Framework;
using OpenGarrison.Core;
using OpenGarrison.Core.LastToDie;
using OpenGarrison.Protocol;

namespace OpenGarrison.Client;

internal enum LastToDieActionStatusTone : byte
{
    Info = 0,
    Ready = 1,
    Active = 2,
    Cooldown = 3,
    Warning = 4,
    Beneficial = 5,
}

internal readonly record struct LastToDieActionStatusLine(
    string Text,
    LastToDieActionStatusTone Tone);

public partial class Game1
{
    private const float LastToDieActionStatusTextScale = 0.78f;
    private const float LastToDieActionStatusLineHeight = 18f;

    private bool ShouldDrawLastToDieActionStatusHud()
    {
        var hostedSnapshot = _networkClient.IsConnected
            ? _networkClient.LastToDieState.Snapshot
            : null;
        return ShouldPresentLastToDieActionStatusHud(
            IsLastToDieSessionActive,
            _lastToDieRun is not null,
            _lastToDiePerkMenuOpen || IsLastToDieFailurePresentationActive(),
            _networkClient.IsConnected,
            hostedSnapshot?.Phase,
            _world.LocalPlayer.IsAlive,
            _world.LocalPlayerAwaitingJoin);
    }

    internal static bool ShouldPresentLastToDieActionStatusHud(
        bool offlineSessionActive,
        bool offlineRunAvailable,
        bool offlinePresentationBlocked,
        bool hostedConnected,
        LastToDieWirePhase? hostedPhase,
        bool localPlayerAlive,
        bool localPlayerAwaitingJoin)
    {
        if (!localPlayerAlive || localPlayerAwaitingJoin)
        {
            return false;
        }

        var offlinePlaying = offlineSessionActive
            && offlineRunAvailable
            && !offlinePresentationBlocked;
        var hostedPlaying = hostedConnected
            && hostedPhase == LastToDieWirePhase.Playing;
        return offlinePlaying || hostedPlaying;
    }

    private void DrawLastToDieActionStatusHud()
    {
        if (!ShouldDrawLastToDieActionStatusHud()
            || !TryResolveHudElement(HudElementId.LastToDieActionStatus, out var resolved))
        {
            return;
        }

        var localPlayer = _world.LocalPlayer;
        var predictedActionPlayer = IsUsingPredictedLocalState(localPlayer)
            ? _predictedLocalPlayerShadow
            : null;
        var lines = BuildLastToDieActionStatusLines(
            localPlayer,
            _config.TicksPerSecond,
            ResolveLastToDieMarkedTargetLabel,
            _world.CountOwnedLastToDieSniperExplosiveArrows(localPlayer),
            predictedActionPlayer);
        if (lines.Count == 0)
        {
            return;
        }

        var layoutScale = Math.Max(0.25f, resolved.Layout.Scale);
        var textScale = LastToDieActionStatusTextScale * layoutScale;
        var lineHeight = LastToDieActionStatusLineHeight * layoutScale;
        var paddingX = 9f * layoutScale;
        var paddingY = 7f * layoutScale;
        var widestLine = lines.Max(line => MeasureBitmapFontWidth(line.Text, textScale));
        var width = Math.Max(
            resolved.Bounds.Width,
            (int)MathF.Ceiling(widestLine + (paddingX * 2f)));
        var height = (int)MathF.Ceiling((paddingY * 2f) + (lineHeight * lines.Count));
        var panel = new Rectangle(
            resolved.Bounds.X,
            resolved.Bounds.Y,
            width,
            Math.Max(1, height));

        _spriteBatch.Draw(
            _pixel,
            panel,
            ApplyCurrentHudElementOpacity(new Color(12, 18, 25, 205)));
        _spriteBatch.Draw(
            _pixel,
            new Rectangle(panel.X, panel.Y, panel.Width, Math.Max(1, (int)MathF.Round(2f * layoutScale))),
            ApplyCurrentHudElementOpacity(new Color(104, 184, 221)));

        var y = panel.Y + paddingY;
        foreach (var line in lines)
        {
            var position = new Vector2(panel.X + paddingX, y);
            DrawBitmapFontText(
                line.Text,
                position + new Vector2(layoutScale, layoutScale),
                ApplyCurrentHudElementOpacity(Color.Black * 0.72f),
                textScale);
            DrawBitmapFontText(
                line.Text,
                position,
                ApplyCurrentHudElementOpacity(GetLastToDieActionStatusColor(line.Tone)),
                textScale);
            y += lineHeight;
        }

        UpdateHudElementBounds(HudElementId.LastToDieActionStatus, panel);
    }

    internal static IReadOnlyList<LastToDieActionStatusLine> BuildLastToDieActionStatusLines(
        PlayerEntity player,
        int ticksPerSecond,
        Func<byte, string?>? resolveMarkedTargetLabel = null,
        int armedExplosiveArrowCount = 0,
        PlayerEntity? predictedActionPlayer = null)
    {
        ArgumentNullException.ThrowIfNull(player);
        ticksPerSecond = Math.Max(1, ticksPerSecond);
        var lines = new List<LastToDieActionStatusLine>(8);
        var actionPlayer = predictedActionPlayer is not null
            && predictedActionPlayer.ClassId == player.ClassId
                ? predictedActionPlayer
                : player;

        switch (player.ClassId)
        {
            case PlayerClass.Spy:
                AddLastToDieSpyActionStatusLines(lines, actionPlayer, ticksPerSecond);
                break;

            case PlayerClass.Sniper:
                AddLastToDieSniperActionStatusLines(
                    lines,
                    actionPlayer,
                    ticksPerSecond,
                    resolveMarkedTargetLabel,
                    armedExplosiveArrowCount);
                break;
        }

        AddLastToDieMedicLinkActionStatusLines(lines, actionPlayer);

        if (player.IsLastToDieMedicHailMaryInvulnerable)
        {
            lines.Add(new LastToDieActionStatusLine(
                $"HAIL MARY: INVULN {FormatLastToDieActionSeconds(player.LastToDieMedicHailMaryTicksRemaining, ticksPerSecond)}",
                LastToDieActionStatusTone.Active));
        }

        if (player.LastToDieGuardianEvasionChance > 0.0001f)
        {
            var evasionPercent = (int)MathF.Round(player.LastToDieGuardianEvasionChance * 100f);
            lines.Add(new LastToDieActionStatusLine(
                $"GUARDIAN: +12 HP/S / {evasionPercent.ToString(CultureInfo.InvariantCulture)}% EVADE",
                LastToDieActionStatusTone.Beneficial));
        }

        if (player.LastToDieStatusOutgoingDamageMultiplier < 0.9999f)
        {
            var movementPenalty = Math.Clamp(
                (int)MathF.Round((1f - player.LastToDieStatusMovementSpeedMultiplier) * 100f),
                0,
                99);
            var damagePenalty = Math.Clamp(
                (int)MathF.Round((1f - player.LastToDieStatusOutgoingDamageMultiplier) * 100f),
                0,
                99);
            lines.Add(new LastToDieActionStatusLine(
                $"TRANQ: -{damagePenalty.ToString(CultureInfo.InvariantCulture)}% DAMAGE / -{movementPenalty.ToString(CultureInfo.InvariantCulture)}% MOVE",
                LastToDieActionStatusTone.Warning));
        }

        return lines;
    }

    private static void AddLastToDieSpyActionStatusLines(
        List<LastToDieActionStatusLine> lines,
        PlayerEntity player,
        int ticksPerSecond)
    {
        if (player.IsLastToDieSpyInfiltrateDashing)
        {
            lines.Add(new LastToDieActionStatusLine(
                $"INFILTRATE: PROJECTILE IMMUNE {FormatLastToDieActionSeconds(player.LastToDieSpyInfiltrateDashTicksRemaining, ticksPerSecond)}",
                LastToDieActionStatusTone.Active));
        }
        else if (player.LastToDieSpyInfiltrateCooldownTicksRemaining > 0)
        {
            lines.Add(new LastToDieActionStatusLine(
                $"INFILTRATE: {FormatLastToDieActionSeconds(player.LastToDieSpyInfiltrateCooldownTicksRemaining, ticksPerSecond)}",
                LastToDieActionStatusTone.Cooldown));
        }
        else if (player.LastToDieSpyInfiltrateEnabled)
        {
            lines.Add(new LastToDieActionStatusLine(
                "Q INFILTRATE: READY",
                LastToDieActionStatusTone.Ready));
        }

        if (player.IsLastToDieSpyAfterlifeActive)
        {
            lines.Add(new LastToDieActionStatusLine(
                $"AFTERLIFE: KILL TO REVIVE {FormatLastToDieActionSeconds(player.LastToDieSpyAfterlifeWindowTicksRemaining, ticksPerSecond)}",
                LastToDieActionStatusTone.Warning));
        }
        else if (player.LastToDieSpyAfterlifeCooldownTicksRemaining > 0)
        {
            lines.Add(new LastToDieActionStatusLine(
                $"AFTERLIFE: {FormatLastToDieActionSeconds(player.LastToDieSpyAfterlifeCooldownTicksRemaining, ticksPerSecond)}",
                LastToDieActionStatusTone.Cooldown));
        }
        else if (player.LastToDieSpyAfterlifeEnabled)
        {
            lines.Add(new LastToDieActionStatusLine(
                "AFTERLIFE: READY",
                LastToDieActionStatusTone.Ready));
        }
    }

    private static void AddLastToDieMedicLinkActionStatusLines(
        List<LastToDieActionStatusLine> lines,
        PlayerEntity player)
    {
        if (player.LastToDieMedicStimulantDripLinkActive
            || player.LastToDieMedicAgilityDriveLinkActive)
        {
            var linkLabel = (player.LastToDieMedicStimulantDripLinkActive,
                player.LastToDieMedicAgilityDriveLinkActive) switch
            {
                (true, true) => "STIMULANT + AGILITY",
                (true, false) => "STIMULANT",
                _ => "AGILITY",
            };
            lines.Add(new LastToDieActionStatusLine(
                $"MEDIC LINK: {linkLabel}",
                LastToDieActionStatusTone.Beneficial));
        }

        if (player.LastToDieMedicMartyrProtectedLinkActive)
        {
            lines.Add(new LastToDieActionStatusLine(
                "MARTYR: PROTECTED AT 1 HP",
                LastToDieActionStatusTone.Beneficial));
        }

        if (player.LastToDieMedicMartyrProtectorLinkActive)
        {
            lines.Add(new LastToDieActionStatusLine(
                "MARTYR: PROTECTING ALLY",
                LastToDieActionStatusTone.Active));
        }
    }

    private static void AddLastToDieSniperActionStatusLines(
        List<LastToDieActionStatusLine> lines,
        PlayerEntity player,
        int ticksPerSecond,
        Func<byte, string?>? resolveMarkedTargetLabel,
        int armedExplosiveArrowCount)
    {
        var profile = player.LastToDieSniperProfile;
        if (player.IsLastToDieSniperGhostCloaked)
        {
            lines.Add(new LastToDieActionStatusLine(
                "GHOST: CLOAKED / FIRE x3",
                LastToDieActionStatusTone.Active));
        }
        else if (player.LastToDieSniperGhostCooldownTicksRemaining > 0)
        {
            lines.Add(new LastToDieActionStatusLine(
                $"GHOST: {FormatLastToDieActionSeconds(player.LastToDieSniperGhostCooldownTicksRemaining, ticksPerSecond)}",
                LastToDieActionStatusTone.Cooldown));
        }
        else if (profile.GhostEnabled)
        {
            lines.Add(new LastToDieActionStatusLine(
                "Q GHOST: READY",
                LastToDieActionStatusTone.Ready));
        }

        if (profile.SpottedEnabled && player.LastToDieSniperMarkedTargetSlot > 0)
        {
            var targetLabel = resolveMarkedTargetLabel?.Invoke(player.LastToDieSniperMarkedTargetSlot);
            if (string.IsNullOrWhiteSpace(targetLabel))
            {
                targetLabel = $"P{player.LastToDieSniperMarkedTargetSlot.ToString(CultureInfo.InvariantCulture)}";
            }

            targetLabel = targetLabel.Trim();
            if (targetLabel.Length > 20)
            {
                targetLabel = targetLabel[..20];
            }

            lines.Add(new LastToDieActionStatusLine(
                $"SPOTTED: {targetLabel}",
                LastToDieActionStatusTone.Warning));
        }

        if (profile.ConquistadorEnabled)
        {
            var bonusPercent = player.LastToDieSniperConquistadorStacks * 2;
            lines.Add(new LastToDieActionStatusLine(
                $"CONQUISTADOR: +{bonusPercent.ToString(CultureInfo.InvariantCulture)}% DAMAGE",
                LastToDieActionStatusTone.Beneficial));
        }

        var volley = player.LastToDieSniperVolleyState;
        if (volley.IsActive)
        {
            var pendingCount = volley.QueuedArrowCount + volley.DueArrowCount;
            lines.Add(new LastToDieActionStatusLine(
                $"VOLLEY: {pendingCount.ToString(CultureInfo.InvariantCulture)} ARROW{(pendingCount == 1 ? string.Empty : "S")} PENDING",
                LastToDieActionStatusTone.Info));
        }

        armedExplosiveArrowCount = Math.Max(0, armedExplosiveArrowCount);
        if (profile.ExplosiveTipEnabled && armedExplosiveArrowCount > 0)
        {
            lines.Add(new LastToDieActionStatusLine(
                $"M2 DETONATE: {armedExplosiveArrowCount.ToString(CultureInfo.InvariantCulture)} ARROW{(armedExplosiveArrowCount == 1 ? string.Empty : "S")}",
                LastToDieActionStatusTone.Ready));
        }
    }

    internal static string FormatLastToDieActionSeconds(int ticks, int ticksPerSecond)
    {
        ticksPerSecond = Math.Max(1, ticksPerSecond);
        ticks = Math.Max(0, ticks);
        if (ticks < ticksPerSecond)
        {
            return $"{(ticks / (float)ticksPerSecond).ToString("0.0", CultureInfo.InvariantCulture)}s";
        }

        return $"{MathF.Ceiling(ticks / (float)ticksPerSecond).ToString("0", CultureInfo.InvariantCulture)}s";
    }

    internal static (string Top, string Bottom) GetLastToDieMedicUberHudLabels(
        MedicUberDeliveryMode mode)
    {
        return mode switch
        {
            MedicUberDeliveryMode.Kritz => ("CRIT", "CRAZE"),
            MedicUberDeliveryMode.RejuvenationRay => ("REJUV", "RAY"),
            _ => ("SUPER", "BURST"),
        };
    }

    private string? ResolveLastToDieMarkedTargetLabel(byte slot)
    {
        return _world.TryGetNetworkPlayer(slot, out var player)
            ? GetHudPlayerLabel(player)
            : null;
    }

    private static Color GetLastToDieActionStatusColor(LastToDieActionStatusTone tone)
    {
        return tone switch
        {
            LastToDieActionStatusTone.Ready => new Color(137, 235, 163),
            LastToDieActionStatusTone.Active => new Color(120, 218, 255),
            LastToDieActionStatusTone.Cooldown => new Color(184, 190, 200),
            LastToDieActionStatusTone.Warning => new Color(255, 191, 94),
            LastToDieActionStatusTone.Beneficial => new Color(166, 235, 202),
            _ => new Color(224, 229, 236),
        };
    }
}
