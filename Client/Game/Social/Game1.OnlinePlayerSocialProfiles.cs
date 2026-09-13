#nullable enable

using System.Collections.Generic;
using OpenGarrison.Protocol;

namespace OpenGarrison.Client;

public partial class Game1
{
    private readonly Dictionary<byte, PlayerSocialProfileState> _onlinePlayerSocialProfilesBySlot = new();
    private readonly Dictionary<byte, PlayerServerTitleState> _onlinePlayerServerTitlesBySlot = new();

    private void HandlePlayerSocialProfileUpdateMessage(PlayerSocialProfileUpdateMessage update)
    {
        for (var index = 0; index < update.RemovedSlots.Count; index += 1)
        {
            _onlinePlayerSocialProfilesBySlot.Remove(update.RemovedSlots[index]);
            _onlinePlayerServerTitlesBySlot.Remove(update.RemovedSlots[index]);
        }

        for (var index = 0; index < update.Profiles.Count; index += 1)
        {
            var profile = update.Profiles[index];
            _onlinePlayerSocialProfilesBySlot[profile.Slot] = profile;
            _onlinePlayerServerTitlesBySlot.Remove(profile.Slot);
        }

        var titles = update.Titles ?? [];
        for (var index = 0; index < titles.Count; index += 1)
        {
            var title = titles[index];
            if (!string.IsNullOrWhiteSpace(title.Text)) _onlinePlayerServerTitlesBySlot[title.Slot] = title;
        }
    }

    private void ClearOnlinePlayerSocialProfiles()
    {
        _onlinePlayerSocialProfilesBySlot.Clear();
        _onlinePlayerServerTitlesBySlot.Clear();
    }

    private bool TryGetOnlinePlayerSocialProfile(byte slot, out PlayerSocialProfileState profile)
    {
        return _onlinePlayerSocialProfilesBySlot.TryGetValue(slot, out profile!);
    }

    private bool TryGetOnlinePlayerServerTitle(byte slot, out PlayerServerTitleState title)
    {
        return _onlinePlayerServerTitlesBySlot.TryGetValue(slot, out title!);
    }
}
