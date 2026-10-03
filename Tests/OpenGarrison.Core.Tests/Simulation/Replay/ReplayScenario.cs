using OpenGarrison.Core;

namespace OpenGarrison.Core.Tests.Simulation.Replay;

public sealed record ReplayScenario(
    string Name,
    int TickCount,
    Func<SimulationWorld> Setup,
    Func<int, IReadOnlyDictionary<byte, PlayerInputSnapshot>> Inputs)
{
    public static ReplayScenario Movement()
    {
        return new ReplayScenario(
            "Movement",
            600,
            () =>
            {
                var world = CreateWorld();
                JoinLocalPlayer(world, PlayerClass.Scout, PlayerTeam.Red);
                return world;
            },
            tick => SingleInput(
                MovementInput(
                    right: true,
                    up: IsMovementJumpTick(tick))));
    }

    public static ReplayScenario Combat()
    {
        var redAimX = 0f;
        var redAimY = 0f;
        var blueAimX = 0f;
        var blueAimY = 0f;

        return new ReplayScenario(
            "Combat",
            1200,
            () =>
            {
                var world = CreateWorld();
                JoinLocalPlayer(world, PlayerClass.Soldier, PlayerTeam.Red);
                JoinNetworkPlayer(world, 2, PlayerClass.Soldier, PlayerTeam.Blue);

                if (!world.NetworkPlayerRules.TryGetNetworkPlayer(2, out var bluePlayer))
                {
                    throw new InvalidOperationException("The combat replay could not materialize slot 2.");
                }

                world.LocalPlayer.SetSpawnRoomState(false);
                bluePlayer.SetSpawnRoomState(false);
                redAimX = bluePlayer.X;
                redAimY = bluePlayer.Y;
                blueAimX = world.LocalPlayer.X;
                blueAimY = world.LocalPlayer.Y;
                return world;
            },
            tick => new Dictionary<byte, PlayerInputSnapshot>
            {
                [SimulationWorld.LocalPlayerSlot] = CombatInput(redAimX, redAimY),
                [2] = CombatInput(blueAimX, blueAimY),
            });
    }

    public static ReplayScenario Structures()
    {
        return new ReplayScenario(
            "Structures",
            900,
            () =>
            {
                var world = CreateWorld();
                JoinLocalPlayer(world, PlayerClass.Engineer, PlayerTeam.Red);
                world.LocalPlayer.SetSpawnRoomState(false);
                return world;
            },
            tick => SingleInput(StructureInput(tick)));
    }

    private static SimulationWorld CreateWorld()
    {
        return new SimulationWorld(new SimulationConfig { EnableLocalDummies = false });
    }

    private static void JoinLocalPlayer(SimulationWorld world, PlayerClass playerClass, PlayerTeam team)
    {
        world.NetworkPlayerRules.PrepareLocalPlayerJoin();
        if (!world.NetworkPlayerRules.TrySetNetworkPlayerTeam(
                SimulationWorld.LocalPlayerSlot,
                team,
                respawnLivePlayerImmediately: true))
        {
            throw new InvalidOperationException("The replay could not configure the local player's team.");
        }

        world.NetworkPlayerRules.CompleteLocalPlayerJoin(playerClass);
    }

    private static void JoinNetworkPlayer(
        SimulationWorld world,
        byte slot,
        PlayerClass playerClass,
        PlayerTeam team)
    {
        if (!world.NetworkPlayerRules.TryPrepareNetworkPlayerJoin(slot)
            || !world.NetworkPlayerRules.TrySetNetworkPlayerTeam(slot, team, respawnLivePlayerImmediately: true)
            || !world.NetworkPlayerRules.TryApplyNetworkPlayerClassSelection(slot, playerClass))
        {
            throw new InvalidOperationException($"The replay could not join network player slot {slot}.");
        }
    }

    private static Dictionary<byte, PlayerInputSnapshot> SingleInput(PlayerInputSnapshot input)
    {
        return new Dictionary<byte, PlayerInputSnapshot>
        {
            [SimulationWorld.LocalPlayerSlot] = input,
        };
    }

    private static PlayerInputSnapshot MovementInput(bool right, bool up)
    {
        return new PlayerInputSnapshot(
            Left: false,
            Right: right,
            Up: up,
            Down: false,
            BuildSentry: false,
            DestroySentry: false,
            Taunt: false,
            FirePrimary: false,
            FireSecondary: false,
            AimWorldX: 0f,
            AimWorldY: 0f,
            DebugKill: false);
    }

    private static PlayerInputSnapshot CombatInput(float aimWorldX, float aimWorldY)
    {
        return new PlayerInputSnapshot(
            Left: false,
            Right: false,
            Up: false,
            Down: false,
            BuildSentry: false,
            DestroySentry: false,
            Taunt: false,
            FirePrimary: true,
            FireSecondary: false,
            AimWorldX: aimWorldX,
            AimWorldY: aimWorldY,
            DebugKill: false);
    }

    private static PlayerInputSnapshot StructureInput(int tick)
    {
        return new PlayerInputSnapshot(
            Left: false,
            Right: false,
            Up: false,
            Down: false,
            BuildSentry: tick == 30,
            DestroySentry: tick == 180,
            Taunt: false,
            FirePrimary: false,
            FireSecondary: false,
            AimWorldX: 0f,
            AimWorldY: 0f,
            DebugKill: false,
            BuildDispenser: tick == 500,
            DestroyDispenser: tick == 750);
    }

    private static bool IsMovementJumpTick(int tick)
    {
        // The short periodic bursts exercise ordinary jump-edge handling. The
        // separated 300/302 presses consume the scout's extra air jump once.
        if (tick is 300 or 302)
        {
            return true;
        }

        return tick < 270 && (tick % 90 is 0 or 1 or 2);
    }
}
