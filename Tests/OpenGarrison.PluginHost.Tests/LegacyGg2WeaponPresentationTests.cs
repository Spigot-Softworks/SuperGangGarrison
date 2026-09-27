using System.Text;
using OpenGarrison.Client;
using OpenGarrison.Core;
using OpenGarrison.Protocol;
using Xunit;

namespace OpenGarrison.PluginHost.Tests;

public sealed class LegacyGg2WeaponPresentationTests
{
    [Theory]
    [InlineData(8, 0x10, "FlamethrowerSnd", "flame")]
    [InlineData(6, 0x10, "ChaingunSnd", "shot")]
    [InlineData(4, 0x08, "MedichaingunSnd", "needle")]
    [InlineData(9, 0x08, "BladeSnd", "blade")]
    public void InputDrivenWeaponsPresentShotsAndSounds(
        byte gg2Class, byte inputKeys, string expectedSound, string projectileKind)
    {
        var (session, x, y) = CreateSession(gg2Class);
        ApplyInputTick(session, x, y, inputKeys);
        var snapshot = session.CreateSnapshotMessage();

        Assert.Contains(snapshot.SoundEvents, sound =>
            sound.SoundName == expectedSound && sound.SourcePlayerId == snapshot.Players[0].PlayerId);
        Assert.True(snapshot.Players[0].PrimaryCooldownTicks > 0);
        Assert.True(projectileKind switch
        {
            "flame" => snapshot.Flames.Count > 0,
            "shot" => snapshot.Shots.Count > 0,
            "needle" => snapshot.Needles.Count > 0,
            "blade" => snapshot.Blades.Count > 0,
            _ => false,
        });
    }

    [Fact]
    public void HeavyStopsReportingFireAfterTriggerRelease()
    {
        var (session, x, y) = CreateSession(6);
        ApplyInputTick(session, x, y, 0x10);
        Assert.True(session.CreateSnapshotMessage().Players[0].PrimaryCooldownTicks > 0);

        ApplyInputTick(session, x, y, 0);
        ApplyInputTick(session, x, y, 0);
        var released = session.CreateSnapshotMessage();
        Assert.Equal(0, released.Players[0].PrimaryCooldownTicks);
        Assert.DoesNotContain(released.SoundEvents, sound => sound.SoundName == "ChaingunSnd");
    }

    [Fact]
    public void GroundedJumpEmitsOneSoundWhileHeld()
    {
        var (session, x, y) = CreateSession(1, grounded: true);
        ApplyInputTick(session, x, y, 0x80);
        Assert.Contains(session.CreateSnapshotMessage().SoundEvents, sound => sound.SoundName == "JumpSnd");

        ApplyInputTick(session, x, y, 0x80);
        Assert.DoesNotContain(session.CreateSnapshotMessage().SoundEvents, sound => sound.SoundName == "JumpSnd");
    }

    [Fact]
    public void LocalSlotTracksEarlierPlayerLeaving()
    {
        var (session, _, _) = CreateSession(1);
        ApplyLivePayload(session, 1, writer => WriteShortString(writer, "Local"));
        ApplyLivePayload(session, 3, writer => { writer.Write((byte)1); writer.Write((byte)0); });
        Assert.Equal((byte)2, session.GetLocalPlayerSlot());

        ApplyLivePayload(session, 2, writer => writer.Write((byte)0));
        Assert.Equal((byte)1, session.GetLocalPlayerSlot());
    }

    [Fact]
    public void RespawnWaitsForPositionInsteadOfShowingOldCorpseLocation()
    {
        var (session, x, y) = CreateSession(1);
        ApplyLivePayload(session, 10, writer =>
        {
            writer.Write((byte)0);
            writer.Write(byte.MaxValue);
            writer.Write(byte.MaxValue);
            writer.Write((byte)8);
        });
        session.CreateSnapshotMessage();
        ApplyLivePayload(session, 5, writer =>
        {
            writer.Write((byte)0);
            writer.Write((byte)0);
            writer.Write((byte)0);
        });
        var pending = session.CreateSnapshotMessage().Players[0];
        Assert.False(pending.IsAlive);
        Assert.False(pending.IsAwaitingJoin);

        ApplyLivePayload(session, 9, writer =>
        {
            writer.Write((byte)1);
            WriteQuickPlayer(writer, 0, x + 30, y);
        });
        var spawned = session.CreateSnapshotMessage();
        Assert.True(spawned.Players[0].IsAlive);
        Assert.Equal(x + 30, spawned.Players[0].X);
        Assert.Contains(spawned.SoundEvents, sound => sound.SoundName == "RespawnSnd");
    }

