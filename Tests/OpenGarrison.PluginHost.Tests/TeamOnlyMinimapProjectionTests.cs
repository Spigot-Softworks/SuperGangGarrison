using Microsoft.Xna.Framework;
using OpenGarrison.Client.Plugins.TeamOnlyMinimap;
using Xunit;

namespace OpenGarrison.PluginHost.Tests;

public sealed class TeamOnlyMinimapProjectionTests
{
    [Fact]
    public void ObjectiveProjectionUsesHudBoundsInsteadOfPlayerCenteredViewport()
    {
        var objectivePosition = new Vector2(100f, 500f);
        var localPlayerPosition = new Vector2(900f, 500f);

        var playerCenteredVisible = TeamOnlyMinimapPlugin.TryProjectPlayerCenteredToScreenForTests(
            levelWidth: 1000f,
            levelHeight: 1000f,
            layoutX: 10f,
            layoutY: 20f,
            layoutWidth: 200f,
            layoutHeight: 100f,
            localPlayerPosition,
            objectivePosition,
            out _);
        var objectiveVisible = TeamOnlyMinimapPlugin.TryProjectObjectiveToScreenForTests(
            levelWidth: 1000f,
            levelHeight: 1000f,
            layoutX: 10f,
            layoutY: 20f,
            layoutWidth: 200f,
            layoutHeight: 100f,
            objectivePosition,
            out var objectiveScreenPosition);

        Assert.False(playerCenteredVisible);
        Assert.True(objectiveVisible);
        Assert.InRange(objectiveScreenPosition.X, 10f, 110f);
        Assert.InRange(objectiveScreenPosition.Y, 20f, 120f);
    }
}
