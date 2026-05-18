#nullable enable

using System.Collections.Generic;
using OpenGarrison.Protocol;

namespace OpenGarrison.Client;

public partial class Game1
{
    private readonly Dictionary<byte, PlayerSocialProfileState> _onlinePlayerSocialProfilesBySlot = new();

    private void HandlePlayerSocialProfileUpdateMessage(PlayerSocialProfileUpdateMessage update)
    {
        for (var index = 0; index < update.RemovedSlots.Count; index += 1)
        {
            _onlinePlayerSocialProfilesBySlot.Remove(update.RemovedSlots[index]);
        }

        for (var index = 0; index < update.Profiles.Count; index += 1)
        {
            var profile = update.Profiles[index];
            _onlinePlayerSocialProfilesBySlot[profile.Slot] = profile;
        }
    }

    private void ClearOnlinePlayerSocialProfiles()
    {
        _onlinePlayerSocialProfilesBySlot.Clear();
    }

    private bool TryGetOnlinePlayerSocialProfile(byte slot, out PlayerSocialProfileState profile)
    {
        return _onlinePlayerSocialProfilesBySlot.TryGetValue(slot, out profile!);
    }
}
