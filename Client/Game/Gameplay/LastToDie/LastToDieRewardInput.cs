namespace OpenGarrison.Client;

// Shared by local and hosted drafts. Time is supplied so transition and held
// input behavior can be tested without a renderer or wall-clock sleeps.
internal sealed class LastToDieRewardInput
{
    internal const int DelayMilliseconds = 750;
    private long _openedAt;
    private bool _armed;
    private Guid _hostedRunId;
    private ulong _hostedOfferId;
    private uint _hostedConnectionGeneration;
    public int SelectedIndex { get; private set; } = -1;
    public bool Submitted { get; private set; }
    public bool Ready => _armed && !Submitted;

    public bool ReconcileHostedContext(Guid runId, ulong offerId, uint connectionGeneration, long now)
    {
        if (_hostedRunId == runId
            && _hostedOfferId == offerId
            && _hostedConnectionGeneration == connectionGeneration)
        {
            return false;
        }

        _hostedRunId = runId;
        _hostedOfferId = offerId;
        _hostedConnectionGeneration = connectionGeneration;
        Reset(now);
        return true;
    }

    public void Reset(long now)
    {
        _openedAt = now;
        _armed = false;
        SelectedIndex = -1;
        Submitted = false;
    }

    public bool Update(long now, bool controlsReleased)
    {
        if (Submitted) return false;
        if (_armed) return true;
        if (now - _openedAt >= DelayMilliseconds && controlsReleased)
            _armed = true;
        // The release/arming frame cannot also choose or confirm.
        return false;
    }

    public void Select(int index)
    {
        if (Ready && index >= 0) SelectedIndex = index;
    }

    public void ClearSelection()
    {
        if (!Submitted) SelectedIndex = -1;
    }

    public bool TrySubmit()
    {
        if (!Ready || SelectedIndex < 0) return false;
        Submitted = true;
        return true;
    }

    public void Reject()
    {
        Submitted = false;
        _armed = false; // Require release before retrying a rejected command.
    }
}
