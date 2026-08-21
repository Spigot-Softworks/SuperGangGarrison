using System.Globalization;
using OpenGarrison.Core;
using OpenGarrison.GameplayModding;
using OpenGarrison.Server.Plugins;

namespace OpenGarrison.Server;

internal sealed class ServerBuiltInCommandRegistrar(
    PluginCommandRegistry commandRegistry,
    Action<List<string>> addStatusSummary,
    Action<List<string>> addRulesSummary,
    Action<List<string>> addLobbySummary,
    Action<List<string>> addMapSummary,
    Action<List<string>> addRotationSummary,
    Action<List<string>> addPlayersSummary,
    Func<string> getDemoStatusLine,
    Func<string?, (bool Success, string Status, string Error)> tryStartDemoRecording,
    Func<(bool Success, string Status, string Error)> tryStopDemoRecording,
    Func<IReadOnlyList<string>> loadedPluginIdsProvider,
    IOpenGarrisonServerCvarRegistry cvarRegistry,
    IOpenGarrisonServerScheduler scheduler,
    Func<OpenGarrisonServerCommandContext, string, IReadOnlyList<string>>? executeLuaCommand = null,
    Func<IReadOnlyList<string>>? reloadLuaPlugins = null)
{
    public void RegisterAll()
    {
        commandRegistry.RegisterBuiltIn(
            "help",
            "Show server and plugin commands.",
            "help",
            (context, _, _) => Task.FromResult<IReadOnlyList<string>>(BuildHelpLines(context)),
            OpenGarrisonServerAdminPermissions.ViewServerState,
            "?");
        commandRegistry.RegisterBuiltIn(
            "status",
            "Show overall server status.",
            "status",
            (_, _, _) => Task.FromResult<IReadOnlyList<string>>(BuildSummaryLines(
                addStatusSummary,
                addRulesSummary,
                addLobbySummary,
                addMapSummary)),
            OpenGarrisonServerAdminPermissions.ViewServerState,
            "info");
        commandRegistry.RegisterBuiltIn(
            "players",
            "List connected players.",
            "players",
            (_, _, _) => Task.FromResult<IReadOnlyList<string>>(BuildSummaryLines(addPlayersSummary)),
            OpenGarrisonServerAdminPermissions.ViewServerState,
            "who");
        commandRegistry.RegisterBuiltIn(
            "map",
            "Show current map details.",
            "map",
            (_, _, _) => Task.FromResult<IReadOnlyList<string>>(BuildSummaryLines(addMapSummary)),
            OpenGarrisonServerAdminPermissions.ViewServerState,
            "level");
        commandRegistry.RegisterBuiltIn(
            "rules",
            "Show match rules.",
            "rules",
            (_, _, _) => Task.FromResult<IReadOnlyList<string>>(BuildSummaryLines(addRulesSummary)),
            OpenGarrisonServerAdminPermissions.ViewServerState);
        commandRegistry.RegisterBuiltIn(
            "timelimit",
            "Set the match time limit in minutes.",
            "timelimit <1-255>",
            (context, arguments, _) =>
            {
                if (!TryParseBoundedInt(arguments, min: 1, max: 255, out var timeLimitMinutes))
                {
                    return Task.FromResult<IReadOnlyList<string>>(["[server] usage: timelimit <1-255>"]);
                }

                return Task.FromResult<IReadOnlyList<string>>(context.AdminOperations.TrySetTimeLimit(timeLimitMinutes)
                    ? [$"[server] time limit set to {timeLimitMinutes} minutes."]
                    : ["[server] unable to set time limit."]);
            },
            OpenGarrisonServerAdminPermissions.ManageMatch,
            "time");
        commandRegistry.RegisterBuiltIn(
            "caplimit",
            "Set the capture limit.",
            "caplimit <1-255>",
            (context, arguments, _) =>
            {
                if (!TryParseBoundedInt(arguments, min: 1, max: 255, out var capLimit))
                {
                    return Task.FromResult<IReadOnlyList<string>>(["[server] usage: caplimit <1-255>"]);
                }

                return Task.FromResult<IReadOnlyList<string>>(context.AdminOperations.TrySetCapLimit(capLimit)
                    ? [$"[server] cap limit set to {capLimit}."]
                    : ["[server] unable to set cap limit."]);
            },
            OpenGarrisonServerAdminPermissions.ManageMatch,
            "cap");
        commandRegistry.RegisterBuiltIn(
            "respawnseconds",
            "Set the respawn time in seconds.",
            "respawnseconds <0-255>",
            (context, arguments, _) =>
            {
                if (!TryParseBoundedInt(arguments, min: 0, max: 255, out var respawnSeconds))
                {
                    return Task.FromResult<IReadOnlyList<string>>(["[server] usage: respawnseconds <0-255>"]);
                }

                return Task.FromResult<IReadOnlyList<string>>(context.AdminOperations.TrySetRespawnSeconds(respawnSeconds)
                    ? [$"[server] respawn set to {respawnSeconds} seconds."]
                    : ["[server] unable to set respawn time."]);
            },
            OpenGarrisonServerAdminPermissions.ManageMatch,
            "respawn");
        commandRegistry.RegisterBuiltIn(
            "lobby",
            "Show lobby registration state.",
            "lobby",
            (_, _, _) => Task.FromResult<IReadOnlyList<string>>(BuildSummaryLines(addLobbySummary)),
            OpenGarrisonServerAdminPermissions.ViewServerState);
        commandRegistry.RegisterBuiltIn(
            "rotation",
            "Show the active map rotation.",
            "rotation",
            (_, _, _) => Task.FromResult<IReadOnlyList<string>>(BuildSummaryLines(addRotationSummary)),
            OpenGarrisonServerAdminPermissions.ViewServerState,
            "maps");
        commandRegistry.RegisterBuiltIn(
            "plugins",
            "List loaded server plugins.",
            "plugins",
            (_, _, _) => Task.FromResult<IReadOnlyList<string>>(BuildPluginLines()),
            OpenGarrisonServerAdminPermissions.ViewServerState);
        commandRegistry.RegisterBuiltIn(
            "demo",
            "Show demo recording status.",
            "demo",
            (_, _, _) => Task.FromResult<IReadOnlyList<string>>([getDemoStatusLine()]),
            OpenGarrisonServerAdminPermissions.ViewServerState,
            "demos",
            "replays");
        commandRegistry.RegisterBuiltIn(
            "demo start",
            "Start server-authoritative demo recording.",
            "demo start [path]",
            (_, arguments, _) =>
            {
                var requestedPath = string.IsNullOrWhiteSpace(arguments) ? null : arguments.Trim();
                var result = tryStartDemoRecording(requestedPath);
                return Task.FromResult<IReadOnlyList<string>>(
                    [result.Success ? result.Status : $"[server] demo start failed: {result.Error}"]);
            },
            OpenGarrisonServerAdminPermissions.ManageServerConfiguration);
        commandRegistry.RegisterBuiltIn(
            "demo stop",
            "Stop and save demo recording.",
            "demo stop",
            (_, _, _) =>
            {
                var result = tryStopDemoRecording();
                return Task.FromResult<IReadOnlyList<string>>(
                    [result.Success ? result.Status : $"[server] demo stop failed: {result.Error}"]);
            },
            OpenGarrisonServerAdminPermissions.ManageServerConfiguration);
        commandRegistry.RegisterBuiltIn(
            "cvars",
            "List host cvars.",
            "cvars [filter]",
            (_, arguments, _) => Task.FromResult<IReadOnlyList<string>>(BuildCvarLines(arguments)),
            OpenGarrisonServerAdminPermissions.ViewServerState);
        commandRegistry.RegisterBuiltIn(
            "cvar",
            "Show or set a host cvar.",
            "cvar <name> [value]",
            (_, arguments, _) => Task.FromResult<IReadOnlyList<string>>(ExecuteCvarCommand(arguments)),
            OpenGarrisonServerAdminPermissions.ManageServerConfiguration);
        commandRegistry.RegisterBuiltIn(
            "frameinfo",
            "Toggle the periodic server frame summary.",
            "frameinfo [on|off|toggle|status]",
            (_, arguments, _) => Task.FromResult<IReadOnlyList<string>>(ExecuteFrameInfoCommand(arguments)),
            OpenGarrisonServerAdminPermissions.ManageServerConfiguration,
            "frame_info");
        commandRegistry.RegisterBuiltIn(
            "timers",
            "List scheduled server tasks.",
            "timers",
            (_, _, _) => Task.FromResult<IReadOnlyList<string>>(BuildTimerLines()),
            OpenGarrisonServerAdminPermissions.ManageScheduler);
        commandRegistry.RegisterBuiltIn(
            "say",
            "Broadcast a system chat message.",
            "say <text>",
            (context, arguments, _) =>
            {
                if (string.IsNullOrWhiteSpace(arguments))
                {
                    return Task.FromResult<IReadOnlyList<string>>(["[server] usage: say <text>"]);
                }

                context.AdminOperations.BroadcastSystemMessage(arguments);
                return Task.FromResult<IReadOnlyList<string>>(["[server] system message sent."]);
            },
            OpenGarrisonServerAdminPermissions.ManagePlayers);
        commandRegistry.RegisterBuiltIn(
            "kick",
            "Disconnect a player slot.",
            "kick <slot> [reason]",
            (context, arguments, _) =>
            {
                if (!TryParseSlotAndOptionalArgument(arguments, out var slot, out var reason))
                {
                    return Task.FromResult<IReadOnlyList<string>>(["[server] usage: kick <slot> [reason]"]);
                }

                var finalReason = string.IsNullOrWhiteSpace(reason) ? "Kicked by admin." : reason;
                return Task.FromResult<IReadOnlyList<string>>(context.AdminOperations.TryDisconnect(slot, finalReason)
                    ? [$"[server] kicked slot {slot}."]
                    : [$"[server] no client at slot {slot}."]);
            },
            OpenGarrisonServerAdminPermissions.ManagePlayers);
        commandRegistry.RegisterBuiltIn(
            "spectate",
            "Move a player to spectator.",
            "spectate <slot>",
            (context, arguments, _) =>
            {
                if (!TryParseSlot(arguments, out var slot))
                {
                    return Task.FromResult<IReadOnlyList<string>>(["[server] usage: spectate <slot>"]);
                }

                return Task.FromResult<IReadOnlyList<string>>(context.AdminOperations.TryMoveToSpectator(slot)
                    ? [$"[server] moved slot {slot} to spectator."]
                    : [$"[server] unable to move slot {slot} to spectator."]);
            },
            OpenGarrisonServerAdminPermissions.ManagePlayers);
        commandRegistry.RegisterBuiltIn(
            "team",
            "Set a player's team.",
            "team <slot> <red|blue>",
            (context, arguments, _) =>
            {
                if (!TryParseSlotAndRequiredArgument(arguments, out var slot, out var teamText)
                    || !TryParseTeam(teamText, out var team))
                {
                    return Task.FromResult<IReadOnlyList<string>>(["[server] usage: team <slot> <red|blue>"]);
                }

                return Task.FromResult<IReadOnlyList<string>>(context.AdminOperations.TrySetTeam(slot, team)
                    ? [$"[server] slot {slot} set to {team}."]
                    : [$"[server] unable to set team for slot {slot}."]);
            },
            OpenGarrisonServerAdminPermissions.ManagePlayers);
        commandRegistry.RegisterBuiltIn(
            "class",
            "Set a player's class.",
            "class <slot> <scout|engineer|pyro|soldier|demoman|heavy|sniper|medic|spy|quote>",
            (context, arguments, _) =>
            {
                if (!TryParseSlotAndRequiredArgument(arguments, out var slot, out var classText)
                    || !Enum.TryParse<PlayerClass>(classText, ignoreCase: true, out var playerClass))
                {
                    return Task.FromResult<IReadOnlyList<string>>(["[server] usage: class <slot> <class>"]);
                }

                return Task.FromResult<IReadOnlyList<string>>(context.AdminOperations.TrySetClass(slot, playerClass)
                    ? [$"[server] slot {slot} class set to {playerClass}."]
                    : [$"[server] unable to set class for slot {slot}."]);
            },
            OpenGarrisonServerAdminPermissions.ManagePlayers);
        commandRegistry.RegisterBuiltIn(
            "loadouts",
            "List a player's available gameplay loadouts.",
            "loadouts <slot>",
            (context, arguments, _) =>
            {
                if (!TryParseSlot(arguments, out var slot))
                {
                    return Task.FromResult<IReadOnlyList<string>>(["[server] usage: loadouts <slot>"]);
                }

                var player = context.ServerState.GetPlayers().FirstOrDefault(candidate => candidate.Slot == slot);
                if (player.Slot != slot || !player.PlayerClass.HasValue)
                {
                    return Task.FromResult<IReadOnlyList<string>>([$"[server] no playable player at slot {slot}."]);
                }

                var loadouts = context.ServerState.GetAvailableGameplayLoadouts(slot);
                if (loadouts.Count == 0)
                {
                    return Task.FromResult<IReadOnlyList<string>>([$"[server] no gameplay loadouts available for slot {slot}."]);
                }

                var lines = new List<string>
                {
                    $"[server] loadouts | slot={slot} | class={player.PlayerClass.Value} | selected={player.GameplayLoadoutId}"
                };
                for (var index = 0; index < loadouts.Count; index += 1)
                {
                    var loadout = loadouts[index];
                    var selectedMarker = loadout.IsSelected ? "*" : " ";
                    lines.Add(
                        $"[server] {selectedMarker} {index + 1}. {loadout.DisplayName} | id={loadout.LoadoutId} | " +
                        $"primary={loadout.PrimaryItemId} | secondary={loadout.SecondaryItemId ?? "-"} | utility={loadout.UtilityItemId ?? "-"}");
                }

                return Task.FromResult<IReadOnlyList<string>>(lines);
            },
            OpenGarrisonServerAdminPermissions.ViewServerState);
        commandRegistry.RegisterBuiltIn(
            "loadout",
            "Set a player's gameplay loadout by id, display name, or numeric choice.",
            "loadout <slot> <id|name|index>",
            (context, arguments, _) =>
            {
                if (!TryParseSlotAndRequiredArgument(arguments, out var slot, out var loadoutSelection))
                {
                    return Task.FromResult<IReadOnlyList<string>>(["[server] usage: loadout <slot> <id|name|index>"]);
                }

                return Task.FromResult<IReadOnlyList<string>>(context.AdminOperations.TrySetGameplayLoadout(slot, loadoutSelection)
                    ? [$"[server] slot {slot} loadout updated."]
                    : [$"[server] unable to set loadout for slot {slot}."]);
            },
            OpenGarrisonServerAdminPermissions.ManagePlayers);
        commandRegistry.RegisterBuiltIn(
            "equipslot",
            "Set a player's gameplay equipped slot.",
            "equipslot <slot> <primary|secondary|utility>",
            (context, arguments, _) =>
            {
                if (!TryParseSlotAndRequiredArgument(arguments, out var slot, out var slotText)
                    || !Enum.TryParse<GameplayEquipmentSlot>(slotText, ignoreCase: true, out var equippedSlot))
                {
                    return Task.FromResult<IReadOnlyList<string>>(["[server] usage: equipslot <slot> <primary|secondary|utility>"]);
                }

                return Task.FromResult<IReadOnlyList<string>>(context.AdminOperations.TrySetGameplayEquippedSlot(slot, equippedSlot)
                    ? [$"[server] slot {slot} equipped slot set to {equippedSlot}."]
                    : [$"[server] unable to set equipped slot for slot {slot}."]);
            },
            OpenGarrisonServerAdminPermissions.ManagePlayers);
        commandRegistry.RegisterBuiltIn(
            "kill",
            "Kill yourself or a playable slot's current character.",
            "kill [self|slot|name]",
            (context, arguments, _) =>
            {
                if (!TryResolveTargetSlot(context, arguments, allowSelf: true, out var slot, out var isSelf))
                {
                    return Task.FromResult<IReadOnlyList<string>>(["[server] usage: kill [self|slot|name]"]);
                }

                if (!isSelf && !TryRequireCheats(context, "kill"))
                {
                    return Task.FromResult<IReadOnlyList<string>>(["[server] kill on another player requires sv_cheats 1."]);
                }

                return Task.FromResult<IReadOnlyList<string>>(context.AdminOperations.TryForceKill(slot)
                    ? [$"[server] killed slot {slot}."]
                    : [$"[server] unable to kill slot {slot}."]);
            },
            OpenGarrisonServerAdminPermissions.ManagePlayers);
        RegisterCheatCommands();
        commandRegistry.RegisterBuiltIn(
            "changemap",
            "Change to another map.",
            "changemap <mapName> [area]",
            (context, arguments, _) =>
            {
                if (!TryParseMapChangeArguments(arguments, out var levelName, out var areaIndex))
                {
                    return Task.FromResult<IReadOnlyList<string>>(["[server] usage: changemap <mapName> [area]"]);
                }

                return Task.FromResult<IReadOnlyList<string>>(context.AdminOperations.TryChangeMap(levelName, areaIndex, preservePlayerStats: false)
                    ? [$"[server] changed map to {levelName} area {areaIndex}."]
                    : [$"[server] unable to change map to {levelName} area {areaIndex}."]);
            },
            OpenGarrisonServerAdminPermissions.ManageMatch,
            "mapchange");
        commandRegistry.RegisterBuiltIn(
            "bots",
            "List current bots.",
            "bots",
            (context, arguments, _) =>
            {
                var botSlots = GetBotSlots(context);
                if (botSlots.Count == 0)
                {
                    return Task.FromResult<IReadOnlyList<string>>(["[server] no bots active."]);
                }

                var lines = new List<string> { $"[server] bots | count={botSlots.Count}" };
                foreach (var (slot, team, playerClass, displayName) in botSlots)
                {
                    lines.Add($"[server] bot | slot={slot} | team={team} | class={playerClass} | name={displayName}");
                }
                return Task.FromResult<IReadOnlyList<string>>(lines);
            },
            OpenGarrisonServerAdminPermissions.ManagePlayers);
        commandRegistry.RegisterBuiltIn(
            "bots add",
            "Add a bot to a specific slot.",
            "bots add <slot> <red|blue> <scout|soldier|medic|...>",
            (context, arguments, _) =>
            {
                if (!TryParseBotAddArguments(arguments, out var slot, out var team, out var playerClass, out var displayName))
                {
                    return Task.FromResult<IReadOnlyList<string>>(["[server] usage: bots add <slot> <red|blue> <class>"]);
                }

                return Task.FromResult<IReadOnlyList<string>>(context.AdminOperations.TryAddBot(slot, team, playerClass, displayName)
                    ? [$"[server] bot added at slot {slot}."]
                    : [$"[server] unable to add bot at slot {slot} (slot may be occupied or invalid)."]);
            },
            OpenGarrisonServerAdminPermissions.ManagePlayers);
        commandRegistry.RegisterBuiltIn(
            "bots remove",
            "Remove a bot from a slot.",
            "bots remove <slot>",
            (context, arguments, _) =>
            {
                if (!TryParseSlot(arguments, out var slot))
                {
                    return Task.FromResult<IReadOnlyList<string>>(["[server] usage: bots remove <slot>"]);
                }

                return Task.FromResult<IReadOnlyList<string>>(context.AdminOperations.TryRemoveBot(slot)
                    ? [$"[server] bot removed from slot {slot}."]
                    : [$"[server] no bot at slot {slot}."]);
            },
            OpenGarrisonServerAdminPermissions.ManagePlayers);
        commandRegistry.RegisterBuiltIn(
            "bots fill",
            "Fill team slots with bots.",
            "bots fill <count> [red|blue] [class]",
            (context, arguments, _) =>
            {
                var parts = arguments.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length == 0 || !int.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var count) || count < 1)
                {
                    return Task.FromResult<IReadOnlyList<string>>(["[server] usage: bots fill <count> [red|blue] [class]"]);
                }

                PlayerTeam? targetTeam = null;
                PlayerClass? requestedClass = null;
                if (parts.Length > 1)
                {
                    if (TryParseTeam(parts[1], out var team))
                    {
                        targetTeam = team;
                    }
                    else if (Enum.TryParse<PlayerClass>(parts[1], ignoreCase: true, out var classFromSecondToken))
                    {
                        requestedClass = classFromSecondToken;
                    }
                    else
                    {
                        return Task.FromResult<IReadOnlyList<string>>(["[server] usage: bots fill <count> [red|blue] [class]"]);
                    }
                }

                PlayerClass classFromThirdToken = default;
                if (parts.Length > 2
                    && (!Enum.TryParse<PlayerClass>(parts[2], ignoreCase: true, out classFromThirdToken)
                        || requestedClass.HasValue))
                {
                    return Task.FromResult<IReadOnlyList<string>>(["[server] usage: bots fill <count> [red|blue] [class]"]);
                }

                if (parts.Length > 2)
                {
                    requestedClass = classFromThirdToken;
                }

                var addedCount = 0;

                if (targetTeam.HasValue)
                {
                    addedCount = context.AdminOperations.TryFillBotTeam(targetTeam.Value, count, requestedClass);
                    return Task.FromResult<IReadOnlyList<string>>([$"[server] filled {addedCount} bot slots on {targetTeam.Value} team."]);
                }
                else
                {
                    var totalAdded = context.AdminOperations.TryFillBots(count, requestedClass);
                    return Task.FromResult<IReadOnlyList<string>>([$"[server] filled {totalAdded} bot slots total ({count} per team)."]);
                }
            },
            OpenGarrisonServerAdminPermissions.ManagePlayers);
        commandRegistry.RegisterBuiltIn(
            "bots clear",
            "Remove all bots.",
            "bots clear",
            (context, arguments, _) =>
            {
                var removed = context.AdminOperations.TryClearAllBots();
                return Task.FromResult<IReadOnlyList<string>>([$"[server] removed {removed} bots."]);
            },
            OpenGarrisonServerAdminPermissions.ManagePlayers);
    }

    private void RegisterCheatCommands()
    {
        commandRegistry.RegisterBuiltIn(
            "explode",
            "Explode yourself or a playable player.",
            "explode [self|slot|name]",
            (context, arguments, _) =>
            {
                if (!TryResolveTargetSlot(context, arguments, allowSelf: true, out var slot, out var isSelf))
                {
                    return Task.FromResult<IReadOnlyList<string>>(["[server] usage: explode [self|slot|name]"]);
                }

                if (!isSelf && !TryRequireCheats(context, "explode"))
                {
                    return Task.FromResult<IReadOnlyList<string>>(["[server] explode on another player requires sv_cheats 1."]);
                }

                return Task.FromResult<IReadOnlyList<string>>(context.AdminOperations.TryExplodePlayer(slot)
                    ? [$"[server] exploded slot {slot}."]
                    : [$"[server] unable to explode slot {slot}."]);
            },
            OpenGarrisonServerAdminPermissions.ManagePlayers);
        commandRegistry.RegisterBuiltIn(
            "build_jump_pad",
            "Build a jump pad at your current position.",
            "build_jump_pad",
            (context, _, _) => Task.FromResult<IReadOnlyList<string>>(TryRequireSourceSlot(context, out var slot)
                && TryRequireCheats(context, "build_jump_pad")
                && context.AdminOperations.TryBuildJumpPad(slot)
                    ? ["[server] jump pad built."]
                    : ["[server] unable to build a jump pad."]),
            OpenGarrisonServerAdminPermissions.ManagePlayers);
        commandRegistry.RegisterBuiltIn(
            "spawn_mimic",
            "Spawn a bot that mirrors a player's input.",
            "spawn_mimic <enemy|friendly> <class>",
            (context, arguments, _) =>
            {
                if (!TryRequireCheats(context, "spawn_mimic")
                    || !TryRequireSourceSlot(context, out var sourceSlot)
                    || !TryParseMimicArguments(context, arguments, sourceSlot, out var team, out var playerClass))
                {
                    return Task.FromResult<IReadOnlyList<string>>(["[server] usage: spawn_mimic <enemy|friendly> <class>"]);
                }

                return Task.FromResult<IReadOnlyList<string>>(context.AdminOperations.TryAddMimicBot(sourceSlot, team, playerClass, "", out var botSlot)
                    ? [$"[server] mimic bot spawned at slot {botSlot}."]
                    : ["[server] unable to spawn mimic bot."]);
            },
            OpenGarrisonServerAdminPermissions.ManagePlayers);
        commandRegistry.RegisterBuiltIn(
            "spawn_followhealer",
            "Spawn a Medic bot that follows and heals you.",
            "spawn_followhealer",
            (context, _, _) => Task.FromResult<IReadOnlyList<string>>(TryRequireSourceSlot(context, out var targetSlot)
                && TryRequireCheats(context, "spawn_followhealer")
                && context.AdminOperations.TryAddFollowHealerBot(targetSlot, "", out var botSlot)
                    ? [$"[server] follow healer spawned at slot {botSlot}."]
                    : ["[server] unable to spawn follow healer."]),
            OpenGarrisonServerAdminPermissions.ManagePlayers);
        commandRegistry.RegisterBuiltIn(
            "noclip",
            "Toggle noclip for yourself or a player.",
            "noclip [slot|name]",
            (context, arguments, _) => Task.FromResult<IReadOnlyList<string>>(TryResolveTargetSlot(context, arguments, allowSelf: true, out var slot, out var ignoredIsSelf)
                && TryRequireCheats(context, "noclip")
                && context.AdminOperations.TryTogglePlayerNoclip(slot, out var enabled)
                    ? [$"[server] noclip {(enabled ? "enabled" : "disabled")} for slot {slot}."]
                    : ["[server] unable to toggle noclip."]),
            OpenGarrisonServerAdminPermissions.ManagePlayers);
        commandRegistry.RegisterBuiltIn(
            "set_speed",
            "Set a player's movement speed multiplier.",
            "set_speed [slot|name] [scale]",
            (context, arguments, _) =>
            {
                if (!TryParseTargetAndScale(context, arguments, out var slot, out var scale)
                    || !TryRequireCheats(context, "set_speed"))
                {
                    return Task.FromResult<IReadOnlyList<string>>(["[server] usage: set_speed [slot|name] [scale]"]);
                }

                return Task.FromResult<IReadOnlyList<string>>(context.AdminOperations.TrySetPlayerMovementSpeedScale(slot, scale)
                    ? [$"[server] speed for slot {slot} set to {scale:G4}x."]
                    : [$"[server] unable to set speed for slot {slot}."]);
            },
            OpenGarrisonServerAdminPermissions.ManagePlayers);
        commandRegistry.RegisterBuiltIn(
            "set_respawnpos",
            "Set your next respawn position.",
            "set_respawnpos <x> <y>",
            (context, arguments, _) =>
            {
                if (!TryRequireSourceSlot(context, out var slot)
                    || !TryParsePosition(arguments, out var x, out var y)
                    || !TryRequireCheats(context, "set_respawnpos"))
                {
                    return Task.FromResult<IReadOnlyList<string>>(["[server] usage: set_respawnpos <x> <y>"]);
                }

                return Task.FromResult<IReadOnlyList<string>>(context.AdminOperations.TrySetPlayerRespawnPosition(slot, x, y)
                    ? [$"[server] respawn position for slot {slot} set to {x:G4}, {y:G4}."]
                    : ["[server] unable to set respawn position."]);
            },
            OpenGarrisonServerAdminPermissions.ManagePlayers);
        commandRegistry.RegisterBuiltIn(
            "tpme",
            "Teleport yourself to another player.",
            "tpme <slot|name>",
            (context, arguments, _) =>
            {
                if (!TryRequireSourceSlot(context, out var sourceSlot)
                    || !TryResolveTargetSlot(context, arguments, allowSelf: false, out var targetSlot, out var ignoredIsSelf)
                    || !TryRequireCheats(context, "tpme"))
                {
                    return Task.FromResult<IReadOnlyList<string>>(["[server] usage: tpme <slot|name>"]);
                }

                return Task.FromResult<IReadOnlyList<string>>(context.AdminOperations.TryTeleportPlayerToPlayer(sourceSlot, targetSlot)
                    ? [$"[server] teleported slot {sourceSlot} to slot {targetSlot}."]
                    : ["[server] unable to teleport player."]);
            },
            OpenGarrisonServerAdminPermissions.ManagePlayers);
        commandRegistry.RegisterBuiltIn(
            "freeze",
            "Toggle a player's movement and actions.",
            "freeze [slot|name]",
            (context, arguments, _) => Task.FromResult<IReadOnlyList<string>>(TryResolveTargetSlot(context, arguments, allowSelf: true, out var slot, out var ignoredIsSelf)
                && TryRequireCheats(context, "freeze")
                && context.AdminOperations.TryTogglePlayerFrozen(slot, out var frozen)
                    ? [$"[server] freeze {(frozen ? "enabled" : "disabled")} for slot {slot}."]
                    : ["[server] unable to toggle freeze."]),
            OpenGarrisonServerAdminPermissions.ManagePlayers);
        commandRegistry.RegisterBuiltIn(
            "resize",
            "Make a player tiny.",
            "resize [slot|name]",
            (context, arguments, _) => Task.FromResult<IReadOnlyList<string>>(TryResolveTargetSlot(context, arguments, allowSelf: true, out var slot, out var ignoredIsSelf)
                && TryRequireCheats(context, "resize")
                && context.AdminOperations.TrySetPlayerScale(slot, 0.5f)
                    ? [$"[server] resized slot {slot}."]
                    : ["[server] unable to resize player."]),
            OpenGarrisonServerAdminPermissions.ManagePlayers);
        commandRegistry.RegisterBuiltIn(
            "stun",
            "Stun a player for a duration.",
            "stun [slot|name] [seconds]",
            (context, arguments, _) =>
            {
                if (!TryParseTargetAndDuration(context, arguments, out var slot, out var seconds)
                    || !TryRequireCheats(context, "stun"))
                {
                    return Task.FromResult<IReadOnlyList<string>>(["[server] usage: stun [slot|name] [seconds]"]);
                }

                return Task.FromResult<IReadOnlyList<string>>(context.AdminOperations.TryStunPlayer(slot, seconds)
                    ? [$"[server] stunned slot {slot} for {seconds:G4}s."]
                    : ["[server] unable to stun player."]);
            },
            OpenGarrisonServerAdminPermissions.ManagePlayers);
        commandRegistry.RegisterBuiltIn(
            "execute",
            "Execute a Lua chunk through the loaded server Lua plugin.",
            "execute <lua>",
            (context, arguments, _) =>
            {
                if (executeLuaCommand is null)
                {
                    return Task.FromResult<IReadOnlyList<string>>(["[server] Lua execution is unavailable."]);
                }

                if (string.IsNullOrWhiteSpace(arguments))
                {
                    return Task.FromResult<IReadOnlyList<string>>(["[server] usage: execute <lua>"]);
                }

                var response = executeLuaCommand(context, arguments.Trim());
                return Task.FromResult<IReadOnlyList<string>>(response.Count == 0
                    ? ["[server] Lua executed."]
                    : response);
            },
            OpenGarrisonServerAdminPermissions.ManagePlugins);
        commandRegistry.RegisterBuiltIn(
            "reload_lua_plugins",
            "Reload loaded Lua server plugins.",
            "reload_lua_plugins",
            (_, _, _) => Task.FromResult(reloadLuaPlugins?.Invoke() ?? ["[server] Lua plugin reload is unavailable."]),
            OpenGarrisonServerAdminPermissions.ManagePlugins,
            "lua_reload",
            "reload_plugins");
    }

    private List<string> BuildHelpLines(OpenGarrisonServerCommandContext context)
    {
        var lines = new List<string>
        {
            "[server] commands:",
        };
        foreach (var command in commandRegistry.GetPrimaryCommands().Where(command => context.HasPermission(command.RequiredPermissions)))
        {
            var ownerSuffix = command.IsBuiltIn ? string.Empty : $" [plugin:{command.OwnerId}]";
            var permissionSuffix = command.RequiredPermissions == OpenGarrisonServerAdminPermissions.None
                ? string.Empty
                : $" [perm:{command.RequiredPermissions}]";
            lines.Add($"[server]   {command.Name} - {command.Description} ({command.Usage}){ownerSuffix}{permissionSuffix}");
        }

        lines.Add("[server] shutdown is handled directly by the host console/admin pipe.");
        return lines;
    }

    private List<string> BuildPluginLines()
    {
        var pluginIds = loadedPluginIdsProvider();
        if (pluginIds.Count == 0)
        {
            return ["[server] plugins | count=0"];
        }

        return
        [
            $"[server] plugins | count={pluginIds.Count}",
            .. pluginIds.Select(pluginId => $"[server] plugin | id={pluginId}")
        ];
    }

    private List<string> BuildCvarLines(string arguments)
    {
        var filter = arguments.Trim();
        var entries = cvarRegistry.GetAll(includeProtectedValues: true)
            .Where(entry => filter.Length == 0
                || entry.Name.Contains(filter, StringComparison.OrdinalIgnoreCase)
                || entry.Description.Contains(filter, StringComparison.OrdinalIgnoreCase))
            .OrderBy(entry => entry.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (entries.Length == 0)
        {
            return [$"[server] cvars | count=0 | filter={filter}"];
        }

        var lines = new List<string>
        {
            $"[server] cvars | count={entries.Length}"
        };
        foreach (var entry in entries)
        {
            var bounds = entry.MinimumNumericValue.HasValue || entry.MaximumNumericValue.HasValue
                ? $" | bounds={entry.MinimumNumericValue?.ToString(CultureInfo.InvariantCulture) ?? "-"}..{entry.MaximumNumericValue?.ToString(CultureInfo.InvariantCulture) ?? "-"}"
                : string.Empty;
            var flags = $"{(entry.IsReadOnly ? "readonly" : "mutable")},{(entry.IsProtected ? "protected" : "public")}";
            lines.Add(
                $"[server] cvar | name={entry.Name} | value={entry.CurrentValue} | type={entry.ValueType} | default={entry.DefaultValue} | flags={flags}{bounds}");
        }

        return lines;
    }

    private IReadOnlyList<string> ExecuteCvarCommand(string arguments)
    {
        var trimmed = arguments.Trim();
        if (trimmed.Length == 0)
        {
            return ["[server] usage: cvar <name> [value]"];
        }

        var parts = trimmed.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
        var name = parts[0];
        if (parts.Length == 1)
        {
            return cvarRegistry.TryGet(name, includeProtectedValue: true, out var existing)
                ? [$"[server] cvar | name={existing.Name} | value={existing.CurrentValue} | default={existing.DefaultValue} | type={existing.ValueType} | protected={(existing.IsProtected ? "yes" : "no")} | readonly={(existing.IsReadOnly ? "yes" : "no")}"]
                : [$"[server] unknown cvar \"{name}\"."];
        }

        return cvarRegistry.TrySet(name, parts[1], allowProtectedMutation: true, out var updated, out var error)
            ? [$"[server] cvar {updated.Name} set to {updated.CurrentValue}."]
            : [$"[server] unable to set cvar \"{name}\": {error}"];
    }

    private IReadOnlyList<string> BuildTimerLines()
    {
        var tasks = scheduler.GetScheduledTasks();
        if (tasks.Count == 0)
        {
            return ["[server] timers | count=0"];
        }

        return
        [
            $"[server] timers | count={tasks.Count}",
            .. tasks.Select(task =>
                $"[server] timer | id={task.TimerId} | type={(task.IsRepeating ? "repeat" : "once")} | interval={task.Interval.TotalSeconds:0.##}s | dueIn={(task.DueIn ?? TimeSpan.Zero).TotalSeconds:0.##}s | description={task.Description}")
        ];
    }

    private static List<string> BuildSummaryLines(params Action<List<string>>[] addSummarySections)
    {
        var lines = new List<string>();
        for (var index = 0; index < addSummarySections.Length; index += 1)
        {
            addSummarySections[index](lines);
        }

        return lines;
    }

    private IReadOnlyList<string> ExecuteFrameInfoCommand(string arguments)
    {
        var operation = string.IsNullOrWhiteSpace(arguments)
            ? "status"
            : arguments.Trim().ToLowerInvariant();
        if (operation is not ("on" or "off" or "toggle" or "status"))
        {
            return ["[server] usage: frameinfo [on|off|toggle|status]"];
        }

        if (!cvarRegistry.TryGet("sv_frame_info", includeProtectedValue: true, out var cvar))
        {
            return ["[server] frame info cvar is unavailable."];
        }

        if (operation == "status")
        {
            return [$"[server] frame info {(IsCvarEnabled(cvar.CurrentValue) ? "enabled" : "disabled")}."];
        }

        var enabled = operation == "toggle"
            ? !IsCvarEnabled(cvar.CurrentValue)
            : operation == "on";
        if (!cvarRegistry.TrySet(
                "sv_frame_info",
                enabled ? "true" : "false",
                allowProtectedMutation: true,
                out _,
                out var error))
        {
            return [$"[server] unable to change frame info: {error}"];
        }

        return [$"[server] frame info {(enabled ? "enabled" : "disabled")}."];
    }

    private static bool IsCvarEnabled(string value)
    {
        return value.Equals("1", StringComparison.OrdinalIgnoreCase)
            || value.Equals("true", StringComparison.OrdinalIgnoreCase)
            || value.Equals("on", StringComparison.OrdinalIgnoreCase)
            || value.Equals("yes", StringComparison.OrdinalIgnoreCase);
    }

    private static bool TryRequireCheats(OpenGarrisonServerCommandContext context, string commandName)
    {
        if (!context.Cvars.TryGet("sv_cheats", includeProtectedValue: true, out var cvar))
        {
            return false;
        }

        return cvar.CurrentValue.Equals("1", StringComparison.OrdinalIgnoreCase)
            || bool.TryParse(cvar.CurrentValue, out var enabled) && enabled;
    }

    private static bool TryRequireSourceSlot(OpenGarrisonServerCommandContext context, out byte slot)
    {
        slot = context.Identity.SourceSlot.GetValueOrDefault();
        return context.Identity.SourceSlot.HasValue && SimulationWorld.IsPlayableNetworkPlayerSlot(slot);
    }

    private static bool TryResolveTargetSlot(
        OpenGarrisonServerCommandContext context,
        string arguments,
        bool allowSelf,
        out byte slot,
        out bool isSelf)
    {
        slot = 0;
        isSelf = false;
        var token = arguments.Trim();
        if (token.Length == 0 || token.Equals("self", StringComparison.OrdinalIgnoreCase) || token.Equals("me", StringComparison.OrdinalIgnoreCase))
        {
            if (!allowSelf || !TryRequireSourceSlot(context, out slot))
            {
                return false;
            }

            isSelf = true;
            return true;
        }

        var firstToken = token.Split(' ', StringSplitOptions.RemoveEmptyEntries)[0];
        if (TryParseSlot(firstToken, out slot))
        {
            var requestedSlot = slot;
            return context.ServerState.GetPlayers().Any(player => player.Slot == requestedSlot);
        }

        var exactName = context.ServerState.GetPlayers()
            .FirstOrDefault(player => player.Name.Equals(token, StringComparison.OrdinalIgnoreCase));
        if (!string.IsNullOrWhiteSpace(exactName.Name))
        {
            slot = exactName.Slot;
            return true;
        }

        var partial = context.ServerState.GetPlayers()
            .Where(player => player.Name.Contains(token, StringComparison.OrdinalIgnoreCase))
            .ToArray();
        if (partial.Length == 1)
        {
            slot = partial[0].Slot;
            return true;
        }

        return false;
    }

    private static bool TryParseMimicArguments(
        OpenGarrisonServerCommandContext context,
        string arguments,
        byte sourceSlot,
        out PlayerTeam team,
        out PlayerClass playerClass)
    {
        team = default;
        playerClass = default;
        var parts = arguments.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 2 || !Enum.TryParse<PlayerClass>(parts[1], true, out playerClass))
        {
            return false;
        }

        var source = context.ServerState.GetPlayers().FirstOrDefault(player => player.Slot == sourceSlot);
        if (!source.Team.HasValue)
        {
            return false;
        }

        if (parts[0].Equals("friendly", StringComparison.OrdinalIgnoreCase))
        {
            team = source.Team.Value;
            return true;
        }

        if (parts[0].Equals("enemy", StringComparison.OrdinalIgnoreCase))
        {
            team = source.Team.Value == PlayerTeam.Red ? PlayerTeam.Blue : PlayerTeam.Red;
            return true;
        }

        return false;
    }

    private static bool TryParseTargetAndScale(OpenGarrisonServerCommandContext context, string arguments, out byte slot, out float scale)
    {
        slot = 0;
        scale = 2f;
        var parts = arguments.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0)
        {
            return TryRequireSourceSlot(context, out slot);
        }

        if (parts.Length == 1 && float.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out scale))
        {
            return TryRequireSourceSlot(context, out slot) && scale is >= 0.1f and <= 4f;
        }

        if (!TryResolveTargetSlot(context, parts[0], allowSelf: true, out slot, out _))
        {
            return false;
        }

        return parts.Length < 2
            || float.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out scale) && scale is >= 0.1f and <= 4f;
    }

    private static bool TryParseTargetAndDuration(OpenGarrisonServerCommandContext context, string arguments, out byte slot, out float seconds)
    {
        slot = 0;
        seconds = 2f;
        var parts = arguments.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0)
        {
            return TryRequireSourceSlot(context, out slot);
        }

        if (parts.Length == 1 && float.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out seconds))
        {
            return TryRequireSourceSlot(context, out slot) && seconds is > 0f and <= 60f;
        }

        if (!TryResolveTargetSlot(context, parts[0], allowSelf: true, out slot, out _))
        {
            return false;
        }

        return parts.Length < 2
            || float.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out seconds) && seconds is > 0f and <= 60f;
    }

    private static bool TryParsePosition(string arguments, out float x, out float y)
    {
        x = 0f;
        y = 0f;
        var normalized = arguments.Trim().Trim('(', ')');
        var parts = normalized.Replace(',', ' ').Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length == 2
            && float.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out x)
            && float.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out y);
    }

    private static bool TryParseBoundedInt(string text, int min, int max, out int value)
    {
        value = 0;
        var trimmed = text.Trim();
        return int.TryParse(trimmed, NumberStyles.Integer, CultureInfo.InvariantCulture, out value)
            && value >= min
            && value <= max;
    }

    private static bool TryParseSlot(string text, out byte slot)
    {
        slot = 0;
        var trimmed = text.Trim();
        return byte.TryParse(trimmed, NumberStyles.Integer, CultureInfo.InvariantCulture, out slot) && slot > 0;
    }

    private static bool TryParseSlotAndOptionalArgument(string arguments, out byte slot, out string argument)
    {
        slot = 0;
        argument = string.Empty;
        var parts = arguments.Trim().Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0 || !TryParseSlot(parts[0], out slot))
        {
            return false;
        }

        argument = parts.Length > 1 ? parts[1].Trim() : string.Empty;
        return true;
    }

    private static bool TryParseSlotAndRequiredArgument(string arguments, out byte slot, out string argument)
    {
        slot = 0;
        argument = string.Empty;
        var parts = arguments.Trim().Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 2 || !TryParseSlot(parts[0], out slot))
        {
            return false;
        }

        argument = parts[1].Trim();
        return argument.Length > 0;
    }

    private static bool TryParseTeam(string text, out PlayerTeam team)
    {
        team = default;
        var normalized = text.Trim();
        if (normalized.Equals("red", StringComparison.OrdinalIgnoreCase))
        {
            team = PlayerTeam.Red;
            return true;
        }

        if (normalized.Equals("blue", StringComparison.OrdinalIgnoreCase) || normalized.Equals("blu", StringComparison.OrdinalIgnoreCase))
        {
            team = PlayerTeam.Blue;
            return true;
        }

        return false;
    }

    private static bool TryParseMapChangeArguments(string arguments, out string levelName, out int areaIndex)
    {
        levelName = string.Empty;
        areaIndex = 1;
        var parts = arguments.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0)
        {
            return false;
        }

        levelName = parts[0].Trim();
        if (levelName.Length == 0)
        {
            return false;
        }

        if (parts.Length < 2)
        {
            return true;
        }

        return int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out areaIndex) && areaIndex >= 1;
    }

    private static bool TryParseBotAddArguments(string arguments, out byte slot, out PlayerTeam team, out PlayerClass playerClass, out string displayName)
    {
        slot = 0;
        team = default;
        playerClass = default;
        displayName = string.Empty;
        var parts = arguments.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 3)
        {
            return false;
        }

        if (!TryParseSlot(parts[0], out slot))
        {
            return false;
        }

        if (!TryParseTeam(parts[1], out team))
        {
            return false;
        }

        if (!Enum.TryParse<PlayerClass>(parts[2], ignoreCase: true, out playerClass))
        {
            return false;
        }

        // Display name is optional; an empty value lets the bot manager assign from the practice name pool.
        if (parts.Length > 3)
        {
            displayName = parts[3].Trim();
        }
        else
        {
            displayName = string.Empty;
        }

        return true;
    }

    private static List<(byte Slot, PlayerTeam Team, PlayerClass PlayerClass, string DisplayName)> GetBotSlots(OpenGarrisonServerCommandContext context)
    {
        return context.AdminOperations
            .GetBotSlots()
            .Select(slot => (slot.Slot, slot.Team, slot.PlayerClass, slot.DisplayName))
            .ToList();
    }
}
