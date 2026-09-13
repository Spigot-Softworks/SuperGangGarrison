#nullable enable

using System.Text;
using OpenGarrison.Core;
using OpenGarrison.Protocol;
using OpenGarrison.Server.Plugins;

namespace OpenGarrison.Server;

internal sealed partial class ServerOutboundMessaging
{
    private VoteCoordinator? _voteCoordinator;
    private Func<string, int, bool>? _changeMapNow;
    private Func<string, int, bool>? _changeMapNextRound;
    private Func<IReadOnlyList<string>>? _voteMapListGetter;
    private Func<byte, string, bool>? _disconnectVoteTarget;
    private Func<byte, bool, bool>? _setVoteTargetMuted;
    private Func<bool>? _scrambleTeams;
    private bool _mapVotingEnabled = true;
    private readonly PluginVoteRegistry _pluginVoteRegistry = new();

    public void ConfigureVoting(
        Func<string, int, bool> changeMapNow,
        Func<string, int, bool> changeMapNextRound,
        Func<IReadOnlyList<string>> voteMapListGetter,
        Func<byte, string, bool>? disconnectVoteTarget = null,
        Func<byte, bool, bool>? setVoteTargetMuted = null,
        Func<bool>? scrambleTeams = null,
        bool mapVotingEnabled = true)
    {
        _changeMapNow = changeMapNow;
        _changeMapNextRound = changeMapNextRound;
        _voteMapListGetter = voteMapListGetter;
        _disconnectVoteTarget = disconnectVoteTarget;
        _setVoteTargetMuted = setVoteTargetMuted;
        _scrambleTeams = scrambleTeams;
        _mapVotingEnabled = mapVotingEnabled;
        _voteCoordinator = new VoteCoordinator(
            () => (long)world.Frame,
            BuildEligibleVoteParticipants,
            BroadcastVoteState,
            world.Config.TicksPerSecond,
            durationTicks: world.Config.TicksPerSecond * 30,
            cooldownTicks: world.Config.TicksPerSecond * 30);
    }

    public void TickVoting() => _voteCoordinator?.Tick();

    public void HandleVotingMapTransition() => _voteCoordinator?.CancelForMapTransition();

    public void SendCurrentVoteState(ClientSession client)
    {
        if (!client.IsAuthorized || _voteCoordinator?.CreateSnapshot() is not { } snapshot)
        {
            return;
        }

        TrySendMessage(client.Peer, snapshot, "vote state");
    }

    public void HandleVoteCommand(ClientSession client, VoteCommandMessage command)
    {
        if (!client.IsAuthorized || _voteCoordinator is null)
        {
            return;
        }

        switch (command.Command)
        {
            case VoteCommandKind.OpenMenu:
                SendVoteMenu(client);
                break;
            case VoteCommandKind.StartMapNow:
                TryStartMapVote(client, command.Target, command.AreaIndex, nextRound: false);
                break;
            case VoteCommandKind.StartMapNextRound:
                TryStartMapVote(client, command.Target, command.AreaIndex, nextRound: true);
                break;
            case VoteCommandKind.StartVip:
                TryStartVipVote(client, command.TargetSlot, (PlayerTeam)command.Team);
                break;
            case VoteCommandKind.StartKick:
                TryStartPlayerModerationVote(client, command.TargetSlot, mute: false);
                break;
            case VoteCommandKind.StartMute:
                TryStartPlayerModerationVote(client, command.TargetSlot, mute: true);
                break;
            case VoteCommandKind.StartScramble:
                TryStartScrambleVote(client);
                break;
            case VoteCommandKind.StartCustom:
                TryStartPublicPluginVote(
                    client,
                    command.Target,
                    command.Argument,
                    command.TargetSlot,
                    command.AreaIndex);
                break;
            case VoteCommandKind.CastYes:
                TryCastVote(client, yes: true, command.VoteId);
                break;
            case VoteCommandKind.CastNo:
                TryCastVote(client, yes: false, command.VoteId);
                break;
            case VoteCommandKind.Cancel:
                TryCancelVote(client, command.VoteId);
                break;
            case VoteCommandKind.RequestStatus:
                SendVoteStatus(client);
                break;
        }
    }

