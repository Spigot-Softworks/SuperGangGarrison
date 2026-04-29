from __future__ import annotations

import argparse
import json
import math
import random
import shutil
import subprocess
from dataclasses import asdict, dataclass
from pathlib import Path
from typing import Any

import numpy as np
import torch
import torch.nn as nn
import torch.nn.functional as F

from mlbot_dataset import (
    build_dataset_filter,
    load_behavior_cloning_dataset,
    parse_csv_filter,
    vectorize_observation,
)
from train_behavior_cloning import (
    BehaviorCloningModel,
    TaskConditionedBehaviorCloningModel,
    TaskConditionedMlpHeadBehaviorCloningModel,
    export_onnx_model,
    load_checkpoint_state_for_model,
)


class ValueNetwork(nn.Module):
    def __init__(self, input_size: int, hidden_size: int = 256) -> None:
        super().__init__()
        self.net = nn.Sequential(
            nn.Linear(input_size, hidden_size),
            nn.Tanh(),
            nn.Linear(hidden_size, hidden_size),
            nn.Tanh(),
            nn.Linear(hidden_size, 1),
        )

    def forward(self, observations: torch.Tensor) -> torch.Tensor:
        return self.net(observations).squeeze(1)


@dataclass
class HeadlessIterationMetrics:
    iteration: int
    rollout_reward_mean: float
    rollout_success_rate: float
    rollout_pickup_rate: float
    rollout_score_rate: float
    rollout_best_min_objective_distance: float
    rollout_best_final_objective_distance: float
    policy_loss: float
    bc_loss: float
    self_imitation_loss: float
    action_margin_loss: float
    value_loss: float
    self_imitation_sample_count: int
    entropy: float
    eval_success: bool
    eval_terminal_reason: str
    eval_min_objective_distance: float
    eval_max_stuck_ticks: float
    eval_ticks_elapsed: int


@dataclass(frozen=True)
class CurriculumStart:
    name: str
    start_x: float
    start_y: float
    carrying_intel: bool | None = None
    start_vx: float | None = None
    start_vy: float | None = None


@dataclass(frozen=True)
class RolloutBuffer:
    observations: torch.Tensor
    move_targets: torch.Tensor
    binary_targets: torch.Tensor
    returns: torch.Tensor
    advantages: torch.Tensor
    old_log_probs: torch.Tensor


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser()
    parser.add_argument("--data-root", required=True)
    parser.add_argument("--output-dir", required=True)
    parser.add_argument("--init-checkpoint", required=True)
    parser.add_argument("--rollout-project", required=True)
    parser.add_argument("--map", required=True)
    parser.add_argument("--team", required=True)
    parser.add_argument("--class", dest="class_id", required=True)
    parser.add_argument("--task", required=True)
    parser.add_argument("--ticks", type=int, default=900)
    parser.add_argument("--start-node-id", type=int, default=-1)
    parser.add_argument("--start-x", type=float, default=None)
    parser.add_argument("--start-y", type=float, default=None)
    parser.add_argument("--start-vx", type=float, default=None)
    parser.add_argument("--start-vy", type=float, default=None)
    parser.add_argument("--carrying-intel", dest="carrying_intel", action="store_true")
    parser.add_argument("--no-carrying-intel", dest="carrying_intel", action="store_false")
    parser.set_defaults(carrying_intel=None)
    parser.add_argument("--iterations", type=int, default=20)
    parser.add_argument("--episodes-per-iter", type=int, default=8)
    parser.add_argument("--selection-episodes-per-iter", type=int, default=4)
    parser.add_argument("--learning-rate", type=float, default=3e-4)
    parser.add_argument("--gamma", type=float, default=0.99)
    parser.add_argument("--gae-lambda", type=float, default=0.95)
    parser.add_argument("--reward-scale", type=float, default=1.0)
    parser.add_argument("--entropy-coef", type=float, default=0.01)
    parser.add_argument("--bc-coef", type=float, default=0.25)
    parser.add_argument("--self-imitation-coef", type=float, default=0.5)
    parser.add_argument("--self-imitation-top-k", type=int, default=4)
    parser.add_argument("--self-imitation-segment-mode", choices=("full", "best-window", "return-breakthrough"), default="full")
    parser.add_argument("--self-imitation-segment-pre-ticks", type=int, default=45)
    parser.add_argument("--self-imitation-segment-post-ticks", type=int, default=180)
    parser.add_argument("--self-imitation-segment-corridor-slack", type=float, default=96.0)
    parser.add_argument("--self-imitation-segment-min-improvement", type=float, default=24.0)
    parser.add_argument("--self-imitation-exclude-zero-min-distance", action="store_true")
    parser.add_argument("--self-imitation-max-min-objective-distance", type=float, default=0.0)
    parser.add_argument("--self-imitation-max-final-objective-distance", type=float, default=0.0)
    parser.add_argument("--action-margin-coef", type=float, default=0.0)
    parser.add_argument("--move-margin", type=float, default=1.0)
    parser.add_argument("--binary-margin", type=float, default=0.5)
    parser.add_argument("--reference-kl-coef", type=float, default=0.1)
    parser.add_argument("--rl-algorithm", choices=("reinforce", "ppo"), default="reinforce")
    parser.add_argument("--ppo-epochs", type=int, default=3)
    parser.add_argument("--ppo-minibatch-size", type=int, default=2048)
    parser.add_argument("--ppo-clip-eps", type=float, default=0.2)
    parser.add_argument("--value-coef", type=float, default=0.25)
    parser.add_argument(
        "--selection-key",
        choices=("deterministic-first", "rollout-first", "final"),
        default="deterministic-first",
        help="How to retain the best checkpoint during headless exploration.",
    )
    parser.add_argument("--bc-batch-size", type=int, default=512)
    parser.add_argument("--temperature", type=float, default=1.0)
    parser.add_argument(
        "--curriculum-start",
        action="append",
        default=[],
        help="Named world start as name,start_x,start_y[,carrying_intel[,start_vx,start_vy]]. Used for stochastic rollout collection.",
    )
    parser.add_argument(
        "--curriculum-start-probability",
        type=float,
        default=1.0,
        help="Probability that each stochastic rollout uses a supplied curriculum start.",
    )
    parser.add_argument("--seed", type=int, default=1337)
    parser.add_argument("--resolved-phases", default="")
    parser.add_argument("--requested-phases", default="")
    parser.add_argument("--class-ids", default="")
    parser.add_argument("--teams", default="")
    parser.add_argument("--maps", default="")
    parser.add_argument("--capture-kinds", default="")
    parser.add_argument("--success-only", action="store_true")
    parser.add_argument("--corrected-upweight", type=int, default=1)
    parser.add_argument("--bc-retarget-class-id", default="")
    parser.add_argument("--allow-empty-bc-anchor", action="store_true")
    parser.add_argument("--rollout-no-build", action="store_true")
    parser.add_argument("--discard-rollouts", action="store_true")
    parser.add_argument("--keep-successful-rollouts", action="store_true")
    parser.add_argument(
        "--max-retained-rollout-iters",
        type=int,
        default=0,
        help="Delete older per-iteration rollout JSON directories after this many iterations. 0 keeps all.",
    )
    parser.add_argument(
        "--archive-top-rollouts",
        type=int,
        default=4,
        help="Copy the top N training rollouts from each iteration into best-rollouts before retention pruning.",
    )
    parser.add_argument("--disable-policy-overrides", action="store_true")
    parser.add_argument(
        "--task-conditioned-heads",
        action="store_true",
        help="Use the V5 task-gated head architecture used by unified Scout/CTF checkpoints.",
    )
    parser.add_argument(
        "--task-conditioned-mlp-heads",
        action="store_true",
        help="Use nonlinear task-gated heads initialized compatibly from linear task-head checkpoints.",
    )
    parser.add_argument("--hidden-size", type=int, default=256)
    parser.add_argument("--cpu", action="store_true")
    return parser.parse_args()


