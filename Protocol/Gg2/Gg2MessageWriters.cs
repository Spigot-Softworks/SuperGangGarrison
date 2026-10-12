using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace OpenGarrison.Protocol.Gg2;

/// <summary>
/// Client-to-server GG2 messages, as GG2's client writes them (ClientInputstate,
/// ClientReserveSlot, PlayerControl and friends). Sizes match the server's command table
/// (GameServerDefineCommands).
/// </summary>
public static class Gg2ClientMessages
{
    private static readonly Encoding Latin1 = Encoding.Latin1;

    /// <summary>HELLO followed by the protocol UUID.</summary>
    public static byte[] Hello()
    {
        var result = new byte[17];
        result[0] = Gg2Protocol.Hello;
        Gg2Protocol.GetProtocolUuidBytes().CopyTo(result, 1);
        return result;
    }

    /// <summary>The reply to PASSWORD_REQUEST: a length byte and the password, no message id.</summary>
    public static byte[] Password(string password)
    {
        var value = Latin1.GetBytes(password ?? string.Empty);
        if (value.Length > byte.MaxValue)
        {
            throw new ArgumentOutOfRangeException(nameof(password), "GG2 passwords cannot exceed 255 bytes.");
        }

        var result = new byte[value.Length + 1];
        result[0] = (byte)value.Length;
        value.CopyTo(result, 1);
        return result;
    }

    public static byte[] ReserveSlot(string playerName) => WithString8(Gg2Protocol.ReserveSlot, TrimName(playerName));

    public static byte[] PlayerJoin() => [Gg2Protocol.PlayerJoin];

    public static byte[] PlayerLeave() => [Gg2Protocol.PlayerLeave];

    /// <summary>GG2 team: 0 red, 1 blue, 2 spectator, 3 any (auto-assign).</summary>
    public static byte[] ChangeTeam(byte team) => [Gg2Protocol.PlayerChangeTeam, team];

    public static byte[] ChangeClass(byte classId)
    {
        if (classId >= Gg2Protocol.ClassCount)
        {
            throw new ArgumentOutOfRangeException(nameof(classId));
        }

        return [Gg2Protocol.PlayerChangeClass, classId];
    }

    public static byte[] ChangeName(string playerName) => WithString8(Gg2Protocol.PlayerChangeName, TrimName(playerName));

    /// <summary>
    /// INPUTSTATE: key bits (see Gg2Protocol.Key*), aim direction as GameMaker degrees * 65536 / 360
    /// from the view centre to the mouse, and the distance halved (capped at 255).
    /// </summary>
    public static byte[] InputState(byte keyState, ushort netAimDirection, byte aimDistanceHalf)
    {
        var result = new byte[5];
        result[0] = Gg2Protocol.InputState;
        result[1] = keyState;
        BinaryPrimitives.WriteUInt16LittleEndian(result.AsSpan(2), netAimDirection);
        result[4] = aimDistanceHalf;
        return result;
    }

    /// <summary>
    /// INPUTSTATE from a screen-space aim vector (x right, y down), the way ClientInputstate
    /// measures from the view centre.
    /// </summary>
    public static byte[] InputStateFromAim(byte keyState, float aimX, float aimY)
    {
        var direction = Math.Atan2(-aimY, aimX);
        if (direction < 0d)
        {
            direction += Math.PI * 2d;
        }

        var netDirection = (ushort)Math.Clamp((int)(direction * 65536d / (Math.PI * 2d)), 0, ushort.MaxValue);
        var distance = (byte)Math.Clamp((int)(Math.Sqrt((double)aimX * aimX + (double)aimY * aimY) / 2d), 0, byte.MaxValue);
        return InputState(keyState, netDirection, distance);
    }

    /// <summary>A payload-less command: BUILD_SENTRY, DESTROY_SENTRY, DROP_INTEL, OMNOMNOMNOM, TOGGLE_ZOOM.</summary>
    public static byte[] Command(byte commandType)
    {
        if (commandType is not (Gg2Protocol.BuildSentry or Gg2Protocol.DestroySentry or Gg2Protocol.DropIntel
            or Gg2Protocol.Omnomnomnom or Gg2Protocol.ToggleZoom))
        {
            throw new ArgumentOutOfRangeException(nameof(commandType), "Not a payload-less GG2 command.");
        }

        return [commandType];
    }

