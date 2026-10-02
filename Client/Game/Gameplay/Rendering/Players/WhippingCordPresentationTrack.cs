namespace OpenGarrison.Client;

internal enum WhippingCordPresentationPhase
{
    Normal,
    Latched,
    ReleaseBackswing,
    Released,
    SwingRecoil,
    SwingComplete,
}

/// <summary>
/// Client-only animation timing for a whip. Remote players receive the latch
/// edge and cooldown samples but do not run local gameplay ticks, so gameplay
/// backswing counters cannot serve as a presentation clock.
/// </summary>
internal sealed class WhippingCordPresentationTrack
{
    private bool _hasObservedLatch;
    private bool _wasLatched;
    private float _releaseElapsedSeconds;
    private float _recoilElapsedSeconds;

    public WhippingCordPresentationPhase Phase { get; private set; }

    public float ReleaseProgress { get; private set; }

    public float RecoilProgress { get; private set; }

    public WhippingCordPresentationPhase Update(
        bool hasWhippingCordEquipped,
        bool isAlive,
        bool isLatched,
        int currentCooldownTicks,
        int maximumCooldownTicks,
        bool attackStarted,
        float elapsedSeconds,
        int recoilTicks,
        int backswingTicks,
        int ticksPerSecond)
    {
        currentCooldownTicks = Math.Max(0, currentCooldownTicks);
        maximumCooldownTicks = Math.Max(1, maximumCooldownTicks);
        recoilTicks = Math.Max(1, recoilTicks);
        backswingTicks = Math.Max(1, backswingTicks);
        ticksPerSecond = Math.Max(1, ticksPerSecond);

        if (!hasWhippingCordEquipped || !isAlive)
        {
            Reset();
            return Phase;
        }

        if (isLatched)
        {
            _hasObservedLatch = true;
            _wasLatched = true;
            _releaseElapsedSeconds = 0f;
            _recoilElapsedSeconds = 0f;
            ReleaseProgress = 0f;
            RecoilProgress = 0f;
            Phase = WhippingCordPresentationPhase.Latched;
            return Phase;
        }

        if (!_hasObservedLatch)
        {
            _hasObservedLatch = true;
            _wasLatched = false;
            Phase = WhippingCordPresentationPhase.Normal;
        }
        else if (_wasLatched)
        {
            _wasLatched = false;
            _releaseElapsedSeconds = 0f;
            ReleaseProgress = 0f;
            Phase = WhippingCordPresentationPhase.ReleaseBackswing;
        }

        var startSwing = attackStarted;
        if (startSwing)
        {
            _releaseElapsedSeconds = 0f;
            _recoilElapsedSeconds = 0f;
            ReleaseProgress = 0f;
            RecoilProgress = 0f;
            Phase = WhippingCordPresentationPhase.SwingRecoil;
        }

        if (Phase == WhippingCordPresentationPhase.ReleaseBackswing)
        {
            var durationSeconds = backswingTicks / (float)ticksPerSecond;
            _releaseElapsedSeconds += Math.Max(0f, elapsedSeconds);
            ReleaseProgress = Math.Clamp(_releaseElapsedSeconds / durationSeconds, 0f, 1f);
            if (ReleaseProgress >= 1f)
            {
                Phase = WhippingCordPresentationPhase.Released;
            }
        }

        if (Phase == WhippingCordPresentationPhase.SwingRecoil)
        {
            UpdateRecoilProgress(
                currentCooldownTicks,
                maximumCooldownTicks,
                recoilTicks,
                elapsedSeconds,
                ticksPerSecond);
        }
        else if (Phase == WhippingCordPresentationPhase.Normal && currentCooldownTicks > 0)
        {
            // A client can first observe a whip midway through its cooldown.
            // Seed that initial pose from the authoritative remaining ticks.
            var sampledTicks = Math.Max(0, maximumCooldownTicks - currentCooldownTicks);
            _recoilElapsedSeconds = sampledTicks / (float)ticksPerSecond;
            RecoilProgress = Math.Clamp(_recoilElapsedSeconds / (recoilTicks / (float)ticksPerSecond), 0f, 1f);
            Phase = RecoilProgress >= 1f
                ? WhippingCordPresentationPhase.SwingComplete
                : WhippingCordPresentationPhase.SwingRecoil;
        }

        return Phase;
    }

    private void UpdateRecoilProgress(
        int currentCooldownTicks,
        int maximumCooldownTicks,
        int recoilTicks,
        float elapsedSeconds,
        int ticksPerSecond)
    {
        var durationSeconds = recoilTicks / (float)ticksPerSecond;
        _recoilElapsedSeconds += Math.Max(0f, elapsedSeconds);
        if (currentCooldownTicks > 0)
        {
            var sampledTicks = Math.Max(0, maximumCooldownTicks - currentCooldownTicks);
            _recoilElapsedSeconds = Math.Max(_recoilElapsedSeconds, sampledTicks / (float)ticksPerSecond);
        }

        RecoilProgress = Math.Clamp(_recoilElapsedSeconds / durationSeconds, 0f, 1f);
        if (RecoilProgress >= 1f)
        {
            Phase = WhippingCordPresentationPhase.SwingComplete;
        }
    }

    public void Reset()
    {
        _hasObservedLatch = false;
        _wasLatched = false;
        _releaseElapsedSeconds = 0f;
        _recoilElapsedSeconds = 0f;
        ReleaseProgress = 0f;
        RecoilProgress = 0f;
        Phase = WhippingCordPresentationPhase.Normal;
    }
}
