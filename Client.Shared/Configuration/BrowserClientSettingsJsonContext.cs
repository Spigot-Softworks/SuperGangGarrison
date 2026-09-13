using System.Text.Json.Serialization;

namespace OpenGarrison.ClientShared;

[JsonSerializable(typeof(ClientSettings))]
internal partial class BrowserClientSettingsJsonContext : JsonSerializerContext;
