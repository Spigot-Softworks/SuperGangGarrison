#nullable enable

using System;

namespace OpenGarrison.Client;

public partial class Game1
{
    private const long LegacyGg2TeamRequestTimeoutMilliseconds = 3000;
    private long _legacyGg2TeamRequestStartedAtMilliseconds = -1;

    private void UpdateLegacyGg2TeamRequest()
    {
        if (_legacyGg2TeamRequestStartedAtMilliseconds < 0)
        {
            return;
        }

        if (!_networkClient.IsConnected || !_networkClient.IsLegacyGg2Connection)
        {
            _legacyGg2TeamRequestStartedAtMilliseconds = -1;
            return;
        }

        if (!_networkClient.IsSpectator)
        {
            _legacyGg2TeamRequestStartedAtMilliseconds = -1;
            return;
        }

        if (Environment.TickCount64 - _legacyGg2TeamRequestStartedAtMilliseconds
            < LegacyGg2TeamRequestTimeoutMilliseconds)
        {
            return;
        }

        _legacyGg2TeamRequestStartedAtMilliseconds = -1;
        OpenOnlineTeamSelection(clearPendingSelections: false,
            statusMessage: "GG2 did not accept that team. Choose an available team.");
    }
}
