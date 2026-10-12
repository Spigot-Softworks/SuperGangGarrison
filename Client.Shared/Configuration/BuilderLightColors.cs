using OpenGarrison.Core;

namespace OpenGarrison.ClientShared;

/// <summary>Moves the obsolete extra logic colour into the builder's existing tertiary selection.</summary>
public static class BuilderLightColors
{
    public static void NormalizeForEditing(IDictionary<string, string> properties)
    {
        if (properties.TryGetValue(MapLightMetadata.TriggerColorKey, out var legacy)
            && !string.IsNullOrWhiteSpace(legacy)
            && (!properties.TryGetValue(MapLightMetadata.AltColor2Key, out var tertiary) || string.IsNullOrWhiteSpace(tertiary)))
            properties[MapLightMetadata.AltColor2Key] = legacy;
        properties.Remove(MapLightMetadata.TriggerColorKey);
        // The old single-input "trigger" action held the extra third colour. Move
        // that wire to the dedicated tertiary slot without changing its behaviour.
        if (properties.TryGetValue(MapLightMetadata.ColorActionKey, out var action)
            && MapLightStyleMetadata.ParseColorAction(action) == MapLightColorAction.Trigger
            && (!properties.TryGetValue(MapLightMetadata.TertiaryColorInputKey, out var tertiaryInput) || string.IsNullOrWhiteSpace(tertiaryInput)))
        {
            if (properties.TryGetValue(MapLightMetadata.ColorInputKey, out var input) && !string.IsNullOrWhiteSpace(input))
            {
                properties[MapLightMetadata.TertiaryColorInputKey] = input;
                properties[MapLightMetadata.ColorInputKey] = string.Empty;
            }
            properties[MapLightMetadata.ColorActionKey] = "holdAlt";
        }
    }
}
