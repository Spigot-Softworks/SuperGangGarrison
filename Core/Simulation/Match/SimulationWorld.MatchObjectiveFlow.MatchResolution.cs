using System.Linq;

namespace OpenGarrison.Core;

public sealed partial class SimulationWorld
{
    private static partial class MatchObjectiveFlowSystem
    {
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

        public static void AdvanceTeamDeathmatchMatchState(SimulationWorld world)
        {
            if (world.MatchState.IsEnded)
            {
                return;
            }

            var killLimitWinner = GetCapLimitWinner(world);
            if (killLimitWinner.HasValue)
            {
                world.MatchState = world.MatchState with { Phase = MatchPhase.Ended, WinnerTeam = killLimitWinner };
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
