#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using OpenGarrison.ClientShared;

namespace OpenGarrison.Client;

public partial class Game1
{
    private const double SocialPresenceHeartbeatIntervalSeconds = 30d;
    private const double FriendsPresenceRefreshIntervalSeconds = 20d;
    private const double FriendRequestsRefreshIntervalSeconds = 10d;
    private const double DirectMessagesPollIntervalSeconds = 5d;

    private NetworkEndpoint? _socialPresenceNetworkEndpoint;
    private Task? _socialPresenceHeartbeatTask;
    private Task? _socialPresenceOfflineTask;
    private Task<IReadOnlyList<FriendPresenceEntry>>? _friendsPresenceRequestTask;
    private Task<IReadOnlyList<FriendRequestEntry>>? _friendRequestsRefreshTask;
    private Task<FriendRequestEntry>? _friendRequestSendTask;
    private Task<FriendRequestEntry>? _friendRequestRespondTask;
    private Task<IReadOnlyList<FriendDirectMessageEntry>>? _directMessagesPollTask;
    private Task<FriendDirectMessageEntry>? _directMessageSendTask;
    private readonly Dictionary<string, FriendPresenceEntry> _friendPresenceByCode = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<FriendRequestEntry> _friendRequestEntries = [];
    private readonly List<FriendDirectMessageEntry> _friendMessageEntries = [];
    private double _socialPresenceSecondsUntilHeartbeat;
    private double _friendsPresenceSecondsUntilRefresh;
    private double _friendRequestsSecondsUntilRefresh;
    private double _directMessagesSecondsUntilPoll;
    private long _lastDirectMessageId;
    private bool _directMessagesInitialPollCompleted;
    private string _lastSocialPresenceSignature = string.Empty;
    private string _lastDirectMessageSenderFriendCode = string.Empty;

    private void SetSocialPresenceNetworkEndpoint(NetworkEndpoint endpoint)
    {
        _socialPresenceNetworkEndpoint = endpoint;
    }

    private void ClearSocialPresenceNetworkEndpoint()
    {
        _socialPresenceNetworkEndpoint = null;
    }

    private void PumpSocialPresence(double elapsedSeconds)
    {
        CompleteSocialPresenceTasks();

        _socialPresenceSecondsUntilHeartbeat = Math.Max(0d, _socialPresenceSecondsUntilHeartbeat - Math.Max(0d, elapsedSeconds));
        var heartbeat = BuildSocialPresenceHeartbeatRequest();
        var signature = BuildSocialPresenceSignature(heartbeat);
        if (_socialPresenceHeartbeatTask is null
            && (_socialPresenceSecondsUntilHeartbeat <= 0d || !string.Equals(signature, _lastSocialPresenceSignature, StringComparison.Ordinal)))
        {
            _lastSocialPresenceSignature = signature;
            _socialPresenceSecondsUntilHeartbeat = SocialPresenceHeartbeatIntervalSeconds;
            _socialPresenceHeartbeatTask = _presenceClient.SendHeartbeatAsync(heartbeat);
        }

        if (!_friendsMenuOpen)
        {
            PumpDirectMessages(elapsedSeconds);
            return;
        }

        _friendsPresenceSecondsUntilRefresh = Math.Max(0d, _friendsPresenceSecondsUntilRefresh - Math.Max(0d, elapsedSeconds));
        if (_friendsPresenceRequestTask is null && _friendsPresenceSecondsUntilRefresh <= 0d)
        {
            RefreshFriendPresence();
        }

        _friendRequestsSecondsUntilRefresh = Math.Max(0d, _friendRequestsSecondsUntilRefresh - Math.Max(0d, elapsedSeconds));
        if (_friendRequestsRefreshTask is null && _friendRequestsSecondsUntilRefresh <= 0d)
        {
            RefreshFriendRequests();
        }

        PumpDirectMessages(elapsedSeconds);
    }

    private void PumpDirectMessages(double elapsedSeconds)
    {
        _directMessagesSecondsUntilPoll = Math.Max(0d, _directMessagesSecondsUntilPoll - Math.Max(0d, elapsedSeconds));
        if (_directMessagesPollTask is null && _directMessagesSecondsUntilPoll <= 0d)
        {
            PollDirectMessages();
        }
    }