    [Fact]
    public void RocketStopsAtAuthoritativeGibDeath()
    {
        var (session, x, y) = CreateSession(1);
        ApplyLivePayload(session, 1, writer => WriteShortString(writer, "Victim"));
        ApplyLivePayload(session, 4, writer => { writer.Write((byte)1); writer.Write((byte)1); });
        ApplyLivePayload(session, 3, writer => { writer.Write((byte)1); writer.Write((byte)1); });
        ApplyLivePayload(session, 5, writer => { writer.Write((byte)1); writer.Write((byte)0); writer.Write((byte)0); });
        ApplyLivePayload(session, 9, writer =>
        {
            writer.Write((byte)2);
            WriteQuickPlayer(writer, 0, x, y);
            WriteQuickPlayer(writer, 0, x + 40, y);
        });
        session.CreateSnapshotMessage();
        ApplyLivePayload(session, 54, writer =>
        {
            writer.Write((byte)0);
            writer.Write(checked((ushort)(x * 5)));
            writer.Write(checked((ushort)(y * 5)));
            writer.Write((sbyte)0);
            writer.Write((sbyte)0);
            writer.Write((ushort)1234);
        });
        Assert.NotEmpty(session.CreateSnapshotMessage().Rockets);

        ApplyLivePayload(session, 10, writer =>
        {
            writer.Write((byte)1);
            writer.Write((byte)0);
            writer.Write(byte.MaxValue);
            writer.Write((byte)9);
        });
        var death = session.CreateSnapshotMessage();
        Assert.Empty(death.Rockets);
        Assert.Contains(death.VisualEvents, effect => effect.EffectName == "Explosion");
    }

    [Theory]
    [InlineData((byte)9, true)]  // rocket
    [InlineData((byte)8, false)] // shotgun
    public void DeathGibsFollowGg2SourceAndSurviveWireEncoding(byte source, bool expectGibs)
    {
        var (session, _, _) = CreateSession(1);
        using var stream = new MemoryStream(new byte[] { 0, byte.MaxValue, byte.MaxValue, source });
        using var reader = new BinaryReader(stream, Encoding.Latin1);
        session.ApplyLiveMessage(reader, 10);

        var snapshot = session.CreateSnapshotMessage();
        Assert.Equal(expectGibs, snapshot.GibSpawnEvents.Count > 0);
        Assert.True(ProtocolCodec.TryDeserialize(ProtocolCodec.Serialize(snapshot), out var decoded));
        var roundTrip = Assert.IsType<SnapshotMessage>(decoded);
        Assert.Equal(snapshot.GibSpawnEvents.Count, roundTrip.GibSpawnEvents.Count);
    }

    [Fact]
    public void MedicCanPresentBeamFromInputBetweenFullUpdates()
    {
        var (session, x, y) = CreateSession(4);
        ApplyLivePayload(session, 1, writer => WriteShortString(writer, "Teammate"));
        ApplyLivePayload(session, 4, writer => { writer.Write((byte)1); writer.Write((byte)1); });
        ApplyLivePayload(session, 3, writer => { writer.Write((byte)1); writer.Write((byte)0); });
        ApplyLivePayload(session, 5, writer => { writer.Write((byte)1); writer.Write((byte)0); writer.Write((byte)0); });
        ApplyLivePayload(session, 9, writer =>
        {
            writer.Write((byte)2);
            WriteQuickPlayer(writer, 0x10, x, y);
            WriteQuickPlayer(writer, 0, x + 35, y);
        });

        var snapshot = session.CreateSnapshotMessage();
        Assert.True(snapshot.Players[0].IsMedicHealing);
        Assert.Equal(snapshot.Players[1].PlayerId, snapshot.Players[0].MedicHealTargetId);
    }