def compute_step_total(step: dict[str, Any]) -> float:
    reward = step["Reward"]
    return float(
        reward.get("ProgressReward", 0.0)
        + reward.get("ObjectiveReward", 0.0)
        + reward.get("DeathPenalty", 0.0)
        + reward.get("TimeoutPenalty", 0.0)
        + reward.get("StuckPenalty", 0.0)
    )


def compute_returns(rewards: list[float], gamma: float) -> list[float]:
    running = 0.0
    returns = [0.0] * len(rewards)
    for index in range(len(rewards) - 1, -1, -1):
        running = rewards[index] + gamma * running
        returns[index] = running
    return returns


def move_direction_to_index(move_direction: int) -> int:
    return move_direction + 1


def parse_optional_bool(raw_value: str) -> bool | None:
    value = raw_value.strip().lower()
    if not value:
        return None
    if value in {"1", "true", "yes", "y", "carry", "carrying"}:
        return True
    if value in {"0", "false", "no", "n", "none", "not-carrying"}:
        return False
    raise ValueError(f"invalid boolean value: {raw_value}")


def parse_curriculum_start(raw_value: str) -> CurriculumStart:
    parts = [part.strip() for part in raw_value.split(",")]
    if len(parts) not in (3, 4, 6) or any(not part for part in parts[:3]):
        raise ValueError(
            "--curriculum-start must be formatted as name,start_x,start_y[,carrying_intel[,start_vx,start_vy]]"
        )
    return CurriculumStart(
        name=parts[0],
        start_x=float(parts[1]),
        start_y=float(parts[2]),
        carrying_intel=parse_optional_bool(parts[3]) if len(parts) >= 4 else None,
        start_vx=float(parts[4]) if len(parts) == 6 else None,
        start_vy=float(parts[5]) if len(parts) == 6 else None,
    )


def choose_curriculum_start(
    curriculum_starts: list[CurriculumStart],
    probability: float,
) -> CurriculumStart | None:
    if not curriculum_starts or random.random() > max(0.0, min(1.0, probability)):
        return None
    return random.choice(curriculum_starts)


def collect_rollouts(
    args: argparse.Namespace,
    onnx_path: Path,
    output_dir: Path,
    iteration: int,
    *,
    episode_count: int | None = None,
    kind: str = "train",
) -> list[dict[str, Any]]:
    curriculum_starts = [parse_curriculum_start(value) for value in args.curriculum_start]
    rollout_dir = output_dir / "headless-rollouts" / f"{kind}-iter-{iteration:03d}"
    rollout_dir.mkdir(parents=True, exist_ok=True)
    documents: list[dict[str, Any]] = []
    total_episodes = episode_count if episode_count is not None else args.episodes_per_iter

    for episode_index in range(total_episodes):
        rollout_path = rollout_dir / f"episode-{episode_index:02d}.json"
        seed = args.seed + (iteration * 1000) + episode_index + (500000 if kind != "train" else 0)
        curriculum_start = choose_curriculum_start(curriculum_starts, args.curriculum_start_probability)
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
                args.map,
                "--team",
                args.team,
                "--class",
                args.class_id,
                "--task",
                args.task,
                "--ticks",
                str(args.ticks),
                "--model",
                str(onnx_path),
                "--out",
                str(rollout_path),
                "--stochastic",
                "--seed",
                str(seed),
                "--temperature",
                str(args.temperature),
            ]
        )
        if args.start_node_id >= 0:
            command.extend(["--start-node-id", str(args.start_node_id)])
        append_world_start_options(command, args, curriculum_start)
        if args.disable_policy_overrides:
            command.append("--disable-policy-overrides")

        completed = subprocess.run(command, check=False, capture_output=True, text=True, encoding="utf-8")
        if completed.returncode != 0:
            raise RuntimeError(
                "stochastic rollout collection failed\n"
                f"command={' '.join(command)}\nstdout={completed.stdout}\nstderr={completed.stderr}"
            )

        with rollout_path.open("r", encoding="utf-8") as handle:
            document = json.load(handle)
        document["_source_path"] = str(rollout_path)
        if curriculum_start is not None:
            document["CurriculumStart"] = asdict(curriculum_start)
        documents.append(document)
        should_keep_success = args.keep_successful_rollouts and rollout_has_primary_event(args.task, document)
        if args.discard_rollouts and not should_keep_success:
            rollout_path.unlink(missing_ok=True)

    return documents


def archive_top_rollouts(output_dir: Path, args: argparse.Namespace, iteration: int, rollouts: list[dict[str, Any]]) -> None:
    if args.archive_top_rollouts <= 0:
        return
    archive_dir = output_dir / "best-rollouts" / f"iter-{iteration:03d}"
    archive_dir.mkdir(parents=True, exist_ok=True)
    for rank, rollout in enumerate(select_top_rollouts(args.task, rollouts, args.archive_top_rollouts), start=1):
        source_path = Path(str(rollout.get("_source_path", "")))
        if not source_path.is_file():
            continue
        destination = archive_dir / f"rank-{rank:02d}-{source_path.name}"
        shutil.copy2(source_path, destination)


