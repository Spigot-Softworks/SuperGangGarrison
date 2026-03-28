using System;
using System.Collections.Generic;
using System.Linq;

namespace OpenGarrison.Core;

public sealed partial class SimulationWorld
{
    private static class MatchObjectiveFlowSystem
    {
        public static void UpdateCaptureTheFlagState(SimulationWorld world)
        {
            if (world.MatchState.IsEnded)
            {
                return;
            }

            var redWasDropped = world.RedIntel.IsDropped;
            var blueWasDropped = world.BlueIntel.IsDropped;
            world.RedIntel.AdvanceTick();
            world.BlueIntel.AdvanceTick();
            if (redWasDropped && world.RedIntel.IsAtBase)
            {
                world.RegisterWorldSoundEvent("IntelDropSnd", world.RedIntel.X, world.RedIntel.Y);
                world.RecordIntelReturnedObjectiveLog(PlayerTeam.Red);
            }

            if (blueWasDropped && world.BlueIntel.IsAtBase)
            {
                world.RegisterWorldSoundEvent("IntelDropSnd", world.BlueIntel.X, world.BlueIntel.Y);
                world.RecordIntelReturnedObjectiveLog(PlayerTeam.Blue);
            }

            foreach (var player in world.EnumerateSimulatedPlayers())
            {
                if (!player.IsAlive)
                {
                    continue;
                }

                world.TryPickUpEnemyIntel(player);
                world.TryScoreCarriedIntel(player);
            }
        }

        public static void UpdateArenaState(SimulationWorld world)
        {
            if (world.MatchState.IsEnded)
            {
                return;
            }

            if (world._arenaUnlockTicksRemaining > 0)
            {
                world._arenaUnlockTicksRemaining -= 1;
            }

            var redCappers = world.CountPlayersInArenaCaptureZone(PlayerTeam.Red);
            var blueCappers = world.CountPlayersInArenaCaptureZone(PlayerTeam.Blue);
            var defended = redCappers > 0 && blueCappers > 0;
            PlayerTeam? capTeam = null;
            var cappers = 0;

            if (redCappers > 0 && blueCappers == 0 && world._arenaPointTeam != PlayerTeam.Red)
            {
                capTeam = PlayerTeam.Red;
                cappers = redCappers;
            }
            else if (blueCappers > 0 && redCappers == 0 && world._arenaPointTeam != PlayerTeam.Blue)
            {
                capTeam = PlayerTeam.Blue;
                cappers = blueCappers;
            }

            if (world._arenaCappingTicks > 0f && world._arenaCappingTeam != capTeam)
            {
                cappers = 0;
            }
            else if (world._arenaPointTeam.HasValue && capTeam == world._arenaPointTeam.Value)
            {
                cappers = 0;
            }

            world._arenaCappers = cappers;

            var capStrength = 0f;
            for (var index = 1; index <= cappers; index += 1)
            {
                capStrength += index <= 2 ? 1f : 0.5f;
            }

            if (world._arenaUnlockTicksRemaining > 0)
            {
                world._arenaCappingTicks = 0f;
                world._arenaCappingTeam = null;
                return;
            }

            if (capTeam.HasValue && cappers > 0 && world._arenaCappingTicks < ArenaPointCapTimeTicksDefault)
            {
                world._arenaCappingTicks += capStrength;
                world._arenaCappingTeam = capTeam;
            }
            else if (world._arenaCappingTicks > 0f && cappers == 0 && !defended)
            {
                world._arenaCappingTicks -= 1f;
                if (world._arenaPointTeam == PlayerTeam.Blue)
                {
                    world._arenaCappingTicks -= blueCappers * 0.5f;
                }
                else if (world._arenaPointTeam == PlayerTeam.Red)
                {
                    world._arenaCappingTicks -= redCappers * 0.5f;
                }
            }

            if (world._arenaCappingTicks <= 0f)
            {
                world._arenaCappingTicks = 0f;
                world._arenaCappingTeam = null;
                return;
            }

            if (world._arenaCappingTicks >= ArenaPointCapTimeTicksDefault && world._arenaCappingTeam.HasValue)
            {
                world._arenaPointTeam = world._arenaCappingTeam.Value;
                EndArenaRound(world, world._arenaPointTeam.Value);
            }
        }

