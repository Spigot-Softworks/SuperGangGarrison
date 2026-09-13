using System.Text.Json.Serialization;

namespace OpenGarrison.Client;

[JsonSerializable(typeof(InputBindingsSettings))]
[JsonSerializable(typeof(LastToDieStatsDocument))]
[JsonSerializable(typeof(HudLayoutDocument))]
[JsonSerializable(typeof(FirstPlayHintsDocument))]
internal partial class BrowserPreferencesJsonContext : JsonSerializerContext;