def prune_retained_rollout_dirs(output_dir: Path, args: argparse.Namespace, current_iteration: int) -> None:
    if args.max_retained_rollout_iters <= 0:
        return
    cutoff = current_iteration - args.max_retained_rollout_iters + 1
    rollout_root = output_dir / "headless-rollouts"
    if not rollout_root.is_dir():
        return

    for directory in rollout_root.iterdir():
        if not directory.is_dir():
            continue
        name = directory.name
        if not (name.startswith("train-iter-") or name.startswith("selection-iter-")):
            continue
        try:
            iteration = int(name.rsplit("-", 1)[-1])
        except ValueError:
            continue
        if iteration < cutoff:
            for path in directory.rglob("*"):
                if path.is_file():
                    path.unlink(missing_ok=True)
            for child in sorted(directory.rglob("*"), reverse=True):
                if child.is_dir():
                    child.rmdir()
            directory.rmdir()


def evaluate_deterministic(args: argparse.Namespace, onnx_path: Path, output_dir: Path, iteration: int) -> dict[str, Any]:
    eval_path = output_dir / "headless-rollouts" / f"iter-{iteration:03d}-eval.json"
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
            "--map",
            args.map,
            "--team",
            args.team,
            "--class",
            args.class_id,
            "--task",
            args.task,
            "--ticks",
            str(args.ticks),
            "--model",
            str(onnx_path),
            "--json-out",
            str(eval_path),
        ]
    )
    if args.start_node_id >= 0:
        command.extend(["--start-node-id", str(args.start_node_id)])
    append_world_start_options(command, args, None)
    if args.disable_policy_overrides:
        command.append("--disable-policy-overrides")

    completed = subprocess.run(command, check=False, capture_output=True, text=True, encoding="utf-8")
    if completed.returncode != 0:
        raise RuntimeError(
            "deterministic evaluation failed\n"
            f"command={' '.join(command)}\nstdout={completed.stdout}\nstderr={completed.stderr}"
        )

    with eval_path.open("r", encoding="utf-8") as handle:
        payload = json.load(handle)
    if args.discard_rollouts:
        eval_path.unlink(missing_ok=True)
    return payload


def append_world_start_options(
    command: list[str],
    args: argparse.Namespace,
    curriculum_start: CurriculumStart | None,
) -> None:
    if curriculum_start is not None:
        command.extend(["--start-x", str(curriculum_start.start_x), "--start-y", str(curriculum_start.start_y)])
        if curriculum_start.start_vx is not None and curriculum_start.start_vy is not None:
            command.extend(["--start-vx", str(curriculum_start.start_vx), "--start-vy", str(curriculum_start.start_vy)])
        elif curriculum_start.start_vx is not None or curriculum_start.start_vy is not None:
            raise ValueError(f"curriculum start {curriculum_start.name} requires both start_vx and start_vy")
        if curriculum_start.carrying_intel is True:
            command.append("--carrying-intel")
        elif curriculum_start.carrying_intel is False:
            command.append("--no-carrying-intel")
        return

    if args.start_x is not None and args.start_y is not None:
        command.extend(["--start-x", str(args.start_x), "--start-y", str(args.start_y)])
    elif args.start_x is not None or args.start_y is not None:
        raise ValueError("world-truth start fixtures require both --start-x and --start-y")

    if args.start_vx is not None and args.start_vy is not None:
        command.extend(["--start-vx", str(args.start_vx), "--start-vy", str(args.start_vy)])
    elif args.start_vx is not None or args.start_vy is not None:
        raise ValueError("world-truth start fixtures require both --start-vx and --start-vy")

    if args.carrying_intel is True:
        command.append("--carrying-intel")
    elif args.carrying_intel is False:
        command.append("--no-carrying-intel")


def primary_event_kind(task_phase: str) -> str:
    task_phase = task_phase.lower()
    if task_phase == "attackintel":
        return "pickup"
    if task_phase == "returnintel":
        return "score"
    return "success"


def rollout_contains_pickup(rollout: dict[str, Any]) -> bool:
    return any(
        step["TerminalReason"] == "picked_up_intel" or step["NextObservation"]["IsCarryingIntel"]
        for step in rollout["Steps"]
    )


def rollout_contains_score(rollout: dict[str, Any]) -> bool:
    return any(step["TerminalReason"] == "scored" for step in rollout["Steps"])


def rollout_has_primary_event(task_phase: str, rollout: dict[str, Any]) -> bool:
    primary_kind = primary_event_kind(task_phase)
    if primary_kind == "pickup":
        return rollout_contains_pickup(rollout)
    if primary_kind == "score":
        return rollout_contains_score(rollout)
    return bool(rollout.get("Success", False))


def rollout_min_objective_distance(rollout: dict[str, Any]) -> float:
    steps = rollout.get("Steps", [])
    if not steps:
        return float("inf")
    return min(float(step["NextObservation"]["ObjectiveDistance"]) for step in steps)


def rollout_final_objective_distance(rollout: dict[str, Any]) -> float:
    steps = rollout.get("Steps", [])
    if not steps:
        return float("inf")
    return float(steps[-1]["NextObservation"]["ObjectiveDistance"])


def step_navigation_distance(step: dict[str, Any]) -> float:
    observation = step["NextObservation"]
    waypoint = observation.get("Waypoint", {})
    if waypoint.get("HasWaypoint", False) and not waypoint.get("IsFinalWaypoint", False):
        return float(waypoint.get("Distance", observation.get("ObjectiveDistance", float("inf"))))
    return float(observation.get("ObjectiveDistance", float("inf")))


def rollout_min_navigation_distance(rollout: dict[str, Any]) -> float:
    steps = rollout.get("Steps", [])
    if not steps:
        return float("inf")
    return min(step_navigation_distance(step) for step in steps)


def rollout_final_navigation_distance(rollout: dict[str, Any]) -> float:
    steps = rollout.get("Steps", [])
    if not steps:
        return float("inf")
    return step_navigation_distance(steps[-1])