    public static byte[] ChatBubble(byte bubbleImage) => [Gg2Protocol.ChatBubble, bubbleImage];

    /// <summary>Asks for the current map's PNG; the server answers with a uint size and the bytes.</summary>
    public static byte[] DownloadMap() => [Gg2Protocol.DownloadMap];

    public static byte[] ClientSettings(bool queueJump) => [Gg2Protocol.ClientSettings, queueJump ? (byte)1 : (byte)0];

    /// <summary>PLUGIN_PACKET: ushort length, then the plugin id and its data.</summary>
    public static byte[] PluginPacket(byte pluginId, ReadOnlySpan<byte> data)
    {
        if (data.Length + 1 > ushort.MaxValue)
        {
            throw new ArgumentOutOfRangeException(nameof(data));
        }

        var result = new byte[3 + 1 + data.Length];
        result[0] = Gg2Protocol.PluginPacket;
        BinaryPrimitives.WriteUInt16LittleEndian(result.AsSpan(1), (ushort)(data.Length + 1));
        result[3] = pluginId;
        data.CopyTo(result.AsSpan(4));
        return result;
    }

    private static string TrimName(string? name)
    {
        name ??= string.Empty;
        return name.Length > Gg2Protocol.MaxPlayerNameLength ? name[..Gg2Protocol.MaxPlayerNameLength] : name;
    }

    private static byte[] WithString8(byte type, string text)
    {
        var bytes = Latin1.GetBytes(text);
        var length = Math.Min(bytes.Length, byte.MaxValue);
        var result = new byte[2 + length];
        result[0] = type;
        result[1] = (byte)length;
        bytes.AsSpan(0, length).CopyTo(result.AsSpan(2));
        return result;
    }
}

/// <summary>
/// Encodes server messages exactly as GG2's server would. Used for tests and tools; the
/// state-update layout must match the reader's (same roster classes and map layout).
/// </summary>
public static class Gg2ServerMessageWriter
{
    private static readonly Encoding Latin1 = Encoding.Latin1;

