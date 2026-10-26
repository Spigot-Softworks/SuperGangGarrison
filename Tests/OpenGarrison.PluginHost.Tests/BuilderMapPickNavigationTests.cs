using System.Reflection;
using System.Runtime.CompilerServices;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using OpenGarrison.Client;
using OpenGarrison.Core;
using Xunit;

namespace OpenGarrison.PluginHost.Tests;

public sealed class BuilderMapPickNavigationTests
{
    private const BindingFlags Fields = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

    [Theory]
    [InlineData("_builderLogicMapPickActive")]
    [InlineData("_builderEntityMapPickActive")]
    [InlineData("_builderMultiEntityMapPickActive")]
    [InlineData("_builderObjectiveMapPickActive")]
    public void EveryPickerKeepsKeyboardPanWheelZoomAndThePendingLink(string picker)
    {
        var game = CreateGame(picker);
        var viewport = Invoke<Rectangle>(game, "GetModernGarrisonBuilderMapViewport");
        // During the dock animation this is visible map beneath the hidden properties bounds.
        var pointer = new Point(viewport.X + 30, viewport.Y + 70);
        Assert.True(Invoke<Rectangle>(game, "GetGarrisonBuilderPropertyEditorBounds").Contains(pointer));
        var mouse = Mouse(pointer, wheel: 120);
        Set(game, "_builderValidationTooltipVisible", true);
        Set(game, "_builderValidationHoverBounds", new Rectangle(pointer.X - 5, pointer.Y - 5, 20, 20));
        Set(game, "_builderEntityRefListDropdownOpen", true);

        Assert.True(Invoke<bool>(game, "UpdateModernGarrisonBuilderMapPick", new KeyboardState(Keys.D, Keys.Down), mouse, 0.1f));

        Assert.True(Get<Vector2>(game, "_builderCamera").X > 0);
        Assert.True(Get<Vector2>(game, "_builderCamera").Y > 0);
        Assert.True(Get<float>(game, "_builderZoom") > 1);
        Assert.True(Get<bool>(game, picker));
        Assert.Equal("node:existing", Get<Dictionary<string, string>>(game, "_builderPropertyEditorValues")["logicInput"]);
        Assert.Equal(0, Get<int>(game, "_builderValidationScroll"));
        Assert.True(game.HandleGarrisonBuilderTextInput('d'));
        Assert.Equal("unchanged", Get<string>(game, "_builderPropertyEditBuffer"));
    }

    [Theory]
    [InlineData("_builderLogicMapPickActive")]
    [InlineData("_builderEntityMapPickActive")]
    [InlineData("_builderMultiEntityMapPickActive")]
    [InlineData("_builderObjectiveMapPickActive")]
    public void MiddleDragPansWithoutCompletingOrCancellingPick(string picker)
    {
        var game = CreateGame(picker);
        var viewport = Invoke<Rectangle>(game, "GetModernGarrisonBuilderMapViewport");
        var start = new Point(viewport.X + 100, viewport.Y + 100);
        var pressed = Mouse(start, middle: ButtonState.Pressed);
        Invoke<bool>(game, "UpdateModernGarrisonBuilderMapPick", new KeyboardState(), pressed, 0.1f);
        Set(game, "_previousMouse", pressed);
        var moved = Mouse(new Point(start.X - 80, start.Y - 40), middle: ButtonState.Pressed);

        Invoke<bool>(game, "UpdateModernGarrisonBuilderMapPick", new KeyboardState(), moved, 0.1f);

        Assert.Equal(new Vector2(80, 40), Get<Vector2>(game, "_builderCamera"));
        Assert.True(Get<bool>(game, picker));
        Set(game, "_previousMouse", moved);
        Invoke<bool>(game, "UpdateModernGarrisonBuilderMapPick", new KeyboardState(), Mouse(start), 0.1f);
        Assert.False(Get<bool>(game, "_builderMapPanDragging"));
        Assert.True(Get<bool>(game, picker));
    }

    [Fact]
    public void EscapeCancelsOnlyThePickAndKeepsPropertiesAndExistingConnection()
    {
        var game = CreateGame("_builderLogicMapPickActive");

        Assert.True(Invoke<bool>(game, "UpdateModernGarrisonBuilderMapPick", new KeyboardState(Keys.Escape), default(MouseState), 0.1f));

        Assert.False(Get<bool>(game, "_builderLogicMapPickActive"));
        Assert.Equal(Game1.GarrisonBuilderPropertyTarget.SelectedMapEntity, Get<Game1.GarrisonBuilderPropertyTarget>(game, "_builderPropertyTarget"));
        Assert.Equal("node:existing", Get<Dictionary<string, string>>(game, "_builderPropertyEditorValues")["logicInput"]);
    }

    private static Game1 CreateGame(string picker)
    {
        var game = (Game1)RuntimeHelpers.GetUninitializedObject(typeof(Game1));
        var services = new ClientServiceContainer();
        Set(game, "_services", services);
        services.Register(new MenuManager((IMenuContext)game));
        var characters = Enumerable.Range(32, 95).Select(i => (char)i).ToList();
        game._consoleFont = new SpriteFont(null!, characters.Select(_ => new Rectangle(0, 0, 8, 12)).ToList(),
            characters.Select(_ => new Rectangle(0, 0, 8, 12)).ToList(), characters, 12, 0,
            characters.Select(_ => new Vector3(0, 8, 0)).ToList(), '?');
        Set(game, "_builderUseModernUi", true);
        Set(game, "_builderEditorEnabled", true);
        Set(game, "_builderEntityPaletteVisible", true);
        Set(game, "_builderZoom", 1f);
        Set(game, "_builderEntities", new List<CustomMapBuilderEntity>());
        Set(game, "_builderEntityOverlapPickScratch", new List<int>());
        Set(game, "_builderMultiEntityMapPickSelectedIndices", new HashSet<int>());
        Set(game, "_builderMultiEntityMapPickSelectionOrder", new List<int>());
        Set(game, "_builderPropertyTarget", Game1.GarrisonBuilderPropertyTarget.SelectedMapEntity);
        Set(game, "_builderPropertyEditorValues", new Dictionary<string, string> { ["logicInput"] = "node:existing" });
        Set(game, "_builderPropertyEditBuffer", "unchanged");
        Set(game, picker, true);
        return game;
    }

    private static MouseState Mouse(Point position, int wheel = 0, ButtonState middle = ButtonState.Released)
        => new(position.X, position.Y, wheel, ButtonState.Released, middle, ButtonState.Released, ButtonState.Released, ButtonState.Released);
    private static T Invoke<T>(Game1 game, string method, params object[] args)
        => (T)typeof(Game1).GetMethod(method, Fields)!.Invoke(game, args)!;
    private static void Set(Game1 game, string name, object value) => typeof(Game1).GetField(name, Fields)!.SetValue(game, value);
    private static T Get<T>(Game1 game, string name) => (T)typeof(Game1).GetField(name, Fields)!.GetValue(game)!;
}