    [Fact]
    public void BuiltSentryPresentsItsStockSoundsWhenTargetAppears()
    {
        var (session, x, y) = CreateSession(5);
        ApplyLivePayload(session, 1, writer => WriteShortString(writer, "Enemy"));
        ApplyLivePayload(session, 4, writer => { writer.Write((byte)1); writer.Write((byte)1); });
        ApplyLivePayload(session, 3, writer => { writer.Write((byte)1); writer.Write((byte)1); });
        ApplyLivePayload(session, 5, writer => { writer.Write((byte)1); writer.Write((byte)0); writer.Write((byte)0); });
        ApplyLivePayload(session, 16, writer =>
        {
            writer.Write((byte)0);
            writer.Write(checked((ushort)(x * 5)));
            writer.Write(checked((ushort)(y * 5)));
            writer.Write((sbyte)1);
        });
        var built = session.CreateSnapshotMessage();
        Assert.Contains(built.SoundEvents, sound => sound.SoundName == "SentryFloorSnd");
        Assert.Contains(built.SoundEvents, sound => sound.SoundName == "SentryBuildSnd");

        var heardAlert = false;
        var heardShot = false;
        for (var tick = 0; tick < 14; tick += 1)
        {
            ApplyLivePayload(session, 9, writer =>
            {
                writer.Write((byte)2);
                WriteQuickPlayer(writer, 0, x, y, hasBuiltSentry: true);
                WriteQuickPlayer(writer, 0, x + 35, y);
            });
            var snapshot = session.CreateSnapshotMessage();
            heardAlert |= snapshot.SoundEvents.Any(sound => sound.SoundName == "SentryAlert");
            heardShot |= snapshot.SoundEvents.Any(sound => sound.SoundName == "ShotgunSnd");
        }

        Assert.True(heardAlert);
        Assert.True(heardShot);
    }

    [Fact]
    public void SeedBasedFireRetainsRecoilEdgeAndShooterSound()
    {
        var (session, x, y) = CreateSession(1);
        using (var stream = new MemoryStream())
        {
            using var writer = new BinaryWriter(stream, Encoding.Latin1, leaveOpen: true);
            writer.Write((byte)0);
            writer.Write(checked((ushort)(x * 5)));
            writer.Write(checked((ushort)(y * 5)));
            writer.Write((sbyte)0);
            writer.Write((sbyte)0);
            writer.Write((ushort)1234);
            stream.Position = 0;
            using var reader = new BinaryReader(stream, Encoding.Latin1);
            session.ApplyLiveMessage(reader, 54);
        }

        ApplyInputTick(session, x, y, 0);
        var snapshot = session.CreateSnapshotMessage();
        Assert.True(snapshot.Players[0].PrimaryCooldownTicks > 0);
        Assert.Contains(snapshot.SoundEvents, sound =>
            sound.SoundName == "RocketSnd" && sound.SourcePlayerId == snapshot.Players[0].PlayerId);
        Assert.True(snapshot.Rockets.Count > 0 || snapshot.VisualEvents.Any(effect => effect.EffectName == "Explosion"));
    }

    [Fact]
    public void QuoteBubbleDoesNotPlayBladeThrowSound()
    {
        var (session, x, y) = CreateSession(9);
        ApplyInputTick(session, x, y, 0x10);
        var snapshot = session.CreateSnapshotMessage();
        Assert.NotEmpty(snapshot.Bubbles);
        Assert.DoesNotContain(snapshot.SoundEvents, sound => sound.SoundName == "BladeSnd");
    }

    [Fact]
    public void RestingPlayerDoesNotDriftBetweenAuthoritativeUpdates()
    {
        var (session, x, y) = CreateSession(1, grounded: true);
        var initial = session.CreateSnapshotMessage().Players[0];
        Assert.True(initial.IsGrounded);
        for (var tick = 0; tick < 12; tick += 1)
        {
            ApplyInputTick(session, x, y, 0);
            var player = session.CreateSnapshotMessage().Players[0];
            Assert.Equal(initial.X, player.X);
            Assert.Equal(initial.Y, player.Y);
            Assert.True(player.IsGrounded);
        }
    }

