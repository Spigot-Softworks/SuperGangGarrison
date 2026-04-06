local plugin = {}

local default_config = {
    voteDurationSeconds = 30,
    cooldownSeconds = 20,
    minimumEligiblePlayers = 1,
    allowSpectatorsToVote = false
}

local config = default_config
local active_vote = nil
local cooldown_ends_at = 0

local function now()
    return plugin.host.get_utc_unix_time()
end

local function trim(text)
    return (text or ""):gsub("^%s+", ""):gsub("%s+$", "")
end

local function starts_with(text, prefix)
    return text:sub(1, #prefix) == prefix
end

local function split_first_word(text)
    local normalized = trim(text)
    local space_index = normalized:find("%s")
    if space_index == nil then
        return normalized, ""
    end

    return normalized:sub(1, space_index - 1), trim(normalized:sub(space_index + 1))
end

local function clamp(value, minimum, maximum)
    if value < minimum then
        return minimum
    end

    if value > maximum then
        return maximum
    end

    return value
end

local function load_config()
    local loaded = plugin.host.load_json_config("chat-voting.json", default_config)
    local normalized = {
        voteDurationSeconds = clamp(tonumber(loaded.voteDurationSeconds) or default_config.voteDurationSeconds, 10, 300),
        cooldownSeconds = clamp(tonumber(loaded.cooldownSeconds) or default_config.cooldownSeconds, 0, 600),
        minimumEligiblePlayers = clamp(tonumber(loaded.minimumEligiblePlayers) or default_config.minimumEligiblePlayers, 1, 32),
        allowSpectatorsToVote = loaded.allowSpectatorsToVote == true
    }
    plugin.host.save_json_config("chat-voting.json", normalized)
    return normalized
end

local function parse_command(text)
    local normalized = trim(text)
    if normalized == "" or not starts_with(normalized, "!") then
        return nil, nil
    end

    local command_name, arguments = split_first_word(normalized:sub(2))
    if command_name == "" then
        return nil, nil
    end

    return string.lower(command_name), arguments
end

local function find_player(slot)
    local players = plugin.host.get_players()
    for _, player in ipairs(players) do
        if player.slot == slot then
            return player
        end
    end

    return nil
end

local function is_player_eligible(player)
    return player ~= nil
        and player.isAuthorized
        and (config.allowSpectatorsToVote or not player.isSpectator)
end

local function get_eligible_players()
    local result = {}
    for _, player in ipairs(plugin.host.get_players()) do
        if is_player_eligible(player) then
            table.insert(result, player)
        end
    end

    return result
end

local function get_eligible_count()
    return #get_eligible_players()
end

local function get_required_yes_votes(eligible_count)
    return math.max(1, math.floor(eligible_count / 2) + 1)
end

local function format_map_label(level_name, area_index, area_count)
    if area_count ~= nil and area_count > 1 then
        return level_name .. " area " .. tostring(area_index) .. "/" .. tostring(area_count)
    end

    return level_name
end

local function clear_active_vote(start_cooldown)
    active_vote = nil
    if start_cooldown then
        cooldown_ends_at = now() + config.cooldownSeconds
    end
end

local function build_vote_counts_label()
    if active_vote == nil then
        return "0/0 yes, 0 no"
    end

    return tostring(active_vote.yes_count) .. "/" .. tostring(get_eligible_count()) .. " yes, " .. tostring(active_vote.no_count) .. " no"
end

local function build_vote_summary()
    local seconds_remaining = math.max(0, active_vote.expires_at - now())
    local action_label = active_vote.kind == "change_map_now" and "votemap" or "votenextround"
    return action_label .. " for "
        .. format_map_label(active_vote.level_name, active_vote.area_index, active_vote.area_count)
        .. " by " .. active_vote.initiator_name
        .. " (" .. build_vote_counts_label() .. ", " .. tostring(seconds_remaining) .. "s left)"
end

local function fail_vote(reason)
    plugin.host.broadcast_system_message(
        reason .. " for "
        .. format_map_label(active_vote.level_name, active_vote.area_index, active_vote.area_count)
        .. " (" .. build_vote_counts_label() .. ").")
    clear_active_vote(true)
end

local function pass_vote()
    local label = format_map_label(active_vote.level_name, active_vote.area_index, active_vote.area_count)
    local success
    if active_vote.kind == "change_map_now" then
        success = plugin.host.try_change_map(active_vote.level_name, active_vote.area_index, false)
        plugin.host.broadcast_system_message(
            success
                and ("Vote passed for " .. label .. ". Changing map now.")
                or ("Vote passed for " .. label .. ", but the action could not be applied."))
    else
        success = plugin.host.try_set_next_round_map(active_vote.level_name, active_vote.area_index)
        plugin.host.broadcast_system_message(
            success
                and ("Vote passed for " .. label .. ". It will be played next round.")
                or ("Vote passed for " .. label .. ", but the action could not be applied."))
    end

    clear_active_vote(true)
end

local function recount_votes()
    local eligible_slots = {}
    for _, player in ipairs(get_eligible_players()) do
        eligible_slots[player.slot] = true
    end

    local yes_count = 0
    local no_count = 0
    for slot, _ in pairs(active_vote.yes_votes) do
        if eligible_slots[slot] then
            yes_count = yes_count + 1
        else
            active_vote.yes_votes[slot] = nil
        end
    end

    for slot, _ in pairs(active_vote.no_votes) do
        if eligible_slots[slot] then
            no_count = no_count + 1
        else
            active_vote.no_votes[slot] = nil
        end
    end

    active_vote.yes_count = yes_count
    active_vote.no_count = no_count
end

local function resolve_vote_if_possible()
    if active_vote == nil then
        return
    end

    recount_votes()
    local eligible_count = get_eligible_count()
    if eligible_count < config.minimumEligiblePlayers then
        fail_vote("Vote canceled: not enough eligible players remain")
        return
    end

    local required_yes_votes = get_required_yes_votes(eligible_count)
    if active_vote.yes_count >= required_yes_votes then
        pass_vote()
        return
    end

    local max_possible_yes_votes = eligible_count - active_vote.no_count
    if max_possible_yes_votes < required_yes_votes then
        fail_vote("Vote failed")
    end
end

local function expire_vote_if_needed()
    if active_vote ~= nil and now() >= active_vote.expires_at then
        fail_vote("Vote expired")
    end
end

local function try_get_eligible_player(slot)
    local player = find_player(slot)
    if is_player_eligible(player) then
        return player
    end

    return nil
end

local function try_resolve_level(arguments)
    local normalized = trim(arguments)
    if normalized == "" then
        return nil, true
    end

    return plugin.host.try_resolve_level(normalized), false
end

local function try_start_vote(event, arguments, vote_kind)
    local initiator = try_get_eligible_player(event.slot)
    if initiator == nil then
        plugin.host.send_system_message(
            event.slot,
            config.allowSpectatorsToVote and "Only authorized players can start votes." or "Only authorized non-spectators can start votes.")
        return true
    end

    if active_vote ~= nil then
        plugin.host.send_system_message(event.slot, "A vote is already active: " .. build_vote_summary())
        return true
    end

    if now() < cooldown_ends_at then
        plugin.host.send_system_message(event.slot, "Votes are on cooldown for " .. tostring(cooldown_ends_at - now()) .. "s.")
        return true
    end

    local level, is_usage_error = try_resolve_level(arguments)
    if level == nil then
        plugin.host.send_system_message(
            event.slot,
            is_usage_error
                and (vote_kind == "change_map_now" and "Usage: !votemap <mapName> [area]" or "Usage: !votenextround <mapName> [area]")
                or ("Unknown map \"" .. trim(arguments) .. "\"."))
        return true
    end

    local eligible_count = get_eligible_count()
    if eligible_count < config.minimumEligiblePlayers then
        plugin.host.send_system_message(
            event.slot,
            "Need at least " .. tostring(config.minimumEligiblePlayers) .. " eligible players to start a vote.")
        return true
    end

    active_vote = {
        kind = vote_kind,
        level_name = level.name,
        area_index = level.mapAreaIndex,
        area_count = level.mapAreaCount,
        initiator_slot = initiator.slot,
        initiator_name = initiator.name,
        expires_at = now() + config.voteDurationSeconds,
        yes_votes = {},
        no_votes = {},
        yes_count = 1,
        no_count = 0
    }
    active_vote.yes_votes[initiator.slot] = true

    local action_label = vote_kind == "change_map_now" and "votemap" or "votenextround"
    plugin.host.broadcast_system_message(
        initiator.name .. " started " .. action_label .. " for "
        .. format_map_label(active_vote.level_name, active_vote.area_index, active_vote.area_count)
        .. ". Type !vote yes or !vote no. ("
        .. tostring(get_required_yes_votes(eligible_count)) .. " yes needed, "
        .. tostring(config.voteDurationSeconds) .. "s)")
    return true
end

local function try_register_vote(event, arguments)
    if active_vote == nil then
        plugin.host.send_system_message(event.slot, "There is no active vote.")
        return true
    end

    local player = try_get_eligible_player(event.slot)
    if player == nil then
        plugin.host.send_system_message(
            event.slot,
            config.allowSpectatorsToVote and "Only authorized players can vote." or "Only authorized non-spectators can vote.")
        return true
    end

    local normalized = string.lower(trim(arguments))
    local is_yes_vote = normalized == "yes" or normalized == "y"
    local is_no_vote = normalized == "no" or normalized == "n"
    if not is_yes_vote and not is_no_vote then
        plugin.host.send_system_message(event.slot, "Usage: !vote <yes|no>")
        return true
    end

    local already_voted_yes = active_vote.yes_votes[player.slot] == true and active_vote.no_votes[player.slot] ~= true
    local already_voted_no = active_vote.no_votes[player.slot] == true and active_vote.yes_votes[player.slot] ~= true
    if (is_yes_vote and already_voted_yes) or (is_no_vote and already_voted_no) then
        plugin.host.send_system_message(event.slot, "You already voted " .. (is_yes_vote and "yes" or "no") .. ".")
        return true
    end

    active_vote.yes_votes[player.slot] = nil
    active_vote.no_votes[player.slot] = nil
    if is_yes_vote then
        active_vote.yes_votes[player.slot] = true
    else
        active_vote.no_votes[player.slot] = true
    end

    recount_votes()
    plugin.host.broadcast_system_message(
        player.name .. " voted " .. (is_yes_vote and "yes" or "no") .. " (" .. build_vote_counts_label() .. ")")
    resolve_vote_if_possible()
    return true
end

local function handle_vote_status(slot)
    if active_vote == nil then
        plugin.host.send_system_message(slot, "There is no active vote.")
        return true
    end

    plugin.host.send_system_message(slot, build_vote_summary())
    return true
end

local function handle_cancel_vote(event)
    if active_vote == nil then
        plugin.host.send_system_message(event.slot, "There is no active vote.")
        return true
    end

    if active_vote.initiator_slot ~= event.slot then
        plugin.host.send_system_message(event.slot, "Only the player who started the vote can cancel it.")
        return true
    end

    plugin.host.broadcast_system_message(
        active_vote.initiator_name .. " canceled the vote for "
        .. format_map_label(active_vote.level_name, active_vote.area_index, active_vote.area_count) .. ".")
    clear_active_vote(true)
    return true
end

function plugin.initialize(host)
    plugin.host = host
    config = load_config()
    host.log("Lua Chat Voting initialized")
end

function plugin.shutdown()
end

function plugin.on_server_heartbeat()
    expire_vote_if_needed()
end

function plugin.on_client_disconnected(e)
    if active_vote == nil then
        return
    end

    active_vote.yes_votes[e.slot] = nil
    active_vote.no_votes[e.slot] = nil
    resolve_vote_if_possible()
end

function plugin.on_map_changing()
    clear_active_vote(false)
end

function plugin.on_map_changed()
    clear_active_vote(false)
end

function plugin.try_handle_chat_message(_, e)
    expire_vote_if_needed()
    local command_name, arguments = parse_command(e.text)
    if command_name == nil then
        return false
    end

    if command_name == "votemap" then
        return try_start_vote(e, arguments, "change_map_now")
    elseif command_name == "votenextround" then
        return try_start_vote(e, arguments, "change_map_next_round")
    elseif command_name == "vote" then
        return try_register_vote(e, arguments)
    elseif command_name == "yes" then
        return try_register_vote(e, "yes")
    elseif command_name == "no" then
        return try_register_vote(e, "no")
    elseif command_name == "votes" or command_name == "votestatus" then
        return handle_vote_status(e.slot)
    elseif command_name == "cancelvote" then
        return handle_cancel_vote(e)
    end

    return false
end

return plugin
