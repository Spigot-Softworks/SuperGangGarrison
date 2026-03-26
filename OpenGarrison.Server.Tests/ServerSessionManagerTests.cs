using System;
using System.Collections.Generic;
using System.Net;
using OpenGarrison.Core;
using OpenGarrison.Protocol;
using Xunit;

namespace OpenGarrison.Server.Tests;

public sealed class ServerSessionManagerTests
{
    [Fact]
    public void HandlePasswordSubmit_WithWrongPassword_SendsSinglePasswordResultAndRemovesClient()
    {
        var world = new SimulationWorld();
        var client = new ClientSession(1, new IPEndPoint(IPAddress.Loopback, 8190), "Player", TimeSpan.Zero)
        {
            IsAuthorized = false,
        };
        var clientsBySlot = new Dictionary<byte, ClientSession> { [1] = client };
        var sentMessages = new List<IProtocolMessage>();
        var manager = CreateManager(
            world,
            clientsBySlot,
            (_, message) => sentMessages.Add(message));

        manager.HandlePasswordSubmit(client, new PasswordSubmitMessage("wrong"));

        Assert.Single(sentMessages);
        var result = Assert.IsType<PasswordResultMessage>(sentMessages[0]);
        Assert.False(result.Accepted);
        Assert.Equal("Incorrect password.", result.Reason);
        Assert.Empty(clientsBySlot);
    }

    [Fact]
    public void HandlePasswordSubmit_WhenRateLimited_ReturnsRateLimitReasonAndRemovesClient()
    {
        var world = new SimulationWorld();
        var client = new ClientSession(1, new IPEndPoint(IPAddress.Loopback, 8190), "Player", TimeSpan.Zero)
        {
            IsAuthorized = false,
        };
        var clientsBySlot = new Dictionary<byte, ClientSession> { [1] = client };
        var sentMessages = new List<IProtocolMessage>();
        var manager = CreateManager(
            world,
            clientsBySlot,
            (_, message) => sentMessages.Add(message),
            getPasswordRateLimitReason: _ => "Too many password attempts. Try again in 10s.");

        manager.HandlePasswordSubmit(client, new PasswordSubmitMessage("wrong"));

        var result = Assert.IsType<PasswordResultMessage>(Assert.Single(sentMessages));
        Assert.False(result.Accepted);
        Assert.Equal("Too many password attempts. Try again in 10s.", result.Reason);
        Assert.Empty(clientsBySlot);
    }

    [Fact]
    public void RefreshPasswordRequests_ReissuesPromptAfterRetryWindow()
    {
        var now = TimeSpan.FromSeconds(3);
        var world = new SimulationWorld();
        var client = new ClientSession(1, new IPEndPoint(IPAddress.Loopback, 8190), "Player", TimeSpan.Zero)
        {
            IsAuthorized = false,
            LastPasswordRequestSentAt = TimeSpan.Zero,
        };
        var clientsBySlot = new Dictionary<byte, ClientSession> { [1] = client };
        var sentMessages = new List<IProtocolMessage>();
        var manager = CreateManager(
            world,
            clientsBySlot,
            (_, message) => sentMessages.Add(message),
            nowProvider: () => now,
            passwordRetrySeconds: 2,
            passwordTimeoutSeconds: 30);

        manager.RefreshPasswordRequests();

        Assert.IsType<PasswordRequestMessage>(Assert.Single(sentMessages));
        Assert.Equal(now, client.LastPasswordRequestSentAt);
    }

