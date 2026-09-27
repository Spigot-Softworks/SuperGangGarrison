using System.Text.Json;

namespace OpenGarrison.GameplayModding;

/// <summary>
/// The definition of a gameplay ability attached to an item.
/// </summary>
/// <param name="Category">The ability category.</param>
/// <param name="Activation">The activation kind.</param>
/// <param name="ExecutorId">The executor id.</param>
/// <param name="Tags">The ability tags.</param>
/// <param name="Parameters">The ability parameters.</param>
/// <param name="Channel">The ability channel.</param>
public sealed record GameplayAbilityDefinition(
    string Category = "",
    string Activation = "",
    string ExecutorId = "",
    IReadOnlyList<string>? Tags = null,
    IReadOnlyDictionary<string, JsonElement>? Parameters = null,
    string Channel = "")
{
    /// <summary>
    /// Gets the ability tags.
    /// </summary>
    public IReadOnlyList<string> Tags { get; init; } = Tags ?? Array.Empty<string>();

    /// <summary>
    /// Gets the ability parameters.
    /// </summary>
    public IReadOnlyDictionary<string, JsonElement> Parameters { get; init; } =
        Parameters ?? new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase);
}
