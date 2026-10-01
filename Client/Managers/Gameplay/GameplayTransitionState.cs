#nullable enable

using OpenGarrison.Core;

namespace OpenGarrison.Client;

public sealed class TeamClassSelectionState
{
    internal bool TeamSelectOpen;
    internal float TeamSelectAlpha = 0.01f;
    internal float TeamSelectPanelY = -120f;
    internal int TeamSelectHoverIndex = -1;
    internal PlayerTeam? PendingClassSelectTeam;
    internal bool ClassSelectOpen;
    internal float ClassSelectAlpha = 0.01f;
    internal float ClassSelectPanelY = -120f;
    internal int ClassSelectHoverIndex = -1;
    internal int ClassSelectPortraitAnimationHoverIndex = -1;
    internal PlayerTeam? ClassSelectPortraitAnimationTeam;
    internal float ClassSelectPortraitAnimationFrame;
    internal bool PendingMapTeamSelection;
}

public sealed class GameplaySessionTransitionState
{
    internal bool LastToDieConnectionPresentationPending;
}

public sealed class PendingHostedConnectState
{
    internal int Ticks = -1;
    internal int Port = OpenGarrisonPreferencesDocument.DefaultServerPort;
}

internal sealed class LoadingOverlayState
{
    internal bool Visible;
    internal bool IsJoining;
    internal string Message = string.Empty;
    internal string JoiningServerLabel = string.Empty;
    internal double? Progress;
    internal bool BrowserProgressVisible;
}
