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
        if (!GetPlayerIsSpyCloaked(player))
        {
            return bodyVisibilityScale;
        }

        if (_networkClient.IsSpectator)
        {
            return bodyVisibilityScale;
        }

        var cloakAlpha = Math.Clamp(GetPlayerSpyCloakAlpha(player), 0f, 1f);
        if (ReferenceEquals(player, _world.LocalPlayer))
        {
            return Math.Max(cloakAlpha, PlayerEntity.SpyMinAllyCloakAlpha) * bodyVisibilityScale;
        }

        if (player.Team == _world.LocalPlayer.Team)
        {
            var allyAlpha = GetPlayerIsSpyBackstabReady(player)
                ? Math.Max(cloakAlpha, PlayerEntity.SpyMinAllyCloakAlpha)
                : cloakAlpha;
            return allyAlpha * bodyVisibilityScale;
        }

        if (IsSpyHiddenFromLocalViewer(player))
        {
            return 0f;
        }

        return cloakAlpha * bodyVisibilityScale;
    }

    private float GetSpyBackstabBodyVisibilityScale(PlayerEntity player)
    {
        if (player.ClassId != PlayerClass.Spy)
        {
            return 1f;
        }

        var visualTicksRemaining = GetPlayerSpyBackstabVisualTicksRemaining(player);
        if (visualTicksRemaining <= 0)
        {
            return 1f;
        }

        const int bodyFadeTicks = 4;
        var elapsedTicks = StabAnimEntity.TotalLifetimeTicks - visualTicksRemaining;
        if (elapsedTicks < bodyFadeTicks)
        {
            return Math.Clamp(1f - (elapsedTicks / (float)bodyFadeTicks), 0f, 1f);
        }

        if (visualTicksRemaining <= StabAnimEntity.FadeOutTicks)
        {
            return Math.Clamp(1f - (visualTicksRemaining / (float)StabAnimEntity.FadeOutTicks), 0f, 1f);
        }

        return 0f;
    }

    private bool IsSpyHiddenFromLocalViewer(PlayerEntity player)
    {
        if (_networkClient.IsSpectator
            || ReferenceEquals(player, _world.LocalPlayer)
            || player.ClassId != PlayerClass.Spy
            || player.Team == _world.LocalPlayer.Team
            || !GetPlayerIsSpyCloaked(player)
            || !_world.LocalPlayer.IsAlive)
        {
            return false;
        }

        return IsSpyHiddenFromLocalViewer(player.Id, player.Team, player.X);
    }

    private bool IsSpyHiddenFromLocalViewer(int ownerId, PlayerTeam ownerTeam, float spyX)
    {
        if (_networkClient.IsSpectator
            || !_world.LocalPlayer.IsAlive
            || ownerId == _world.LocalPlayer.Id
            || ownerTeam == _world.LocalPlayer.Team)
        {
            return false;
        }

        var viewerFacingSign = IsFacingLeftByAim(_world.LocalPlayer) ? -1 : 1;
        return Math.Sign(spyX - _world.LocalPlayer.X) == -viewerFacingSign;
    }
}
