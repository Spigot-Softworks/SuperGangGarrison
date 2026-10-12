using System;
using System.Collections.Generic;

namespace OpenGarrison.Core;

/// <summary>
/// Logic-driven state of a level's placed lights: power (with on/off transitions) and the
/// colour state. Ticked by map logic on the server and on clients (like spritesheet
/// playback), reset each round. Also caches the fixed geometry of damaging lasers, which
/// the server damages with and clients draw, so both agree exactly.
/// </summary>
public sealed class MapLightRuntimeState
{
    private const int InputCount = 6;
    private const int OnSlot = 0;
    private const int OffSlot = 1;
    private const int ToggleSlot = 2;
    private const int FollowSlot = 3;
    private const int ColorSlot = 4;
    private const int TertiaryColorSlot = 5;

    private IReadOnlyList<MapLightMarker>? _boundLights;
    private MapLogicGraph? _boundGraph;
    private int[] _nodes = [];
    private bool[] _previousSignals = [];
    private bool[] _powered = [];
    private float[] _progress = [];
    private float[] _colorProgress = [];
    private MapLightColorState[] _lastColorState = [];
    private bool[] _receivedNetworkState = [];
    private MapLightColorState[] _colorState = [];
    private bool[] _tertiaryColorHeld = [];
    private int[] _cycleStep = [];
    private int[] _logicLightIndices = [];
    private bool _hasLogic;
    private bool _hasNetworkAuthority;

    private IReadOnlyList<MapLightMarker>? _fixedBeamLights;
    private IReadOnlyList<LevelSolid>? _fixedBeamSolids;
    private MapLightFixedBeam[] _fixedBeams = [];

    /// <summary>True when any light has a wired logic input.</summary>
    public bool HasLogic => _hasLogic;

    /// <summary>
    /// True on a client once the server has sent light state: logic signals are then
    /// left to the server (which also sees server-only events), and only transitions run here.
    /// </summary>
    public bool HasNetworkAuthority => _hasNetworkAuthority;

    /// <summary>Indices of the lights with a wired logic input (the ones the server sends).</summary>
    public IReadOnlyList<int> LogicLightIndices => _logicLightIndices;

    public int Count => _powered.Length;

    /// <summary>Resolves the lights' logic inputs against the graph (once per level).</summary>
    public void Bind(IReadOnlyList<MapLightMarker> lights, MapLogicGraph graph)
    {
        if (ReferenceEquals(lights, _boundLights) && ReferenceEquals(graph, _boundGraph))
        {
            return;
        }

        _boundLights = lights;
        _boundGraph = graph;
        var count = lights.Count;
        _nodes = new int[count * InputCount];
        _previousSignals = new bool[count * InputCount];
        _powered = new bool[count];
        _progress = new float[count];
        _colorProgress = new float[count];
        _lastColorState = new MapLightColorState[count];
        _receivedNetworkState = new bool[count];
        _colorState = new MapLightColorState[count];
        _tertiaryColorHeld = new bool[count];
        _cycleStep = new int[count];
        _hasLogic = false;
        _hasNetworkAuthority = false;
        var logicIndices = new List<int>();
        for (var index = 0; index < count; index += 1)
        {
            var style = lights[index].Style;
            var offset = index * InputCount;
            _nodes[offset + OnSlot] = Resolve(graph, style.OnInput);
            _nodes[offset + OffSlot] = Resolve(graph, style.OffInput);
            _nodes[offset + ToggleSlot] = Resolve(graph, style.ToggleInput);
            _nodes[offset + FollowSlot] = Resolve(graph, style.FollowInput);
            _nodes[offset + ColorSlot] = Resolve(graph, style.ColorInput);
            _nodes[offset + TertiaryColorSlot] = Resolve(graph, style.TertiaryColorInput);
            var wired = false;
            for (var slot = 0; slot < InputCount; slot += 1)
            {
                wired |= _nodes[offset + slot] >= 0;
            }

            if (wired)
            {
                logicIndices.Add(index);
            }

            _hasLogic |= wired;
        }

        _logicLightIndices = logicIndices.ToArray();
        Reset(lights);
    }

