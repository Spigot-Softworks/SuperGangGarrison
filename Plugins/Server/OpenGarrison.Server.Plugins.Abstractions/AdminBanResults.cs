namespace OpenGarrison.Server.Plugins;

public readonly record struct OpenGarrisonServerAddressActionResult(
    bool Success,
    string Address,
    string ErrorMessage);

public readonly record struct OpenGarrisonServerBanActionResult(
    bool Success,
    string Address,
    string ErrorMessage,
    bool IsPermanent,
    long ExpiresUnixTimeSeconds);
