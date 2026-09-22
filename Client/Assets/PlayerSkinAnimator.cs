using System;

namespace OpenGarrison.Client;

// Presentation only: animation never changes movement, weapon timing, or hitboxes.
internal sealed class PlayerSkinAnimator
{
    private PlayerSkinDefinition? _skin;
    private bool _airborne;
    private bool _blast;
    private float _airSeconds;
    private float _landingSeconds = float.PositiveInfinity;
    private float _clipSeconds;
    private float _runPosition;
    private float _runDirection = 1f;
    public string ClipName { get; private set; } = "idle";
    public int Pose { get; private set; }

    public bool TryGetPose(PlayerSkinDefinition skin, out int pose)
    {
        pose = ReferenceEquals(_skin, skin) ? Pose : skin.Clips["idle"].Frames[0];
        return !ReferenceEquals(_skin, skin) || skin.Clips.ContainsKey(ClipName);
    }

    public void Update(PlayerSkinDefinition skin, float elapsed, bool airborne, float verticalSpeed,
        float horizontalSpeed, float facingScale, bool blastMovement, bool fired, bool grounded = true)
    {
        if (!ReferenceEquals(_skin, skin))
        {
            _skin = skin;
            _airborne = false;
            _blast = false;
            _landingSeconds = float.PositiveInfinity;
            _runPosition = 0;
            _runDirection = 1f;
            _clipSeconds = 0;
            ClipName = "idle";
        }
        elapsed = Math.Clamp(elapsed, 0, 0.25f);
        // A render/prediction sample can say that the player is no longer airborne
        // before the simulation has actually reached the support surface. Keep the
        // airborne clip until the authoritative player state confirms the landing.
        // Ground support can briefly disappear while the movement solver steps
        // onto an incline or stair. Only keep an already-started jump airborne
        // until support returns; a support gap must not start a new jump.
        var animationAirborne = airborne || (_airborne && !grounded);
        var landed = _airborne && !animationAirborne;
        if (animationAirborne)
        {
            _airSeconds = _airborne ? _airSeconds + elapsed : 0;
            _blast |= blastMovement || fired;
            _landingSeconds = float.PositiveInfinity;
        }
        else
        {
            _landingSeconds = landed ? 0 : _landingSeconds + elapsed;
        }

        string Choose(string normal, string blast) => _blast && skin.Clips.ContainsKey(blast) ? blast : normal;
        string next;
        if (animationAirborne)
        {
            var start = Choose("jumpStart", "blastStart");
            next = verticalSpeed < 0 && skin.Clips.TryGetValue(start, out var startClip)
                && _airSeconds < startClip.Duration ? start
                : verticalSpeed < 0 ? Choose("rise", "blastRise") : Choose("fall", "blastFall");
        }
        else if (skin.Clips.TryGetValue(Choose("land", "blastLand"), out var landClip)
            && _landingSeconds < landClip.Duration)
        {
            next = Choose("land", "blastLand");
        }
        else
        {
            _blast = false;
            var movingBackward = horizontalSpeed * facingScale < 0;
            next = MathF.Abs(horizontalSpeed) < 6
                ? "idle"
                : movingBackward && skin.Clips.ContainsKey("runBackward") ? "runBackward" : "run";
        }

        _clipSeconds = next == ClipName ? _clipSeconds + elapsed : 0;
        ClipName = next;
        if (!skin.Clips.TryGetValue(next, out var clip))
        {
            // An omitted action uses the class's existing presentation.
            Pose = skin.Clips["idle"].Frames[0];
            _runPosition = 0;
            _runDirection = 1f;
        }
        else if (next is "run" or "runBackward")
        {
            var movingBackward = horizontalSpeed * facingScale < 0;
            var reverseFallback = next == "run" && movingBackward && !skin.Clips.ContainsKey("runBackward");
            var runDirection = reverseFallback ? -1f : 1f;
            var directionChanged = _runDirection != runDirection;
            _runDirection = runDirection;
            if (!directionChanged)
            {
                _runPosition += runDirection * MathF.Abs(horizontalSpeed) * elapsed / skin.PixelsPerRunFrame;
            }

            _runPosition %= clip.Frames.Length;
            if (_runPosition < 0)
            {
                _runPosition += clip.Frames.Length;
            }
            Pose = clip.Sample(_runPosition);
        }
        else
        {
            _runPosition = 0;
            _runDirection = 1f;
            Pose = clip.Sample(_clipSeconds * clip.FramesPerSecond);
        }
        _airborne = animationAirborne;
    }
}