    private bool TryHandleVoteChatCommand(ClientSession client, string text)
    {
        if (_voteCoordinator is null || !TrySplitCommand(text, out var command, out var arguments))
        {
            return false;
        }

        switch (command)
        {
            case "votemenu":
                SendVoteMenu(client);
                return true;
            case "votemap":
                TryStartMapVote(client, arguments, explicitAreaIndex: null, nextRound: false);
                return true;
            case "votenextround":
                TryStartMapVote(client, arguments, explicitAreaIndex: null, nextRound: true);
                return true;
            case "votevip":
            case "vipvote":
                TryStartVipVote(client, arguments);
                return true;
            case "votekick":
            case "kickvote":
                TryStartPlayerModerationVote(client, arguments, mute: false);
                return true;
            case "votemute":
            case "votegag":
            case "mutevote":
                TryStartPlayerModerationVote(client, arguments, mute: true);
                return true;
            case "votescramble":
            case "scramblevote":
                TryStartScrambleVote(client);
                return true;
            case "votecustom":
            case "voteplugin":
                TryStartPublicPluginVote(client, arguments);
                return true;
            case "votehelp":
            case "votecommands":
                SendVoteHelp(client);
                return true;
            case "vote":
                if (IsYes(arguments))
                {
                    TryCastVote(client, yes: true);
                }
                else if (IsNo(arguments))
                {
                    TryCastVote(client, yes: false);
                }
                else
                {
                    SendSystemMessage(client.Slot, "Usage: !vote <yes|no>");
                }
                return true;
            case "yes":
                TryCastVote(client, yes: true);
                return true;
            case "no":
                TryCastVote(client, yes: false);
                return true;
            case "votes":
            case "votestatus":
            case "vipstatus":
                SendVoteStatus(client);
                return true;
            case "cancelvote":
            case "vipcancel":
                TryCancelVote(client);
                return true;
            default:
                return false;
        }
    }

    private void TryStartMapVote(
        ClientSession client,
        string arguments,
        int? explicitAreaIndex,
        bool nextRound)
    {
        if (!_mapVotingEnabled) { SendSystemMessage(client.Slot, "Map voting is unavailable in Last to Die."); return; }
        if (!TryResolveVoteMap(arguments, explicitAreaIndex, out var level, out var error))
        {
            SendSystemMessage(client.Slot, error);
            return;
        }

        var kind = nextRound ? ServerVoteKind.ChangeMapNextRound : ServerVoteKind.ChangeMapNow;
        var action = nextRound ? _changeMapNextRound : _changeMapNow;
        if (action is null)
        {
            SendSystemMessage(client.Slot, "Map voting is unavailable.");
            return;
        }

        var mapLabel = FormatMapLabel(level.Name, level.MapAreaIndex, level.MapAreaCount);
        var subject = nextRound ? $"next map: {mapLabel}" : $"change map now: {mapLabel}";
        TryStartVote(
            client,
            new VoteDefinition(
                kind,
                subject,
                GetVoteIdentity(client),
                client.Name,
                () => action(level.Name, level.MapAreaIndex)));
    }

    private void TryStartVipVote(ClientSession client, string arguments)
    {
        var tokens = arguments.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (tokens.Length == 0)
        {
            SendSystemMessage(client.Slot, world.VipRequiresDualVip
                ? "Usage: !votevip <red|blue> <player>"
                : "Usage: !votevip <player>");
            return;
        }

        var team = PlayerTeam.Red;
        var firstTargetToken = 0;
        if (TryParseVoteTeam(tokens[0], out var parsedTeam))
        {
            team = parsedTeam;
            firstTargetToken = 1;
        }

        if (firstTargetToken >= tokens.Length)
        {
            SendSystemMessage(client.Slot, "Usage: !votevip <player>");
            return;
        }

        var targetText = string.Join(' ', tokens[firstTargetToken..]);
        if (!TryResolveVoteClient(targetText, out var target, out var error))
        {
            SendSystemMessage(client.Slot, error);
            return;
        }

        if (world.VipRequiresDualVip && firstTargetToken == 0)
        {
            team = world.GetNetworkPlayerConfiguredTeam(target.Slot);
        }

        TryStartVipVote(client, target.Slot, team);
    }

