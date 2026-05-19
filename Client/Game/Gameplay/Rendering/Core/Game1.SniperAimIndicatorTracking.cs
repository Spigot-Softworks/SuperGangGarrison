using Microsoft.Xna.Framework;
using OpenGarrison.Core;
using System.Collections.Generic;

namespace OpenGarrison.Client;

public partial class Game1
{
    private readonly struct ClientSniperAimIndicator
    {
        public readonly float X;
        public readonly float Y;
        public readonly PlayerTeam Team;
        public readonly float BaseTransparency;
        public readonly int TicksRemaining;

        public ClientSniperAimIndicator(float x, float y, PlayerTeam team, float baseTransparency, int ticksRemaining)
        {
            X = x;
            Y = y;
            Team = team;
            BaseTransparency = baseTransparency;
            TicksRemaining = ticksRemaining;
        }

        public ClientSniperAimIndicator WithTicksRemaining(int ticksRemaining)
        {
            return new ClientSniperAimIndicator(X, Y, Team, BaseTransparency, ticksRemaining);
        }
    }

    private readonly Dictionary<int, ClientSniperAimIndicator> _clientSniperAimIndicators = new();
    private const int SniperAimIndicatorFadeTicks = 2;  // Fade out over 2 ticks

    private void UpdateClientSniperAimIndicators()
    {
        // Track which snipers still have active indicators in this snapshot
        var activeSnipers = new HashSet<int>();

        // Update or add indicators from the current snapshot
        foreach (var indicator in _world.SniperAimIndicators)
        {
            activeSnipers.Add(indicator.SniperPlayerId);

            // Add or refresh indicator with full lifetime
            _clientSniperAimIndicators[indicator.SniperPlayerId] = new ClientSniperAimIndicator(
                indicator.X,
                indicator.Y,
                indicator.Team,
                indicator.Transparency,
                SniperAimIndicatorFadeTicks);
        }

        // Decay indicators that are no longer in the snapshot
        var toRemove = new List<int>();
        foreach (var kvp in _clientSniperAimIndicators)
        {
            if (!activeSnipers.Contains(kvp.Key))
            {
                var indicator = kvp.Value;
                var newTicksRemaining = indicator.TicksRemaining - 1;
                if (newTicksRemaining <= 0)
                {
                    toRemove.Add(kvp.Key);
                }
                else
                {
                    _clientSniperAimIndicators[kvp.Key] = indicator.WithTicksRemaining(newTicksRemaining);
                }
            }
        }

        // Remove expired indicators
        foreach (var key in toRemove)
        {
            _clientSniperAimIndicators.Remove(key);
        }
    }
}
