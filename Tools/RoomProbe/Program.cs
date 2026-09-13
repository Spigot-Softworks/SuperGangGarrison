using System.Diagnostics;
using OpenGarrison.Client;
using OpenGarrison.ClientShared;
using OpenGarrison.Core;
using OpenGarrison.Protocol;

if (args.FirstOrDefault() == "LEAVE")
{
    using var leaveHttp = new HttpClient();
    using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(40));
    const string alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
    var friend = new string(Enumerable.Range(0, 8).Select(_ => alphabet[Random.Shared.Next(alphabet.Length)]).ToArray());
    var request = new PeerRoomRequest(Guid.NewGuid().ToString(), Guid.NewGuid().ToString("N"),
        $"OG2-{friend[..4]}-{friend[4..]}", "Leave regression", "", ProtocolVersion.Current, "leave-test", Settings: new(BlueBots: 0));
    for (var iteration = 0; iteration < 4; iteration++)
    {
        request = request with { RequestId = Guid.NewGuid().ToString("N"), Kind = iteration % 2 == 0 ? "LastToDie" : "Practice" };
        using var room = await PeerRoomConnection.OpenAsync(leaveHttp, new("http://127.0.0.1:18765"), request, true, deadline.Token);
        if (iteration == 2) room.Dispose(); // Release still works after a broken signaling socket.
        var leaving = room.LeaveAsync();
        if (!ReferenceEquals(leaving, room.LeaveAsync())) throw new InvalidOperationException("Leave is not idempotent.");
        await leaving;
    }
    Console.WriteLine("CONFIRMED LEAVE AND IMMEDIATE ROOM RECREATION PASS (4 alternating rooms)");
    return;
}

if (args.FirstOrDefault() == "RTC")
{
    using var ice = System.Text.Json.JsonDocument.Parse("[]");
    IPeerDataConnection? peer = null;
    peer = PeerDataConnectionFactory.Create(false, ice.RootElement,
        signal => Console.WriteLine("SIGNAL:" + signal), bytes =>
        {
            Console.WriteLine("RECEIVED:" + bytes.Length);
            if (!peer!.TrySend(bytes)) throw new InvalidOperationException("Native echo send failed.");
        });
    using (peer)
        while (await Console.In.ReadLineAsync() is { } signal) peer.ApplySignal(signal);
    return;
}

// Local integration fixture: SELF creates four native clients. A room code joins a browser host.
if (args.Length < 2) throw new ArgumentException("Usage: RoomProbe SELF|ROOM_CODE CONTENT_ID [CONTENT_ROOT]");
if (args.Length > 2) ContentRoot.Initialize(Path.GetFullPath(args[2]));
var self = args[0] == "SELF";
using var http = new HttpClient();
using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(self ? 2 : 8));
var probes = new List<Probe>();
try
{
    var owner = await Probe.Open(http, args[0], args[1], self, timeout.Token);
    probes.Add(owner);
    if (self)
        for (var index = 1; index < 4; index++)
            probes.Add(await Probe.Open(http, owner.Room.Connection.Grant.Code, args[1], false, timeout.Token));
    var clock = Stopwatch.StartNew();
    var previous = clock.Elapsed.TotalSeconds;
    var nextLog = 0d;
    var reconnectPass = 0;
    double hostPauseUntil = 0;
    var hostPauseStarted = false;
    while (!timeout.IsCancellationRequested)
    {
        var now = clock.Elapsed.TotalSeconds;
        foreach (var probe in probes)
            if (!ReferenceEquals(probe, probes[0]) || now >= hostPauseUntil) probe.Update(now - previous, now);
        previous = now;
        if (now >= nextLog)
        {
            nextLog = now + 5;
            foreach (var probe in probes) Console.WriteLine(probe.Describe());
        }
        if (self && probes.All(p => p.Playing))
        {
            if (!hostPauseStarted)
            {
                hostPauseStarted = true; hostPauseUntil = now + 6;
                Console.WriteLine("FOUR NATIVE PLAYERS PASS");
                continue;
            }
            if (now < hostPauseUntil) { await Task.Delay(10); continue; }
            if (probes.Any(p => !p.Client.IsConnected)) throw new InvalidOperationException("Guests disconnected during a six-second host load.");
            if (reconnectPass == 0) Console.WriteLine("SLOW HOST GRACE PASS");
            if (reconnectPass++ < 2)
            {
                var guest = probes[3];
                guest.Client.Disconnect(); guest.Playing = false;
                guest.Room.RequestReconnect();
            }
            else { Console.WriteLine("REPEATED GUEST RECONNECT PASS"); break; }
        }
        await Task.Delay(10);
    }
    if (probes.Any(p => !p.Playing)) throw new TimeoutException(string.Join("\n", probes.Select(p => p.Describe())));
}
finally
{
    foreach (var probe in probes.AsEnumerable().Reverse()) { await probe.Room.Connection.LeaveAsync(); probe.Dispose(); }
}