    private void TryStartVipVote(ClientSession client, byte targetSlot, PlayerTeam team)
    {
        if (!world.IsVipModeActive)
        {
            SendSystemMessage(client.Slot, "VIP votes are only available on vip_ maps.");
            return;
        }

        if (!clientsBySlot.TryGetValue(targetSlot, out var target) || !IsEligibleVoteParticipant(target))
        {
            SendSystemMessage(client.Slot, "Selected VIP must be an active player.");
            return;
        }

        if (!world.VipRequiresDualVip)
        {
            team = PlayerTeam.Red;
        }
        else if (team is not PlayerTeam.Red and not PlayerTeam.Blue)
        {
            team = world.GetNetworkPlayerConfiguredTeam(target.Slot);
        }

        if (world.VipRequiresDualVip
            && world.GetNetworkPlayerConfiguredTeam(target.Slot) != team)
        {
            SendSystemMessage(client.Slot, $"{target.Name} is not on the {team} team.");
            return;
        }

        var capturedTeam = team;
        var targetIdentity = GetVoteIdentity(target);
        var targetName = target.Name;
        TryStartVote(
            client,
            new VoteDefinition(
                ServerVoteKind.SelectVip,
                $"{targetName} as {capturedTeam} VIP",
                GetVoteIdentity(client),
                client.Name,
                () => ApplyVipVote(capturedTeam, targetIdentity)));
    }

    private bool ApplyVipVote(PlayerTeam team, string targetIdentity)
    {
        if (!TryGetClientByVoteIdentity(targetIdentity, out var target)
            || !IsEligibleVoteParticipant(target)
            || (world.VipRequiresDualVip && world.GetNetworkPlayerConfiguredTeam(target.Slot) != team))
        {
            return false;
        }

        return world.TrySetPreferredVipSlot(team, target.Slot);
    }

    private void TryStartPlayerModerationVote(ClientSession client, string arguments, bool mute)
    {
        if (!TryResolveVoteClient(arguments, out var target, out var error))
        {
            SendSystemMessage(client.Slot, string.IsNullOrWhiteSpace(arguments)
                ? $"Usage: !vote{(mute ? "mute" : "kick")} <player>"
                : error);
            return;
        }

        TryStartPlayerModerationVote(client, target.Slot, mute);
    }

    private void TryStartPlayerModerationVote(ClientSession client, byte targetSlot, bool mute)
    {
        if ((!mute && _disconnectVoteTarget is null) || (mute && _setVoteTargetMuted is null))
        {
            SendSystemMessage(client.Slot, $"Player {(mute ? "mute" : "kick")} voting is unavailable.");
            return;
        }

        if (!clientsBySlot.TryGetValue(targetSlot, out var target) || !IsEligibleVoteParticipant(target))
        {
            SendSystemMessage(client.Slot, "Selected target must be an active player.");
            return;
        }

        if (target.Slot == client.Slot)
        {
            SendSystemMessage(client.Slot, $"You cannot call a {(mute ? "mute" : "kick")} vote against yourself.");
            return;
        }

        if (mute && target.IsGagged)
        {
            SendSystemMessage(client.Slot, $"{target.Name} is already muted by the server.");
            return;
        }

        var targetIdentity = GetVoteIdentity(target);
        var targetName = target.Name;
        TryStartVote(
            client,
            new VoteDefinition(
                mute ? ServerVoteKind.MutePlayer : ServerVoteKind.KickPlayer,
                $"{(mute ? "mute" : "kick")} {targetName}",
                GetVoteIdentity(client),
                client.Name,
                () => ApplyPlayerModerationVote(targetIdentity, targetName, mute)));
    }