def summarize_rollouts(task_phase: str, rollouts: list[dict[str, Any]]) -> dict[str, float]:
    rewards = [float(rollout["TotalReward"]) for rollout in rollouts]
    successes = [1.0 if rollout["Success"] else 0.0 for rollout in rollouts]
    pickups = [1.0 if rollout_contains_pickup(rollout) else 0.0 for rollout in rollouts]
    scores = [1.0 if rollout_contains_score(rollout) else 0.0 for rollout in rollouts]
    min_distances = [rollout_min_objective_distance(rollout) for rollout in rollouts]
    final_distances = [rollout_final_objective_distance(rollout) for rollout in rollouts]
    min_navigation_distances = [rollout_min_navigation_distance(rollout) for rollout in rollouts]
    final_navigation_distances = [rollout_final_navigation_distance(rollout) for rollout in rollouts]
    return {
        "reward_mean": float(np.mean(rewards)),
        "success_rate": float(np.mean(successes)),
        "pickup_rate": float(np.mean(pickups)),
        "score_rate": float(np.mean(scores)),
        "best_min_objective_distance": float(np.min(min_distances)),
        "best_final_objective_distance": float(np.min(final_distances)),
        "mean_min_objective_distance": float(np.mean(min_distances)),
        "mean_final_objective_distance": float(np.mean(final_distances)),
        "best_min_navigation_distance": float(np.min(min_navigation_distances)),
        "best_final_navigation_distance": float(np.min(final_navigation_distances)),
        "mean_min_navigation_distance": float(np.mean(min_navigation_distances)),
        "mean_final_navigation_distance": float(np.mean(final_navigation_distances)),
        "primary_event_rate": float(
            np.mean(
                pickups if primary_event_kind(task_phase) == "pickup"
                else scores if primary_event_kind(task_phase) == "score"
                else successes
            )
        ),
    }


def rollout_episode_score(task_phase: str, rollout: dict[str, Any]) -> tuple[float, ...]:
    min_distance = rollout_min_objective_distance(rollout)
    final_distance = rollout_final_objective_distance(rollout)
    min_navigation_distance = rollout_min_navigation_distance(rollout)
    final_navigation_distance = rollout_final_navigation_distance(rollout)
    return (
        1.0 if rollout["Success"] else 0.0,
        1.0 if primary_event_kind(task_phase) == "pickup" and rollout_contains_pickup(rollout) else 0.0,
        1.0 if primary_event_kind(task_phase) == "score" and rollout_contains_score(rollout) else 0.0,
        -min_navigation_distance,
        -final_navigation_distance,
        -min_distance,
        -final_distance,
        float(rollout["TotalReward"]),
        -float(rollout["TicksElapsed"]),
    )


def select_top_rollouts(task_phase: str, rollouts: list[dict[str, Any]], top_k: int) -> list[dict[str, Any]]:
    if top_k <= 0 or len(rollouts) <= top_k:
        return rollouts
    return sorted(rollouts, key=lambda rollout: rollout_episode_score(task_phase, rollout), reverse=True)[:top_k]


def select_self_imitation_rollouts(args: argparse.Namespace, rollouts: list[dict[str, Any]]) -> list[dict[str, Any]]:
    filtered: list[dict[str, Any]] = []
    for rollout in rollouts:
        min_distance = rollout_min_objective_distance(rollout)
        final_distance = rollout_final_objective_distance(rollout)
        has_primary_event = (
            rollout_contains_pickup(rollout)
            if primary_event_kind(args.task) == "pickup"
            else rollout_contains_score(rollout)
            if primary_event_kind(args.task) == "score"
            else bool(rollout.get("Success", False))
        )
        if args.self_imitation_exclude_zero_min_distance and min_distance <= 1.0 and not has_primary_event:
            continue
        if args.self_imitation_max_min_objective_distance > 0.0 and min_distance > args.self_imitation_max_min_objective_distance:
            continue
        if args.self_imitation_max_final_objective_distance > 0.0 and final_distance > args.self_imitation_max_final_objective_distance:
            continue
        filtered.append(rollout)
    return select_top_rollouts(args.task, filtered, args.self_imitation_top_k)


def build_behavior_anchor(args: argparse.Namespace) -> tuple[torch.Tensor, torch.Tensor, torch.Tensor, torch.Tensor] | None:
    dataset_filter = build_dataset_filter(
        resolved_phases=parse_csv_filter(args.resolved_phases),
        requested_phases=parse_csv_filter(args.requested_phases),
        class_ids=parse_csv_filter(args.class_ids),
        teams=parse_csv_filter(args.teams),
        map_names=parse_csv_filter(args.maps),
        capture_kinds=parse_csv_filter(args.capture_kinds),
        success_only=args.success_only,
        corrected_upweight=args.corrected_upweight,
    )
    try:
        dataset = load_behavior_cloning_dataset(
            Path(args.data_root),
            dataset_filter,
            retarget_class_id=args.bc_retarget_class_id or None,
        )
    except ValueError:
        if not args.allow_empty_bc_anchor:
            raise

        print("warning: no BC anchor samples matched filters; continuing with rollout-only updates")
        return None

    return (
        torch.from_numpy(dataset.features),
        torch.from_numpy(dataset.move_targets),
        torch.from_numpy(dataset.binary_targets),
        torch.from_numpy(dataset.aim_targets),
    )


def sample_bc_batch(
    features: torch.Tensor,
    move_targets: torch.Tensor,
    binary_targets: torch.Tensor,
    aim_targets: torch.Tensor,
    batch_size: int,
) -> tuple[torch.Tensor, torch.Tensor, torch.Tensor, torch.Tensor]:
    count = features.shape[0]
    indices = torch.randint(0, count, (min(batch_size, count),))
    return features[indices], move_targets[indices], binary_targets[indices], aim_targets[indices]


def build_action_clone_loss(
    model: BehaviorCloningModel,
    observations: torch.Tensor,
    move_targets: torch.Tensor,
    binary_targets: torch.Tensor,
) -> torch.Tensor:
    move_logits, binary_logits, _ = model(observations)
    return F.cross_entropy(move_logits, move_targets) + F.binary_cross_entropy_with_logits(binary_logits, binary_targets)


def build_action_margin_loss(
    model: BehaviorCloningModel,
    observations: torch.Tensor,
    move_targets: torch.Tensor,
    binary_targets: torch.Tensor,
    move_margin: float,
    binary_margin: float,
) -> torch.Tensor:
    move_logits, binary_logits, _ = model(observations)
    target_logits = move_logits.gather(1, move_targets.unsqueeze(1)).squeeze(1)
    other_logits = move_logits.masked_fill(
        F.one_hot(move_targets, num_classes=move_logits.shape[1]).bool(),
        float("-inf"),
    )
    best_other_logits = other_logits.max(dim=1).values
    move_loss = F.relu(move_margin - (target_logits - best_other_logits)).mean()
    signed_binary_logits = torch.where(binary_targets > 0.5, binary_logits, -binary_logits)
    binary_loss = F.relu(binary_margin - signed_binary_logits).mean()
    return move_loss + binary_loss


