#nullable enable

using OpenGarrison.Core;
using OpenGarrison.Protocol.Gg2;

namespace OpenGarrison.Client;

/// <summary>
/// What a GG2 state update's size depends on, read from the loaded map: the HUD object
/// GG2 creates for its mode, how many control points ControlPointHUD serializes, and how
/// many moving platforms there are.
/// </summary>
internal static class Gg2MapLayoutResolver
{
    public static Gg2MapLayout Resolve(GameModeKind mode, SimpleLevel level)
    {
        var hudKind = mode switch
        {
            GameModeKind.CaptureTheFlag => Gg2HudKind.CaptureTheFlag,
            GameModeKind.ControlPoint => Gg2HudKind.ControlPoint,
            GameModeKind.KingOfTheHill => Gg2HudKind.KingOfTheHill,
            GameModeKind.DoubleKingOfTheHill => Gg2HudKind.DoubleKingOfTheHill,
            GameModeKind.Arena => Gg2HudKind.Arena,
            GameModeKind.TeamDeathmatch => Gg2HudKind.TeamDeathmatch,
            GameModeKind.Generator => Gg2HudKind.Generator,
            _ => Gg2HudKind.None,
        };

        var controlPoints = hudKind == Gg2HudKind.ControlPoint
            ? level.GetRoomObjects(RoomObjectType.ControlPoint).Count
            : 0;
        return new Gg2MapLayout(hudKind, controlPoints, level.MovingPlatforms.Count);
    }
}
