using System;
using Microsoft.Xna.Framework;
using OpenGarrison.Core;

namespace OpenGarrison.Client;

public partial class Game1
{
    private void DrawKingpinPrototypeHud()
    {
        var player = _world.LocalPlayer;
        if (!player.IsAlive || !KingpinPrototype.IsKingpin(player)) return;
        var position = new Vector2(ViewportWidth * 0.5f, ViewportHeight - 120f);
        var text = $"{KingpinPrototype.SpecialName(KingpinPrototype.SelectedSpecial(player))}  |  Meter {MathF.Floor(KingpinPrototype.Meter(player))}%";
        DrawHudTextCentered(text, position + Vector2.One, Color.Black, 0.8f);
        DrawHudTextCentered(text, position, Color.White, 0.8f);
    }
}