    private void CompleteSocialPresenceTasks()
    {
        if (_socialPresenceHeartbeatTask is not null && _socialPresenceHeartbeatTask.IsCompleted)
        {
            if (!_socialPresenceHeartbeatTask.IsCompletedSuccessfully)
            {
                AddConsoleLine($"presence heartbeat failed: {_socialPresenceHeartbeatTask.Exception?.GetBaseException().Message ?? "unknown error"}");
            }

            _socialPresenceHeartbeatTask = null;
        }

        if (_socialPresenceOfflineTask is not null && _socialPresenceOfflineTask.IsCompleted)
        {
            _socialPresenceOfflineTask = null;
        }

        if (_friendsPresenceRequestTask is null || !_friendsPresenceRequestTask.IsCompleted)
        {
            CompleteFriendRequestTasks();
            CompleteDirectMessageTasks();
            return;
        }

        if (_friendsPresenceRequestTask.IsCompletedSuccessfully)
        {
            _friendPresenceByCode.Clear();
            foreach (var entry in _friendsPresenceRequestTask.Result)
            {
                if (ClientIdentityDocument.TryNormalizeFriendCode(entry.FriendCode, out var friendCode))
                {
                    _friendPresenceByCode[friendCode] = entry;
                    UpdateStoredFriendDisplayName(friendCode, entry.DisplayName);
                }
            }

            _friendList.Save();
            _menuStatusMessage = _friendsMenuOpen ? string.Empty : _menuStatusMessage;
        }
        else if (_friendsMenuOpen)
        {
            _menuStatusMessage = $"Friends unavailable: {_friendsPresenceRequestTask.Exception?.GetBaseException().Message ?? "request failed"}";
        }

        _friendsPresenceRequestTask = null;
        CompleteFriendRequestTasks();
        CompleteDirectMessageTasks();
    }

    private void CompleteFriendRequestTasks()
    {
        if (_friendRequestsRefreshTask is not null && _friendRequestsRefreshTask.IsCompleted)
        {
            if (_friendRequestsRefreshTask.IsCompletedSuccessfully)
            {
                _friendRequestEntries.Clear();
                _friendRequestEntries.AddRange(_friendRequestsRefreshTask.Result);
                ApplyAcceptedOutgoingFriendRequests();
            }
            else if (_friendsMenuOpen)
            {
                _menuStatusMessage = $"Requests unavailable: {_friendRequestsRefreshTask.Exception?.GetBaseException().Message ?? "request failed"}";
            }

            _friendRequestsRefreshTask = null;
        }

        if (_friendRequestSendTask is not null && _friendRequestSendTask.IsCompleted)
        {
            if (_friendRequestSendTask.IsCompletedSuccessfully)
            {
                UpsertFriendRequestEntry(_friendRequestSendTask.Result);
                _menuStatusMessage = _friendRequestSendTask.Result.Status == "accepted"
                    ? "Friend added."
                    : "Friend request sent.";
                ApplyAcceptedFriendRequest(_friendRequestSendTask.Result);
                RefreshFriendRequests();
            }
            else
            {
                _menuStatusMessage = $"Request failed: {_friendRequestSendTask.Exception?.GetBaseException().Message ?? "request failed"}";
            }

            _friendRequestSendTask = null;
        }

        if (_friendRequestRespondTask is not null && _friendRequestRespondTask.IsCompleted)
        {
            if (_friendRequestRespondTask.IsCompletedSuccessfully)
            {
                var entry = _friendRequestRespondTask.Result;
                UpsertFriendRequestEntry(entry);
                ApplyAcceptedFriendRequest(entry);
                _menuStatusMessage = entry.Status == "accepted" ? "Friend request accepted." : "Friend request denied.";
                RefreshFriendRequests();
            }
            else
            {
                _menuStatusMessage = $"Response failed: {_friendRequestRespondTask.Exception?.GetBaseException().Message ?? "request failed"}";
            }

            _friendRequestRespondTask = null;
        }
    }