    /// <summary>
    /// Back to each light's starting state (start of a round). A network client keeps
    /// the server's state until it receives a replacement; local graph resets cannot
    /// determine the server's latched colours or power.
    /// </summary>
    public void Reset(IReadOnlyList<MapLightMarker> lights)
    {
        if (_hasNetworkAuthority || lights.Count != _powered.Length)
        {
            return;
        }

        Array.Clear(_previousSignals);
        Array.Clear(_colorState);
        Array.Clear(_lastColorState);
        Array.Clear(_tertiaryColorHeld);
        Array.Clear(_cycleStep);
        for (var index = 0; index < lights.Count; index += 1)
        {
            var startOn = lights[index].Style.StartOn;
            _powered[index] = startOn;
            _progress[index] = startOn ? 1f : 0f;
            _colorProgress[index] = 1f;
        }
    }

    /// <summary>Applies this tick's logic signals and advances on/off transitions.</summary>
    public void Tick(IReadOnlyList<MapLightMarker> lights, MapLogicGraph graph, float deltaSeconds)
    {
        Bind(lights, graph);
        for (var index = 0; index < lights.Count; index += 1)
        {
            var style = lights[index].Style;
            if (_hasLogic && !_hasNetworkAuthority)
            {
                ApplySignals(index, style, graph);
            }

            AdvanceTransition(index, style, deltaSeconds);
            AdvanceColorTransition(index, style, deltaSeconds);
        }
    }

    /// <summary>
    /// One light's logic state packed for the network: bit 0 power, bits 1-2 colour
    /// state, bits 3-4 colour cycle step.
    /// </summary>
    public byte GetNetworkState(int index)
    {
        if (index < 0 || index >= _powered.Length)
        {
            return 0;
        }

        return (byte)((_powered[index] ? 1 : 0)
            | (((int)GetColorState(index) & 3) << 1)
            | ((_cycleStep[index] & 3) << 3));
    }

    /// <summary>
    /// Applies the server's state for one light (client side). From then on this client
    /// stops evaluating light logic itself; transitions still animate locally.
    /// </summary>
    public void ApplyNetworkState(int index, byte state)
    {
        if (index < 0 || index >= _powered.Length)
        {
            return;
        }

        _hasNetworkAuthority = true;
        _tertiaryColorHeld[index] = false;
        _powered[index] = (state & 1) != 0;
        var color = (state >> 1) & 3;
        _colorState[index] = color <= (int)MapLightColorState.Trigger ? (MapLightColorState)color : MapLightColorState.Normal;
        _cycleStep[index] = Math.Min(2, (state >> 3) & 3);
        if (!_receivedNetworkState[index])
        {
            // Joining a map establishes the current colour; only later changes animate.
            _receivedNetworkState[index] = true;
            _lastColorState[index] = GetColorState(index);
            _colorProgress[index] = 1f;
        }
    }

    /// <summary>The light's power target (what logic says), used for damage.</summary>
    public bool IsPowered(int index, MapLightStyle style) =>
        index >= 0 && index < _powered.Length ? _powered[index] : style.StartOn;

    /// <summary>Runtime inputs for drawing a light.</summary>
    public MapLightRuntimeSample GetSample(int index, MapLightStyle style)
    {
        if (index < 0 || index >= _powered.Length)
        {
            return MapLightRuntimeSample.Initial(style);
        }

        return new MapLightRuntimeSample(_powered[index], _progress[index], GetColorState(index), ColorProgress: _colorProgress[index]);
    }

    private MapLightColorState GetColorState(int index) =>
        _tertiaryColorHeld[index] ? MapLightColorState.Trigger : _colorState[index];

    /// <summary>
    /// Fixed beams of the damaging lasers, clipped at the level's solids. Computed once per
    /// level; the server damages along these and clients draw exactly them.
    /// </summary>
    public IReadOnlyList<MapLightFixedBeam> GetFixedBeams(IReadOnlyList<MapLightMarker> lights, IReadOnlyList<LevelSolid> solids)
    {
        if (!ReferenceEquals(lights, _fixedBeamLights) || !ReferenceEquals(solids, _fixedBeamSolids))
        {
            _fixedBeamLights = lights;
            _fixedBeamSolids = solids;
            _fixedBeams = MapLightBeamGeometry.BuildFixedBeams(lights, solids);
        }

        return _fixedBeams;
    }