    private bool ApplyPlayerModerationVote(string targetIdentity, string targetName, bool mute)
    {
        if (!TryGetClientByVoteIdentity(targetIdentity, out var target)
            || !IsEligibleVoteParticipant(target))
        {
            return false;
        }

        if (mute)
        {
            if (_setVoteTargetMuted is null || target.IsGagged || !_setVoteTargetMuted(target.Slot, true))
            {
                return false;
            }

            SendSystemMessage(target.Slot, "You were muted by player vote for this connection.");
            return true;
        }

        return _disconnectVoteTarget?.Invoke(target.Slot, $"Kicked by player vote ({targetName}).") == true;
    }

    private void TryStartScrambleVote(ClientSession client)
    {
        if (_scrambleTeams is null)
        {
            SendSystemMessage(client.Slot, "Team scramble voting is unavailable.");
            return;
        }

        if (BuildEligibleVoteParticipants().Length < 2)
        {
            SendSystemMessage(client.Slot, "At least two active players are required to scramble teams.");
            return;
        }

        TryStartVote(
            client,
            new VoteDefinition(
                ServerVoteKind.ScrambleTeams,
                "scramble the teams",
                GetVoteIdentity(client),
                client.Name,
                _scrambleTeams));
    }

    public bool TryRegisterPluginVoteKind(
        string pluginId,
        OpenGarrisonServerVoteRegistration registration,
        out string errorMessage)
    {
        if (!_pluginVoteRegistry.TryRegister(pluginId, registration, out errorMessage))
        {
            return false;
        }

        log($"[vote] plugin {pluginId} registered vote kind {registration.Id}.");
        return true;
    }

    public void UnregisterPluginVoteKinds(string pluginId)
    {
        _voteCoordinator?.CancelOwnedVote(pluginId);
        if (_pluginVoteRegistry.RemoveOwner(pluginId))
        {
            log($"[vote] removed vote kinds owned by plugin {pluginId}.");
        }
    }

    public bool TryStartPluginVote(
        string pluginId,
        string voteKindId,
        byte initiatorSlot,
        string argument,
        out string errorMessage)
    {
        if (!clientsBySlot.TryGetValue(initiatorSlot, out var initiator)
            || !IsEligibleVoteParticipant(initiator))
        {
            errorMessage = "The vote initiator must be an active player.";
            return false;
        }

        if (!_pluginVoteRegistry.TryGetOwned(pluginId, voteKindId, out var registration))
        {
            errorMessage = $"Plugin vote kind \"{voteKindId}\" is not registered by {pluginId}.";
            return false;
        }

        return TryStartRegisteredPluginVote(
            initiator,
            registration,
            argument,
            targetSlot: 0,
            explicitAreaIndex: null,
            sendErrorToClient: false,
            out errorMessage);
    }

    private void TryStartPublicPluginVote(ClientSession client, string arguments)
    {
        var separator = arguments.IndexOf(' ');
        var voteKindId = (separator < 0 ? arguments : arguments[..separator]).Trim();
        var voteArguments = separator < 0 ? string.Empty : arguments[(separator + 1)..].Trim();
        if (voteKindId.Length == 0)
        {
            SendSystemMessage(client.Slot, "Usage: !votecustom <owner:id> [target]");
            return;
        }

        TryStartPublicPluginVote(client, voteKindId, voteArguments, targetSlot: 0, explicitAreaIndex: null);
    }

    private void TryStartPublicPluginVote(
        ClientSession client,
        string voteKindId,
        string arguments,
        byte targetSlot,
        int? explicitAreaIndex)
    {
        if (!_pluginVoteRegistry.TryResolvePublic(voteKindId, out var registration, out var error))
        {
            SendSystemMessage(client.Slot, error);
            return;
        }

        TryStartRegisteredPluginVote(
            client,
            registration,
            arguments,
            targetSlot,
            explicitAreaIndex,
            sendErrorToClient: true,
            out _);
    }