    [Fact]
    public void RepeatedStationaryServerPositionPreventsVisualDriftOnImperfectCollisionMaps()
    {
        var (session, x, y) = CreateSession(1);
        using (var stream = new MemoryStream())
        {
            using var writer = new BinaryWriter(stream, Encoding.Latin1, leaveOpen: true);
            writer.Write((byte)1); // player count
            writer.Write((byte)1); // character present
            writer.Write((byte)0); // keys
            writer.Write((ushort)0); // aim
            writer.Write((byte)50);
            writer.Write(checked((ushort)(x * 5)));
            writer.Write(checked((ushort)(y * 5)));
            writer.Write((sbyte)0);
            writer.Write((sbyte)0);
            writer.Write((byte)100); // health
            writer.Write((byte)4); // ammo
            writer.Write((byte)0); // flags
            stream.Position = 0;
            using var reader = new BinaryReader(stream, Encoding.Latin1);
            session.ApplyLiveMessage(reader, 9);
        }

        var initial = session.CreateSnapshotMessage().Players[0];
        Assert.False(initial.IsGrounded);
        for (var tick = 0; tick < 12; tick += 1)
        {
            ApplyInputTick(session, x, y, 0);
            var player = session.CreateSnapshotMessage().Players[0];
            Assert.Equal(initial.X, player.X);
            Assert.Equal(initial.Y, player.Y);
        }
    }

    [Fact]
    public void RepeatedServerPositionKeepsRestingPlayerAndCameraAnchorStableDespiteVelocityQuantization()
    {
        var (session, x, y) = CreateSession(1, grounded: true);
        ApplyLivePayload(session, 9, writer =>
        {
            writer.Write((byte)1);
            WriteQuickPlayer(writer, 0, x, y, horizontalSpeed: 3, verticalSpeed: 3);
        });
        ApplyLivePayload(session, 9, writer =>
        {
            writer.Write((byte)1);
            writer.Write((byte)1); // character present
            writer.Write((byte)0); // no movement input
            writer.Write((ushort)0); // aim
            writer.Write((byte)50); // aim distance
            writer.Write(checked((ushort)(x * 5 + 3))); // subpixel GG2 position correction
            writer.Write(checked((ushort)(y * 5 + 3)));
            writer.Write((sbyte)3); // residual encoded velocity
            writer.Write((sbyte)3);
            writer.Write((byte)100); // health
            writer.Write((byte)4); // ammo
            writer.Write((byte)0); // flags
        });

        for (var tick = 0; tick < 12; tick += 1)
        {
            ApplyInputTick(session, x, y, 0);
            var player = session.CreateSnapshotMessage().Players[0];
            Assert.Equal(x, player.X);
            Assert.Equal(y, player.Y);
            Assert.Equal(0f, player.HorizontalSpeed);
            Assert.Equal(0f, player.VerticalSpeed);
        }
    }

    [Fact]
    public void GroundedIdlePlayerDoesNotGetPushedUpByApproximateWalkmaskCollision()
    {
        var (session, x, y) = CreateSession(1, grounded: true);
        ApplyLivePayload(session, 9, writer =>
        {
            writer.Write((byte)1);
            WriteQuickPlayer(writer, 0, x, y + 2, horizontalSpeed: 3, verticalSpeed: 3);
        });
        var serverY = session.CreateSnapshotMessage().Players[0].Y;
        for (var tick = 0; tick < 12; tick += 1)
        {
            ApplyInputTick(session, x, y, 0);
            Assert.Equal(serverY, session.CreateSnapshotMessage().Players[0].Y);
        }
    }

    [Fact]
    public void HeavyEatingPacketStartsAndCompletesTheGg2Animation()
    {
        var (session, x, y) = CreateSession(6);
        ApplyLivePayload(session, 24, writer => writer.Write((byte)0));
        var started = session.CreateSnapshotMessage();
        Assert.True(started.Players[0].IsHeavyEating);
        Assert.Equal(128, started.Players[0].HeavyEatTicksRemaining);
        Assert.Contains(started.SoundEvents, sound => sound.SoundName == "HeavyEatSnd");

        for (var tick = 0; tick < 128; tick += 1)
        {
            ApplyInputTick(session, x, y, 0);
        }

        var completed = session.CreateSnapshotMessage();
        Assert.False(completed.Players[0].IsHeavyEating);
        Assert.Equal(0, completed.Players[0].HeavyEatTicksRemaining);
    }

