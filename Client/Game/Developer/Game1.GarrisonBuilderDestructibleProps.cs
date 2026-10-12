#nullable enable
using System.Globalization;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using OpenGarrison.Core;

namespace OpenGarrison.Client;

public partial class Game1
{
    private bool _builderDestroyedOpacityDragging;
    private Rectangle _builderDestroyedOpacityTrack;

    private void EnsureGarrisonBuilderDestructionPropertyRows()
    {
        if (!TryGetGarrisonBuilderEditedEntityType(out var type)) return;
        void Add(string key, string value) => _builderPropertyEditorValues.TryAdd(key, value);
        if (DamageableMetadata.IsDamageableEntityType(type))
        {
            Add(DamageableMetadata.RespawnPropertyKey, "0");
            Add(DamageableMetadata.OnDestroyedPropertyKey, "");
            Add(DamageableMetadata.OnRedDestroyedPropertyKey, "");
            Add(DamageableMetadata.OnBlueDestroyedPropertyKey, "");
        }
        if (DamageTriggerMetadata.IsDamageTriggerEntityType(type))
        {
            Add(DestructiblePropMetadata.DestructionKey, "fadeOut");
            Add(DestructiblePropMetadata.MinimumOpacityKey, "0");
            Add(DestructiblePropMetadata.RespawnKey, "fadeIn");
            Add(DestructiblePropMetadata.TransitionKey, "0.5");
            Add(DestructiblePropMetadata.DestructionSoundKey, "");
            Add(DestructiblePropMetadata.RespawnSoundKey, "");
        }
        if (CustomMapCustomSpriteMetadata.IsCustomSpriteEntityType(type)
            || ForegroundSpriteMetadata.IsForegroundSpriteEntityType(type) || SpritesheetMetadata.IsSpritesheetEntityType(type)
            || (MapLightMetadata.IsLightEntityType(type) && !string.IsNullOrWhiteSpace(GetGarrisonBuilderPropertyEditorValue(MapLightMetadata.MaskKey))))
            Add(DestructiblePropMetadata.ZoneKey, "");
    }

    private bool TryHandleGarrisonBuilderDestructionPropertyClick(string key, string value, Rectangle row, Point point)
    {
        if (!TryGetGarrisonBuilderEditedEntityType(out var type) || !DamageTriggerMetadata.IsDamageTriggerEntityType(type)) return false;
        string? next = key switch
        {
            DestructiblePropMetadata.DestructionKey => value == "breakApart" ? "fadeOut" : "breakApart",
            DestructiblePropMetadata.RespawnKey => value == "staticWarp" ? "fadeIn" : "staticWarp",
            _ => null,
        };
        if (next is not null)
        {
            _builderPropertyEditorValues[key] = next;
            MarkGarrisonBuilderPropertyEditorChanged();
            ApplyGarrisonBuilderPropertyEditorLivePreview();
            return true;
        }
        if (key == DestructiblePropMetadata.MinimumOpacityKey)
        {
            _builderDestroyedOpacityTrack = GetBuilderDestroyedOpacityTrack(row);
            _builderDestroyedOpacityDragging = true;
            SetBuilderDestroyedOpacity(point.X);
            return true;
        }
        if (key != DestructiblePropMetadata.DestructionSoundKey && key != DestructiblePropMetadata.RespawnSoundKey) return false;
        if (!string.IsNullOrWhiteSpace(_builderSelectedResourceName)
            && _builderDocument.Resources.TryGetValue(_builderSelectedResourceName, out var resource)
            && IsGarrisonBuilderResourceUsableForGameplayMessageProperty(resource, GameplayMessageMetadata.SoundPropertyKey) && resource.Name != value)
        {
            _builderPropertyEditorValues[key] = resource.Name;
            _builderSelectedResourceName = "";
            MarkGarrisonBuilderPropertyEditorChanged();
            ApplyGarrisonBuilderPropertyEditorLivePreview();
            return true;
        }
        if (!string.IsNullOrWhiteSpace(value))
        {
            _builderPropertyEditorValues[key] = "";
            MarkGarrisonBuilderPropertyEditorChanged();
            ApplyGarrisonBuilderPropertyEditorLivePreview();
            return true;
        }
        BeginAddingGarrisonBuilderResource(CustomMapBuilderResourceKind.MessageSound,
            key == DestructiblePropMetadata.DestructionSoundKey ? "prop_destroy_sound" : "prop_respawn_sound", key);
        return true;
    }

