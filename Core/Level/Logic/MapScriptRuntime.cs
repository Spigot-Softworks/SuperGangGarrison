using System.Diagnostics;
using MoonSharp.Interpreter;

namespace OpenGarrison.Core;

/// <summary>A map-local Lua VM. Only explicit map operations cross the scripting boundary.</summary>
public sealed class MapScriptRuntime
{
    private readonly SimpleLevel _level;
    private readonly Func<int, PlayerTeam?> _owner;
    private readonly Action<string> _pulse;
    private readonly Func<string, bool> _signal;
    private readonly Action<PlayerTeam, int> _score;
    private readonly Action<PlayerTeam?> _endRound;
    private readonly Dictionary<string, Table> _objects = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, Table> _groups = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<(double Due, DynValue Callback)> _timers = [];
    private readonly Queue<string> _messages = new();
    private readonly Script _script;
    private double _elapsed;
    private int _remainingSlices = 100;
    public string? Error { get; private set; }
    public IReadOnlyCollection<string> Messages => _messages;

    public MapScriptRuntime(SimpleLevel level, Func<int, PlayerTeam?>? owner = null,
        Action<string>? pulse = null, Func<string, bool>? signal = null,
        Action<PlayerTeam, int>? score = null, Action<PlayerTeam?>? endRound = null)
    {
        _level = level;
        _owner = owner ?? (_ => null);
        _pulse = pulse ?? (_ => { });
        _signal = signal ?? (_ => false);
        _score = score ?? ((_, _) => { });
        _endRound = endRound ?? (_ => { });
        _script = new Script(CoreModules.Preset_HardSandbox);
        // Native library calls that can re-enter Lua cannot be preempted by its
        // instruction budget. Keep callbacks in the event dispatcher instead.
        _script.Globals.Get("table").Table.Set("sort", DynValue.Nil);
        _script.Globals.Get("string").Table.Set("gsub", DynValue.Nil);
        _script.Globals.Get("string").Table.Set("rep", DynValue.Nil);
        _script.Globals["RED"] = "red";
        _script.Globals["BLU"] = "blue";
        _script.Globals["BLUE"] = "blue";
        _script.Globals["NEUTRAL"] = "neutral";
        BuildSelections();
        _script.Globals["object"] = DynValue.NewCallback((_, args) =>
            DynValue.NewTable(FindSelection(_objects, args[0].String, "object")));
        _script.Globals["group"] = DynValue.NewCallback((_, args) =>
            DynValue.NewTable(FindSelection(_groups, args[0].String, "group")));
        var map = new Table(_script);
        map["pulse"] = DynValue.NewCallback((_, args) => { _pulse(RequireString(args[0])); return DynValue.Nil; });
        map["signal"] = DynValue.NewCallback((_, args) => DynValue.NewBoolean(_signal(RequireString(args[0]))));
        map["owner"] = DynValue.NewCallback((_, args) => DynValue.NewString(TeamName(_owner(RequirePoint(args[0])))));
        map["score"] = DynValue.NewCallback((_, args) =>
        {
            _score(RequireTeam(args[0]), checked((int)Math.Clamp(args[1].Number, -1000, 1000)));
            return DynValue.Nil;
        });
        map["end_round"] = DynValue.NewCallback((_, args) =>
        {
            _endRound(args[0].IsNil() ? null : RequireTeam(args[0])); return DynValue.Nil;
        });
        map["after"] = DynValue.NewCallback((_, args) =>
        {
            if (args[0].Type != DataType.Number || !double.IsFinite(args[0].Number) || args[0].Number < 0
                || args[1].Type != DataType.Function) throw new ScriptRuntimeException("map.after expects seconds and a function.");
            if (_timers.Count >= 128) throw new ScriptRuntimeException("Map script has too many pending timers (128 maximum).");
            _timers.Add((_elapsed + Math.Max(0.001, args[0].Number), args[1]));
            return DynValue.Nil;
        });
        var log = DynValue.NewCallback((_, args) =>
        {
            var text = string.Join(" ", args.GetArray().Select(value => value.ToPrintString()));
            _messages.Enqueue(text.Length > 512 ? text[..512] : text);
            while (_messages.Count > 16) _messages.Dequeue();
            return DynValue.Nil;
        });
        map["log"] = log;
        _script.Globals["print"] = log;
        _script.Globals["map"] = map;
    }

    public void Start()
    {
        if (Error is not null) { if (Error.Length > 1000) Error = Error[..1000]; return; }
        Error = MapScriptMetadata.Validate(_level.MapScriptSource);
        if (Error is not null) return;
        try
        {
            Execute(_script.LoadString(_level.MapScriptSource, codeFriendlyName: "map.lua"));
            Invoke("on_start");
        }
        catch (Exception error) when (IsScriptFailure(error)) { Fail(error); }
    }

    public void Capture(int point, PlayerTeam team)
        => Invoke("on_capture", DynValue.NewNumber(point), DynValue.NewString(TeamName(team)));
    public void Signal(string name, bool value)
        => Invoke("on_signal", DynValue.NewString(name), DynValue.NewBoolean(value));
    public void Tick(double seconds)
    {
        if (Error is not null) return;
        _remainingSlices = 100;
        _elapsed += seconds;
        // Newly scheduled callbacks wait until the following tick, even with zero delay.
        var due = _timers.Where(timer => timer.Due <= _elapsed).ToArray();
        _timers.RemoveAll(timer => timer.Due <= _elapsed);
        foreach (var timer in due)
        {
            try { Execute(timer.Callback); }
            catch (Exception error) when (IsScriptFailure(error)) { Fail(error); break; }
        }
        Invoke("on_tick", DynValue.NewNumber(seconds));
    }