    [Fact]
    public void CloakedSpyAttackStartsBackstabAnimationBeforeAnyKill()
    {
        var (session, x, y) = CreateSession(7);
        ApplyLivePayload(session, 9, writer =>
        {
            writer.Write((byte)1);
            WriteQuickPlayer(writer, 0, x, y, flags: 1);
        });
        session.CreateSnapshotMessage();

        ApplyInputTick(session, x, y, 0x10);
        var started = session.CreateSnapshotMessage();
        Assert.True(started.Players[0].SpyBackstabVisualTicksRemaining > 0);
        Assert.Contains(started.VisualEvents, effect => effect.EffectName == "BackstabRed");

        ApplyInputTick(session, x, y, 0x10);
        Assert.DoesNotContain(session.CreateSnapshotMessage().VisualEvents,
            effect => effect.EffectName == "BackstabRed");
    }

    [Fact]
    public void MatchClockAdvancesBetweenGg2ScoreUpdatesAndResynchronizes()
    {
        var (session, x, y) = CreateSession(1, mapName: "ctf_conflict");
        Assert.Equal(120, session.CreateSnapshotMessage().TimeRemainingTicks);
        for (var tick = 0; tick < 30; tick += 1)
        {
            ApplyInputTick(session, x, y, 0);
        }
        Assert.Equal(90, session.CreateSnapshotMessage().TimeRemainingTicks);

        ApplyLivePayload(session, 28, writer =>
        {
            writer.Write((byte)1); // player count
            writer.Write((byte)0); // red caps
            writer.Write((byte)0); // blue caps
            writer.Write((byte)10); // respawn seconds
            writer.Write((byte)10); // time limit in minutes
            writer.Write((uint)75); // server clock correction
        });
        Assert.Equal(75, session.CreateSnapshotMessage().TimeRemainingTicks);
        ApplyInputTick(session, x, y, 0);
        Assert.Equal(74, session.CreateSnapshotMessage().TimeRemainingTicks);
    }

    [Fact]
    public void OwnedKothPointAdvancesOnlyTheOwningTeamsClock()
    {
        var (session, x, y) = CreateSession(1, initialKothPointTeam: 0);
        var initial = session.CreateSnapshotMessage();
        Assert.NotEmpty(initial.ControlPoints);
        Assert.Equal(GameModeKind.KingOfTheHill, (GameModeKind)initial.GameMode);
        Assert.Equal(0, initial.KothUnlockTicksRemaining);
        Assert.Equal((byte)1, initial.ControlPoints[0].Team);
        Assert.False(initial.ControlPoints[0].IsLocked);
        for (var tick = 0; tick < 30; tick += 1)
        {
            ApplyInputTick(session, x, y, 0);
        }

        var snapshot = session.CreateSnapshotMessage();
        Assert.Equal(5370, snapshot.KothRedTimerTicksRemaining);
        Assert.Equal(5400, snapshot.KothBlueTimerTicksRemaining);
    }

    [Fact]
    public void LiveStockMapChangeReplacesLevelAndClearsOldPresentation()
    {
        var (session, x, y) = CreateSession(1);
        using (var stream = new MemoryStream())
        {
            using var writer = new BinaryWriter(stream, Encoding.Latin1, leaveOpen: true);
            writer.Write((byte)0);
            writer.Write(checked((ushort)(x * 5)));
            writer.Write(checked((ushort)(y * 5)));
            writer.Write((sbyte)0);
            writer.Write((sbyte)0);
            writer.Write((ushort)1234);
            stream.Position = 0;
            using var reader = new BinaryReader(stream, Encoding.Latin1);
            session.ApplyLiveMessage(reader, 54);
        }

        Assert.True(session.CreateSnapshotMessage().Rockets.Count > 0);
        using (var stream = new MemoryStream())
        {
            using var writer = new BinaryWriter(stream, Encoding.Latin1, leaveOpen: true);
            WriteShortString(writer, "koth_valley");
            WriteShortString(writer, "");
            stream.Position = 0;
            using var reader = new BinaryReader(stream, Encoding.Latin1);
            session.ApplyLiveMessage(reader, LegacyGg2Wire.ChangeMap);
        }

        session.ActivateLiveMapChange(null);
        var snapshot = session.CreateSnapshotMessage();
        Assert.Equal("gg2_stock_koth_valley", snapshot.LevelName);
        Assert.Equal(GameModeKind.KingOfTheHill, (GameModeKind)snapshot.GameMode);
        Assert.False(snapshot.Players[0].IsAlive);
        Assert.Empty(snapshot.Rockets);
        Assert.Empty(snapshot.SoundEvents);
        Assert.Empty(snapshot.VisualEvents);
    }

