from __future__ import annotations

import argparse
import json
import shutil
import subprocess
from dataclasses import asdict, dataclass
from pathlib import Path
from typing import Any


@dataclass(frozen=True)
class ReplayLane:
    name: str
    level_name: str
    team: str
    class_id: str
    task: str
    ticks: int
    model_path: str
    start_node_id: int = -1
    start_x: float | None = None
    start_y: float | None = None
    carrying_intel: bool | None = None


@dataclass
class ReplayAttempt:
    lane: str
    seed: int
    stochastic: bool
    success: bool
    terminal_reason: str
    ticks_elapsed: int
    total_reward: float
    output_path: str


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description="Build a successful MLBot rollout replay bank.")
    parser.add_argument("--rollout-project", required=True)
    parser.add_argument("--output-dir", required=True)
    parser.add_argument(
        "--lane",
        action="append",
        required=True,
        help="name,map,team,class,task,ticks,model_path[,start_node_id] or name,map,team,class,task,ticks,model_path,start_x,start_y[,carrying_intel].",
    )
    parser.add_argument("--seeds", default="1337")
    parser.add_argument("--temperature", type=float, default=1.4)
    parser.add_argument("--successes-per-lane", type=int, default=4)
    parser.add_argument("--attempts-per-lane", type=int, default=16)
    parser.add_argument("--include-deterministic", action="store_true")
    parser.add_argument("--rollout-no-build", action="store_true")
    parser.add_argument("--disable-policy-overrides", action="store_true")
    return parser.parse_args()


def parse_lane(raw_value: str) -> ReplayLane:
    parts = [part.strip() for part in raw_value.split(",")]
    if len(parts) not in (7, 8, 9, 10) or any(not part for part in parts[:7]):
        raise ValueError(
            "--lane must be formatted as name,map,team,class,task,ticks,model_path[,start_node_id] "
            "or name,map,team,class,task,ticks,model_path,start_x,start_y[,carrying_intel]"
        )
    if len(parts) in (9, 10):
        return ReplayLane(
            name=parts[0],
            level_name=parts[1],
            team=parts[2],
            class_id=parts[3],
            task=parts[4],
            ticks=int(parts[5]),
            model_path=parts[6],
            start_x=float(parts[7]),
            start_y=float(parts[8]),
            carrying_intel=parse_optional_bool(parts[9]) if len(parts) == 10 else None,
        )
    return ReplayLane(
        name=parts[0],
        level_name=parts[1],
        team=parts[2],
        class_id=parts[3],
        task=parts[4],
        ticks=int(parts[5]),
        model_path=parts[6],
        start_node_id=int(parts[7]) if len(parts) == 8 and parts[7] else -1,
    )


def parse_optional_bool(raw_value: str) -> bool | None:
    value = raw_value.strip().lower()
    if not value:
        return None
    if value in {"1", "true", "yes", "y", "carry", "carrying"}:
        return True
    if value in {"0", "false", "no", "n", "none", "not-carrying"}:
        return False
    raise ValueError(f"invalid boolean value: {raw_value}")


def parse_seeds(raw_value: str) -> list[int]:
    seeds = [int(part.strip()) for part in raw_value.split(",") if part.strip()]
    if not seeds:
        raise ValueError("--seeds did not contain any seeds")
    return seeds


def run_lane_attempt(
    args: argparse.Namespace,
    lane: ReplayLane,
    output_path: Path,
    seed: int,
    stochastic: bool,
) -> ReplayAttempt:
    command = [
        "dotnet",
        "run",
        "--project",
        args.rollout_project,
    ]
    if args.rollout_no_build:
        command.append("--no-build")
    command.extend(
        [
            "--",
            "export-rollout",
            "--map",
            lane.level_name,
            "--team",
            lane.team,
            "--class",
            lane.class_id,
            "--task",
            lane.task,
            "--ticks",
            str(lane.ticks),
            "--model",
            lane.model_path,
            "--out",
            str(output_path),
        ]
    )
    if lane.start_node_id >= 0:
        command.extend(["--start-node-id", str(lane.start_node_id)])
    if lane.start_x is not None and lane.start_y is not None:
        command.extend(["--start-x", str(lane.start_x), "--start-y", str(lane.start_y)])
    elif lane.start_x is not None or lane.start_y is not None:
        raise ValueError(f"lane {lane.name} must provide both start_x and start_y")
    if lane.carrying_intel is True:
        command.append("--carrying-intel")
    elif lane.carrying_intel is False:
        command.append("--no-carrying-intel")
    if stochastic:
        command.extend(["--stochastic", "--seed", str(seed), "--temperature", str(args.temperature)])
    if args.disable_policy_overrides:
        command.append("--disable-policy-overrides")

    completed = subprocess.run(command, check=False, capture_output=True, text=True, encoding="utf-8")
    if completed.returncode != 0:
        raise RuntimeError(
            "rollout export failed\n"
            f"command={' '.join(command)}\nstdout={completed.stdout}\nstderr={completed.stderr}"
        )

    with output_path.open("r", encoding="utf-8") as handle:
        payload: dict[str, Any] = json.load(handle)
    return ReplayAttempt(
        lane=lane.name,
        seed=seed,
        stochastic=stochastic,
        success=bool(payload["Success"]),
        terminal_reason=str(payload["TerminalReason"]),
        ticks_elapsed=int(payload["TicksElapsed"]),
        total_reward=float(payload["TotalReward"]),
        output_path=str(output_path),
    )


def main() -> None:
    args = parse_args()
    lanes = [parse_lane(raw_value) for raw_value in args.lane]
    seeds = parse_seeds(args.seeds)
    output_dir = Path(args.output_dir)
    output_dir.mkdir(parents=True, exist_ok=True)
    attempts_dir = output_dir / "attempts"
    successes_dir = output_dir / "successes"
    attempts_dir.mkdir(parents=True, exist_ok=True)
    successes_dir.mkdir(parents=True, exist_ok=True)

    all_attempts: list[ReplayAttempt] = []
    for lane in lanes:
        lane_successes = 0
        lane_attempts = 0
        schedules: list[tuple[int, bool]] = []
        if args.include_deterministic:
            schedules.append((0, False))
        schedules.extend((seed, True) for seed in seeds)

        for seed, stochastic in schedules:
            if lane_successes >= args.successes_per_lane or lane_attempts >= args.attempts_per_lane:
                break
            suffix = f"seed-{seed}" if stochastic else "deterministic"
            output_path = attempts_dir / f"{lane.name}-{suffix}.json"
            attempt = run_lane_attempt(args, lane, output_path, seed, stochastic)
            all_attempts.append(attempt)
            lane_attempts += 1
            print(
                f"lane={lane.name} seed={seed} stochastic={stochastic} success={attempt.success} "
                f"terminal={attempt.terminal_reason} ticks={attempt.ticks_elapsed}"
            )
            if attempt.success:
                lane_successes += 1
                shutil.copy2(output_path, successes_dir / output_path.name)

    with (output_dir / "replay-bank-summary.json").open("w", encoding="utf-8") as handle:
        json.dump(
            {
                "lanes": [asdict(lane) for lane in lanes],
                "attempts": [asdict(attempt) for attempt in all_attempts],
            },
            handle,
            indent=2,
        )


if __name__ == "__main__":
    main()
