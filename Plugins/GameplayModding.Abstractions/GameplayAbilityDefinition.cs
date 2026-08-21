using System.Text.Json;

namespace OpenGarrison.GameplayModding;

public sealed record GameplayAbilityDefinition(
    string Category = "",
    string Activation = "",
    string ExecutorId = "",
    IReadOnlyList<string>? Tags = null,
    IReadOnlyDictionary<string, JsonElement>? Parameters = null)
{
    public IReadOnlyList<string> Tags { get; init; } = Tags ?? Array.Empty<string>();

    public IReadOnlyDictionary<string, JsonElement> Parameters { get; init; } =
        Parameters ?? new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase);
}
