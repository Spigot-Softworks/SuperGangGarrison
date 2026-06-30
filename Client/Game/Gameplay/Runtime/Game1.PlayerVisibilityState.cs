#nullable enable

using System;
using OpenGarrison.Core;

namespace OpenGarrison.Client;

public partial class Game1
{
    private float GetPlayerVisibilityAlpha(PlayerEntity player)
    {
        if (!player.IsAlive)
        {
            return 1f;
        }

        var bodyVisibilityScale = GetSpyBackstabBodyVisibilityScale(player);
        if (player.IsExperimentalGhostDashVisible)
        {
            var trailAlpha = Math.Clamp(player.ExperimentalGhostDashTrailAlpha, 0f, 1f);
            return (1f + ((0.42f - 1f) * trailAlpha)) * bodyVisibilityScale;
        }

        if (_world.IsPlayerInsideExperimentalEngineerMisdirectionFieldForVisuals(player))
        {
            return 0.72f * bodyVisibilityScale;
        }

        if (!GetPlayerIsSpyCloaked(player))
        {
            return bodyVisibilityScale;
        }

        var cloakAlpha = Math.Clamp(GetPlayerSpyCloakAlpha(player), 0f, 1f);
        var backstabRevealAlpha = GetSpyBackstabRevealAlpha(player, bodyVisibilityScale);

        if (ReferenceEquals(player, _world.LocalPlayer) && _localSpySuperjumpCloakRevealAlpha > 0f)
        {
            cloakAlpha = Math.Max(cloakAlpha, _localSpySuperjumpCloakRevealAlpha);
        }

        // Apply superjump visual reveal on top for remote spies (purely cosmetic, no gameplay implication)
        if (!ReferenceEquals(player, _world.LocalPlayer)
            && _spySuperjumpCloakRevealTicks.TryGetValue(player.Id, out var revealTicksRemaining)
            && revealTicksRemaining > 0)
        {
            var revealAlpha = SpySuperjumpCloakRevealStartAlpha
                * (revealTicksRemaining / (float)SpySuperjumpCloakRevealTicks);
            cloakAlpha = Math.Max(cloakAlpha, revealAlpha);
        }

        if (backstabRevealAlpha > 0f)
        {
            cloakAlpha = Math.Max(cloakAlpha, backstabRevealAlpha);
        }

        if (IsLocalSpectatorPresentationActive())
        {
            var allyAlpha = GetPlayerIsSpyBackstabReady(player)
                ? Math.Max(cloakAlpha, PlayerEntity.SpyMinAllyCloakAlpha)
                : cloakAlpha;
            return ApplySpyCloakBodyVisibilityScale(allyAlpha, bodyVisibilityScale, backstabRevealAlpha);
        }
        if (ReferenceEquals(player, _world.LocalPlayer))
        {
            return ApplySpyCloakBodyVisibilityScale(
                Math.Max(cloakAlpha, PlayerEntity.SpyMinAllyCloakAlpha),
                bodyVisibilityScale,
                backstabRevealAlpha);
        }

        if (player.Team == _world.LocalPlayer.Team)
        {
            var allyAlpha = GetPlayerIsSpyBackstabReady(player)
                ? Math.Max(cloakAlpha, PlayerEntity.SpyMinAllyCloakAlpha)
                : cloakAlpha;
            return ApplySpyCloakBodyVisibilityScale(allyAlpha, bodyVisibilityScale, backstabRevealAlpha);
        }

        if (IsSpyHiddenFromLocalViewer(player) && !GetPlayerIsSpyVisibleToEnemies(player))
        {
            return 0f;
        }

        return ApplySpyCloakBodyVisibilityScale(cloakAlpha, bodyVisibilityScale, backstabRevealAlpha);
    }

    private static float ApplySpyCloakBodyVisibilityScale(
        float cloakAlpha,
        float bodyVisibilityScale,
        float backstabRevealAlpha)
    {
        if (backstabRevealAlpha > 0f)
        {
            return Math.Clamp(Math.Max(cloakAlpha, backstabRevealAlpha) * bodyVisibilityScale, 0f, 1f);
        }

        return cloakAlpha * bodyVisibilityScale;
    }

    private float GetSpyBackstabRevealAlpha(PlayerEntity player, float bodyVisibilityScale)
    {
        if (player.ClassId != PlayerClass.Spy)
        {
            return 0f;
        }

        if (!GetPlayerIsSpyBackstabAnimating(player) && !TryGetActiveBackstabVisual(player, out _))
        {
            return 0f;
        }

        return Math.Clamp(1f - bodyVisibilityScale, 0f, 0.99f);
    }