    [Fact]
    public void RefreshPasswordRequests_TimesOutWaitingPasswordClients()
    {
        var now = TimeSpan.FromSeconds(31);
        var world = new SimulationWorld();
        var client = new ClientSession(1, new IPEndPoint(IPAddress.Loopback, 8190), "Player", TimeSpan.Zero)
        {
            IsAuthorized = false,
            LastPasswordRequestSentAt = TimeSpan.FromSeconds(1),
        };
        var clientsBySlot = new Dictionary<byte, ClientSession> { [1] = client };
        var sentMessages = new List<IProtocolMessage>();
        var manager = CreateManager(
            world,
            clientsBySlot,
            (_, message) => sentMessages.Add(message),
            nowProvider: () => now,
            passwordRetrySeconds: 2,
            passwordTimeoutSeconds: 30);

        manager.RefreshPasswordRequests();

        var denied = Assert.IsType<ConnectionDeniedMessage>(Assert.Single(sentMessages));
        Assert.Equal("Password entry timed out.", denied.Reason);
        Assert.Empty(clientsBySlot);
    }

    [Fact]
    public void PruneTimedOutClients_KeepsLoopbackClientWithinExtendedGraceWindow()
    {
        var world = new SimulationWorld();
        var client = new ClientSession(1, new IPEndPoint(IPAddress.Loopback, 8190), "Player", TimeSpan.Zero);
        var clientsBySlot = new Dictionary<byte, ClientSession> { [1] = client };
        var manager = CreateManager(
            world,
            clientsBySlot,
            (_, _) => { },
            nowProvider: () => TimeSpan.FromSeconds(10));

        manager.PruneTimedOutClients();

        Assert.True(clientsBySlot.ContainsKey(1));
    }

    [Fact]
    public void PruneTimedOutClients_RemovesLoopbackClientAfterExtendedGraceWindow()
    {
        var world = new SimulationWorld();
        var client = new ClientSession(1, new IPEndPoint(IPAddress.Loopback, 8190), "Player", TimeSpan.Zero);
        var clientsBySlot = new Dictionary<byte, ClientSession> { [1] = client };
        var manager = CreateManager(
            world,
            clientsBySlot,
            (_, _) => { },
            nowProvider: () => TimeSpan.FromSeconds(31));

        manager.PruneTimedOutClients();

        Assert.Empty(clientsBySlot);
    }

    [Fact]
    public void PruneTimedOutClients_RemovesRemoteClientAfterConfiguredTimeout()
    {
        var world = new SimulationWorld();
        var client = new ClientSession(1, new IPEndPoint(IPAddress.Parse("192.168.10.50"), 8190), "Player", TimeSpan.Zero);
        var clientsBySlot = new Dictionary<byte, ClientSession> { [1] = client };
        var manager = CreateManager(
            world,
            clientsBySlot,
            (_, _) => { },
            nowProvider: () => TimeSpan.FromSeconds(10));

        manager.PruneTimedOutClients();

        Assert.Empty(clientsBySlot);
    }

    private static ServerSessionManager CreateManager(
        SimulationWorld world,
        Dictionary<byte, ClientSession> clientsBySlot,
        Action<IPEndPoint, IProtocolMessage> sendMessage,
        Func<TimeSpan>? nowProvider = null,
        Func<IPEndPoint, string?>? getPasswordRateLimitReason = null,
        Action<IPEndPoint>? recordPasswordFailure = null,
        Action<IPEndPoint>? clearPasswordFailures = null,
        double clientTimeoutSeconds = 5,
        double passwordTimeoutSeconds = 30,
        double passwordRetrySeconds = 2)
    {
        return new ServerSessionManager(
            world,
            clientsBySlot,
            maxPlayableClients: 10,
            maxTotalClients: 10,
            maxSpectatorClients: 10,
            nowProvider: nowProvider ?? (() => TimeSpan.Zero),
            serverPassword: "secret",
            passwordRequired: true,
            clientTimeoutSeconds: clientTimeoutSeconds,
            passwordTimeoutSeconds: passwordTimeoutSeconds,
            passwordRetrySeconds: passwordRetrySeconds,
            getPasswordRateLimitReason: getPasswordRateLimitReason ?? (_ => null),
            recordPasswordFailure: recordPasswordFailure ?? (_ => { }),
            clearPasswordFailures: clearPasswordFailures ?? (_ => { }),
            sendMessage: sendMessage,
            log: _ => { });
    }
}