    private void CompleteDirectMessageTasks()
    {
        if (_directMessagesPollTask is not null && _directMessagesPollTask.IsCompleted)
        {
            if (_directMessagesPollTask.IsCompletedSuccessfully)
            {
                var playNotification = _directMessagesInitialPollCompleted;
                var addedIncomingMessage = false;
                foreach (var message in _directMessagesPollTask.Result)
                {
                    addedIncomingMessage |= AddDirectMessageEntry(message, appendChatLine: true);
                }

                if (playNotification && addedIncomingMessage)
                {
                    PlayDirectMessageNotificationSound();
                }

                _directMessagesInitialPollCompleted = true;
            }
            else if (_friendsMenuOpen && _friendsMenuTab == FriendsMenuTab.Messages)
            {
                _menuStatusMessage = $"Messages unavailable: {_directMessagesPollTask.Exception?.GetBaseException().Message ?? "request failed"}";
            }

            _directMessagesPollTask = null;
        }

        if (_directMessageSendTask is not null && _directMessageSendTask.IsCompleted)
        {
            if (_directMessageSendTask.IsCompletedSuccessfully)
            {
                AddDirectMessageEntry(_directMessageSendTask.Result, appendChatLine: true);
                _menuStatusMessage = "Message sent.";
            }
            else
            {
                var message = _directMessageSendTask.Exception?.GetBaseException().Message ?? "request failed";
                _menuStatusMessage = $"Message failed: {message}";
                AppendDirectMessageChatLine("DM", $"Message failed: {message}", incoming: true);
            }

            _directMessageSendTask = null;
        }
    }

    private void RefreshFriendPresence()
    {
        if (_friendsPresenceRequestTask is not null)
        {
            return;
        }

        _friendsPresenceSecondsUntilRefresh = FriendsPresenceRefreshIntervalSeconds;
        _friendsPresenceRequestTask = _presenceClient.GetFriendPresenceAsync(_friendList.Friends.Select(friend => friend.FriendCode));
    }

    private void RefreshFriendRequests()
    {
        if (_friendRequestsRefreshTask is not null)
        {
            return;
        }

        _friendRequestsSecondsUntilRefresh = FriendRequestsRefreshIntervalSeconds;
        _friendRequestsRefreshTask = _presenceClient.GetFriendRequestsAsync(_clientIdentity);
    }

    private void PollDirectMessages()
    {
        if (_directMessagesPollTask is not null)
        {
            return;
        }

        _directMessagesSecondsUntilPoll = DirectMessagesPollIntervalSeconds;
        _directMessagesPollTask = _presenceClient.PollDirectMessagesAsync(_clientIdentity, _lastDirectMessageId);
    }

    private void UpsertFriendRequestEntry(FriendRequestEntry entry)
    {
        var existingIndex = _friendRequestEntries.FindIndex(candidate => candidate.RequestId == entry.RequestId);
        if (existingIndex >= 0)
        {
            _friendRequestEntries[existingIndex] = entry;
        }
        else
        {
            _friendRequestEntries.Insert(0, entry);
        }
    }

    private void ApplyAcceptedOutgoingFriendRequests()
    {
        var changed = false;
        foreach (var entry in _friendRequestEntries)
        {
            changed |= ApplyAcceptedFriendRequest(entry);
        }

        if (changed)
        {
            _friendList.Save();
            RefreshFriendPresence();
        }
    }

    private bool ApplyAcceptedFriendRequest(FriendRequestEntry entry)
    {
        if (!string.Equals(entry.Status, "accepted", StringComparison.OrdinalIgnoreCase)
            || string.IsNullOrWhiteSpace(entry.FriendCode)
            || !_friendList.TryAdd(entry.FriendCode, entry.DisplayName))
        {
            return false;
        }

        _friendList.Save();
        RefreshFriendPresence();
        return true;
    }

    private bool AddDirectMessageEntry(FriendDirectMessageEntry message, bool appendChatLine)
    {
        var incoming = string.Equals(message.Direction, "incoming", StringComparison.OrdinalIgnoreCase);
        if (incoming && message.MessageId > _lastDirectMessageId)
        {
            _lastDirectMessageId = message.MessageId;
        }

        if (_friendMessageEntries.Any(entry => entry.MessageId == message.MessageId && message.MessageId > 0))
        {
            return false;
        }

        _friendMessageEntries.Add(message);
        while (_friendMessageEntries.Count > 80)
        {
            _friendMessageEntries.RemoveAt(0);
        }

        if (!incoming)
        {
            return false;
        }

        _lastDirectMessageSenderFriendCode = message.FriendCode;
        UpdateStoredFriendDisplayName(message.FriendCode, message.DisplayName);
        if (appendChatLine)
        {
            AppendDirectMessageChatLine(GetFriendDisplayName(message.FriendCode, message.DisplayName), message.Text, incoming: true);
        }

        return true;
    }

    private string GetFriendDisplayName(string friendCode, string fallbackName = "")
    {
        if (!string.IsNullOrWhiteSpace(fallbackName))
        {
            return fallbackName.Trim();
        }

        var friend = _friendList.Friends.FirstOrDefault(entry => string.Equals(entry.FriendCode, friendCode, StringComparison.OrdinalIgnoreCase));
        return friend?.DisplayLabel ?? friendCode;
    }