def compute_action_log_probs(
    model: BehaviorCloningModel,
    observations: torch.Tensor,
    move_targets: torch.Tensor,
    binary_targets: torch.Tensor,
    temperature: float = 1.0,
) -> tuple[torch.Tensor, torch.Tensor]:
    move_logits, binary_logits, _ = model(observations)
    temperature = max(0.05, float(temperature))
    move_logits = move_logits / temperature
    binary_logits = binary_logits / temperature
    move_log_probs = F.log_softmax(move_logits, dim=1)
    chosen_move_log_probs = move_log_probs.gather(1, move_targets.unsqueeze(1)).squeeze(1)
    binary_log_probs = -F.binary_cross_entropy_with_logits(binary_logits, binary_targets, reduction="none").sum(dim=1)
    categorical_probs = F.softmax(move_logits, dim=1)
    move_entropy = -(categorical_probs * move_log_probs).sum(dim=1)
    binary_probs = torch.sigmoid(binary_logits)
    binary_entropy = -(
        binary_probs * torch.log(binary_probs.clamp_min(1e-6))
        + (1 - binary_probs) * torch.log((1 - binary_probs).clamp_min(1e-6))
    ).sum(dim=1)
    return chosen_move_log_probs + binary_log_probs, move_entropy + binary_entropy


def build_bc_loss(
    model: BehaviorCloningModel,
    bc_anchor: tuple[torch.Tensor, torch.Tensor, torch.Tensor, torch.Tensor] | None,
    args: argparse.Namespace,
    device: torch.device,
) -> tuple[torch.Tensor, torch.Tensor | None]:
    if bc_anchor is None:
        return torch.zeros((), device=device), None

    bc_features, bc_move_targets, bc_binary_targets, bc_aim_targets = bc_anchor
    bc_obs, bc_move, bc_binary, bc_aim = sample_bc_batch(
        bc_features, bc_move_targets, bc_binary_targets, bc_aim_targets, args.bc_batch_size
    )
    bc_obs = bc_obs.to(device)
    bc_move = bc_move.to(device)
    bc_binary = bc_binary.to(device)
    bc_aim = bc_aim.to(device)
    bc_move_logits, bc_binary_logits, bc_aim_pred = model(bc_obs)
    bc_loss = (
        F.cross_entropy(bc_move_logits, bc_move)
        + F.binary_cross_entropy_with_logits(bc_binary_logits, bc_binary)
        + F.l1_loss(bc_aim_pred, bc_aim)
    )
    return bc_loss, bc_obs


def build_reference_kl(
    model: BehaviorCloningModel,
    reference_model: BehaviorCloningModel,
    bc_obs: torch.Tensor | None,
    device: torch.device,
) -> torch.Tensor:
    if bc_obs is None:
        return torch.zeros((), device=device)

    bc_move_logits, bc_binary_logits, _ = model(bc_obs)
    with torch.no_grad():
        ref_move_logits, ref_binary_logits, _ = reference_model(bc_obs)
    ref_move_probs = F.softmax(ref_move_logits, dim=1)
    move_kl = F.kl_div(F.log_softmax(bc_move_logits, dim=1), ref_move_probs, reduction="batchmean")
    ref_binary_probs = torch.sigmoid(ref_binary_logits).clamp(1e-6, 1 - 1e-6)
    current_binary_log_probs = F.logsigmoid(bc_binary_logits)
    current_binary_log_neg_probs = F.logsigmoid(-bc_binary_logits)
    binary_kl = (
        ref_binary_probs * (torch.log(ref_binary_probs) - current_binary_log_probs)
        + (1 - ref_binary_probs) * (torch.log(1 - ref_binary_probs) - current_binary_log_neg_probs)
    ).mean()
    return move_kl + binary_kl


def select_rollout_steps(rollout: dict[str, Any], args: argparse.Namespace, *, for_self_imitation: bool) -> list[dict[str, Any]]:
    steps = rollout.get("Steps", [])
    if not for_self_imitation or not steps or args.self_imitation_segment_mode == "full":
        return list(steps)

    distances = [step_navigation_distance(step) for step in steps]
    best_index = min(range(len(distances)), key=lambda index: distances[index])
    anchor_index = best_index

    if args.self_imitation_segment_mode == "return-breakthrough" and args.task.casefold() == "returnintel":
        start_distance = distances[0]
        best_distance = distances[best_index]
        breakthrough_cutoff = min(
            best_distance + args.self_imitation_segment_corridor_slack,
            start_distance - args.self_imitation_segment_min_improvement,
        )
        for index, distance in enumerate(distances):
            if distance <= breakthrough_cutoff:
                anchor_index = index
                break

    start_index = max(0, anchor_index - args.self_imitation_segment_pre_ticks)
    end_index = min(len(steps), anchor_index + args.self_imitation_segment_post_ticks + 1)
    return list(steps[start_index:end_index])


def build_rollout_tensors(
    rollouts: list[dict[str, Any]],
    gamma: float,
    args: argparse.Namespace | None = None,
    *,
    for_self_imitation: bool = False,
) -> tuple[torch.Tensor, torch.Tensor, torch.Tensor, torch.Tensor]:
    observations: list[np.ndarray] = []
    move_targets: list[int] = []
    binary_targets: list[list[float]] = []
    returns: list[float] = []

    for rollout in rollouts:
        steps = select_rollout_steps(rollout, args, for_self_imitation=for_self_imitation) if args is not None else rollout["Steps"]
        rewards = [compute_step_total(step) for step in steps]
        discounted = compute_returns(rewards, gamma)
        for step, ret in zip(steps, discounted):
            observations.append(vectorize_observation(step["Observation"]))
            move_targets.append(move_direction_to_index(int(step["Action"]["MoveDirection"])))
            binary_targets.append(
                [
                    1.0 if step["Action"]["Jump"] else 0.0,
                    1.0 if step["Action"]["Crouch"] else 0.0,
                    1.0 if step["Action"]["FirePrimary"] else 0.0,
                    1.0 if step["Action"]["FireSecondary"] else 0.0,
                    1.0 if step["Action"]["DropIntel"] else 0.0,
                ]
            )
            returns.append(ret)

    return (
        torch.from_numpy(np.stack(observations).astype(np.float32)),
        torch.tensor(move_targets, dtype=torch.long),
        torch.tensor(binary_targets, dtype=torch.float32),
        torch.tensor(returns, dtype=torch.float32),
    )


