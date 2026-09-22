using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using OpenGarrison.ClientShared;
using OpenGarrison.SessionRuntime;

namespace OpenGarrison.Client;

public partial class Game1
{
    private readonly Queue<Guid> _pendingRunClaims = new();
    private RunUploadQueue? _runUploads;
    private Task? _runUploadTask;
    private Task? _runSaveTask;
    private double _runUploadRetrySeconds;

    private void PumpRunUploads(double elapsedSeconds)
    {
        _runUploads ??= new RunUploadQueue();
        if (_runSaveTask is { IsCompleted: false }) return;
        if (_runSaveTask?.IsFaulted == true)
        {
            _ = _runSaveTask.Exception;
            _menuStatusMessage = "The run recording could not be saved. Check storage space.";
        }
        _runSaveTask = null;
        if (_pendingRunClaims.TryDequeue(out var attempt))
        {
            _runSaveTask = _runUploads.AddAsync(_clientIdentity.ClientId, _clientIdentity.FriendCode,
                "", [], attempt.ToString());
            _runUploadRetrySeconds = 0;
            return;
        }
        var host = _embeddedSessionHost ?? _peerRoomSession?.Host;
        if (host is not null && host.TryTakeRecordedRun(out var recording, out _))
        {
            try
            {
                _runSaveTask = _runUploads.AddAsync(_clientIdentity.ClientId, _clientIdentity.FriendCode,
                    recording.Ruleset, recording.Compress());
                _runUploadRetrySeconds = 0;
            }
            catch (Exception exception)
            {
                _menuStatusMessage = "The run recording could not be saved: " + exception.Message;
            }
            return;
        }
        if (_runUploadTask is { IsCompleted: false }) return;
        if (_runUploadTask?.IsFaulted == true)
        {
            _ = _runUploadTask.Exception;
            _menuStatusMessage = "Run upload pending. Check storage and connection.";
        }
        _runUploadTask = null;
        _runUploadRetrySeconds -= Math.Max(0, elapsedSeconds);
        if (_runUploadRetrySeconds <= 0)
        {
            _runUploadRetrySeconds = 30;
            _runUploadTask = _runUploads.SynchronizeAsync(_clientIdentity, _presenceClient);
        }
    }
}