    private void EnsureRemoteSpyBackstabVisuals()
    {
        for (var index = 0; index < _world.RemoteSnapshotPlayers.Count; index += 1)
        {
            var player = _world.RemoteSnapshotPlayers[index];
            if (player.ClassId != PlayerClass.Spy || player.Team == _world.LocalPlayer.Team)
            {
                continue;
            }

            var playerId = player.Id;
            var visualTicksRemaining = GetPlayerSpyBackstabVisualTicksRemaining(player);
            _lastObservedRemoteSpyBackstabVisualTicks.TryGetValue(playerId, out var previousVisualTicksRemaining);
            if (visualTicksRemaining <= 0)
            {
                _lastObservedRemoteSpyBackstabVisualTicks.Remove(playerId);
                continue;
            }

            if (previousVisualTicksRemaining <= 0
                && !TryGetActiveBackstabVisual(player, out _))
            {
                SpawnBackstabVisual(playerId, player.Team, player.X, player.Y, player.AimDirectionDegrees);
            }

            _lastObservedRemoteSpyBackstabVisualTicks[playerId] = visualTicksRemaining;
        }
    }

    private float GetSpyBackstabBodyVisibilityScale(PlayerEntity player)
    {
        if (player.ClassId != PlayerClass.Spy)
        {
            return 1f;
        }

        if (TryGetActiveBackstabVisual(player, out var animation))
        {
            return Math.Clamp(1f - animation.Alpha, 0f, 1f);
        }

        if (!GetPlayerIsSpyBackstabAnimating(player))
        {
            return 1f;
        }

        var visualTicksRemaining = GetPlayerSpyBackstabVisualTicksRemaining(player);
        if (visualTicksRemaining <= 0)
        {
            return 1f;
        }

        var elapsedTicks = StabAnimEntity.TotalLifetimeTicks - Math.Clamp(visualTicksRemaining, 0, StabAnimEntity.TotalLifetimeTicks);
        if (elapsedTicks <= StabAnimEntity.WarmupTicks)
        {
            var warmupProgress = elapsedTicks / (float)StabAnimEntity.WarmupTicks;
            return Math.Clamp(1f - (warmupProgress * 0.99f), 0.01f, 1f);
        }

        var fullyVisibleBackstabTicks = StabAnimEntity.WarmupTicks + StabAnimEntity.SwingTicks;
        if (elapsedTicks <= fullyVisibleBackstabTicks)
        {
            return 0.01f;
        }

        var fadeTicks = Math.Clamp(elapsedTicks - fullyVisibleBackstabTicks, 0, StabAnimEntity.FadeOutTicks);
        var fadeProgress = fadeTicks / (float)StabAnimEntity.FadeOutTicks;
        var estimatedBackstabAlpha = Math.Clamp(0.99f * (1f - fadeProgress), 0f, 0.99f);
        return Math.Clamp(1f - estimatedBackstabAlpha, 0f, 1f);
    }

    private bool IsSpyHiddenFromLocalViewer(PlayerEntity player)
    {
        if (IsLocalSpectatorPresentationActive()
            || ReferenceEquals(player, _world.LocalPlayer)
            || player.ClassId != PlayerClass.Spy
            || player.Team == _world.LocalPlayer.Team
            || !_world.LocalPlayer.IsAlive)
        {
            return false;
        }

        return IsSpyHiddenFromLocalViewer(player.Id, player.Team, player.X);
    }

    private bool IsSpyHiddenFromLocalViewer(int ownerId, PlayerTeam ownerTeam, float spyX)
    {
        if (IsLocalSpectatorPresentationActive()
            || !_world.LocalPlayer.IsAlive
            || ownerId == _world.LocalPlayer.Id
            || ownerTeam == _world.LocalPlayer.Team)
        {
            return false;
        }

        var viewerFacingSign = IsFacingLeftByAim(_world.LocalPlayer) ? -1 : 1;
        return Math.Sign(spyX - _world.LocalPlayer.X) == -viewerFacingSign;
    }

    private bool IsBackstabReplacementRenderActive(PlayerEntity player)
    {
        if (player.ClassId != PlayerClass.Spy)
        {
            return false;
        }

        var hasActiveVisual = TryGetActiveBackstabVisual(player, out _);
        // Use || for both local and remote players: show the backstab replacement render
        // whenever the visual entity exists OR the server tick counter says animating.
        // Using && for remote players caused the animation to silently disappear when the
        // BackstabBlue/Red visual event was dropped under snapshot budget pressure (it is
        // Optional while sound events are required), or when the event arrived after the
        // tick counter had already expired.
        return hasActiveVisual || GetPlayerIsSpyBackstabAnimating(player);
    }

    private float GetBackstabReplacementDirectionDegrees(PlayerEntity player)
    {
        if (TryGetActiveBackstabVisual(player, out var animation))
        {
            return animation.DirectionDegrees;
        }

        if (ReferenceEquals(player, _world.LocalPlayer) && GetPlayerIsSpyBackstabAnimating(player))
        {
            return player.SpyBackstabDirectionDegrees;
        }

        return player.AimDirectionDegrees;
    }

    private bool TryGetActiveBackstabVisual(PlayerEntity player, out StabAnimEntity animation)
    {
        var targetPlayerId = ReferenceEquals(player, _world.LocalPlayer)
            ? GetResolvedLocalPlayerId()
            : player.Id;

        for (var index = 0; index < _backstabVisuals.Count; index += 1)
        {
            var backstabVisual = _backstabVisuals[index].Animation;
            if (backstabVisual.OwnerId != targetPlayerId || backstabVisual.IsExpired)
            {
                continue;
            }

            animation = backstabVisual;
            return true;
        }

        animation = null!;
        return false;
    }
}