def build_ppo_rollout_buffer(
    rollouts: list[dict[str, Any]],
    args: argparse.Namespace,
    model: BehaviorCloningModel,
    critic: ValueNetwork,
    device: torch.device,
) -> RolloutBuffer:
    episode_observations: list[list[np.ndarray]] = []
    episode_move_targets: list[list[int]] = []
    episode_binary_targets: list[list[list[float]]] = []
    episode_rewards: list[list[float]] = []

    for rollout in rollouts:
        observations: list[np.ndarray] = []
        move_targets: list[int] = []
        binary_targets: list[list[float]] = []
        rewards: list[float] = []
        for step in rollout.get("Steps", []):
            observations.append(vectorize_observation(step["Observation"]))
            move_targets.append(move_direction_to_index(int(step["Action"]["MoveDirection"])))
            binary_targets.append(
                [
                    1.0 if step["Action"]["Jump"] else 0.0,
                    1.0 if step["Action"]["Crouch"] else 0.0,
                    1.0 if step["Action"]["FirePrimary"] else 0.0,
                    1.0 if step["Action"]["FireSecondary"] else 0.0,
                    1.0 if step["Action"]["DropIntel"] else 0.0,
                ]
            )
            rewards.append(compute_step_total(step) * args.reward_scale)
        if observations:
            episode_observations.append(observations)
            episode_move_targets.append(move_targets)
            episode_binary_targets.append(binary_targets)
            episode_rewards.append(rewards)

    if not episode_observations:
        raise ValueError("no rollout samples were available for PPO")

    all_observations = torch.from_numpy(
        np.stack([item for episode in episode_observations for item in episode]).astype(np.float32)
    ).to(device)
    all_move_targets = torch.tensor(
        [item for episode in episode_move_targets for item in episode],
        dtype=torch.long,
        device=device,
    )
    all_binary_targets = torch.tensor(
        [item for episode in episode_binary_targets for item in episode],
        dtype=torch.float32,
        device=device,
    )
    with torch.no_grad():
        old_log_probs, _ = compute_action_log_probs(
            model,
            all_observations,
            all_move_targets,
            all_binary_targets,
            args.temperature,
        )
        values = critic(all_observations).detach().cpu().numpy().astype(np.float32)

    advantages: list[float] = []
    returns: list[float] = []
    offset = 0
    for rewards in episode_rewards:
        count = len(rewards)
        episode_values = values[offset : offset + count]
        offset += count
        episode_advantages = [0.0] * count
        gae = 0.0
        for index in range(count - 1, -1, -1):
            next_value = float(episode_values[index + 1]) if index + 1 < count else 0.0
            delta = rewards[index] + (args.gamma * next_value) - float(episode_values[index])
            gae = delta + (args.gamma * args.gae_lambda * gae)
            episode_advantages[index] = gae
        advantages.extend(episode_advantages)
        returns.extend(float(value) + advantage for value, advantage in zip(episode_values, episode_advantages))

    advantage_tensor = torch.tensor(advantages, dtype=torch.float32, device=device)
    advantage_tensor = (advantage_tensor - advantage_tensor.mean()) / advantage_tensor.std().clamp_min(1e-6)
    return RolloutBuffer(
        observations=all_observations,
        move_targets=all_move_targets,
        binary_targets=all_binary_targets,
        returns=torch.tensor(returns, dtype=torch.float32, device=device),
        advantages=advantage_tensor,
        old_log_probs=old_log_probs,
    )


def rollout_score_key(payload: dict[str, Any]) -> tuple[float, ...]:
    return (
        1.0 if payload["Success"] else 0.0,
        1.0 if payload.get("ScoreTick") is not None else 0.0,
        1.0 if payload.get("PickupTick") is not None else 0.0,
        -float(payload["MinObjectiveDistance"]),
        -float(payload["FinalObjectiveDistance"]),
        -float(payload["MaxStuckTicks"]),
        float(payload["TotalReward"]),
        -float(payload["TicksElapsed"]),
    )


def headless_selection_key(task_phase: str, eval_payload: dict[str, Any], rollout_summary: dict[str, float]) -> tuple[float, ...]:
    primary_kind = primary_event_kind(task_phase)
    min_navigation_distance = float(eval_payload.get("MinNavigationDistance", eval_payload["MinObjectiveDistance"]))
    final_navigation_distance = float(eval_payload.get("FinalNavigationDistance", eval_payload["FinalObjectiveDistance"]))
    deterministic_primary = (
        1.0 if eval_payload.get("PickupTick") is not None else 0.0
        if primary_kind == "pickup"
        else 1.0 if eval_payload.get("ScoreTick") is not None else 0.0
        if primary_kind == "score"
        else 1.0 if eval_payload["Success"] else 0.0
    )
    return (
        1.0 if eval_payload["Success"] else 0.0,
        deterministic_primary,
        -min_navigation_distance,
        -final_navigation_distance,
        -float(eval_payload["MinObjectiveDistance"]),
        -float(eval_payload["FinalObjectiveDistance"]),
        -float(eval_payload["MaxStuckTicks"]),
        float(eval_payload["TotalReward"]),
        rollout_summary["success_rate"],
        rollout_summary["primary_event_rate"],
        -rollout_summary["best_min_navigation_distance"],
        -rollout_summary["best_final_navigation_distance"],
        -rollout_summary["best_min_objective_distance"],
        -rollout_summary["best_final_objective_distance"],
        rollout_summary["reward_mean"],
        -float(eval_payload["TicksElapsed"]),
    )


def select_checkpoint_key(
    args: argparse.Namespace,
    eval_payload: dict[str, Any],
    rollout_summary: dict[str, float],
) -> tuple[float, ...]:
    if args.selection_key == "rollout-first":
        return (
            rollout_summary["success_rate"],
            rollout_summary["primary_event_rate"],
            -rollout_summary["best_min_navigation_distance"],
            -rollout_summary["best_final_navigation_distance"],
            -rollout_summary["best_min_objective_distance"],
            -rollout_summary["best_final_objective_distance"],
            rollout_summary["reward_mean"],
            *headless_selection_key(args.task, eval_payload, rollout_summary),
        )
    return headless_selection_key(args.task, eval_payload, rollout_summary)