sealed class Probe : IDisposable
{
    public required PlayerHostedRoomSession Room;
    public required Guid Identity;
    public readonly NetworkGameClient Client = new();
    public bool Playing;
    private double _nextCommand;
    private Exception? _failure;
    public static async Task<Probe> Open(HttpClient http, string roomCode, string content, bool create, CancellationToken token)
    {
        var id = Guid.NewGuid();
        var alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
        var code = new string(Enumerable.Range(0, 8).Select(_ => alphabet[Random.Shared.Next(alphabet.Length)]).ToArray());
        var request = new PeerRoomRequest(id.ToString(), Guid.NewGuid().ToString("N"), $"OG2-{code[..4]}-{code[4..]}",
            "Native probe", Guid.NewGuid().ToString("N"), ProtocolVersion.Current, content,
            Code: create ? "" : roomCode, Settings: new(BlueBots: 0));
        var connection = await PeerRoomConnection.OpenAsync(http, new("http://127.0.0.1:18765"), request, create, token);
        var probe = new Probe { Room = new(connection), Identity = id };
        probe.Room.Failed += reason => probe._failure = new InvalidOperationException(reason);
        probe.Room.ConnectLocal += transport =>
        {
            probe.Client.Disconnect();
            if (!probe.Client.Connect(transport, "Native probe", 0, out var error, clientInstanceId: id))
                probe._failure = new InvalidOperationException(error);
        };
        Console.WriteLine("NATIVE ROOM JOINED " + connection.Grant.Slot);
        return probe;
    }
    public void Update(double elapsed, double now)
    {
        Room.Update(elapsed);
        foreach (var message in Client.ReceiveMessages())
        {
            if (message is WelcomeMessage welcome) Client.SetLocalPlayerSlot(welcome.PlayerSlot);
            if (message is SnapshotMessage snapshot) { Client.AcknowledgeSnapshot(snapshot.Frame); Client.NotifyWorldSnapshotApplied(snapshot); }
            if (message is ConnectionDeniedMessage denied) throw new InvalidOperationException(denied.ToString());
        }
        if (Client.TryConsumeDisconnectReason(out var reason))
        {
            Console.WriteLine("NATIVE RECONNECT: " + reason);
            Room.RequestReconnect();
        }
        if (_failure is not null) throw _failure;
        if (now < _nextCommand) return;
        _nextCommand = now + .6;
        if (Room.State is { Phase: "Lobby", Players: { } members } room)
        {
            if (members.Any(p => p.Slot == Room.Connection.Grant.Slot && !p.Ready)) Room.Connection.Send(new("ready", Ready: true));
            if (Room.IsOwner && members.Length == 4 && members.All(p => p.Ready && p.Connected))
                Room.Connection.Send(new("start", Revision: room.Revision));
        }
        var state = Client.LastToDieState.Snapshot;
        var local = state?.Players.FirstOrDefault(p => p.Slot == Client.LocalPlayerSlot);
        if (local is null) return;
        if (state!.Phase == LastToDieWirePhase.Lobby)
        {
            if (!local.IsReady) Client.SendLastToDieCommand(LastToDieCommandKind.Ready);
            if (Room.IsOwner && Room.State!.Players!.Where(p => p.Connected).All(p => state.Players.Any(m => m.Slot == p.Slot && m.IsReady && m.IsConnected)))
                Client.SendLastToDieCommand(LastToDieCommandKind.RequestStart);
        }
        if (state.Phase == LastToDieWirePhase.SurvivorChoice && string.IsNullOrEmpty(local.SurvivorId))
            Client.SendLastToDieCommand(LastToDieCommandKind.ChooseSurvivor, "ltd.survivor.spy");
        if (state.Phase == LastToDieWirePhase.RewardChoice && local.ActiveOfferChoices.Count > 0)
            Client.SendLastToDieCommand(LastToDieCommandKind.SelectReward, local.ActiveOfferChoices[0], local.ActiveOfferId);
        if (state.Phase == LastToDieWirePhase.Playing && !Playing)
        { Console.WriteLine("NATIVE GAMEPLAY PASS slot=" + Client.LocalPlayerSlot); Playing = true; }
    }
    public string Describe() => $"seat={Room.Connection.Grant.Slot}, room={Room.State?.Phase}, slot={Client.LocalPlayerSlot}, phase={Client.LastToDieState.Snapshot?.Phase}, "
        + string.Join(";", Client.LastToDieState.Snapshot?.Players.Select(p => $"{p.Slot}:connected={p.IsConnected},ready={p.IsReady},host={p.IsHost}") ?? [])
        + $", result={Client.LastToDieState.LatestCommandResult}";
    public void Dispose() { Client.Dispose(); Room.Dispose(); }
}
