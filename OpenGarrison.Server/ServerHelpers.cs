using System.Collections.Generic;
using System.Net;
using OpenGarrison.Core;
using OpenGarrison.Protocol;

internal static partial class ServerHelpers
{
    internal static bool EndpointsEqual(IPEndPoint left, IPEndPoint right)
    {
        return left.Address.Equals(right.Address) && left.Port == right.Port;
    }

    internal static bool IsSequenceNewer(uint sequence, uint previousSequence)
    {
        return unchecked((int)(sequence - previousSequence)) > 0;
    }

    internal static ClientSession? FindClient(IReadOnlyDictionary<byte, ClientSession> clientsBySlot, IPEndPoint remoteEndPoint)
    {
        foreach (var client in clientsBySlot.Values)
        {
            if (EndpointsEqual(remoteEndPoint, client.EndPoint))
            {
                return client;
            }
        }

        return null;
    }

    internal static byte FindAvailableSlot(
        IReadOnlyDictionary<byte, ClientSession> clientsBySlot,
        int maxTotalClients,
        int maxSpectatorClients,
        int maxPlayableClients)
    {
        if (clientsBySlot.Count >= maxTotalClients)
        {
            return 0;
        }

        var playableSlot = FindAvailablePlayableSlot(clientsBySlot, maxPlayableClients);
        if (playableSlot != 0)
        {
            return playableSlot;
        }

        return FindAvailableSpectatorSlot(clientsBySlot, maxTotalClients, maxSpectatorClients);
    }

    internal static byte FindAvailablePlayableSlot(
        IReadOnlyDictionary<byte, ClientSession> clientsBySlot,
        int maxPlayableClients,
        byte? excludingSlot = null)
    {
        for (var slotValue = 1; slotValue <= maxPlayableClients; slotValue += 1)
        {
            var slot = (byte)slotValue;
            if (excludingSlot.HasValue && slot == excludingSlot.Value)
            {
                return slot;
            }

            if (!clientsBySlot.ContainsKey(slot))
            {
                return slot;
            }
        }

        return 0;
    }

    internal static byte FindAvailableSpectatorSlot(
        IReadOnlyDictionary<byte, ClientSession> clientsBySlot,
        int maxTotalClients,
        int maxSpectatorClients,
        byte? excludingSlot = null)
    {
        var occupiedCount = clientsBySlot.Count;
        if (excludingSlot.HasValue && clientsBySlot.ContainsKey(excludingSlot.Value))
        {
            occupiedCount -= 1;
        }

        if (occupiedCount >= maxTotalClients)
        {
            return 0;
        }

        for (var offset = 0; offset < maxSpectatorClients; offset += 1)
        {
            var spectatorSlot = (byte)(SimulationWorld.FirstSpectatorSlot + offset);
            if (excludingSlot.HasValue && spectatorSlot == excludingSlot.Value)
            {
                return spectatorSlot;
            }

            if (!clientsBySlot.ContainsKey(spectatorSlot))
            {
                return spectatorSlot;
            }
        }

        return 0;
    }

    internal static bool IsSpectatorSlot(byte slot)
    {
        return slot >= SimulationWorld.FirstSpectatorSlot;
    }

    internal static PlayerInputSnapshot ToCoreInput(InputStateMessage message)
    {
        var buttons = message.Buttons;
        return new PlayerInputSnapshot(
            Left: buttons.HasFlag(InputButtons.Left),
            Right: buttons.HasFlag(InputButtons.Right),
            Up: buttons.HasFlag(InputButtons.Up),
            Down: buttons.HasFlag(InputButtons.Down),
            BuildSentry: buttons.HasFlag(InputButtons.BuildSentry),
            DestroySentry: buttons.HasFlag(InputButtons.DestroySentry),
            Taunt: buttons.HasFlag(InputButtons.Taunt),
            FirePrimary: buttons.HasFlag(InputButtons.FirePrimary),
            FireSecondary: buttons.HasFlag(InputButtons.FireSecondary),
            AimWorldX: message.AimWorldX,
            AimWorldY: message.AimWorldY,
            DebugKill: buttons.HasFlag(InputButtons.DebugKill),
            DropIntel: buttons.HasFlag(InputButtons.DropIntel));
    }
}