    private void Invoke(string callback, params DynValue[] args)
    {
        if (Error is not null) return;
        var function = _script.Globals.Get(callback);
        if (function.IsNil()) return;
        try
        {
            if (function.Type != DataType.Function) throw new ScriptRuntimeException(callback + " must be a function.");
            Execute(function, args);
        }
        catch (Exception error) when (IsScriptFailure(error)) { Fail(error); }
    }

    private void Execute(DynValue function, params DynValue[] args)
    {
        var coroutine = _script.CreateCoroutine(function).Coroutine;
        coroutine.AutoYieldCounter = 1000;
        var watch = Stopwatch.StartNew();
        for (var slice = 0; slice < 100; slice++)
        {
            if (Error is not null) return;
            if (--_remainingSlices < 0) break;
            if (slice == 0) coroutine.Resume(args); else coroutine.Resume();
            if (coroutine.State == CoroutineState.Dead) return;
            if (watch.ElapsedMilliseconds > 25) break;
        }
        throw new ScriptRuntimeException("Map script exceeded its execution budget; check for an infinite loop.");
    }

    private void BuildSelections()
    {
        var named = new Dictionary<string, List<(int Room, string Spawn)>>(StringComparer.OrdinalIgnoreCase);
        var grouped = new Dictionary<string, List<(int Room, string Spawn)>>(StringComparer.OrdinalIgnoreCase);
        void Add(string name, string groups, (int Room, string Spawn) target)
        {
            if (!string.IsNullOrWhiteSpace(name))
            {
                if (!named.TryGetValue(name, out var entries)) named[name] = entries = [];
                entries.Add(target);
            }
            foreach (var group in MapObjectMetadata.ParseGroups(groups))
            {
                if (!grouped.TryGetValue(group, out var entries)) grouped[group] = entries = [];
                entries.Add(target);
            }
        }
        for (var index = 0; index < _level.RoomObjects.Count; index++)
        {
            var marker = _level.RoomObjects[index];
            Add(marker.ObjectName, marker.ObjectGroups, (index, ""));
        }
        foreach (var spawn in _level.RedSpawns.Concat(_level.BlueSpawns).DistinctBy(spawn => spawn.MapEntityId))
            if (!string.IsNullOrEmpty(spawn.MapEntityId)) Add(spawn.ObjectName, spawn.ObjectGroups, (-1, spawn.MapEntityId));
        foreach (var (name, targets) in named)
        {
            if (targets.Count > 1) { Error = $"Object name '{name}' is used more than once. Use groups for collections."; continue; }
            _objects[name] = Selection(targets);
        }
        foreach (var (name, targets) in grouped) _groups[name] = Selection(targets.Distinct().ToList());
    }

    private Table Selection(List<(int Room, string Spawn)> targets)
    {
        var table = new Table(_script);
        table["count"] = targets.Count;
        bool Active((int Room, string Spawn) target) => target.Room >= 0 ? _level.IsRoomObjectActive(target.Room)
            : !_level.MapScriptSpawnOverrides.TryGetValue(target.Spawn, out var enabled) || enabled;
        DynValue Set(bool? enabled, bool toggle = false)
        {
            foreach (var target in targets)
            {
                var value = toggle ? !Active(target) : enabled;
                if (target.Room >= 0) _level.SetMapScriptObjectOverride(target.Room, value);
                else if (value.HasValue) _level.MapScriptSpawnOverrides[target.Spawn] = value.Value;
                else _level.MapScriptSpawnOverrides.Remove(target.Spawn);
            }
            return DynValue.Nil;
        }
        table["enable"] = DynValue.NewCallback((_, _) => Set(true));
        table["disable"] = DynValue.NewCallback((_, _) => Set(false));
        table["toggle"] = DynValue.NewCallback((_, _) => Set(null, toggle: true));
        table["clear"] = DynValue.NewCallback((_, _) => Set(null));
        table["is_enabled"] = DynValue.NewCallback((_, _) => DynValue.NewBoolean(targets.All(Active)));
        return table;
    }

    private static Table FindSelection(Dictionary<string, Table> selections, string? name, string kind)
        => name is not null && selections.TryGetValue(name, out var table) ? table
            : throw new ScriptRuntimeException($"Unknown {kind} '{name}'. Set its name/groups in object properties.");
    private static string RequireString(DynValue value) => value.Type == DataType.String ? value.String
        : throw new ScriptRuntimeException("Expected a name string.");
    private static int RequirePoint(DynValue value) => value.Type == DataType.Number && value.Number >= 1 && value.Number <= 65535
        ? (int)value.Number : throw new ScriptRuntimeException("Control point numbers start at 1.");
    private static PlayerTeam RequireTeam(DynValue value) => RequireString(value).ToLowerInvariant() switch
    {
        "red" => PlayerTeam.Red, "blue" or "blu" => PlayerTeam.Blue,
        _ => throw new ScriptRuntimeException("Expected RED or BLU."),
    };
    private static string TeamName(PlayerTeam? team) => team switch { PlayerTeam.Red => "red", PlayerTeam.Blue => "blue", _ => "neutral" };
    private static bool IsScriptFailure(Exception error) => error is InterpreterException or InvalidOperationException or ArgumentException or OverflowException;
    private void Fail(Exception error)
    {
        var message = error is InterpreterException lua ? lua.DecoratedMessage ?? lua.Message : error.Message;
        Error = message.Length > 1000 ? message[..1000] : message;
    }
    internal void Abort(string error) => Error = error;
}