    private bool TryStartRegisteredPluginVote(
        ClientSession client,
        RegisteredPluginVoteKind registered,
        string arguments,
        byte targetSlot,
        int? explicitAreaIndex,
        bool sendErrorToClient,
        out string errorMessage)
    {
        errorMessage = string.Empty;
        var initiatorIdentity = GetVoteIdentity(client);
        if (_voteCoordinator is null || !_voteCoordinator.CanStart(initiatorIdentity, out errorMessage))
        {
            if (sendErrorToClient && !string.IsNullOrWhiteSpace(errorMessage))
            {
                SendSystemMessage(client.Slot, errorMessage);
            }
            return false;
        }

        ClientSession? target = null;
        SimpleLevel? level = null;
        switch (registered.Registration.TargetKind)
        {
            case OpenGarrisonServerVoteTargetKind.Player:
                if (targetSlot != 0)
                {
                    if (!clientsBySlot.TryGetValue(targetSlot, out target) || !IsEligibleVoteParticipant(target))
                    {
                        errorMessage = "Selected target must be an active player.";
                    }
                }
                else
                {
                    if (TryResolveVoteClient(arguments, out target!, out errorMessage))
                    {
                        errorMessage = string.Empty;
                    }
                }
                break;
            case OpenGarrisonServerVoteTargetKind.Map:
                if (TryResolveVoteMap(arguments, explicitAreaIndex, out level!, out errorMessage))
                {
                    errorMessage = string.Empty;
                }
                break;
        }

        if (!string.IsNullOrWhiteSpace(errorMessage))
        {
            if (sendErrorToClient)
            {
                SendSystemMessage(client.Slot, errorMessage);
            }
            return false;
        }

        var normalizedArgument = NormalizePluginVoteText(arguments, ProtocolCodec.MaxChatBytes);
        var request = new OpenGarrisonServerVoteRequest(
            registered.GlobalId,
            client.Slot,
            client.Name,
            registered.Registration.TargetKind == OpenGarrisonServerVoteTargetKind.None ? normalizedArgument : string.Empty,
            target?.Slot,
            target?.UserId,
            target is null ? string.Empty : GetVoteIdentity(target),
            target?.Name ?? string.Empty,
            target is null ? null : world.GetNetworkPlayerConfiguredTeam(target.Slot),
            level?.Name ?? string.Empty,
            level?.MapAreaIndex ?? 0);

        OpenGarrisonServerVoteValidationResult validation;
        try
        {
            validation = registered.Registration.Validate?.Invoke(request)
                ?? OpenGarrisonServerVoteValidationResult.Accept(BuildDefaultPluginVoteSubject(registered, request, level));
        }
        catch (Exception ex)
        {
            log($"[vote] plugin {registered.OwnerPluginId} validation for {registered.LocalId} failed: {ex.Message}");
            validation = OpenGarrisonServerVoteValidationResult.Reject("The plugin rejected this vote request.");
        }

        if (!validation.Accepted || string.IsNullOrWhiteSpace(validation.Subject))
        {
            var pluginError = NormalizePluginVoteText(validation.ErrorMessage, 160);
            errorMessage = string.IsNullOrWhiteSpace(pluginError)
                ? "The plugin rejected this vote request."
                : pluginError;
            if (sendErrorToClient)
            {
                SendSystemMessage(client.Slot, errorMessage);
            }
            return false;
        }

        var subject = NormalizePluginVoteText(validation.Subject, 160);
        if (subject.Length == 0)
        {
            errorMessage = "The plugin returned an invalid vote subject.";
            if (sendErrorToClient)
            {
                SendSystemMessage(client.Slot, errorMessage);
            }
            return false;
        }

        var definition = new VoteDefinition(
            ServerVoteKind.PluginDefined,
            subject,
            initiatorIdentity,
            client.Name,
            () => ApplyRegisteredPluginVote(registered, request),
            registered.OwnerPluginId);
        if (_voteCoordinator is null || !_voteCoordinator.TryStart(definition, out errorMessage))
        {
            if (sendErrorToClient && !string.IsNullOrWhiteSpace(errorMessage))
            {
                SendSystemMessage(client.Slot, errorMessage);
            }
            return false;
        }

        return true;
    }