    private bool TrySendDirectMessage(string targetFriendCode, string text, bool echoToChat)
    {
        if (_directMessageSendTask is not null)
        {
            _menuStatusMessage = "Message send already in progress.";
            return false;
        }

        if (!ClientIdentityDocument.TryNormalizeFriendCode(targetFriendCode, out var normalizedTarget)
            || string.IsNullOrWhiteSpace(text))
        {
            _menuStatusMessage = "Choose a friend and enter a message.";
            return false;
        }

        _directMessageSendTask = _presenceClient.SendDirectMessageAsync(_clientIdentity, normalizedTarget, text.Trim());
        if (echoToChat)
        {
            AppendDirectMessageChatLine($"To {GetFriendDisplayName(normalizedTarget)}", text.Trim(), incoming: false);
        }

        return true;
    }

    private void SendSocialPresenceOffline()
    {
        try
        {
            _socialPresenceOfflineTask = _presenceClient.SendOfflineAsync(_clientIdentity);
            if (!OperatingSystem.IsBrowser())
            {
                _socialPresenceOfflineTask.GetAwaiter().GetResult();
            }
        }
        catch
        {
        }
    }

    private PresenceHeartbeatRequest BuildSocialPresenceHeartbeatRequest()
    {
        var request = new PresenceHeartbeatRequest
        {
            ClientId = _clientIdentity.ClientId,
            ClientSecret = _clientIdentity.ClientSecret,
            FriendCode = _clientIdentity.FriendCode,
            DisplayName = GetSocialPresenceDisplayName(),
            Status = "menu",
            PlayerCardJson = PlayerCardProfile.Serialize(_clientIdentity.PlayerCard),
        };

        if (_networkClient.IsConnected && _gameplaySessionKind == GameplaySessionKind.Online)
        {
            request.Status = "server";
            request.Mode = _world.MatchRules.Mode.ToString();
            request.Map = _world.Level.Name;
            request.ServerName = _networkClient.ServerDescription ?? string.Empty;
            ApplySocialPresenceEndpoint(request);
            return request;
        }

        switch (_gameplaySessionKind)
        {
            case GameplaySessionKind.Jump:
                request.Status = "jump";
                request.Mode = "Jump";
                request.Map = _world.Level.Name;
                break;
            case GameplaySessionKind.LastToDie:
                request.Status = "last_to_die";
                request.Mode = "Last to Die";
                request.Map = _world.Level.Name;
                break;
            case GameplaySessionKind.Practice:
                request.Status = "practice";
                request.Mode = "Practice";
                request.Map = _world.Level.Name;
                break;
        }

        return request;
    }

    private string GetSocialPresenceDisplayName()
    {
        return !string.IsNullOrWhiteSpace(_clientIdentity.DisplayName)
            ? _clientIdentity.DisplayName.Trim()
            : _world.LocalPlayer.DisplayName;
    }

    private void ApplySocialPresenceEndpoint(PresenceHeartbeatRequest request)
    {
        if (!_socialPresenceNetworkEndpoint.HasValue)
        {
            return;
        }

        var endpoint = _socialPresenceNetworkEndpoint.Value;
        request.Host = endpoint.Host;
        request.UdpPort = endpoint.UdpPort;
        request.WebSocketPort = endpoint.WebSocketPort;
        request.WebSocketUrl = endpoint.WebSocketUrl;
        request.Joinable = endpoint.TryResolveForCurrentRuntime(out _, out _, out _);
    }

    private static string BuildSocialPresenceSignature(PresenceHeartbeatRequest request)
    {
        return string.Join(
            '|',
            request.DisplayName,
            request.Status,
            request.Mode,
            request.Map,
            request.ServerName,
            request.Host,
            request.UdpPort,
            request.WebSocketPort,
            request.WebSocketUrl,
            request.Joinable,
            request.PlayerCardJson);
    }

    private void UpdateStoredFriendDisplayName(string friendCode, string displayName)
    {
        if (string.IsNullOrWhiteSpace(displayName))
        {
            return;
        }

        var friend = _friendList.Friends.FirstOrDefault(entry => string.Equals(entry.FriendCode, friendCode, StringComparison.OrdinalIgnoreCase));
        if (friend is not null)
        {
            friend.DisplayName = displayName.Trim();
        }
    }
}
