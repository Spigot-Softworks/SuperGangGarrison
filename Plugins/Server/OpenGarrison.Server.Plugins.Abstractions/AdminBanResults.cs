namespace OpenGarrison.Server.Plugins;

/// <summary>
/// The result of an IP address action such as an unban.
/// </summary>
/// <param name="Success">Whether the action succeeded.</param>
/// <param name="Address">The IP address the action applied to.</param>
/// <param name="ErrorMessage">The error message when the action failed.</param>
public readonly record struct OpenGarrisonServerAddressActionResult(
    bool Success,
    string Address,
    string ErrorMessage);

/// <summary>
/// The result of a player or IP ban action.
/// </summary>
/// <param name="Success">Whether the ban succeeded.</param>
/// <param name="Address">The banned address or player identifier.</param>
/// <param name="ErrorMessage">The error message when the ban failed.</param>
/// <param name="IsPermanent">Whether the ban is permanent.</param>
/// <param name="ExpiresUnixTimeSeconds">The ban expiry as Unix time seconds (0 when permanent).</param>
public readonly record struct OpenGarrisonServerBanActionResult(
    bool Success,
    string Address,
    string ErrorMessage,
    bool IsPermanent,
    long ExpiresUnixTimeSeconds);