    private static Rectangle GetBuilderDestroyedOpacityTrack(Rectangle row) => new(row.X + row.Width / 2, row.Center.Y - 3, Math.Max(20, row.Width / 2 - 42), 6);
    private void SetBuilderDestroyedOpacity(int x)
    {
        var percent = (int)MathF.Round(Math.Clamp((x - _builderDestroyedOpacityTrack.X) / (float)_builderDestroyedOpacityTrack.Width, 0f, 1f) * 100);
        _builderPropertyEditorValues[DestructiblePropMetadata.MinimumOpacityKey] = percent.ToString(CultureInfo.InvariantCulture);
        MarkGarrisonBuilderPropertyEditorChanged();
        ApplyGarrisonBuilderPropertyEditorLivePreview();
    }

    private bool UpdateBuilderDestroyedOpacitySlider(MouseState mouse)
    {
        if (!_builderDestroyedOpacityDragging) return false;
        if (mouse.LeftButton != ButtonState.Pressed) { _builderDestroyedOpacityDragging = false; return true; }
        SetBuilderDestroyedOpacity(mouse.X);
        return true;
    }

    private bool DrawBuilderDestroyedOpacitySlider(Rectangle row, string key, string value, float scale)
    {
        if (key != DestructiblePropMetadata.MinimumOpacityKey) return false;
        var track = GetBuilderDestroyedOpacityTrack(row);
        var percent = DestructiblePropMetadata.ParseNumber(value, 0, 100);
        _spriteBatch.Draw(_pixel, row, _builderUseModernUi ? new Color(62, 58, 54) : new Color(184, 184, 184));
        var color = _builderUseModernUi ? Color.White : Color.Black;
        DrawBitmapFontText("Minimum opacity", new Vector2(row.X + 6, row.Y + 4), color, scale);
        _spriteBatch.Draw(_pixel, track, new Color(40, 40, 40));
        _spriteBatch.Draw(_pixel, new Rectangle(track.X, track.Y, (int)(track.Width * percent / 100), track.Height), new Color(185, 155, 100));
        _spriteBatch.Draw(_pixel, new Rectangle(track.X + (int)(track.Width * percent / 100) - 2, track.Y - 3, 4, 12), Color.White);
        DrawBitmapFontText($"{percent:0}%", new Vector2(track.Right + 5, row.Y + 4), color, scale);
        return true;
    }

    private static string? GetDestructionPropertyLabel(string key) => key switch
    {
        DestructiblePropMetadata.DestructionKey => "On destruction",
        DestructiblePropMetadata.RespawnKey => "On respawn",
        DestructiblePropMetadata.MinimumOpacityKey => "Minimum opacity",
        DestructiblePropMetadata.TransitionKey => "Transition time (sec)",
        DestructiblePropMetadata.DestructionSoundKey => "Sound on destruction",
        DestructiblePropMetadata.RespawnSoundKey => "Sound on respawn",
        DamageableMetadata.RespawnPropertyKey => "Respawn timer (sec, 0 = off)",
        DamageableMetadata.OnDestroyedPropertyKey => "On any destruction",
        DamageableMetadata.OnRedDestroyedPropertyKey => "On Red destruction",
        DamageableMetadata.OnBlueDestroyedPropertyKey => "On Blue destruction",
        _ => null,
    };

    private static string? FormatDestructionPropertyValue(string key, string value) => key switch
    {
        DestructiblePropMetadata.DestructionKey => $"On destruction: {(value == "breakApart" ? "Break apart" : "Fade out")}",
        DestructiblePropMetadata.RespawnKey => $"On respawn: {(value == "staticWarp" ? "Static warp" : "Fade in")}",
        _ => null,
    };
}