    public static byte[] Write(Gg2ServerMessage message, IReadOnlyList<byte> rosterClasses, Gg2MapLayout layout)
    {
        ArgumentNullException.ThrowIfNull(message);
        ArgumentNullException.ThrowIfNull(rosterClasses);
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream, Latin1, leaveOpen: true);
        writer.Write(message is Gg2StateUpdateMessage stateUpdate ? stateUpdate.UpdateType : message.Type);
        switch (message)
        {
            case Gg2ServerHello hello:
                WriteString8(writer, hello.ServerName);
                WriteString8(writer, hello.MapName);
                WriteString8(writer, hello.MapMd5);
                writer.Write(hello.PluginsRequired ? (byte)1 : (byte)0);
                WriteString16(writer, hello.PluginList);
                break;
            case Gg2SignalMessage:
                break;
            case Gg2KickMessage kick:
                writer.Write(kick.Reason);
                break;
            case Gg2JoinUpdateMessage join:
                writer.Write(join.LocalPlayerIndex);
                writer.Write(join.MapArea);
                break;
            case Gg2ChangeMapMessage change:
                WriteString8(writer, change.MapName);
                WriteString8(writer, change.MapMd5);
                break;
            case Gg2PlayerJoinMessage join:
                WriteString8(writer, join.Name);
                break;
            case Gg2PlayerLeaveMessage leave:
                writer.Write(leave.PlayerIndex);
                break;
            case Gg2PlayerChangeTeamMessage team:
                writer.Write(team.PlayerIndex);
                writer.Write(team.Team);
                break;
            case Gg2PlayerChangeClassMessage change:
                writer.Write(change.PlayerIndex);
                writer.Write(change.ClassId);
                break;
            case Gg2PlayerChangeNameMessage name:
                writer.Write(name.PlayerIndex);
                WriteString8(writer, name.Name);
                break;
            case Gg2PlayerSpawnMessage spawn:
                writer.Write(spawn.PlayerIndex);
                writer.Write(spawn.SpawnPointId);
                writer.Write(spawn.SpawnGroup);
                break;
            case Gg2PlayerDeathMessage death:
                writer.Write(death.Victim);
                writer.Write(death.Killer);
                writer.Write(death.Assistant);
                writer.Write(death.DamageSource);
                break;
            case Gg2BalanceMessage balance:
                writer.Write(balance.PlayerIndex);
                if (balance.NewClass is { } newClass)
                {
                    writer.Write(newClass);
                }

                break;
            case Gg2ChatBubbleMessage bubble:
                writer.Write(bubble.PlayerIndex);
                writer.Write(bubble.BubbleImage);
                break;
            case Gg2BuildSentryMessage build:
                writer.Write(build.PlayerIndex);
                writer.Write(build.XTimes5);
                writer.Write(build.YTimes5);
                writer.Write(build.Direction);
                break;
            case Gg2DestroySentryMessage destroy:
                writer.Write(destroy.Owner);
                writer.Write(destroy.Killer);
                writer.Write(destroy.Assistant);
                writer.Write(destroy.DamageSource);
                break;
            case Gg2PlayerEventMessage playerEvent:
                writer.Write(playerEvent.PlayerIndex);
                break;
            case Gg2ReturnIntelMessage returned:
                writer.Write(returned.Team);
                break;
            case Gg2GeneratorDestroyMessage generator:
                writer.Write(generator.Team);
                break;
            case Gg2ArenaEndRoundMessage arena:
                writer.Write(arena.Winner);
                writer.Write((byte)arena.RedMvps.Count);
                writer.Write((byte)arena.BlueMvps.Count);
                writer.Write(arena.RedWins);
                writer.Write(arena.BlueWins);
                foreach (var mvp in arena.RedMvps)
                {
                    WriteMvp(writer, mvp);
                }

                foreach (var mvp in arena.BlueMvps)
                {
                    WriteMvp(writer, mvp);
                }

                break;
            case Gg2MapEndMessage mapEnd:
                WriteString8(writer, mapEnd.NextMap);
                writer.Write(mapEnd.Winners);
                writer.Write(mapEnd.MapArea);
                break;
            case Gg2RewardChallengeMessage challenge:
                writer.Write(challenge.Challenge);
                break;
            case Gg2RewardUpdateMessage reward:
                writer.Write(reward.PlayerIndex);
                WriteString16(writer, reward.Rewards);
                break;
            case Gg2MessageStringMessage text:
                WriteString8(writer, text.Text);
                break;
            case Gg2SentryPositionMessage position:
                writer.Write(position.PlayerIndex);
                writer.Write(position.XTimes5);
                writer.Write(position.YTimes5);
                break;
            case Gg2WeaponFireMessage fire:
                writer.Write(fire.PlayerIndex);
                writer.Write(fire.XTimes5);
                writer.Write(fire.YTimes5);
                writer.Write(fire.HorizontalSpeed85);
                writer.Write(fire.VerticalSpeed85);
                writer.Write(fire.Seed);
                break;
            case Gg2PluginPacketMessage plugin:
                writer.Write((ushort)(plugin.Data.Length + 1));
                writer.Write(plugin.PluginId);
                writer.Write(plugin.Data);
                break;
            case Gg2ClientSettingsMessage settings:
                writer.Write(settings.PlayerIndex);
                writer.Write(settings.QueueJump);
                break;
            case Gg2StateUpdateMessage update:
                WriteStateUpdate(writer, update, rosterClasses, layout);
                break;
            default:
                throw new ArgumentException($"Cannot encode {message.GetType().Name}.", nameof(message));
        }

