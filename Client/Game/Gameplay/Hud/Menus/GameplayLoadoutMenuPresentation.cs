#nullable enable

using Microsoft.Xna.Framework;
using OpenGarrison.Core;

namespace OpenGarrison.Client;

internal static class GameplayLoadoutMenuPresentation
{
    private static readonly int[] ClassStripXOffsets = [24, 64, 104, 156, 196, 236, 288, 328, 368];

    internal static readonly PlayerClass[] ClassStripOrder =
    [
        PlayerClass.Scout,
        PlayerClass.Pyro,
        PlayerClass.Soldier,
        PlayerClass.Heavy,
        PlayerClass.Demoman,
        PlayerClass.Medic,
        PlayerClass.Engineer,
        PlayerClass.Spy,
        PlayerClass.Sniper,
    ];

    public static GameplayLoadoutMenuLayout CreateLayout(int viewportWidth, int viewportHeight)
    {
        const float baseWidth = 600f;
        const float baseHeight = 600f;
        var scale = MathF.Min((viewportWidth - 24f) / baseWidth, (viewportHeight - 24f) / baseHeight);
        scale = MathF.Max(1f, scale);
        var panelWidth = (int)MathF.Round(baseWidth * scale);
        var panelHeight = (int)MathF.Round(baseHeight * scale);
        var panel = new Rectangle((viewportWidth - panelWidth) / 2, (viewportHeight - panelHeight) / 2, panelWidth, panelHeight);

        Rectangle ScaleRect(float x, float y, float width, float height)
        {
            return new Rectangle(
                panel.X + (int)MathF.Round(x * scale),
                panel.Y + (int)MathF.Round(y * scale),
                (int)MathF.Round(width * scale),
                (int)MathF.Round(height * scale));
        }

        return new GameplayLoadoutMenuLayout(
            panel,
            ScaleRect(112f, 16f, 381f, 50f),
            ScaleRect(0f, 95f, 600f, 45f),
            ScaleRect(40f, 192f, 96f, 288f),
            ScaleRect(464f, 192f, 96f, 288f),
            ScaleRect(160f, 178f, 280f, 150f),
            ScaleRect(162f, 326f, 276f, 172f),
            ScaleRect(0f, 532f, 600f, 68f),
            ScaleRect(32f, 544f, 64f, 32f),
            scale);
    }

    public static Rectangle GetClassStripButtonBounds(GameplayLoadoutMenuLayout layout, int classIndex)
    {
        return new Rectangle(
            layout.PanelBounds.X + (int)MathF.Round((88f + ClassStripXOffsets[classIndex]) * layout.Scale),
            layout.PanelBounds.Y,
            (int)MathF.Round(36f * layout.Scale),
            (int)MathF.Round(60f * layout.Scale));
    }

    public static Vector2 GetClassStripIconPosition(GameplayLoadoutMenuLayout layout, PlayerClass classId)
    {
        var classIndex = Array.IndexOf(ClassStripOrder, classId);
        if (classIndex < 0)
        {
            classIndex = 0;
        }

        return new Vector2(
            layout.PanelBounds.X + (88f + ClassStripXOffsets[classIndex]) * layout.Scale,
            layout.PanelBounds.Y + 16f * layout.Scale);
    }

    public static Rectangle GetColumnOptionBounds(Rectangle columnBounds, int optionIndex)
    {
        var tileWidth = (int)MathF.Round(columnBounds.Width * (80f / 96f));
        var tileHeight = columnBounds.Height / 6;
        var offsetX = (columnBounds.Width - tileWidth) / 2;
        var y = columnBounds.Y + (int)MathF.Round(columnBounds.Height * (8f / 288f)) + (optionIndex * (columnBounds.Height / 5));
        return new Rectangle(columnBounds.X + offsetX, y, tileWidth, tileHeight);
    }

    public static bool TryGetSelectionFrame(string itemId, out int frameIndex)
    {
        frameIndex = itemId switch
        {
            "weapon.scattergun" => 0,
            "weapon.flamethrower" => 80,
            "weapon.rocketlauncher" => 10,
            "weapon.directhit" => 11,
            "weapon.blackbox" => 13,
            "weapon.minigun" => 60,
            "weapon.tomislav" => 61,
            "weapon.brassbeast" => 63,
            "ability.heavy-sandvich" => 65,
            "weapon.revolver" => 70,
            "weapon.diamondback" => 72,
            "weapon.rifle" => 20,
            "weapon.medigun" => 45,
            "ability.medic-needlegun" => 40,
            "weapon.mine_launcher" => 30,
            "ability.demoman-detonate" => 35,
            "ability.pyro-airblast" => 85,
            "ability.spy-cloak" => 75,
            "ability.sniper-scope" => 25,
            "ability.engineer-pda" => 55,
            _ => -1,
        };

        if (frameIndex < 0)
        {
            return false;
        }

        return true;
    }

    public static int GetClassSelectFrame(PlayerClass playerClass)
    {
        return playerClass switch
        {
            PlayerClass.Scout => 0,
            PlayerClass.Pyro => 1,
            PlayerClass.Soldier => 2,
            PlayerClass.Heavy => 3,
            PlayerClass.Demoman => 4,
            PlayerClass.Medic => 5,
            PlayerClass.Engineer => 6,
            PlayerClass.Spy => 7,
            PlayerClass.Sniper => 8,
            _ => 0,
        };
    }
}

internal readonly record struct GameplayLoadoutMenuLayout(
    Rectangle PanelBounds,
    Rectangle ClassStripBounds,
    Rectangle HeaderBounds,
    Rectangle LeftColumnBounds,
    Rectangle RightColumnBounds,
    Rectangle PreviewBounds,
    Rectangle DescriptionBounds,
    Rectangle FooterBounds,
    Rectangle BackButtonBounds,
    float Scale);
