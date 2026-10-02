namespace OpenGarrison.Core;

/// <summary>Constants shared by the world and the systems extracted from it.</summary>
internal static class SimulationConstants
{
    public const string ClassChangeKillFeedSuffix = " bid farewell, cruel world!";
    public const byte LocalPlayerSlot = 1;
    public const string DefaultLocalPlayerName = "Player 1";
    public const int MaxPlayableNetworkPlayers = 40;
    public static IReadOnlyList<byte> NetworkPlayerSlots { get; } = Enumerable.Range(1, MaxPlayableNetworkPlayers).Select(static value => (byte)value).ToArray();
    public const float PyroAirblastDistance = 150f;
    public const int CombatTraceLifetimeTicks = 3;
    public const int KillFeedLifetimeTicks = 150;
    public const int DefaultGibLevel = 3;
    public const int DeathCamFocusFreezeDelayTicks = 60;
    public const int KillFeedLocalInvolvedLifetimeTicks = 300;
    public const int NetworkProjectileRemovalSuppressionTicks = 180;
}