    private bool ApplyRegisteredPluginVote(
        RegisteredPluginVoteKind registered,
        OpenGarrisonServerVoteRequest request)
    {
        if (!_pluginVoteRegistry.IsCurrent(registered))
        {
            return false;
        }

        if (request.TargetSlot.HasValue
            && (!TryGetClientByVoteIdentity(request.TargetIdentity, out var target)
                || !IsEligibleVoteParticipant(target)
                || target.Slot != request.TargetSlot.Value))
        {
            return false;
        }

        try
        {
            return registered.Registration.Apply(request);
        }
        catch (Exception ex)
        {
            log($"[vote] plugin {registered.OwnerPluginId} action for {registered.LocalId} failed: {ex.Message}");
            return false;
        }
    }

    private static string BuildDefaultPluginVoteSubject(
        RegisteredPluginVoteKind registered,
        OpenGarrisonServerVoteRequest request,
        SimpleLevel? level)
    {
        return registered.Registration.TargetKind switch
        {
            OpenGarrisonServerVoteTargetKind.Player => $"{registered.Registration.DisplayName}: {request.TargetName}",
            OpenGarrisonServerVoteTargetKind.Map when level is not null =>
                $"{registered.Registration.DisplayName}: {FormatMapLabel(level.Name, level.MapAreaIndex, level.MapAreaCount)}",
            _ when !string.IsNullOrWhiteSpace(request.Argument) =>
                $"{registered.Registration.DisplayName}: {request.Argument}",
            _ => registered.Registration.DisplayName,
        };
    }

    private void TryStartVote(ClientSession client, VoteDefinition definition)
    {
        if (_voteCoordinator is null)
        {
            return;
        }

        if (!_voteCoordinator.TryStart(definition, out var error))
        {
            SendSystemMessage(client.Slot, error);
        }
    }

    private void TryCastVote(ClientSession client, bool yes, ulong expectedVoteId = 0)
    {
        if (_voteCoordinator is null)
        {
            return;
        }

        if (!_voteCoordinator.TryCast(GetVoteIdentity(client), client.Name, yes, out var error, expectedVoteId))
        {
            SendSystemMessage(client.Slot, error);
        }
    }

    private void TryCancelVote(ClientSession client, ulong expectedVoteId = 0)
    {
        if (_voteCoordinator is null)
        {
            return;
        }

        if (!_voteCoordinator.TryCancel(GetVoteIdentity(client), client.Name, force: false, out var error, expectedVoteId))
        {
            SendSystemMessage(client.Slot, error);
        }
    }

    private void SendVoteStatus(ClientSession client)
    {
        var snapshot = _voteCoordinator?.CreateSnapshot();
        if (snapshot is null)
        {
            var cooldown = _voteCoordinator?.CooldownTicksRemaining ?? 0;
            SendSystemMessage(client.Slot, cooldown > 0
                ? $"There is no active vote. Cooldown: {(int)Math.Ceiling(cooldown / (double)world.Config.TicksPerSecond)}s."
                : "There is no active vote.");
            return;
        }

        TrySendMessage(client.Peer, snapshot, "vote state");
        SendSystemMessage(client.Slot, snapshot.Message);
    }

    private void SendVoteHelp(ClientSession client)
    {
        SendSystemMessage(
            client.Slot,
            "Call votes: !votemap, !votenextround, !votevip, !votekick, !votemute, !votescramble.");
        SendSystemMessage(
            client.Slot,
            "Vote controls: !vote yes, !vote no, !votes, !cancelvote, or !votemenu.");
        if (_pluginVoteRegistry.GetCatalog().Count > 0)
        {
            SendSystemMessage(
                client.Slot,
                "Plugin votes are listed in !votemenu and start with !votecustom <owner:id> [target].");
        }
    }