        public static void AdvanceMatchState(SimulationWorld world)
        {
            if (world.MatchRules.Mode == GameModeKind.Arena)
            {
                AdvanceArenaMatchState(world);
                return;
            }

            if (world.MatchRules.Mode == GameModeKind.ControlPoint)
            {
                AdvanceControlPointMatchState(world);
                return;
            }

            if (world.MatchRules.Mode == GameModeKind.Generator)
            {
                AdvanceGeneratorMatchState(world);
                return;
            }

            if (world.MatchState.IsEnded)
            {
                return;
            }

            var capWinner = GetCapLimitWinner(world);
            if (capWinner.HasValue)
            {
                world.MatchState = world.MatchState with { Phase = MatchPhase.Ended, WinnerTeam = capWinner };
                world.QueuePendingMapChange();
                return;
            }

            if (world.MatchState.Phase == MatchPhase.Overtime)
            {
                if (!AreCaptureTheFlagObjectivesSettled(world))
                {
                    return;
                }

                world.MatchState = world.MatchState with
                {
                    Phase = MatchPhase.Ended,
                    WinnerTeam = GetHigherCapWinner(world),
                };
                world.QueuePendingMapChange();
                return;
            }

            if (world.MatchState.TimeRemainingTicks > 0)
            {
                world.MatchState = world.MatchState with { TimeRemainingTicks = world.MatchState.TimeRemainingTicks - 1 };
                if (world.MatchState.TimeRemainingTicks > 0)
                {
                    return;
                }
            }

            if (AreCaptureTheFlagObjectivesSettled(world))
            {
                world.MatchState = world.MatchState with { Phase = MatchPhase.Ended, WinnerTeam = GetHigherCapWinner(world) };
                world.QueuePendingMapChange();
                return;
            }

            world.MatchState = world.MatchState with { Phase = MatchPhase.Overtime, WinnerTeam = null };
        }

        public static void UpdateGeneratorState(SimulationWorld world)
        {
            _ = world;
            // Generator objectives are passive. Damage resolution happens in the combat systems.
        }

        public static void AdvanceGeneratorMatchState(SimulationWorld world)
        {
            if (world.MatchState.IsEnded)
            {
                return;
            }

            var capWinner = GetCapLimitWinner(world);
            if (capWinner.HasValue)
            {
                world.MatchState = world.MatchState with { Phase = MatchPhase.Ended, WinnerTeam = capWinner };
                world.QueuePendingMapChange();
                return;
            }

            if (world.MatchState.TimeRemainingTicks > 0)
            {
                world.MatchState = world.MatchState with { TimeRemainingTicks = world.MatchState.TimeRemainingTicks - 1 };
                if (world.MatchState.TimeRemainingTicks > 0)
                {
                    return;
                }
            }

            world.MatchState = world.MatchState with { Phase = MatchPhase.Ended, WinnerTeam = GetHigherCapWinner(world) };
            world.QueuePendingMapChange();
        }

        public static void AdvanceControlPointMatchState(SimulationWorld world)
        {
            if (world.MatchState.IsEnded)
            {
                return;
            }

            if (world._controlPointSetupMode && world._controlPointSetupTicksRemaining > 0)
            {
                world._controlPointSetupTicksRemaining -= 1;
                var ticksPerSecond = world.Config.TicksPerSecond;
                if (world._controlPointSetupTicksRemaining == ticksPerSecond * 6
                    || world._controlPointSetupTicksRemaining == ticksPerSecond * 5
                    || world._controlPointSetupTicksRemaining == ticksPerSecond * 4
                    || world._controlPointSetupTicksRemaining == ticksPerSecond * 3)
                {
                    world.RegisterWorldSoundEvent("CountDown1Snd", world.LocalPlayer.X, world.LocalPlayer.Y);
                }
                else if (world._controlPointSetupTicksRemaining == ticksPerSecond * 2)
                {
                    world.RegisterWorldSoundEvent("CountDown2Snd", world.LocalPlayer.X, world.LocalPlayer.Y);
                }
                else if (world._controlPointSetupTicksRemaining == ticksPerSecond)
                {
                    world.MatchState = world.MatchState with { TimeRemainingTicks = world.MatchRules.TimeLimitTicks };
                    world.RegisterWorldSoundEvent("SirenSnd", world.LocalPlayer.X, world.LocalPlayer.Y);
                }
            }

            world.UpdateControlPointSetupGates();

            if (world.MatchState.TimeRemainingTicks > 0)
            {
                world.MatchState = world.MatchState with { TimeRemainingTicks = world.MatchState.TimeRemainingTicks - 1 };
            }

            var overtimeActive = world.MatchState.TimeRemainingTicks <= 0 && world._controlPoints.Any(point => point.CappingTicks > 0f);
            if (overtimeActive && !world.MatchState.IsOvertime)
            {
                world.MatchState = world.MatchState with { Phase = MatchPhase.Overtime, WinnerTeam = null };
            }

            var winner = ResolveControlPointWinner(world, overtimeActive);
            if (winner.HasValue)
            {
                world.MatchState = world.MatchState with { Phase = MatchPhase.Ended, WinnerTeam = winner };
                world.QueuePendingMapChange();
                return;
            }

            if (world.MatchState.TimeRemainingTicks <= 0 && !overtimeActive)
            {
                world.MatchState = world.MatchState with { Phase = MatchPhase.Ended, WinnerTeam = null };
                world.QueuePendingMapChange();
            }
            else if (!overtimeActive && world.MatchState.IsOvertime)
            {
                world.MatchState = world.MatchState with { Phase = MatchPhase.Running, WinnerTeam = null };
            }
        }

