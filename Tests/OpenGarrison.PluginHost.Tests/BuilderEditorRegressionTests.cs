using System.Collections;
using System.Reflection;
using System.Runtime.CompilerServices;
using OpenGarrison.Client;
using OpenGarrison.Core;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Xunit;

namespace OpenGarrison.PluginHost.Tests;

public sealed class BuilderEditorRegressionTests
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private static Game1 Session(params CustomMapBuilderEntity[] entities)
    {
        var game = (Game1)RuntimeHelpers.GetUninitializedObject(typeof(Game1));
        Set(game, "ScrollbarDrag", new ScrollbarDragController());
        foreach (var field in typeof(Game1).GetFields(Private).Where(f => f.Name.StartsWith("_builder", StringComparison.Ordinal)))
        {
            if (field.FieldType == typeof(string)) field.SetValue(game, "");
            else if (field.FieldType.IsGenericType && field.FieldType.Namespace == "System.Collections.Generic"
                && field.FieldType.GetConstructor(Type.EmptyTypes) is not null) field.SetValue(game, Activator.CreateInstance(field.FieldType));
        }
        Set(game, "_builderDocument", CustomMapBuilderDocument.CreateEmpty("test") with { Entities = entities });
        Set(game, "_builderEntities", entities.ToList()); Set(game, "_builderEditorEnabled", true);
        Set(game, "_builderSelectedEntityIndex", -1);
        Call(game, "MarkGarrisonBuilderSaved");
        return game;
    }
    private static void Set(Game1 game, string name, object? value) => typeof(Game1).GetField(name, Private)!.SetValue(game, value);
    private static object? Get(Game1 game, string name) => typeof(Game1).GetField(name, Private)!.GetValue(game);
    private static object? Call(Game1 game, string name, params object?[] args) => typeof(Game1).GetMethod(name, Private | BindingFlags.Static)!.Invoke(game, args);
    private static void SetEnum(Game1 game, string field, string value) => Set(game, field, Enum.Parse(typeof(Game1).GetField(field, Private)!.FieldType, value));

    public static IEnumerable<object[]> ResizeCases()
    {
        foreach (var type in new[] { "barrier", "spawnroom", "teleport", "logicPlayerTrigger", "logicArea", "logicDamageable" })
        foreach (var handle in Enum.GetValues<Game1.GarrisonBuilderResizeHandle>().Where(h => h != Game1.GarrisonBuilderResizeHandle.None))
        foreach (var snap in new[] { false, true })
            yield return [type, (int)handle, snap];
    }

    [Theory]
    [MemberData(nameof(ResizeCases))]
    public void GrabbingResizeHandlePreservesBoundsThenMovesOnlyTheDraggedEdges(string type, int handleValue, bool snap)
    {
        var handle = (Game1.GarrisonBuilderResizeHandle)handleValue;
        var entity = CustomMapBuilderEntity.Create(type, 101, 83, xScale: 2, yScale: 3).NormalizeForEditing();
        var game = Session(entity);
        Set(game, "_builderSelectedEntityIndex", 0);
        Set(game, "_builderGridAlign", snap);
        // Atlas-backed frames do not require a graphics device to supply their dimensions.
        var cache = (Dictionary<string, LoadedGameMakerSprite>)Get(game, "_builderCatalogSpriteCache")!;
        Assert.True(CustomMapBuilderEntityCatalog.TryGetDefinition(type, out var definition));
        cache[definition.EntitySpriteName] = new LoadedGameMakerSprite(
            [new LoadedSpriteFrame(null!, new Rectangle(0, 0, 42, 42), OwnsTexture: false)], new Point(21, 21));
        var before = Bounds(game, entity);
        var handles = (Dictionary<Game1.GarrisonBuilderResizeHandle, Vector2>)Call(game, "GetGarrisonBuilderResizeHandlePoints", before.Left, before.Top, before.Width, before.Height)!;
        var cursor = handles[handle] + new Vector2(2, 2);
        Assert.True((bool)Call(game, "TryBeginGarrisonBuilderResize", cursor.ToPoint(), cursor)!);
        Assert.Equal(handle, Get(game, "_builderActiveResizeHandle"));

        Call(game, "ApplyGarrisonBuilderResizeDrag", cursor);
        AssertBounds(before, Bounds(game, ((List<CustomMapBuilderEntity>)Get(game, "_builderEntities")!)[0]));

        var delta = new Vector2(12, 6);
        Call(game, "ApplyGarrisonBuilderResizeDrag", cursor + delta);
        var expected = Game1.ResolveGarrisonBuilderResizeDragBounds(handle, before.Left, before.Top, before.Width, before.Height,
            handles[handle].X + delta.X, handles[handle].Y + delta.Y);
        // Left handles on the narrow barrier reach its six-pixel minimum.
        if (expected.Width < 6) expected = (before.Left + before.Width - 6, expected.Top, 6, expected.Height);
        AssertBounds(expected, Bounds(game, ((List<CustomMapBuilderEntity>)Get(game, "_builderEntities")!)[0]));
    }

    private static void AssertBounds((float Left, float Top, float Width, float Height) expected,
        (float Left, float Top, float Width, float Height) actual)
    {
        Assert.Equal(expected.Left, actual.Left, 3);
        Assert.Equal(expected.Top, actual.Top, 3);
        Assert.Equal(expected.Width, actual.Width, 3);
        Assert.Equal(expected.Height, actual.Height, 3);
    }

    private static (float Left, float Top, float Width, float Height) Bounds(Game1 game, CustomMapBuilderEntity entity)
    {
        object?[] args = [entity, 0f, 0f, 0f, 0f];
        Assert.True((bool)Call(game, "TryGetGarrisonBuilderEntityWorldBounds", args)!);
        return ((float)args[1]!, (float)args[2]!, (float)args[3]!, (float)args[4]!);
    }

    [Theory]
    [InlineData(false, "Escape")]
    [InlineData(true, "Escape")]
    [InlineData(true, "Enter")]
    [InlineData(true, "RightClick")]
    public void ConfirmedMessageEditsSurviveClosingThePropertyList(bool clone, string closeWith)
    {
        var original = CustomMapBuilderEntity.Create("gameplayMessage", 100, 100,
            new Dictionary<string, string> { [GameplayMessageMetadata.TextPropertyKey] = "Original message" }).NormalizeForEditing();
        var game = Session(original);
        Set(game, "_builderSelectedEntityIndex", 0);
        if (clone) Call(game, "CloneGarrisonBuilderSelectedEntities");
        var index = clone ? 1 : 0;
        var before = ((List<CustomMapBuilderEntity>)Get(game, "_builderEntities")!)[index];
        Call(game, "BeginEditingGarrisonBuilderSelectedEntity");
        SetEnum(game, "_builderPropertyEditMode", "EditValue");
        Set(game, "_builderPropertyEditKey", GameplayMessageMetadata.TextPropertyKey);
        Set(game, "_builderPropertyEditBuffer", "Edited message");
        Assert.True((bool)Call(game, "UpdateGarrisonBuilderPropertyEditor", new KeyboardState(Keys.Enter), default(MouseState))!);
        Assert.Equal("Edited message", ((List<CustomMapBuilderEntity>)Get(game, "_builderEntities")!)[index].Properties[GameplayMessageMetadata.TextPropertyKey]);

        if (closeWith == "RightClick") Call(game, "HandleGarrisonBuilderPropertyEditorClick", Point.Zero, false);
        else Call(game, "UpdateGarrisonBuilderPropertyEditor", new KeyboardState(Enum.Parse<Keys>(closeWith)), default(MouseState));

        var entities = (List<CustomMapBuilderEntity>)Get(game, "_builderEntities")!;
        Assert.Equal("None", Get(game, "_builderPropertyTarget")!.ToString());
        Assert.Equal("Edited message", entities[index].Properties[GameplayMessageMetadata.TextPropertyKey]);
        Assert.Equal("Edited message", ((CustomMapBuilderDocument)Get(game, "_builderDocument")!).Entities[index].Properties[GameplayMessageMetadata.TextPropertyKey]);
        Assert.Equal(before.X, entities[index].X);
        Assert.Equal(before.Y, entities[index].Y);
        if (clone)
        {
            Assert.Equal("Original message", entities[0].Properties[GameplayMessageMetadata.TextPropertyKey]);
            Assert.Equal(before.Properties[MapLogicMetadata.MapEntityIdPropertyKey], entities[index].Properties[MapLogicMetadata.MapEntityIdPropertyKey]);
        }
        Assert.True((bool)Call(game, "TryUndoGarrisonBuilder")!);
        Assert.Equal("Original message", ((List<CustomMapBuilderEntity>)Get(game, "_builderEntities")!)[index].Properties[GameplayMessageMetadata.TextPropertyKey]);
        Assert.True((bool)Call(game, "TryRedoGarrisonBuilder")!);
        Assert.Equal("Edited message", ((List<CustomMapBuilderEntity>)Get(game, "_builderEntities")!)[index].Properties[GameplayMessageMetadata.TextPropertyKey]);
    }

    [Fact]
    public void EscapeInATextFieldCancelsOnlyTheUnconfirmedValue()
    {
        var entity = CustomMapBuilderEntity.Create("gameplayMessage", 100, 100,
            new Dictionary<string, string> { [GameplayMessageMetadata.TextPropertyKey] = "Original" }).NormalizeForEditing();
        var game = Session(entity);
        Set(game, "_builderSelectedEntityIndex", 0);
        Call(game, "BeginEditingGarrisonBuilderSelectedEntity");
        var values = (Dictionary<string, string>)Get(game, "_builderPropertyEditorValues")!;
        values[GameplayMessageMetadata.TextPropertyKey] = "Confirmed";
        Call(game, "ApplyGarrisonBuilderPropertyEditorLivePreview");
        SetEnum(game, "_builderPropertyEditMode", "EditValue");
        Set(game, "_builderPropertyEditKey", GameplayMessageMetadata.TextPropertyKey);
        Set(game, "_builderPropertyEditBuffer", "Unconfirmed");
        Call(game, "UpdateGarrisonBuilderPropertyEditor", new KeyboardState(Keys.Escape), default(MouseState));
        Assert.Equal("List", Get(game, "_builderPropertyEditMode")!.ToString());
        Assert.Equal("Confirmed", values[GameplayMessageMetadata.TextPropertyKey]);
        Call(game, "UpdateGarrisonBuilderPropertyEditor", new KeyboardState(Keys.Escape), default(MouseState));
        Assert.Equal("Confirmed", ((List<CustomMapBuilderEntity>)Get(game, "_builderEntities")!)[0].Properties[GameplayMessageMetadata.TextPropertyKey]);
    }

    [Fact]
    public void LivePreviewCapturesTheOriginalAndCancelRestoresIt()
    {
        var entity = CustomMapBuilderEntity.Create("medCabinet", 10, 10, new Dictionary<string, string> { ["heal"] = "true" }).NormalizeForEditing();
        var game = Session(entity);
        Set(game, "_builderSelectedEntityIndex", 0); SetEnum(game, "_builderPropertyTarget", "SelectedMapEntity");
        Set(game, "_builderPropertyEditorValues", new Dictionary<string, string> { ["heal"] = "false" });
        Call(game, "ApplyGarrisonBuilderPropertyEditorLivePreview"); Call(game, "RecordGarrisonBuilderHistory"); Call(game, "RecordGarrisonBuilderHistory");
        var undo = (IList)Get(game, "_builderUndoStack")!; Assert.Single(undo.Cast<object>());
        var snapshot = undo[0]!;
        var savedEntities = (CustomMapBuilderEntity[])snapshot.GetType().GetProperty("Entities")!.GetValue(snapshot)!;
        Assert.Equal("true", savedEntities[0].Properties["heal"]);
        Call(game, "CompleteGarrisonBuilderPropertyTransaction", false);
        Assert.Equal("true", ((List<CustomMapBuilderEntity>)Get(game, "_builderEntities")!)[0].Properties["heal"]);
        Assert.Empty(undo.Cast<object>()); Assert.False((bool)Get(game, "_builderDirty")!);
    }

    [Fact]
    public void NewDocumentClearsHistoryAndOldGestures()
    {
        var game = Session(CustomMapBuilderEntity.Create("redspawn", 12, 24));
        Call(game, "RecordGarrisonBuilderHistory");
        Set(game, "_builderAllowDiscard", true); Set(game, "_builderEntityDragging", true); Set(game, "_builderAreaSelectDragging", true);
        Call(game, "CreateNewGarrisonBuilderDocument");
        Assert.Empty(((IList)Get(game, "_builderUndoStack")!).Cast<object>());
        Assert.Empty((List<CustomMapBuilderEntity>)Get(game, "_builderEntities")!);
        Assert.False((bool)Get(game, "_builderEntityDragging")!); Assert.False((bool)Get(game, "_builderAreaSelectDragging")!);
        Assert.False((bool)Get(game, "_builderDirty")!);
    }

    [Fact]
    public void UndoClearReturnsToSavedRevisionAndRedoRestoresTheDeletion()
    {
        var entity = CustomMapBuilderEntity.Create("redspawn", 12, 24, new Dictionary<string,string> { [MapLogicMetadata.MapEntityIdPropertyKey] = "aaaa0001" }).NormalizeForEditing();
        var game = Session(entity);
        Call(game, "ClearGarrisonBuilderEntities");
        Assert.True((bool)Call(game, "TryUndoGarrisonBuilder")!);
        Assert.Single((List<CustomMapBuilderEntity>)Get(game, "_builderEntities")!);
        Assert.False((bool)Get(game, "_builderDirty")!);
        Assert.True((bool)Call(game, "TryRedoGarrisonBuilder")!);
        Assert.Empty((List<CustomMapBuilderEntity>)Get(game, "_builderEntities")!);
        Assert.True((bool)Get(game, "_builderDirty")!);
    }

    [Fact]
    public void FocusTerminationEndsEveryCanvasGesture()
    {
        var game = Session();
        foreach (var name in new[] { "_builderEntityDragging", "_builderAreaSelectDragging", "_builderPlacementDragging", "_builderEraseDragging", "_builderLayerOffsetDragging", "_builderMapPanDragging", "_builderGameplayMessageImageDragging", "_builderMultiEntityMapPickAreaSelectDragging" }) Set(game, name, true);
        Call(game, "FinishGarrisonBuilderGestures");
        foreach (var name in new[] { "_builderEntityDragging", "_builderAreaSelectDragging", "_builderPlacementDragging", "_builderEraseDragging", "_builderLayerOffsetDragging", "_builderMapPanDragging", "_builderGameplayMessageImageDragging", "_builderMultiEntityMapPickAreaSelectDragging" }) Assert.False((bool)Get(game, name)!);
    }

    [Fact]
    public void CloneRemapsInternalTargetsAndKeepsExternalTargets()
    {
        var first = CustomMapBuilderEntity.Create("logicArea", 10, 10, new Dictionary<string, string> { [MapLogicMetadata.MapEntityIdPropertyKey] = "aaaa0001" }).NormalizeForEditing();
        var external = CustomMapBuilderEntity.Create("logicArea", 20, 10, new Dictionary<string, string> { [MapLogicMetadata.MapEntityIdPropertyKey] = "aaaa0002" }).NormalizeForEditing();
        var trigger = CustomMapBuilderEntity.Create(MapLogicMetadata.ActivatorEntityType, 30, 10, new Dictionary<string, string>
        {
            [MapLogicMetadata.MapEntityIdPropertyKey] = "aaaa0003",
            [MapLogicMetadata.ActivatorEntityPropertyKey] = MapLogicEntityReferenceList.Format([MapLogicEntityReference.FormatEntityRef(first), MapLogicEntityReference.FormatEntityRef(external)]),
        }).NormalizeForEditing();
        var game = Session(first, external, trigger);
        object?[] args = [new[] { 0, 2 }, false, null]; Assert.True((bool)Call(game, "TryDuplicateGarrisonBuilderEntities", args)!);
        var entities = (List<CustomMapBuilderEntity>)Get(game, "_builderEntities")!;
        Assert.Equal(MapLogicEntityReferenceList.Format([MapLogicEntityReference.FormatEntityRef(entities[3]), MapLogicEntityReference.FormatEntityRef(external)]), entities[4].Properties[MapLogicMetadata.ActivatorEntityPropertyKey]);
    }

    [Fact]
    public void RebaseKeepsHistoryAssetsIndependentOfOriginalFolder()
    {
        var game = Session();
        var original = CustomMapBuilderDocument.CreateEmpty("map") with { BackgroundImagePath = "old/bg.png", WalkmaskImagePath = "old/wm.png" };
        Set(game, "_builderDocument", original); Call(game, "RecordGarrisonBuilderHistory");
        var saved = original with { BackgroundImagePath = "new/bg.png", WalkmaskImagePath = "new/wm.png" };
        Call(game, "RebaseGarrisonBuilderSavedAssets", original, saved);
        Assert.Equal("new/bg.png", ((CustomMapBuilderDocument)Get(game, "_builderDocument")!).BackgroundImagePath);
        var snapshot = ((IList)Get(game, "_builderUndoStack")!)[0]!;
        Assert.Equal("new/wm.png", ((CustomMapBuilderDocument)snapshot.GetType().GetProperty("Document")!.GetValue(snapshot)!).WalkmaskImagePath);
    }
}