def train_headless(args: argparse.Namespace) -> None:
    random.seed(args.seed)
    np.random.seed(args.seed)
    torch.manual_seed(args.seed)

    device = torch.device("cuda" if torch.cuda.is_available() and not args.cpu else "cpu")
    if args.task_conditioned_mlp_heads:
        model_class = TaskConditionedMlpHeadBehaviorCloningModel
    else:
        model_class = TaskConditionedBehaviorCloningModel if args.task_conditioned_heads else BehaviorCloningModel
    model = model_class(hidden_size=args.hidden_size).to(device)
    initial_state = load_checkpoint_state_for_model(model, Path(args.init_checkpoint))
    model.load_state_dict(initial_state)
    optimizer = torch.optim.AdamW(model.parameters(), lr=args.learning_rate, weight_decay=1e-4)
    reference_model = model_class(hidden_size=args.hidden_size).to(device)
    reference_model.load_state_dict(initial_state)
    reference_model.eval()
    critic: ValueNetwork | None = None
    critic_optimizer: torch.optim.Optimizer | None = None

    bc_anchor = build_behavior_anchor(args)
    output_dir = Path(args.output_dir)
    output_dir.mkdir(parents=True, exist_ok=True)

    metrics_history: list[HeadlessIterationMetrics] = []
    baseline_onnx_path = output_dir / "baseline-policy.onnx"
    export_onnx_model(model, baseline_onnx_path)
    best_eval: dict[str, Any] | None = evaluate_deterministic(args, baseline_onnx_path, output_dir, 0)
    best_rollout_summary = summarize_rollouts(
        args.task,
        collect_rollouts(
            args,
            baseline_onnx_path,
            output_dir,
            0,
            episode_count=args.selection_episodes_per_iter,
            kind="selection",
        ),
    )
    best_state: dict[str, torch.Tensor] | None = {key: value.detach().cpu().clone() for key, value in model.state_dict().items()}

    for iteration in range(1, args.iterations + 1):
        current_onnx_path = output_dir / "current-policy.onnx"
        export_onnx_model(model, current_onnx_path)
        rollouts = collect_rollouts(args, current_onnx_path, output_dir, iteration, kind="train")
        rollout_obs, rollout_move_targets, rollout_binary_targets, rollout_returns = build_rollout_tensors(rollouts, args.gamma)
        top_rollouts = select_self_imitation_rollouts(args, rollouts)
        if not top_rollouts:
            top_rollouts = select_top_rollouts(args.task, rollouts, args.self_imitation_top_k)
        clone_obs, clone_move_targets, clone_binary_targets, _ = build_rollout_tensors(
            top_rollouts,
            args.gamma,
            args,
            for_self_imitation=True,
        )

        rollout_obs = rollout_obs.to(device)
        rollout_move_targets = rollout_move_targets.to(device)
        rollout_binary_targets = rollout_binary_targets.to(device)
        rollout_returns = (rollout_returns.to(device) * args.reward_scale)
        rollout_returns = (rollout_returns - rollout_returns.mean()) / (rollout_returns.std().clamp_min(1e-6))
        clone_obs = clone_obs.to(device)
        clone_move_targets = clone_move_targets.to(device)
        clone_binary_targets = clone_binary_targets.to(device)

        value_loss = torch.zeros((), device=device)
        if args.rl_algorithm == "ppo":
            if critic is None:
                critic = ValueNetwork(int(rollout_obs.shape[1]), args.hidden_size).to(device)
                critic_optimizer = torch.optim.AdamW(critic.parameters(), lr=args.learning_rate, weight_decay=1e-4)
            assert critic_optimizer is not None
            model.train()
            critic.train()
            rollout_buffer = build_ppo_rollout_buffer(rollouts, args, model, critic, device)

            policy_loss_total = 0.0
            bc_loss_total = 0.0
            self_imitation_loss_total = 0.0
            action_margin_loss_total = 0.0
            value_loss_total = 0.0
            entropy_total = 0.0
            update_count = 0
            batch_size = max(1, min(args.ppo_minibatch_size, rollout_buffer.observations.shape[0]))
            for _ in range(args.ppo_epochs):
                permutation = torch.randperm(rollout_buffer.observations.shape[0], device=device)
                for start in range(0, rollout_buffer.observations.shape[0], batch_size):
                    batch_indices = permutation[start : start + batch_size]
                    batch_obs = rollout_buffer.observations[batch_indices]
                    batch_move = rollout_buffer.move_targets[batch_indices]
                    batch_binary = rollout_buffer.binary_targets[batch_indices]
                    batch_old_log_probs = rollout_buffer.old_log_probs[batch_indices]
                    batch_advantages = rollout_buffer.advantages[batch_indices]
                    batch_value_targets = rollout_buffer.returns[batch_indices]

                    optimizer.zero_grad(set_to_none=True)
                    critic_optimizer.zero_grad(set_to_none=True)
                    new_log_probs, batch_entropy = compute_action_log_probs(
                        model,
                        batch_obs,
                        batch_move,
                        batch_binary,
                        args.temperature,
                    )
                    ratio = torch.exp(new_log_probs - batch_old_log_probs)
                    unclipped_policy_loss = ratio * batch_advantages
                    clipped_policy_loss = torch.clamp(
                        ratio,
                        1.0 - args.ppo_clip_eps,
                        1.0 + args.ppo_clip_eps,
                    ) * batch_advantages
                    policy_loss = -torch.min(unclipped_policy_loss, clipped_policy_loss).mean()
                    entropy = batch_entropy.mean()
                    value_loss = F.mse_loss(critic(batch_obs), batch_value_targets)
                    bc_loss, bc_obs = build_bc_loss(model, bc_anchor, args, device)
                    reference_kl = build_reference_kl(model, reference_model, bc_obs, device)
                    self_imitation_loss = build_action_clone_loss(model, clone_obs, clone_move_targets, clone_binary_targets)
                    action_margin_loss = build_action_margin_loss(
                        model,
                        clone_obs,
                        clone_move_targets,
                        clone_binary_targets,
                        args.move_margin,
                        args.binary_margin,
                    )
                    loss = (
                        policy_loss
                        + (args.value_coef * value_loss)
                        + (args.bc_coef * bc_loss)
                        + (args.self_imitation_coef * self_imitation_loss)
                        + (args.action_margin_coef * action_margin_loss)
                        + (args.reference_kl_coef * reference_kl)
                        - (args.entropy_coef * entropy)
                    )
                    loss.backward()
                    optimizer.step()
                    critic_optimizer.step()

                    policy_loss_total += float(policy_loss.detach().cpu())
                    bc_loss_total += float(bc_loss.detach().cpu())
                    self_imitation_loss_total += float(self_imitation_loss.detach().cpu())
                    action_margin_loss_total += float(action_margin_loss.detach().cpu())
                    value_loss_total += float(value_loss.detach().cpu())
                    entropy_total += float(entropy.detach().cpu())
                    update_count += 1

            policy_loss = torch.tensor(policy_loss_total / max(1, update_count), device=device)
            bc_loss = torch.tensor(bc_loss_total / max(1, update_count), device=device)
            self_imitation_loss = torch.tensor(self_imitation_loss_total / max(1, update_count), device=device)
            action_margin_loss = torch.tensor(action_margin_loss_total / max(1, update_count), device=device)
            value_loss = torch.tensor(value_loss_total / max(1, update_count), device=device)
            entropy = torch.tensor(entropy_total / max(1, update_count), device=device)
        else:
            model.train()
            optimizer.zero_grad(set_to_none=True)
            action_log_probs, action_entropy = compute_action_log_probs(
                model,
                rollout_obs,
                rollout_move_targets,
                rollout_binary_targets,
                args.temperature,
            )
            policy_loss = -(action_log_probs * rollout_returns).mean()
            entropy = action_entropy.mean()
            bc_loss, bc_obs = build_bc_loss(model, bc_anchor, args, device)
            reference_kl = build_reference_kl(model, reference_model, bc_obs, device)
            self_imitation_loss = build_action_clone_loss(model, clone_obs, clone_move_targets, clone_binary_targets)
            action_margin_loss = build_action_margin_loss(
                model,
                clone_obs,
                clone_move_targets,
                clone_binary_targets,
                args.move_margin,
                args.binary_margin,
            )
            loss = (
                policy_loss
                + (args.bc_coef * bc_loss)
                + (args.self_imitation_coef * self_imitation_loss)
                + (args.action_margin_coef * action_margin_loss)
                + (args.reference_kl_coef * reference_kl)
                - (args.entropy_coef * entropy)
            )
            loss.backward()
            optimizer.step()

        export_onnx_model(model, current_onnx_path)
        eval_payload = evaluate_deterministic(args, current_onnx_path, output_dir, iteration)
        archive_top_rollouts(output_dir, args, iteration, rollouts)
        selection_rollouts = collect_rollouts(
            args,
            current_onnx_path,
            output_dir,
            iteration,
            episode_count=args.selection_episodes_per_iter,
            kind="selection",
        )
        prune_retained_rollout_dirs(output_dir, args, iteration)
        selection_rollout_summary = summarize_rollouts(args.task, selection_rollouts)
        if args.selection_key == "final" or best_eval is None or (
            select_checkpoint_key(args, eval_payload, selection_rollout_summary)
            > select_checkpoint_key(args, best_eval, best_rollout_summary)
        ):
            best_eval = eval_payload
            best_rollout_summary = selection_rollout_summary
            best_state = {key: value.detach().cpu().clone() for key, value in model.state_dict().items()}

        rollout_summary = summarize_rollouts(args.task, rollouts)
        metrics = HeadlessIterationMetrics(
            iteration=iteration,
            rollout_reward_mean=rollout_summary["reward_mean"],
            rollout_success_rate=rollout_summary["success_rate"],
            rollout_pickup_rate=rollout_summary["pickup_rate"],
            rollout_score_rate=rollout_summary["score_rate"],
            rollout_best_min_objective_distance=rollout_summary["best_min_objective_distance"],
            rollout_best_final_objective_distance=rollout_summary["best_final_objective_distance"],
            policy_loss=float(policy_loss.detach().cpu()),
            bc_loss=float(bc_loss.detach().cpu()),
            self_imitation_loss=float(self_imitation_loss.detach().cpu()),
            action_margin_loss=float(action_margin_loss.detach().cpu()),
            value_loss=float(value_loss.detach().cpu()),
            self_imitation_sample_count=int(clone_obs.shape[0]),
            entropy=float(entropy.detach().cpu()),
            eval_success=bool(eval_payload["Success"]),
            eval_terminal_reason=str(eval_payload["TerminalReason"]),
            eval_min_objective_distance=float(eval_payload["MinObjectiveDistance"]),
            eval_max_stuck_ticks=float(eval_payload["MaxStuckTicks"]),
            eval_ticks_elapsed=int(eval_payload["TicksElapsed"]),
        )
        metrics_history.append(metrics)
        print(
            f"iter={iteration} rollout_reward_mean={metrics.rollout_reward_mean:.2f} "
            f"rollout_success_rate={metrics.rollout_success_rate:.2f} pickup_rate={metrics.rollout_pickup_rate:.2f} "
            f"score_rate={metrics.rollout_score_rate:.2f} rollout_best_min_distance={metrics.rollout_best_min_objective_distance:.1f} "
            f"policy_loss={metrics.policy_loss:.4f} bc_loss={metrics.bc_loss:.4f} "
            f"self_imitation_loss={metrics.self_imitation_loss:.4f} action_margin_loss={metrics.action_margin_loss:.4f} "
            f"value_loss={metrics.value_loss:.4f} self_imitation_samples={metrics.self_imitation_sample_count} "
            f"entropy={metrics.entropy:.4f} "
            f"eval_success={metrics.eval_success} eval_terminal_reason={metrics.eval_terminal_reason} "
            f"eval_min_objective_distance={metrics.eval_min_objective_distance:.1f} eval_max_stuck={metrics.eval_max_stuck_ticks:.1f}"
        )

    if best_state is None or best_eval is None:
        raise RuntimeError("headless training did not produce a best checkpoint")

    checkpoint_path = output_dir / "model.pt"
    torch.save(best_state, checkpoint_path)
    model.load_state_dict(best_state)
    onnx_path = output_dir / "model.onnx"
    export_onnx_model(model, onnx_path)
    critic_path: Path | None = None
    if critic is not None:
        critic_path = output_dir / "critic.pt"
        torch.save(critic.state_dict(), critic_path)

    with (output_dir / "metrics.json").open("w", encoding="utf-8") as handle:
        json.dump([asdict(item) for item in metrics_history], handle, indent=2)
    with (output_dir / "selection-summary.json").open("w", encoding="utf-8") as handle:
        json.dump(
            {
                "best_eval": best_eval,
                "best_rollout_summary": best_rollout_summary,
                "baseline_eval": evaluate_deterministic(args, baseline_onnx_path, output_dir, -1),
                "baseline_rollout_summary": summarize_rollouts(
                    args.task,
                    collect_rollouts(
                        args,
                        baseline_onnx_path,
                        output_dir,
                        -1,
                        episode_count=args.selection_episodes_per_iter,
                        kind="selection",
                    ),
                ),
                "critic_checkpoint": str(critic_path) if critic_path is not None else "",
                "rl_algorithm": args.rl_algorithm,
                "selection_key": args.selection_key,
                "gamma": args.gamma,
                "gae_lambda": args.gae_lambda,
                "reward_scale": args.reward_scale,
            },
            handle,
            indent=2,
        )

    print(f"saved checkpoint={checkpoint_path}")
    print(f"saved onnx={onnx_path}")
    if critic_path is not None:
        print(f"saved critic={critic_path}")
    print(f"saved metrics={output_dir / 'metrics.json'}")
    print(f"saved selection_summary={output_dir / 'selection-summary.json'}")


if __name__ == "__main__":
    args = parse_args()
    train_headless(args)