        private static void AdvanceArenaMatchState(SimulationWorld world)
        {
            if (world.MatchState.IsEnded)
            {
                return;
            }

            var redAlive = world.ArenaRedAliveCount;
            var blueAlive = world.ArenaBlueAliveCount;
            var redPlayers = world.ArenaRedPlayerCount;
            var bluePlayers = world.ArenaBluePlayerCount;

            if (redPlayers > 0 && bluePlayers > 0)
            {
                if (redAlive == 0 && blueAlive > 0)
                {
                    EndArenaRound(world, PlayerTeam.Blue);
                    return;
                }

                if (blueAlive == 0 && redAlive > 0)
                {
                    EndArenaRound(world, PlayerTeam.Red);
                    return;
                }
            }

            if (world.MatchState.TimeRemainingTicks > 0)
            {
                world.MatchState = world.MatchState with { TimeRemainingTicks = world.MatchState.TimeRemainingTicks - 1 };
                if (world.MatchState.TimeRemainingTicks > 0)
                {
                    return;
                }
            }

            if (redAlive > 0 && blueAlive > 0 && redPlayers > 0 && bluePlayers > 0)
            {
                world.MatchState = world.MatchState with { Phase = MatchPhase.Overtime, WinnerTeam = null };
                return;
            }

            world.MatchState = world.MatchState with { Phase = MatchPhase.Ended, WinnerTeam = null };
            world.QueuePendingMapChange();
        }

        private static void EndArenaRound(SimulationWorld world, PlayerTeam winner)
        {
            if (world.MatchState.IsEnded)
            {
                return;
            }

            if (winner == PlayerTeam.Red)
            {
                world._arenaRedConsecutiveWins += 1;
                world._arenaBlueConsecutiveWins = 0;
            }
            else
            {
                world._arenaBlueConsecutiveWins += 1;
                world._arenaRedConsecutiveWins = 0;
            }

            world.MatchState = world.MatchState with { Phase = MatchPhase.Ended, WinnerTeam = winner };
            world.QueuePendingMapChange();
        }

        private static bool AreCaptureTheFlagObjectivesSettled(SimulationWorld world)
        {
            return world.IsIntelAtHome(world.RedIntel) && world.IsIntelAtHome(world.BlueIntel);
        }

        private static PlayerTeam? GetCapLimitWinner(SimulationWorld world)
        {
            if (world.RedCaps >= world.MatchRules.CapLimit)
            {
                return PlayerTeam.Red;
            }

            if (world.BlueCaps >= world.MatchRules.CapLimit)
            {
                return PlayerTeam.Blue;
            }

            return null;
        }

        private static PlayerTeam? GetHigherCapWinner(SimulationWorld world)
        {
            if (world.RedCaps > world.BlueCaps)
            {
                return PlayerTeam.Red;
            }

            if (world.BlueCaps > world.RedCaps)
            {
                return PlayerTeam.Blue;
            }

            return null;
        }

        private static PlayerTeam? ResolveControlPointWinner(SimulationWorld world, bool overtimeActive)
        {
            if (world._controlPoints.Count == 0)
            {
                return null;
            }

            if (!world._controlPointSetupMode)
            {
                var firstTeam = world._controlPoints[0].Team;
                var lastTeam = world._controlPoints[^1].Team;
                if (firstTeam.HasValue && lastTeam.HasValue && firstTeam.Value == lastTeam.Value)
                {
                    return firstTeam.Value;
                }

                return null;
            }

            var finalTeam = world._controlPoints[^1].Team;
            if (finalTeam == PlayerTeam.Red)
            {
                return PlayerTeam.Red;
            }

            if (world.MatchState.TimeRemainingTicks <= 0 && !overtimeActive)
            {
                return PlayerTeam.Blue;
            }

            return null;
        }
    }
}