    private void SendVoteMenu(ClientSession client)
    {
        if (!IsEligibleVoteParticipant(client))
        {
            SendSystemMessage(client.Slot, "Join a team and select a class before calling or voting in a vote.");
            return;
        }

        var maps = new List<VoteMenuMapEntry>();
        var seenMaps = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var configuredName in _mapVotingEnabled ? _voteMapListGetter?.Invoke() ?? Array.Empty<string>() : Array.Empty<string>())
        {
            var level = SimpleLevelFactory.CreateImportedLevel(configuredName, 1);
            if (level is null || !seenMaps.Add(level.Name))
            {
                continue;
            }

            var displayName = OpenGarrisonStockMapCatalog.TryGetDefinition(level.Name, out var definition)
                ? definition.DisplayName : level.Name;
            maps.Add(new VoteMenuMapEntry(level.Name, displayName, level.MapAreaCount));
        }

        var players = clientsBySlot.Values
            .Where(IsEligibleVoteParticipant)
            .OrderBy(static candidate => candidate.Slot)
            .Select(candidate => new VoteMenuPlayerEntry(
                candidate.Slot,
                candidate.Name,
                (byte)world.GetNetworkPlayerConfiguredTeam(candidate.Slot),
                candidate.IsGagged))
            .ToArray();
        var customVotes = _pluginVoteRegistry.GetCatalog()
            .Select(static entry => new VoteMenuCustomEntry(
                entry.GlobalId,
                entry.Registration.DisplayName,
                entry.Registration.Description,
                (byte)entry.Registration.TargetKind))
            .ToArray();
        TrySendMessage(
            client.Peer,
            new VoteMenuMessage(
                maps,
                players,
                world.IsVipModeActive,
                _voteCoordinator?.HasActiveVote == true,
                _voteCoordinator?.CooldownTicksRemaining ?? 0,
                _voteCoordinator?.ActiveVoteId ?? 0,
                KickVoteAvailable: _disconnectVoteTarget is not null && players.Length > 1,
                MuteVoteAvailable: _setVoteTargetMuted is not null
                    && players.Any(player => player.Slot != client.Slot && !player.IsMuted),
                ScrambleVoteAvailable: _scrambleTeams is not null && players.Length > 1,
                RegisteredVotes: customVotes),
            "vote menu");