        writer.Flush();
        return stream.ToArray();
    }

    private static void WriteMvp(BinaryWriter writer, Gg2ArenaMvp mvp)
    {
        writer.Write(mvp.PlayerIndex);
        writer.Write(mvp.Kills);
        writer.Write(mvp.Healing);
        writer.Write(mvp.Points);
    }

    private static void WriteStateUpdate(BinaryWriter writer, Gg2StateUpdateMessage update, IReadOnlyList<byte> rosterClasses, Gg2MapLayout layout)
    {
        var full = update.IsFullUpdate;
        var quickOrFull = full || update.IsQuickUpdate;
        if (full)
        {
            writer.Write(update.TdmInvulnerabilityTicks);
        }

        writer.Write((byte)rosterClasses.Count);
        if (!update.IsCapsUpdate)
        {
            for (var index = 0; index < update.Players.Count; index += 1)
            {
                WritePlayer(writer, update.Players[index], rosterClasses[index], full, quickOrFull);
            }

            if (quickOrFull)
            {
                foreach (var platform in update.Platforms)
                {
                    writer.Write(platform.XTimes5);
                    writer.Write(platform.YTimes5);
                    writer.Write(platform.MovementState);
                }
            }
        }

        if (update.Full is { } tail)
        {
            WriteIntelSet(writer, tail.RedIntel);
            WriteIntelSet(writer, tail.BlueIntel);
            writer.Write(tail.CapLimit);
            writer.Write(tail.RedCaps);
            writer.Write(tail.BlueCaps);
            writer.Write(tail.RespawnSeconds);
            WriteHud(writer, tail.Hud, full: true);
            writer.Write(tail.ClassLimits);
        }
        else if (update.Caps is { } caps)
        {
            writer.Write(caps.RedCaps);
            writer.Write(caps.BlueCaps);
            writer.Write(caps.RespawnSeconds);
            WriteHud(writer, caps.Hud, full: false);
        }
    }

    private static void WritePlayer(BinaryWriter writer, Gg2PlayerState player, byte classId, bool full, bool quickOrFull)
    {
        if (full && player.Stats is { } stats)
        {
            writer.Write(stats.Kills);
            writer.Write(stats.Deaths);
            writer.Write(stats.Caps);
            writer.Write(stats.Assists);
            writer.Write(stats.Destruction);
            writer.Write(stats.Stabs);
            writer.Write(stats.Healing);
            writer.Write(stats.Defenses);
            writer.Write(stats.Invulns);
            writer.Write(stats.Bonus);
            writer.Write(stats.Points);
            writer.Write(stats.QueueJump);
            WriteString16(writer, stats.Rewards);
            writer.Write(stats.DominationKills);
        }

        byte subobjects = 0;
        if (player.Character is not null)
        {
            subobjects |= 0x01;
        }

        if (player.Sentry is not null)
        {
            subobjects |= 0x02;
        }

        writer.Write(subobjects);
        if (player.Character is { } character)
        {
            writer.Write(character.KeyState);
            writer.Write(character.NetAimDirection);
            writer.Write(character.AimDistanceHalf);
            if (quickOrFull && character.Quick is { } quick)
            {
                writer.Write(quick.XTimes5);
                writer.Write(quick.YTimes5);
                writer.Write(quick.HorizontalSpeed85);
                writer.Write(quick.VerticalSpeed85);
                writer.Write(quick.Health);
                writer.Write(quick.Ammo);
                writer.Write(quick.StatusFlags);
            }

            if (full && character.Full is { } extra)
            {
                writer.Write(extra.AnimationOffset);
                writer.Write(extra.ClassValue);
                writer.Write(extra.IntelAlarm);
                writer.Write(extra.HasIntel);
                writer.Write(extra.IntelRecharge);
                if (classId == Gg2Protocol.ClassMedic)
                {
                    writer.Write(extra.Weapon.HealTarget ?? Gg2Protocol.NoPlayer);
                }
                else
                {
                    writer.Write(extra.Weapon.ReadyToShoot);
                    writer.Write(extra.Weapon.CooldownTicks);
                    if (classId == Gg2Protocol.ClassDemoman)
                    {
                        var mines = extra.Weapon.Mines ?? Array.Empty<Gg2MineState>();
                        writer.Write((byte)mines.Count);
                        foreach (var mine in mines)
                        {
                            writer.Write(mine.XTimes5);
                            writer.Write(mine.YTimes5);
                            writer.Write(mine.HorizontalSpeed5);
                            writer.Write(mine.VerticalSpeed5);
                            writer.Write(mine.Stickied);
                        }
                    }
                }
            }
        }

        if (quickOrFull && player.Sentry is { } sentry)
        {
            if (full)
            {
                writer.Write(sentry.StartDirection ?? (sbyte)1);
                writer.Write(sentry.XTimes5 ?? (ushort)0);
                writer.Write(sentry.YTimes5 ?? (ushort)0);
            }

            writer.Write(sentry.Packed);
            if (full && !sentry.Built)
            {
                writer.Write(sentry.VerticalSpeed ?? (byte)0);
            }
        }
    }

    private static void WriteIntelSet(BinaryWriter writer, IReadOnlyList<Gg2IntelState> intel)
    {
        writer.Write((ushort)intel.Count);
        foreach (var item in intel)
        {
            writer.Write(item.XTimes5);
            writer.Write(item.YTimes5);
            writer.Write(item.ReturnAlarm);
        }
    }

    private static void WriteHud(BinaryWriter writer, Gg2HudState? hud, bool full)
    {
        switch (hud)
        {
            case null:
                break;
            case Gg2CtfHudState ctf:
                writer.Write(ctf.TimeLimitMinutes);
                writer.Write(ctf.Timer);
                break;
            case Gg2ControlPointHudState cp:
                writer.Write(cp.TimeLimitMinutes);
                writer.Write(cp.Timer);
                writer.Write(cp.SetupTimer);
                WritePoints(writer, cp.Points);
                break;
            case Gg2KothHudState koth:
                writer.Write(koth.UnlockTicks);
                writer.Write(koth.RedTimer);
                writer.Write(koth.BlueTimer);
                WritePoints(writer, koth.Points);
                break;
            case Gg2ArenaHudState arena:
                if (full)
                {
                    var round = arena.Round ?? new Gg2ArenaRoundState(0, 0, 0, -1, 0);
                    writer.Write(round.RedWins);
                    writer.Write(round.BlueWins);
                    writer.Write(round.State);
                    writer.Write(round.Winners);
                    writer.Write(round.EndCount);
                }

                writer.Write(arena.TimeLimitMinutes);
                writer.Write(arena.Timer);
                writer.Write(arena.UnlockTicks);
                writer.Write((byte)(arena.RoundStart / 2));
                WritePoints(writer, [arena.Point]);
                break;
            case Gg2TeamDeathmatchHudState tdm:
                writer.Write(tdm.TimeLimitMinutes);
                writer.Write(tdm.Timer);
                writer.Write(tdm.KillLimit);
                break;
            case Gg2GeneratorHudState generator:
                writer.Write(generator.TimeLimitMinutes);
                writer.Write(generator.Timer);
                if (generator.Blue is { } blue)
                {
                    writer.Write(blue.Health);
                    writer.Write(blue.ShieldHealth);
                }

                if (generator.Red is { } red)
                {
                    writer.Write(red.Health);
                    writer.Write(red.ShieldHealth);
                }

                break;
            case Gg2InvasionHudState invasion:
                writer.Write(invasion.TimeLimitMinutes);
                writer.Write(invasion.Timer);
                writer.Write(invasion.SetupTimer);
                break;
        }
    }

    private static void WritePoints(BinaryWriter writer, IReadOnlyList<Gg2ControlPointState> points)
    {
        foreach (var point in points)
        {
            writer.Write(point.Team);
            writer.Write(point.CappingTeam);
            writer.Write(point.Capping);
        }
    }

    private static void WriteString8(BinaryWriter writer, string text)
    {
        var bytes = Latin1.GetBytes(text ?? string.Empty);
        writer.Write((byte)Math.Min(bytes.Length, byte.MaxValue));
        writer.Write(bytes, 0, Math.Min(bytes.Length, byte.MaxValue));
    }

    private static void WriteString16(BinaryWriter writer, string text)
    {
        var bytes = Latin1.GetBytes(text ?? string.Empty);
        writer.Write((ushort)Math.Min(bytes.Length, ushort.MaxValue));
        writer.Write(bytes, 0, Math.Min(bytes.Length, ushort.MaxValue));
    }
}