    /// <summary>The fixed (wall-clipped) length of one damaging laser beam, if it is one.</summary>
    public bool TryGetFixedBeamLength(IReadOnlyList<MapLightMarker> lights, IReadOnlyList<LevelSolid> solids, int sourceIndex, int beamIndex, out float length)
    {
        foreach (var beam in GetFixedBeams(lights, solids))
        {
            if (beam.SourceIndex == sourceIndex && beam.BeamIndex == beamIndex)
            {
                length = beam.Length;
                return true;
            }
        }

        length = 0f;
        return false;
    }

    private void ApplySignals(int index, MapLightStyle style, MapLogicGraph graph)
    {
        var offset = index * InputCount;
        var follow = _nodes[offset + FollowSlot];
        if (follow >= 0)
        {
            // Level-driven: lit exactly while the signal is true (overrides the edge inputs).
            _powered[index] = graph.GetOutput(follow);
        }
        else
        {
            if (RisingEdge(offset + OnSlot, graph))
            {
                _powered[index] = true;
            }

            if (RisingEdge(offset + OffSlot, graph))
            {
                _powered[index] = false;
            }

            if (RisingEdge(offset + ToggleSlot, graph))
            {
                _powered[index] = !_powered[index];
            }
        }

        var tertiaryNode = _nodes[offset + TertiaryColorSlot];
        _tertiaryColorHeld[index] = tertiaryNode >= 0 && graph.GetOutput(tertiaryNode);
        var colorNode = _nodes[offset + ColorSlot];
        if (colorNode < 0)
        {
            return;
        }

        switch (style.ColorAction)
        {
            case MapLightColorAction.HoldAlt:
                _colorState[index] = graph.GetOutput(colorNode) ? MapLightColorState.Swapped : MapLightColorState.Normal;
                break;
            case MapLightColorAction.Trigger:
                _colorState[index] = graph.GetOutput(colorNode) ? MapLightColorState.Trigger : MapLightColorState.Normal;
                break;
            case MapLightColorAction.Swap:
                if (RisingEdge(offset + ColorSlot, graph))
                {
                    _colorState[index] = _colorState[index] == MapLightColorState.Swapped ? MapLightColorState.Normal : MapLightColorState.Swapped;
                }

                break;
            case MapLightColorAction.Cycle:
                if (RisingEdge(offset + ColorSlot, graph))
                {
                    _cycleStep[index] = (_cycleStep[index] + 1) % 3;
                    _colorState[index] = (MapLightColorState)_cycleStep[index];
                }

                break;
        }
    }

    private bool RisingEdge(int slot, MapLogicGraph graph)
    {
        var node = _nodes[slot];
        if (node < 0)
        {
            return false;
        }

        var signal = graph.GetOutput(node);
        var rising = signal && !_previousSignals[slot];
        _previousSignals[slot] = signal;
        return rising;
    }

    private void AdvanceTransition(int index, MapLightStyle style, float deltaSeconds)
    {
        var target = _powered[index] ? 1f : 0f;
        if (style.Transition == MapLightTransition.Instant || style.FadeTime <= 0.0001f)
        {
            _progress[index] = target;
            return;
        }

        var step = deltaSeconds / style.FadeTime;
        _progress[index] = target > _progress[index]
            ? MathF.Min(target, _progress[index] + step)
            : MathF.Max(target, _progress[index] - step);
    }

    private void AdvanceColorTransition(int index, MapLightStyle style, float deltaSeconds)
    {
        var color = GetColorState(index);
        if (style.ColorTransition == MapLightTransition.Instant || style.ColorTransitionTime <= 0.0001f || !_powered[index])
        {
            _colorProgress[index] = 1f;
        }
        else
        {
            if (color != _lastColorState[index])
            {
                _colorProgress[index] = 0f;
            }

            _colorProgress[index] = MathF.Min(1f, _colorProgress[index] + MathF.Max(0f, deltaSeconds) / style.ColorTransitionTime);
        }

        _lastColorState[index] = color;
    }

    private static int Resolve(MapLogicGraph graph, string logicRef) =>
        string.IsNullOrWhiteSpace(logicRef) ? -1 : MapLogicGraphImporter.ResolveLogicSignalNodeIndex(graph, logicRef);
}