    private static (ReDsmReplayTransport.ReDsmReplayTranslator.LegacyReplaySession Session, int X, int Y)
        CreateSession(byte gg2Class, bool grounded = false, string mapName = "koth_corinth",
            byte initialKothPointTeam = byte.MaxValue)
    {
        Assert.True(LegacyGg2BundledStockMaps.TryRegister(mapName, out var levelName));
        var level = SimpleLevelFactory.CreateImportedLevel(levelName)!;
        var (x, y) = grounded ? FindGroundedPoint(level) : FindOpenPoint(level);
        var session = ReDsmReplayTransport.ReDsmReplayTranslator.LegacyReplaySession.CreateLive(
            new LegacyGg2ServerHello("Presentation test", mapName, "", false, ""));

        using var stream = new MemoryStream();
        using (var writer = new BinaryWriter(stream, Encoding.Latin1, leaveOpen: true))
        {
            writer.Write((byte)44); // JOIN_UPDATE
            writer.Write((byte)1);
            writer.Write((byte)1);
            writer.Write((byte)7); // CHANGE_MAP
            WriteShortString(writer, mapName);
            WriteShortString(writer, "");
            writer.Write((byte)1); // PLAYER_JOIN
            WriteShortString(writer, "Shooter");
            writer.Write(new byte[] { 4, 0, gg2Class });
            writer.Write(new byte[] { 3, 0, 0 });
            writer.Write((byte)8); // FULL_UPDATE
            writer.Write((ushort)0);
            writer.Write((byte)1);
            writer.Write(new byte[13]);
            writer.Write((ushort)0);
            writer.Write((byte)1); // character present
            writer.Write((byte)0); // no keys
            writer.Write((ushort)0); // aim right
            writer.Write((byte)50);
            writer.Write(checked((ushort)(x * 5)));
            writer.Write(checked((ushort)(y * 5)));
            writer.Write((sbyte)0);
            writer.Write((sbyte)0);
            writer.Write((byte)100);
            writer.Write((byte)100); // ammo
            writer.Write((byte)0); // flags
            writer.Write((byte)0); // animation offset
            writer.Write((byte)0); // class-specific byte
            writer.Write((short)0);
            writer.Write((byte)0); // intel
            writer.Write((short)0); // recharge
            if (gg2Class == 4)
            {
                writer.Write(byte.MaxValue); // no heal target
            }
            else
            {
                writer.Write((byte)1); // ready
                writer.Write((byte)0); // cooldown
            }

            writer.Write((ushort)0); // red intel
            writer.Write((ushort)0); // blue intel
            writer.Write((byte)3); // cap limit
            writer.Write((byte)0);
            writer.Write((byte)0);
            writer.Write((byte)10); // respawn seconds
            if (mapName == "ctf_conflict")
            {
                writer.Write((byte)10); // time limit in minutes
                writer.Write((uint)120); // initial match clock
            }
            else
            {
                writer.Write((ushort)0); // KOTH unlock
                writer.Write((ushort)5400);
                writer.Write((ushort)5400);
                foreach (var _ in level.GetRoomObjects(RoomObjectType.ControlPoint))
                {
                    writer.Write(initialKothPointTeam);
                    writer.Write(byte.MaxValue);
                    writer.Write((ushort)0);
                }
            }

            writer.Write(new byte[10]); // class limits
        }

        session.ApplyJoinStatePayload(stream.ToArray());
        session.CreateSnapshotMessage(); // clear initial events
        return (session, x, y);
    }