        SendCurrentVoteState(client);
    }

    private void BroadcastVoteState(VoteStateMessage state)
    {
        foreach (var client in clientsBySlot.Values)
        {
            if (client.IsAuthorized)
            {
                TrySendMessage(client.Peer, state, "vote state");
            }
        }

        recordBroadcastMessage?.Invoke(state);
        if (!string.IsNullOrWhiteSpace(state.Message))
        {
            BroadcastSystemMessage(state.Message);
        }
    }

    private VoteParticipant[] BuildEligibleVoteParticipants()
    {
        return clientsBySlot.Values
            .Where(IsEligibleVoteParticipant)
            .Select(client => new VoteParticipant(GetVoteIdentity(client), client.Slot, client.Name))
            .ToArray();
    }

    private bool IsEligibleVoteParticipant(ClientSession client)
    {
        return client.IsAuthorized
            && !ServerHelpers.IsSpectatorSlot(client.Slot)
            && world.TryGetNetworkPlayer(client.Slot, out _)
            && !world.IsNetworkPlayerAwaitingJoin(client.Slot);
    }

    private static string GetVoteIdentity(ClientSession client)
    {
        return client.ClientInstanceId != Guid.Empty
            ? $"client:{client.ClientInstanceId:D}"
            : $"session:{client.UserId}";
    }

    private bool TryGetClientByVoteIdentity(string identity, out ClientSession client)
    {
        client = clientsBySlot.Values.FirstOrDefault(candidate =>
            string.Equals(GetVoteIdentity(candidate), identity, StringComparison.Ordinal))!;
        return client is not null;
    }

    private static bool TryResolveVoteMap(
        string arguments,
        int? explicitAreaIndex,
        out SimpleLevel level,
        out string error)
    {
        level = null!;
        error = "Usage: !votemap <mapName> [area]";
        var tokens = arguments.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (tokens.Length == 0)
        {
            return false;
        }

        var areaIndex = explicitAreaIndex.GetValueOrDefault(1);
        var mapTokenCount = tokens.Length;
        if (!explicitAreaIndex.HasValue && tokens.Length > 1 && int.TryParse(tokens[^1], out var parsedArea))
        {
            areaIndex = parsedArea;
            mapTokenCount -= 1;
        }

        if (areaIndex <= 0 || mapTokenCount <= 0)
        {
            return false;
        }

        var mapName = string.Join(' ', tokens[..mapTokenCount]);
        level = SimpleLevelFactory.CreateImportedLevel(mapName, areaIndex)!;
        if (level is null || level.MapAreaIndex != areaIndex)
        {
            error = $"Unknown map or area \"{arguments.Trim()}\".";
            return false;
        }

        return true;
    }

    private bool TryResolveVoteClient(string text, out ClientSession client, out string error)
    {
        client = null!;
        error = "Player not found.";
        var trimmed = text.Trim();
        if (byte.TryParse(trimmed, out var slot)
            && clientsBySlot.TryGetValue(slot, out client!)
            && IsEligibleVoteParticipant(client))
        {
            return true;
        }

        var matches = clientsBySlot.Values
            .Where(IsEligibleVoteParticipant)
            .Where(candidate => candidate.Name.Contains(trimmed, StringComparison.OrdinalIgnoreCase))
            .Take(2)
            .ToArray();
        if (matches.Length == 1)
        {
            client = matches[0];
            return true;
        }

        error = matches.Length == 0 ? "Player not found." : "Player name is ambiguous; use the slot number.";
        return false;
    }

    private static bool TrySplitCommand(string text, out string command, out string arguments)
    {
        command = string.Empty;
        arguments = string.Empty;
        if (string.IsNullOrWhiteSpace(text) || text[0] != '!')
        {
            return false;
        }

        var separator = text.IndexOf(' ');
        command = (separator < 0 ? text[1..] : text[1..separator]).Trim().ToLowerInvariant();
        arguments = separator < 0 ? string.Empty : text[(separator + 1)..].Trim();
        return command.Length > 0;
    }

    private static bool IsYes(string value)
        => value.Equals("yes", StringComparison.OrdinalIgnoreCase)
            || value.Equals("y", StringComparison.OrdinalIgnoreCase);

    private static bool IsNo(string value)
        => value.Equals("no", StringComparison.OrdinalIgnoreCase)
            || value.Equals("n", StringComparison.OrdinalIgnoreCase);

    private static bool TryParseVoteTeam(string token, out PlayerTeam team)
    {
        if (token.Equals("red", StringComparison.OrdinalIgnoreCase) || token.Equals("r", StringComparison.OrdinalIgnoreCase))
        {
            team = PlayerTeam.Red;
            return true;
        }

        if (token.Equals("blue", StringComparison.OrdinalIgnoreCase)
            || token.Equals("blu", StringComparison.OrdinalIgnoreCase)
            || token.Equals("b", StringComparison.OrdinalIgnoreCase))
        {
            team = PlayerTeam.Blue;
            return true;
        }

        team = default;
        return false;
    }

    private static string FormatMapLabel(string name, int areaIndex, int areaCount)
        => areaCount > 1 ? $"{name} (area {areaIndex}/{areaCount})" : name;

    private static string NormalizePluginVoteText(string? value, int maxUtf8Bytes)
    {
        var normalized = new string((value ?? string.Empty)
            .Trim()
            .Select(static ch => char.IsControl(ch) ? ' ' : ch)
            .ToArray());
        try
        {
            return ProtocolCodec.TruncateUtf8(normalized, maxUtf8Bytes).Trim();
        }
        catch (EncoderFallbackException)
        {
            return string.Empty;
        }
    }
}