/// <summary>One fixed beam of a damaging laser, already clipped at walls.</summary>
public readonly record struct MapLightFixedBeam(
    int SourceIndex,
    int BeamIndex,
    float X,
    float Y,
    float DirectionX,
    float DirectionY,
    float Length,
    float Width)
{
    public float EndX => X + (DirectionX * Length);

    public float EndY => Y + (DirectionY * Length);
}

/// <summary>Beam geometry shared by the server (laser damage) and the client (drawing).</summary>
public static class MapLightBeamGeometry
{
    /// <summary>Solids the beam origin sits inside are ignored, so a laser sunk into a wall still fires.</summary>
    public static float ClipToSolids(IReadOnlyList<LevelSolid> solids, float x, float y, float directionX, float directionY, float maxLength)
    {
        var nearest = maxLength;
        for (var index = 0; index < solids.Count; index += 1)
        {
            var solid = solids[index];
            if (x >= solid.Left && x <= solid.Right && y >= solid.Top && y <= solid.Bottom)
            {
                continue;
            }

            if (TryIntersect(x, y, directionX, directionY, solid.Left, solid.Top, solid.Right, solid.Bottom, out var enter, out _)
                && enter >= 0f
                && enter < nearest)
            {
                nearest = enter;
            }
        }

        return nearest;
    }

    /// <summary>
    /// Ray against an axis-aligned box (slab method). Distances are along the unit
    /// direction; enter can be negative when the ray starts inside.
    /// </summary>
    public static bool TryIntersect(
        float x,
        float y,
        float directionX,
        float directionY,
        float left,
        float top,
        float right,
        float bottom,
        out float enter,
        out float exit)
    {
        enter = float.NegativeInfinity;
        exit = float.PositiveInfinity;
        if (!Slab(x, directionX, left, right, ref enter, ref exit) || !Slab(y, directionY, top, bottom, ref enter, ref exit))
        {
            return false;
        }

        return exit >= enter && exit >= 0f;
    }

    /// <summary>Distance along a beam to where it first touches a box, if within the length.</summary>
    public static bool TryHitBox(float x, float y, float directionX, float directionY, float length, float halfWidth, float left, float top, float right, float bottom, out float distance)
    {
        // Grow the box by the beam's half width so a thick beam grazing a player hits.
        if (TryIntersect(x, y, directionX, directionY, left - halfWidth, top - halfWidth, right + halfWidth, bottom + halfWidth, out var enter, out _)
            && enter <= length)
        {
            distance = MathF.Max(0f, enter);
            return true;
        }

        distance = 0f;
        return false;
    }

    /// <summary>Every damaging laser's beams, clipped at the solids.</summary>
    public static MapLightFixedBeam[] BuildFixedBeams(IReadOnlyList<MapLightMarker> lights, IReadOnlyList<LevelSolid> solids)
    {
        var result = new List<MapLightFixedBeam>();
        for (var index = 0; index < lights.Count; index += 1)
        {
            var light = lights[index];
            var style = light.Style;
            if (!style.IsDamagingLaser)
            {
                continue;
            }

            var count = Math.Max(1, style.Beams);
            for (var beam = 0; beam < count; beam += 1)
            {
                var angle = MapLightEmission.GetFanAngle(style, MapLightMetadata.GetAimDirection(light), beam);
                var (directionX, directionY) = MapLightMetadata.GetDirectionVector(angle);
                var length = ClipToSolids(solids, light.X, light.Y, directionX, directionY, light.Radius);
                result.Add(new MapLightFixedBeam(index, beam, light.X, light.Y, directionX, directionY, length, style.Width));
            }
        }

        return result.ToArray();
    }

    private static bool Slab(float origin, float direction, float min, float max, ref float enter, ref float exit)
    {
        if (MathF.Abs(direction) < 1e-6f)
        {
            return origin >= min && origin <= max;
        }

        var t1 = (min - origin) / direction;
        var t2 = (max - origin) / direction;
        if (t1 > t2)
        {
            (t1, t2) = (t2, t1);
        }

        enter = MathF.Max(enter, t1);
        exit = MathF.Min(exit, t2);
        return enter <= exit;
    }
}