    private static void ApplyInputTick(
        ReDsmReplayTransport.ReDsmReplayTranslator.LegacyReplaySession session,
        int x, int y, byte keys)
    {
        using var stream = new MemoryStream();
        using (var writer = new BinaryWriter(stream, Encoding.Latin1, leaveOpen: true))
        {
            writer.Write((byte)1); // player count
            writer.Write((byte)1); // character present
            writer.Write(keys);
            writer.Write((ushort)0); // aim right
            writer.Write((byte)50);
        }

        stream.Position = 0;
        using var reader = new BinaryReader(stream, Encoding.Latin1);
        session.ApplyLiveMessage(reader, 6);
    }

    private static void ApplyLivePayload(
        ReDsmReplayTransport.ReDsmReplayTranslator.LegacyReplaySession session,
        byte messageType,
        Action<BinaryWriter> writePayload)
    {
        using var stream = new MemoryStream();
        using (var writer = new BinaryWriter(stream, Encoding.Latin1, leaveOpen: true))
        {
            writePayload(writer);
        }
        stream.Position = 0;
        using var reader = new BinaryReader(stream, Encoding.Latin1);
        session.ApplyLiveMessage(reader, messageType);
    }

    private static void WriteQuickPlayer(
        BinaryWriter writer, byte keys, int x, int y, bool hasBuiltSentry = false,
        sbyte horizontalSpeed = 0, sbyte verticalSpeed = 0, byte flags = 0)
    {
        writer.Write((byte)(hasBuiltSentry ? 3 : 1)); // character and optional sentry
        writer.Write(keys);
        writer.Write((ushort)0); // aim right
        writer.Write((byte)50);
        writer.Write(checked((ushort)(x * 5)));
        writer.Write(checked((ushort)(y * 5)));
        writer.Write(horizontalSpeed);
        writer.Write(verticalSpeed);
        writer.Write((byte)100);
        writer.Write((byte)4);
        writer.Write(flags);
        if (hasBuiltSentry)
        {
            writer.Write((byte)(0x80 | 127));
        }
    }

    private static (int X, int Y) FindOpenPoint(SimpleLevel level)
    {
        for (var y = 80; y < level.Bounds.Height - 80; y += 20)
        {
            for (var x = 80; x < level.Bounds.Width - 80; x += 20)
            {
                if (!level.IntersectsSolid(x - 40, y - 40, x + 60, y + 40))
                {
                    return (x, y);
                }
            }
        }

        throw new Xunit.Sdk.XunitException("Corinth has no open space for the weapon presentation test.");
    }

    private static (int X, int Y) FindGroundedPoint(SimpleLevel level)
    {
        var soldier = CharacterClassCatalog.GetDefinition(PlayerClass.Soldier);
        for (var y = 60; y < level.Bounds.Height - 20; y += 1)
        {
            for (var x = 60; x < level.Bounds.Width - 60; x += 20)
            {
                var probeY = y + soldier.CollisionBottom + 1f;
                var leftProbeX = x + soldier.CollisionLeft + 2f;
                var rightProbeX = x + soldier.CollisionRight - 2f;
                var hasFloor = level.Solids.Any(solid =>
                    (leftProbeX >= solid.Left && leftProbeX < solid.Right
                        && probeY >= solid.Top && probeY < solid.Bottom)
                    || (rightProbeX >= solid.Left && rightProbeX < solid.Right
                        && probeY >= solid.Top && probeY < solid.Bottom));
                if (hasFloor && !level.IntersectsSolid(
                    x + soldier.CollisionLeft, y + soldier.CollisionTop,
                    x + soldier.CollisionRight, y + soldier.CollisionBottom))
                {
                    return (x, y);
                }
            }
        }

        throw new Xunit.Sdk.XunitException("Corinth has no resting floor for the movement test.");
    }

    private static void WriteShortString(BinaryWriter writer, string text)
    {
        var bytes = Encoding.Latin1.GetBytes(text);
        writer.Write(checked((byte)bytes.Length));
        writer.Write(bytes);
    }
}
