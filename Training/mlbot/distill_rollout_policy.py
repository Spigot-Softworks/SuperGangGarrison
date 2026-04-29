from __future__ import annotations

import argparse
import json
import subprocess
from dataclasses import asdict, dataclass
from pathlib import Path
from typing import Any

import numpy as np
import torch
import torch.nn.functional as F

from mlbot_dataset import (
    DISTANCE_SCALE,
    build_dataset_filter,
    load_behavior_cloning_dataset,
    parse_csv_filter,
    vectorize_observation,
)
from train_behavior_cloning import (
    BehaviorCloningModel,
    TaskConditionedBehaviorCloningModel,
    TaskConditionedMlpHeadBehaviorCloningModel,
    copy_shared_policy_head_to_task_head,
    export_onnx_model,
    load_checkpoint_state,
    load_checkpoint_state_for_model,
)


@dataclass
class DistillationEpochMetrics:
    epoch: int
    rollout_clone_loss: float
    action_margin_loss: float
    bc_loss: float
    reference_kl: float
    eval_success: bool
    eval_terminal_reason: str
    eval_min_objective_distance: float
    eval_final_objective_distance: float
    eval_max_stuck_ticks: float
    eval_ticks_elapsed: int
    regression_successes: int
    regression_count: int
    update_accepted: bool
    gate_rejection_reason: str = ""


@dataclass(frozen=True)
class EvaluationSpec:
    level_name: str
    team: str
    class_id: str
    task: str
    ticks: int
    start_node_id: int = -1
    start_x: float | None = None
    start_y: float | None = None
    start_vx: float | None = None
    start_vy: float | None = None
    carrying_intel: bool | None = None
    start_is_grounded: bool | None = None
    start_remaining_air_jumps: int | None = None
    start_facing_dir_x: float | None = None
    start_previous_move_input: int | None = None
    start_previous_jump_held: bool | None = None
    start_previous_drop_input: bool | None = None
    start_previous_fire_primary: bool | None = None
    start_previous_fire_secondary: bool | None = None
    start_previous_position_delta_x: float | None = None
    start_previous_position_delta_y: float | None = None
    start_previous_velocity_x: float | None = None
    start_previous_velocity_y: float | None = None
    start_previous_facing_dir_x: float | None = None
    start_previous_is_grounded: bool | None = None
    start_objective_distance: float | None = None
    start_objective_distance_delta: float | None = None
    start_previous_objective_distance_delta: float | None = None
    start_airborne_ticks: float | None = None
    start_jump_ticks: float | None = None
    start_frames_since_jump_pressed: float | None = None
    start_frames_since_jump_released: float | None = None


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser()
    parser.add_argument("--data-root", required=True)
    parser.add_argument("--output-dir", required=True)
    parser.add_argument("--init-checkpoint", required=True)
    parser.add_argument("--rollout-path", action="append", default=[])
    parser.add_argument("--rollout-dir", action="append", default=[])
    parser.add_argument("--rollout-glob", default="episode-*.json")
    parser.add_argument(
        "--allow-mixed-rollout-contexts",
        action="store_true",
        help="Use provided rollout files from multiple map/team/class/task contexts as replay teachers.",
    )
    parser.add_argument("--top-k", type=int, default=8)
    parser.add_argument(
        "--top-k-per-rollout-context",
        type=int,
        default=0,
        help="Select up to this many teachers per rollout map/team/class/task context before merging.",
    )
    parser.add_argument("--teacher-selection-key", choices=("route-progress", "fast-success"), default="route-progress")
    parser.add_argument(
        "--segment-mode",
        choices=("full", "best-window", "return-breakthrough", "terminal-window"),
        default="full",
    )
    parser.add_argument("--segment-pre-ticks", type=int, default=45)
    parser.add_argument("--segment-post-ticks", type=int, default=180)
    parser.add_argument("--segment-corridor-slack", type=float, default=96.0)
    parser.add_argument("--segment-min-improvement", type=float, default=24.0)
    parser.add_argument("--exclude-zero-min-distance", action="store_true")
    parser.add_argument("--max-min-objective-distance", type=float, default=0.0)
    parser.add_argument("--max-final-objective-distance", type=float, default=0.0)
    parser.add_argument("--epochs", type=int, default=12)
    parser.add_argument("--batches-per-epoch", type=int, default=1)
    parser.add_argument("--learning-rate", type=float, default=1e-4)
    parser.add_argument("--batch-size", type=int, default=512)
    parser.add_argument(
        "--balance-rollout-contexts",
        action="store_true",
        help="Sample each optimizer batch evenly from the selected teacher rollouts.",
    )
    parser.add_argument("--bc-coef", type=float, default=0.4)
    parser.add_argument("--reference-kl-coef", type=float, default=0.1)
    parser.add_argument("--action-margin-coef", type=float, default=0.0)
    parser.add_argument("--move-margin", type=float, default=1.0)
    parser.add_argument("--binary-margin", type=float, default=0.5)
    parser.add_argument("--early-max-tick", type=int, default=0)
    parser.add_argument("--early-sample-weight", type=float, default=1.0)
    parser.add_argument("--jump-sample-weight", type=float, default=1.0)
    parser.add_argument("--counter-waypoint-sample-weight", type=float, default=1.0)
    parser.add_argument("--neutral-move-sample-weight", type=float, default=1.0)
    parser.add_argument(
        "--vertical-waypoint-min-abs-y",
        type=float,
        default=0.0,
        help="Enable vertical-waypoint weighting when abs(Waypoint.RelativeY) is at least this value.",
    )
    parser.add_argument("--vertical-waypoint-sample-weight", type=float, default=1.0)
    parser.add_argument("--neutral-vertical-waypoint-sample-weight", type=float, default=1.0)
    parser.add_argument(
        "--corrective-replay-sample-weight",
        type=float,
        default=1.0,
        help="Multiply samples from mined corrective replay documents.",
    )
    parser.add_argument(
        "--stall-recovery-sample-weight",
        type=float,
        default=4.0,
        help="Multiply the first useful motion samples after a stalled teacher segment.",
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
    parser.add_argument("--allow-empty-bc-anchor", action="store_true")
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
    parser.add_argument("--start-is-grounded", type=parse_optional_bool, default=None)
    parser.add_argument("--start-remaining-air-jumps", type=int, default=None)
    parser.add_argument("--start-facing-dir-x", type=float, default=None)
    parser.add_argument("--start-prev-move", type=int, default=None)
    parser.add_argument("--start-prev-jump-held", type=parse_optional_bool, default=None)
    parser.add_argument("--start-prev-drop", type=parse_optional_bool, default=None)
    parser.add_argument("--start-prev-fire-primary", type=parse_optional_bool, default=None)
    parser.add_argument("--start-prev-fire-secondary", type=parse_optional_bool, default=None)
    parser.add_argument("--start-prev-dx", type=float, default=None)
    parser.add_argument("--start-prev-dy", type=float, default=None)
    parser.add_argument("--start-prev-vx", type=float, default=None)
    parser.add_argument("--start-prev-vy", type=float, default=None)
    parser.add_argument("--start-prev-facing-dir-x", type=float, default=None)
    parser.add_argument("--start-prev-is-grounded", type=parse_optional_bool, default=None)
    parser.add_argument("--start-objective-distance", type=float, default=None)
    parser.add_argument("--start-objective-distance-delta", type=float, default=None)
    parser.add_argument("--start-prev-objective-distance-delta", type=float, default=None)
    parser.add_argument("--start-airborne-ticks", type=float, default=None)
    parser.add_argument("--start-jump-ticks", type=float, default=None)
    parser.add_argument("--start-frames-since-jump-pressed", type=float, default=None)
    parser.add_argument("--start-frames-since-jump-released", type=float, default=None)
    parser.add_argument(
        "--regression-eval",
        action="append",
        default=[],
        help="Additional eval as map,team,class,task,ticks,start_node_id. Start node may be omitted.",
    )
    parser.add_argument("--rollout-no-build", action="store_true")
    parser.add_argument("--disable-policy-overrides", action="store_true")
    parser.add_argument("--cpu", action="store_true")
    parser.add_argument(
        "--task-conditioned-heads",
        action="store_true",
        help="Use one shared backbone with task-gated move/binary/aim heads inside a single ONNX policy.",
    )
    parser.add_argument(
        "--task-conditioned-mlp-heads",
        action="store_true",
        help="Use nonlinear task-gated heads initialized compatibly from linear task-head checkpoints.",
    )
    parser.add_argument(
        "--task-head-init",
        action="append",
        default=[],
        help="Initialize a task-conditioned head from a shared-head checkpoint as TaskPhase=checkpoint.pt.",
    )
    parser.add_argument(
        "--freeze-backbone",
        action="store_true",
        help="Freeze the model backbone and train only policy heads.",
    )
    parser.add_argument(
        "--regression-gated-updates",
        action="store_true",
        help="Reject and roll back candidate updates that reduce the aggregate regression score.",
    )
    parser.add_argument(
        "--target-first-selection",
        action="store_true",
        help="Rank candidate checkpoints by the target eval before regression evals. Use for focused blocker training.",
    )
    parser.add_argument(
        "--require-strict-regression-improvement",
        action="store_true",
        help="With --regression-gated-updates, accept only strictly better matrix scores instead of equal scores.",
    )
    parser.add_argument(
        "--require-target-success-retention",
        action="store_true",
        help="With --regression-gated-updates, reject updates that lose target success once the accepted policy has it.",
    )
    parser.add_argument(
        "--require-regression-success-retention",
        action="store_true",
        help="With --regression-gated-updates, reject updates that reduce the accepted regression success count.",
    )
    parser.add_argument(
        "--max-accepted-stuck-ticks",
        type=float,
        default=0.0,
        help="Reject candidate best checkpoints whose target eval exceeds this MaxStuckTicks value.",
    )
    parser.add_argument(
        "--reset-optimizer-on-reject",
        action="store_true",
        help="Clear AdamW momentum whenever a regression-gated update is rejected.",
    )
    return parser.parse_args()


def parse_evaluation_spec(raw_value: str) -> EvaluationSpec:
    parts = [part.strip() for part in raw_value.split(",")]
    if len(parts) not in (5, 6, 7, 8, 10) or any(not part for part in parts[:5]):
        raise ValueError(
            "--regression-eval must be formatted as map,team,class,task,ticks[,start_node_id] "
            "or map,team,class,task,ticks,start_x,start_y[,carrying_intel[,start_vx,start_vy]]"
        )
    if len(parts) in (7, 8, 10):
        return EvaluationSpec(
            level_name=parts[0],
            team=parts[1],
            class_id=parts[2],
            task=parts[3],
            ticks=int(parts[4]),
            start_x=float(parts[5]),
            start_y=float(parts[6]),
            carrying_intel=parse_optional_bool(parts[7]) if len(parts) in (8, 10) else None,
            start_vx=float(parts[8]) if len(parts) == 10 else None,
            start_vy=float(parts[9]) if len(parts) == 10 else None,
        )

    return EvaluationSpec(
        level_name=parts[0],
        team=parts[1],
        class_id=parts[2],
        task=parts[3],
        ticks=int(parts[4]),
        start_node_id=int(parts[5]) if len(parts) == 6 and parts[5] else -1,
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


def rollout_score(task_phase: str, rollout: dict[str, Any], selection_key: str = "route-progress") -> tuple[float, ...]:
    min_distance = rollout_min_objective_distance(rollout)
    final_distance = rollout_final_objective_distance(rollout)
    has_pickup = primary_event_kind(task_phase) == "pickup" and rollout_contains_pickup(rollout)
    has_score = primary_event_kind(task_phase) == "score" and rollout_contains_score(rollout)
    if selection_key == "fast-success":
        return (
            1.0 if rollout["Success"] else 0.0,
            1.0 if has_pickup else 0.0,
            1.0 if has_score else 0.0,
            -float(rollout["TicksElapsed"]),
            -min_distance,
            -final_distance,
            float(rollout["TotalReward"]),
        )

    return (
        1.0 if rollout["Success"] else 0.0,
        1.0 if has_pickup else 0.0,
        1.0 if has_score else 0.0,
        -min_distance,
        -final_distance,
        float(rollout["TotalReward"]),
        -float(rollout["TicksElapsed"]),
    )


def rollout_context_key(rollout: dict[str, Any], fallback_task_phase: str) -> tuple[str, str, str, str]:
    return (
        str(rollout.get("LevelName", "")),
        str(rollout.get("Team", "")),
        str(rollout.get("ClassId", "")),
        rollout_task_phase(rollout, fallback_task_phase),
    )


def select_top_rollouts(rollouts: list[dict[str, Any]], args: argparse.Namespace) -> list[dict[str, Any]]:
    if args.top_k_per_rollout_context > 0:
        by_context: dict[tuple[str, str, str, str], list[dict[str, Any]]] = {}
        for rollout in rollouts:
            by_context.setdefault(rollout_context_key(rollout, args.task), []).append(rollout)

        selected: list[dict[str, Any]] = []
        for context_rollouts in by_context.values():
            selected.extend(
                sorted(
                    context_rollouts,
                    key=lambda rollout: rollout_score(
                        rollout_task_phase(rollout, args.task),
                        rollout,
                        args.teacher_selection_key,
                    ),
                    reverse=True,
                )[: args.top_k_per_rollout_context]
            )
        return selected

    return sorted(
        rollouts,
        key=lambda rollout: rollout_score(
            rollout_task_phase(rollout, args.task),
            rollout,
            args.teacher_selection_key,
        ),
        reverse=True,
    )[: args.top_k]


def discover_rollout_paths(args: argparse.Namespace) -> list[Path]:
    paths: list[Path] = []
    for raw_path in args.rollout_path:
        path = Path(raw_path)
        if path.is_file():
            paths.append(path)

    for raw_dir in args.rollout_dir:
        directory = Path(raw_dir)
        if directory.is_dir():
            paths.extend(sorted(directory.rglob(args.rollout_glob)))

    unique_paths: list[Path] = []
    seen: set[Path] = set()
    for path in paths:
        resolved = path.resolve()
        if resolved not in seen:
            seen.add(resolved)
            unique_paths.append(resolved)
    return unique_paths


def load_rollouts(paths: list[Path]) -> list[dict[str, Any]]:
    rollouts: list[dict[str, Any]] = []
    for path in paths:
        with path.open("r", encoding="utf-8") as handle:
            rollout = json.load(handle)
        rollout["_source_path"] = str(path)
        rollouts.append(rollout)
    return rollouts


def matches_requested_context(rollout: dict[str, Any], args: argparse.Namespace) -> bool:
    expected = {
        "LevelName": args.map,
        "Team": args.team,
        "ClassId": args.class_id,
        "TaskPhase": args.task,
    }
    return all(str(rollout.get(key, "")).casefold() == value.casefold() for key, value in expected.items())


def rollout_task_phase(rollout: dict[str, Any], fallback_task_phase: str) -> str:
    return str(rollout.get("TaskPhase") or fallback_task_phase)


def filter_rollouts(rollouts: list[dict[str, Any]], args: argparse.Namespace) -> list[dict[str, Any]]:
    filtered: list[dict[str, Any]] = []
    for rollout in rollouts:
        if not args.allow_mixed_rollout_contexts and not matches_requested_context(rollout, args):
            continue

        task_phase = rollout_task_phase(rollout, args.task)
        primary_kind = primary_event_kind(task_phase)
        min_distance = rollout_min_objective_distance(rollout)
        final_distance = rollout_final_objective_distance(rollout)
        has_primary_event = (
            rollout_contains_pickup(rollout)
            if primary_kind == "pickup"
            else rollout_contains_score(rollout)
            if primary_kind == "score"
            else bool(rollout.get("Success", False))
        )
        if args.exclude_zero_min_distance and min_distance <= 1.0 and not has_primary_event:
            continue
        if args.max_min_objective_distance > 0.0 and min_distance > args.max_min_objective_distance:
            continue
        if args.max_final_objective_distance > 0.0 and final_distance > args.max_final_objective_distance:
            continue

        filtered.append(rollout)
    return filtered


def move_direction_to_index(move_direction: int) -> int:
    return move_direction + 1


def step_objective_distance(step: dict[str, Any]) -> float:
    return float(step["NextObservation"]["ObjectiveDistance"])


def step_navigation_distance(step: dict[str, Any]) -> float:
    observation = step["NextObservation"]
    waypoint = observation.get("Waypoint", {})
    if waypoint.get("HasWaypoint", False) and not waypoint.get("IsFinalWaypoint", False):
        return float(waypoint.get("Distance", observation.get("ObjectiveDistance", float("inf"))))
    return float(observation.get("ObjectiveDistance", float("inf")))


def primary_event_step_index(rollout: dict[str, Any], task_phase: str) -> int | None:
    primary_kind = primary_event_kind(task_phase)
    steps = rollout.get("Steps", [])
    for index, step in enumerate(steps):
        terminal_reason = str(step.get("TerminalReason", ""))
        next_observation = step.get("NextObservation", {})
        if primary_kind == "pickup" and (terminal_reason == "picked_up_intel" or next_observation.get("IsCarryingIntel")):
            return index
        if primary_kind == "score" and terminal_reason == "scored":
            return index
    if bool(rollout.get("Success", False)) and steps:
        return len(steps) - 1
    return None


def select_rollout_steps(rollout: dict[str, Any], args: argparse.Namespace) -> list[dict[str, Any]]:
    steps = rollout.get("Steps", [])
    if not steps or args.segment_mode == "full":
        return list(steps)

    task_phase = rollout_task_phase(rollout, args.task)
    if args.segment_mode == "terminal-window":
        event_index = primary_event_step_index(rollout, task_phase)
        if event_index is None:
            return list(steps)
        start_index = max(0, event_index - args.segment_pre_ticks)
        end_index = min(len(steps), event_index + args.segment_post_ticks + 1)
        return list(steps[start_index:end_index])

    if args.segment_mode == "return-breakthrough" and task_phase.casefold() != "returnintel":
        return list(steps)

    distances = [step_navigation_distance(step) for step in steps]
    best_index = min(range(len(distances)), key=lambda index: distances[index])
    anchor_index = best_index

    if args.segment_mode == "return-breakthrough" and task_phase.casefold() == "returnintel":
        start_distance = distances[0]
        best_distance = distances[best_index]
        breakthrough_cutoff = min(
            best_distance + args.segment_corridor_slack,
            start_distance - args.segment_min_improvement,
        )
        for index, distance in enumerate(distances):
            if distance <= breakthrough_cutoff:
                anchor_index = index
                break

    start_index = max(0, anchor_index - args.segment_pre_ticks)
    end_index = min(len(steps), anchor_index + args.segment_post_ticks + 1)
    return list(steps[start_index:end_index])


def build_rollout_clone_tensors(
    rollouts: list[dict[str, Any]], args: argparse.Namespace
) -> tuple[torch.Tensor, torch.Tensor, torch.Tensor, torch.Tensor, torch.Tensor, torch.Tensor, list[dict[str, Any]]]:
    observations: list[np.ndarray] = []
    move_targets: list[int] = []
    binary_targets: list[list[float]] = []
    aim_targets: list[list[float]] = []
    sample_weights: list[float] = []
    rollout_group_ids: list[int] = []
    segment_summaries: list[dict[str, Any]] = []

    for rollout_index, rollout in enumerate(rollouts):
        selected_steps = expand_stall_recovery_samples(select_rollout_steps(rollout, args))
        is_corrective_replay = bool(rollout.get("CorrectionSource"))
        selected_start_tick = int(selected_steps[0].get("Tick", 0)) if selected_steps else None
        selected_end_tick = int(selected_steps[-1].get("Tick", 0)) if selected_steps else None
        segment_summaries.append(
            {
                "source_path": rollout.get("_source_path"),
                "correction_source": rollout.get("CorrectionSource"),
                "selected_steps": len(selected_steps),
                "selected_start_tick": selected_start_tick,
                "selected_end_tick": selected_end_tick,
                "rollout_steps": len(rollout.get("Steps", [])),
                "min_objective_distance": rollout_min_objective_distance(rollout),
                "final_objective_distance": rollout_final_objective_distance(rollout),
                "total_reward": float(rollout.get("TotalReward", 0.0)),
                "success": bool(rollout.get("Success", False)),
                "terminal_reason": rollout.get("TerminalReason"),
            }
        )

        skipped_stalled_samples = 0
        recovery_samples = 0
        for step in selected_steps:
            observation = step["Observation"]
            next_observation = step.get("NextObservation", {})
            action = step["Action"]
            move_direction = int(action["MoveDirection"])
            stalled_sample_kind = step.get("_stall_sample_kind") or classify_stalled_rollout_sample(observation, next_observation, action)
            if stalled_sample_kind == "dead":
                skipped_stalled_samples += 1
                continue
            if stalled_sample_kind == "recovery":
                recovery_samples += 1
            waypoint = observation.get("Waypoint", {})
            waypoint_relative_x = float(waypoint.get("RelativeX", 0.0))
            waypoint_relative_y = float(waypoint.get("RelativeY", 0.0))
            sample_weight = 1.0
            if args.early_max_tick > 0 and int(step.get("Tick", 0)) <= args.early_max_tick:
                sample_weight *= max(0.0, args.early_sample_weight)
            if action["Jump"]:
                sample_weight *= max(0.0, args.jump_sample_weight)
            if move_direction == 0:
                sample_weight *= max(0.0, args.neutral_move_sample_weight)
            elif waypoint_relative_x != 0.0 and np.sign(move_direction) != np.sign(waypoint_relative_x):
                sample_weight *= max(0.0, args.counter_waypoint_sample_weight)
            if (
                args.vertical_waypoint_min_abs_y > 0.0
                and abs(waypoint_relative_y) >= args.vertical_waypoint_min_abs_y
            ):
                sample_weight *= max(0.0, args.vertical_waypoint_sample_weight)
                if move_direction == 0:
                    sample_weight *= max(0.0, args.neutral_vertical_waypoint_sample_weight)
            if is_corrective_replay:
                sample_weight *= max(0.0, args.corrective_replay_sample_weight)
            if stalled_sample_kind == "recovery":
                sample_weight *= max(0.0, args.stall_recovery_sample_weight)

            observations.append(vectorize_observation(step["Observation"]))
            move_targets.append(move_direction_to_index(move_direction))
            rollout_group_ids.append(rollout_index)
            binary_targets.append(
                [
                    1.0 if action["Jump"] else 0.0,
                    1.0 if action["Crouch"] else 0.0,
                    1.0 if action["FirePrimary"] else 0.0,
                    1.0 if action["FireSecondary"] else 0.0,
                    1.0 if action["DropIntel"] else 0.0,
                ]
            )
            aim_targets.append(
                [
                    np.clip((float(action["AimWorldX"]) - float(observation["BotX"])) / DISTANCE_SCALE, -1.0, 1.0),
                    np.clip((float(action["AimWorldY"]) - float(observation["BotY"])) / DISTANCE_SCALE, -1.0, 1.0),
                ]
            )
            sample_weights.append(sample_weight)

        segment_summaries[-1]["skipped_stalled_samples"] = skipped_stalled_samples
        segment_summaries[-1]["stall_recovery_samples"] = recovery_samples

    if not observations:
        raise ValueError("no rollout samples were available for distillation")

    return (
        torch.from_numpy(np.stack(observations).astype(np.float32)),
        torch.tensor(move_targets, dtype=torch.long),
        torch.tensor(binary_targets, dtype=torch.float32),
        torch.tensor(aim_targets, dtype=torch.float32),
        torch.tensor(sample_weights, dtype=torch.float32),
        torch.tensor(rollout_group_ids, dtype=torch.long),
        segment_summaries,
    )


def expand_stall_recovery_samples(steps: list[dict[str, Any]]) -> list[dict[str, Any]]:
    expanded: list[dict[str, Any]] = []
    stalled_buffer: list[dict[str, Any]] = []
    for step in steps:
        kind = classify_stalled_rollout_sample(
            step["Observation"],
            step.get("NextObservation", {}),
            step["Action"],
        )
        if kind == "dead":
            stalled_buffer.append(step)
            continue

        if kind == "recovery" and stalled_buffer:
            recovery_action = step["Action"]
            for stalled_step in stalled_buffer:
                synthetic = dict(stalled_step)
                synthetic["Action"] = recovery_action
                synthetic["_stall_sample_kind"] = "recovery"
                expanded.append(synthetic)
            stalled_buffer.clear()

        if kind:
            step = dict(step)
            step["_stall_sample_kind"] = kind
        expanded.append(step)

    return expanded


def classify_stalled_rollout_sample(
    observation: dict[str, Any],
    next_observation: dict[str, Any],
    action: dict[str, Any],
) -> str:
    stuck_ticks = float(observation.get("StuckTicks", 0.0))
    if stuck_ticks < 10.0:
        return ""

    move_direction = int(action.get("MoveDirection", 0))
    if move_direction == 0:
        return ""

    bot_x = float(observation.get("BotX", 0.0))
    bot_y = float(observation.get("BotY", 0.0))
    next_x = float(next_observation.get("BotX", bot_x))
    next_y = float(next_observation.get("BotY", bot_y))
    velocity_x = abs(float(observation.get("VelocityX", 0.0)))
    moved_x = abs(next_x - bot_x)
    moved_y = abs(next_y - bot_y)
    made_motion = moved_x >= 1.5 or moved_y >= 2.0 or velocity_x >= 12.0
    if bool(action.get("Jump", False)) and made_motion:
        return "recovery"

    probes = observation.get("Probes", {})
    blocked = (
        move_direction < 0
        and (bool(probes.get("TouchingLeftWall", False)) or float(probes.get("LeftFootObstacleDistance", 999.0)) <= 10.0)
    ) or (
        move_direction > 0
        and (bool(probes.get("TouchingRightWall", False)) or float(probes.get("RightFootObstacleDistance", 999.0)) <= 10.0)
    )

    if blocked and not made_motion and stuck_ticks >= 14.0:
        return "dead"
    if stuck_ticks >= 30.0 and not made_motion:
        return "dead"
    if made_motion and stuck_ticks >= 18.0:
        return "recovery"
    return ""


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
        dataset = load_behavior_cloning_dataset(Path(args.data_root), dataset_filter)
    except ValueError:
        if not args.allow_empty_bc_anchor:
            raise

        print("warning: no BC anchor samples matched filters; continuing with rollout-only distillation")
        return None

    return (
        torch.from_numpy(dataset.features),
        torch.from_numpy(dataset.move_targets),
        torch.from_numpy(dataset.binary_targets),
        torch.from_numpy(dataset.aim_targets),
    )


def sample_batch(
    features: torch.Tensor,
    move_targets: torch.Tensor,
    binary_targets: torch.Tensor,
    aim_targets: torch.Tensor,
    batch_size: int,
    sample_weights: torch.Tensor | None = None,
    group_ids: torch.Tensor | None = None,
    balance_groups: bool = False,
) -> tuple[torch.Tensor, torch.Tensor, torch.Tensor, torch.Tensor, torch.Tensor | None]:
    count = features.shape[0]
    effective_batch_size = min(batch_size, count)
    if balance_groups and group_ids is not None and group_ids.numel() == count:
        unique_groups = torch.unique(group_ids)
        per_group = max(1, effective_batch_size // max(1, unique_groups.numel()))
        sampled_indices: list[torch.Tensor] = []
        for group in unique_groups:
            group_indices = torch.nonzero(group_ids == group, as_tuple=False).flatten()
            if group_indices.numel() == 0:
                continue
            sampled_offsets = torch.randint(0, group_indices.numel(), (per_group,))
            sampled_indices.append(group_indices[sampled_offsets])

        indices = torch.cat(sampled_indices) if sampled_indices else torch.randint(0, count, (effective_batch_size,))
        if indices.numel() < effective_batch_size:
            extra = torch.randint(0, count, (effective_batch_size - indices.numel(),))
            indices = torch.cat([indices, extra])
        elif indices.numel() > effective_batch_size:
            indices = indices[torch.randperm(indices.numel())[:effective_batch_size]]
    else:
        indices = torch.randint(0, count, (effective_batch_size,))
    weights = sample_weights[indices] if sample_weights is not None else None
    return features[indices], move_targets[indices], binary_targets[indices], aim_targets[indices], weights


def weighted_mean(values: torch.Tensor, sample_weights: torch.Tensor | None) -> torch.Tensor:
    if sample_weights is None:
        return values.mean()

    weights = sample_weights.to(values.device)
    return (values * weights).sum() / weights.sum().clamp_min(1e-6)


def build_clone_loss(
    move_logits: torch.Tensor,
    move_targets: torch.Tensor,
    binary_logits: torch.Tensor,
    binary_targets: torch.Tensor,
    aim_pred: torch.Tensor,
    aim_targets: torch.Tensor,
    sample_weights: torch.Tensor | None,
) -> torch.Tensor:
    move_loss = F.cross_entropy(move_logits, move_targets, reduction="none")
    binary_loss = F.binary_cross_entropy_with_logits(binary_logits, binary_targets, reduction="none").mean(dim=1)
    aim_loss = F.l1_loss(aim_pred, aim_targets, reduction="none").mean(dim=1)
    return weighted_mean(move_loss + binary_loss + aim_loss, sample_weights)


def build_action_margin_loss(
    move_logits: torch.Tensor,
    move_targets: torch.Tensor,
    binary_logits: torch.Tensor,
    binary_targets: torch.Tensor,
    move_margin: float,
    binary_margin: float,
    sample_weights: torch.Tensor | None = None,
) -> torch.Tensor:
    target_logits = move_logits.gather(1, move_targets.unsqueeze(1)).squeeze(1)
    other_logits = move_logits.masked_fill(
        F.one_hot(move_targets, num_classes=move_logits.shape[1]).bool(),
        float("-inf"),
    )
    best_other_logits = other_logits.max(dim=1).values
    move_loss = F.relu(move_margin - (target_logits - best_other_logits))

    signed_binary_logits = torch.where(binary_targets > 0.5, binary_logits, -binary_logits)
    binary_loss = F.relu(binary_margin - signed_binary_logits).mean(dim=1)
    return weighted_mean(move_loss + binary_loss, sample_weights)


def evaluate_deterministic(
    args: argparse.Namespace,
    onnx_path: Path,
    output_dir: Path,
    epoch: int,
    spec: EvaluationSpec | None = None,
    label: str = "eval",
) -> dict[str, Any]:
    if spec is None:
        spec = EvaluationSpec(
            level_name=args.map,
            team=args.team,
            class_id=args.class_id,
            task=args.task,
            ticks=args.ticks,
            start_node_id=args.start_node_id,
            start_x=args.start_x,
            start_y=args.start_y,
            start_vx=args.start_vx,
            start_vy=args.start_vy,
            carrying_intel=args.carrying_intel,
            start_is_grounded=args.start_is_grounded,
            start_remaining_air_jumps=args.start_remaining_air_jumps,
            start_facing_dir_x=args.start_facing_dir_x,
            start_previous_move_input=args.start_prev_move,
            start_previous_jump_held=args.start_prev_jump_held,
            start_previous_drop_input=args.start_prev_drop,
            start_previous_fire_primary=args.start_prev_fire_primary,
            start_previous_fire_secondary=args.start_prev_fire_secondary,
            start_previous_position_delta_x=args.start_prev_dx,
            start_previous_position_delta_y=args.start_prev_dy,
            start_previous_velocity_x=args.start_prev_vx,
            start_previous_velocity_y=args.start_prev_vy,
            start_previous_facing_dir_x=args.start_prev_facing_dir_x,
            start_previous_is_grounded=args.start_prev_is_grounded,
            start_objective_distance=args.start_objective_distance,
            start_objective_distance_delta=args.start_objective_distance_delta,
            start_previous_objective_distance_delta=args.start_prev_objective_distance_delta,
            start_airborne_ticks=args.start_airborne_ticks,
            start_jump_ticks=args.start_jump_ticks,
            start_frames_since_jump_pressed=args.start_frames_since_jump_pressed,
            start_frames_since_jump_released=args.start_frames_since_jump_released,
        )

    eval_path = output_dir / f"{label}-epoch-{epoch:02d}.json"
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
            spec.level_name,
            "--team",
            spec.team,
            "--class",
            spec.class_id,
            "--task",
            spec.task,
            "--ticks",
            str(spec.ticks),
            "--model",
            str(onnx_path),
            "--json-out",
            str(eval_path),
        ]
    )
    if spec.start_node_id >= 0:
        command.extend(["--start-node-id", str(spec.start_node_id)])
    append_world_start_options(command, spec)
    if args.disable_policy_overrides:
        command.append("--disable-policy-overrides")

    completed = subprocess.run(command, check=False, capture_output=True, text=True, encoding="utf-8")
    if completed.returncode != 0:
        raise RuntimeError(
            "deterministic evaluation failed\n"
            f"command={' '.join(command)}\nstdout={completed.stdout}\nstderr={completed.stderr}"
        )

    with eval_path.open("r", encoding="utf-8") as handle:
        return json.load(handle)


def append_world_start_options(command: list[str], spec: EvaluationSpec) -> None:
    if spec.start_x is not None and spec.start_y is not None:
        command.extend(["--start-x", str(spec.start_x), "--start-y", str(spec.start_y)])
    elif spec.start_x is not None or spec.start_y is not None:
        raise ValueError("world-truth eval fixtures require both start_x and start_y")

    if spec.start_vx is not None and spec.start_vy is not None:
        command.extend(["--start-vx", str(spec.start_vx), "--start-vy", str(spec.start_vy)])
    elif spec.start_vx is not None or spec.start_vy is not None:
        raise ValueError("world-truth eval fixtures require both start_vx and start_vy")

    if spec.carrying_intel is True:
        command.append("--carrying-intel")
    elif spec.carrying_intel is False:
        command.append("--no-carrying-intel")

    append_optional(command, "--start-is-grounded", spec.start_is_grounded)
    append_optional(command, "--start-remaining-air-jumps", spec.start_remaining_air_jumps)
    append_optional(command, "--start-facing-dir-x", spec.start_facing_dir_x)
    append_optional(command, "--start-prev-move", spec.start_previous_move_input)
    append_optional(command, "--start-prev-jump-held", spec.start_previous_jump_held)
    append_optional(command, "--start-prev-drop", spec.start_previous_drop_input)
    append_optional(command, "--start-prev-fire-primary", spec.start_previous_fire_primary)
    append_optional(command, "--start-prev-fire-secondary", spec.start_previous_fire_secondary)
    append_optional(command, "--start-prev-dx", spec.start_previous_position_delta_x)
    append_optional(command, "--start-prev-dy", spec.start_previous_position_delta_y)
    append_optional(command, "--start-prev-vx", spec.start_previous_velocity_x)
    append_optional(command, "--start-prev-vy", spec.start_previous_velocity_y)
    append_optional(command, "--start-prev-facing-dir-x", spec.start_previous_facing_dir_x)
    append_optional(command, "--start-prev-is-grounded", spec.start_previous_is_grounded)
    append_optional(command, "--start-objective-distance", spec.start_objective_distance)
    append_optional(command, "--start-objective-distance-delta", spec.start_objective_distance_delta)
    append_optional(command, "--start-prev-objective-distance-delta", spec.start_previous_objective_distance_delta)
    append_optional(command, "--start-airborne-ticks", spec.start_airborne_ticks)
    append_optional(command, "--start-jump-ticks", spec.start_jump_ticks)
    append_optional(command, "--start-frames-since-jump-pressed", spec.start_frames_since_jump_pressed)
    append_optional(command, "--start-frames-since-jump-released", spec.start_frames_since_jump_released)


def append_optional(command: list[str], flag: str, value: bool | int | float | None) -> None:
    if value is None:
        return
    if isinstance(value, bool):
        command.extend([flag, "true" if value else "false"])
    else:
        command.extend([flag, str(value)])


def eval_has_primary_event(task_phase: str, payload: dict[str, Any]) -> bool:
    primary_kind = primary_event_kind(task_phase)
    if bool(payload.get("Success", False)):
        return True
    if primary_kind == "pickup":
        return payload.get("PickupTick") is not None
    if primary_kind == "score":
        return payload.get("ScoreTick") is not None
    return False


def eval_score_key(payload: dict[str, Any], task_phase: str | None = None) -> tuple[float, ...]:
    task_phase = task_phase or str(payload.get("TaskPhase", ""))
    min_navigation_distance = float(payload.get("MinNavigationDistance", payload["MinObjectiveDistance"]))
    final_navigation_distance = float(payload.get("FinalNavigationDistance", payload["FinalObjectiveDistance"]))
    return (
        1.0 if payload["Success"] else 0.0,
        1.0 if eval_has_primary_event(task_phase, payload) else 0.0,
        -min_navigation_distance,
        -final_navigation_distance,
        -float(payload["MinObjectiveDistance"]),
        -float(payload["FinalObjectiveDistance"]),
        -float(payload["MaxStuckTicks"]),
        float(payload["TotalReward"]),
        -float(payload["TicksElapsed"]),
    )


def aggregate_eval_score(
    target_task_phase: str,
    target_eval: dict[str, Any],
    regression_evals: list[tuple[EvaluationSpec, dict[str, Any]]],
    target_first: bool = False,
) -> tuple[float, ...]:
    if not regression_evals:
        return eval_score_key(target_eval, target_task_phase)

    regression_scores = [eval_score_key(payload, spec.task) for spec, payload in regression_evals]
    regression_successes = sum(1.0 for spec, payload in regression_evals if bool(payload.get("Success", False)))
    regression_primary_events = sum(1.0 for spec, payload in regression_evals if eval_has_primary_event(spec.task, payload))
    min_regression_score = min(regression_scores)
    target_score = eval_score_key(target_eval, target_task_phase)
    if target_first:
        return (
            *target_score,
            regression_successes,
            regression_primary_events,
            *min_regression_score,
        )
    return (
        regression_successes,
        regression_primary_events,
        *min_regression_score,
        *target_score,
    )


def create_model(args: argparse.Namespace) -> torch.nn.Module:
    if args.task_conditioned_mlp_heads:
        return TaskConditionedMlpHeadBehaviorCloningModel()
    return TaskConditionedBehaviorCloningModel() if args.task_conditioned_heads else BehaviorCloningModel()


def task_phase_head_index(task_phase: str) -> int:
    normalized = task_phase.casefold()
    if normalized == "attackintel":
        return 0
    if normalized == "returnintel":
        return 1
    if normalized == "captureobjective":
        return 2
    if normalized == "defendobjective":
        return 3
    raise ValueError(f"unsupported task phase for task head init: {task_phase}")


def apply_task_head_initializers(model: torch.nn.Module, args: argparse.Namespace) -> None:
    if not args.task_head_init:
        return
    if not args.task_conditioned_heads:
        raise ValueError("--task-head-init requires --task-conditioned-heads")

    for raw_value in args.task_head_init:
        task_phase, separator, checkpoint_path = raw_value.partition("=")
        if not separator or not task_phase.strip() or not checkpoint_path.strip():
            raise ValueError("--task-head-init must be formatted as TaskPhase=checkpoint.pt")
        source_state = load_checkpoint_state(Path(checkpoint_path.strip()))
        copy_shared_policy_head_to_task_head(model, source_state, task_phase_head_index(task_phase.strip()))


def freeze_backbone_if_requested(model: torch.nn.Module, args: argparse.Namespace) -> None:
    if not args.freeze_backbone:
        return
    backbone = getattr(model, "backbone", None)
    if backbone is None:
        raise ValueError("--freeze-backbone requires a model with a backbone attribute")
    for parameter in backbone.parameters():
        parameter.requires_grad = False


def clone_model_state(model: torch.nn.Module) -> dict[str, torch.Tensor]:
    return {key: value.detach().cpu().clone() for key, value in model.state_dict().items()}


def create_optimizer(model: torch.nn.Module, learning_rate: float) -> torch.optim.Optimizer:
    return torch.optim.AdamW(
        (parameter for parameter in model.parameters() if parameter.requires_grad),
        lr=learning_rate,
        weight_decay=1e-4,
    )


def should_accept_regression_gated_update(
    epoch_score: tuple[float, ...],
    accepted_score: tuple[float, ...],
    eval_payload: dict[str, Any],
    accepted_eval: dict[str, Any],
    regression_successes: int,
    accepted_regression_successes: int,
    args: argparse.Namespace,
) -> tuple[bool, str]:
    if (
        args.max_accepted_stuck_ticks > 0.0
        and bool(eval_payload.get("Success", False))
        and float(eval_payload.get("MaxStuckTicks", 0.0)) > args.max_accepted_stuck_ticks
    ):
        return False, "target_max_stuck_exceeded"
    if not args.regression_gated_updates:
        return True, ""
    if (
        args.require_target_success_retention
        and bool(accepted_eval.get("Success", False))
        and not bool(eval_payload.get("Success", False))
    ):
        return False, "target_success_regressed"
    if args.require_regression_success_retention and regression_successes < accepted_regression_successes:
        return False, "regression_success_count_regressed"
    accepted = epoch_score > accepted_score if args.require_strict_regression_improvement else epoch_score >= accepted_score
    return (accepted, "" if accepted else "aggregate_score_regressed")


def main() -> None:
    args = parse_args()
    torch.manual_seed(args.seed)
    np.random.seed(args.seed)
    regression_specs = [parse_evaluation_spec(raw_value) for raw_value in args.regression_eval]

    device = torch.device("cuda" if torch.cuda.is_available() and not args.cpu else "cpu")
    output_dir = Path(args.output_dir)
    output_dir.mkdir(parents=True, exist_ok=True)

    rollout_paths = discover_rollout_paths(args)
    if not rollout_paths:
        raise ValueError("no rollout json files were provided")

    rollouts = load_rollouts(rollout_paths)
    filtered_rollouts = filter_rollouts(rollouts, args)
    if not filtered_rollouts:
        filter_description = (
            "mixed rollout replay set"
            if args.allow_mixed_rollout_contexts
            else "requested map/team/class/task filters"
        )
        raise ValueError(f"no rollout json files matched the {filter_description}")

    print(f"loaded_rollouts={len(rollouts)} filtered_rollouts={len(filtered_rollouts)}")
    top_rollouts = select_top_rollouts(filtered_rollouts, args)
    (
        clone_features,
        clone_move_targets,
        clone_binary_targets,
        clone_aim_targets,
        clone_sample_weights,
        clone_group_ids,
        segment_summaries,
    ) = build_rollout_clone_tensors(top_rollouts, args)
    print(
        f"selected_rollouts={len(top_rollouts)} clone_samples={clone_features.shape[0]} "
        f"segment_mode={args.segment_mode} batches_per_epoch={args.batches_per_epoch} "
        f"balance_contexts={args.balance_rollout_contexts} "
        f"weight_mean={clone_sample_weights.mean().item():.2f} weight_max={clone_sample_weights.max().item():.2f}"
    )
    bc_anchor = build_behavior_anchor(args)

    model = create_model(args).to(device)
    initial_state = load_checkpoint_state_for_model(model, Path(args.init_checkpoint))
    model.load_state_dict(initial_state)
    apply_task_head_initializers(model, args)
    freeze_backbone_if_requested(model, args)
    reference_model = create_model(args).to(device)
    reference_model.load_state_dict(initial_state)
    apply_task_head_initializers(reference_model, args)
    reference_model.eval()
    optimizer = create_optimizer(model, args.learning_rate)

    baseline_onnx = output_dir / "baseline-policy.onnx"
    export_onnx_model(model, baseline_onnx)
    best_eval = evaluate_deterministic(args, baseline_onnx, output_dir, 0)
    best_regression_evals = [
        (spec, evaluate_deterministic(args, baseline_onnx, output_dir, 0, spec, f"regression-{index:02d}"))
        for index, spec in enumerate(regression_specs, start=1)
    ]
    best_score = aggregate_eval_score(args.task, best_eval, best_regression_evals, args.target_first_selection)
    best_state = clone_model_state(model)
    accepted_score = best_score
    accepted_eval = best_eval
    accepted_regression_successes = sum(
        1 for _, payload in best_regression_evals if bool(payload.get("Success", False))
    )
    accepted_state = clone_model_state(model)
    metrics: list[DistillationEpochMetrics] = []

    for epoch in range(1, args.epochs + 1):
        model.train()
        rollout_clone_loss_total = 0.0
        action_margin_loss_total = 0.0
        bc_loss_total = 0.0
        reference_kl_total = 0.0
        batch_count = max(1, args.batches_per_epoch)

        for _ in range(batch_count):
            optimizer.zero_grad(set_to_none=True)

            clone_obs, clone_move, clone_binary, clone_aim, clone_weights = sample_batch(
                clone_features,
                clone_move_targets,
                clone_binary_targets,
                clone_aim_targets,
                args.batch_size,
                clone_sample_weights,
                clone_group_ids,
                args.balance_rollout_contexts,
            )
            clone_obs = clone_obs.to(device)
            clone_move = clone_move.to(device)
            clone_binary = clone_binary.to(device)
            clone_aim = clone_aim.to(device)
            clone_weights = clone_weights.to(device) if clone_weights is not None else None
            clone_move_logits, clone_binary_logits, clone_aim_pred = model(clone_obs)
            rollout_clone_loss = build_clone_loss(
                clone_move_logits,
                clone_move,
                clone_binary_logits,
                clone_binary,
                clone_aim_pred,
                clone_aim,
                clone_weights,
            )
            action_margin_loss = build_action_margin_loss(
                clone_move_logits,
                clone_move,
                clone_binary_logits,
                clone_binary,
                args.move_margin,
                args.binary_margin,
                clone_weights,
            )

            if bc_anchor is not None:
                bc_features, bc_move_targets, bc_binary_targets, bc_aim_targets = bc_anchor
                bc_obs, bc_move, bc_binary, bc_aim, _ = sample_batch(
                    bc_features, bc_move_targets, bc_binary_targets, bc_aim_targets, args.batch_size
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
                reference_kl = move_kl + binary_kl
            else:
                bc_loss = torch.zeros((), device=device)
                reference_kl = torch.zeros((), device=device)

            loss = (
                rollout_clone_loss
                + (args.action_margin_coef * action_margin_loss)
                + (args.bc_coef * bc_loss)
                + (args.reference_kl_coef * reference_kl)
            )
            loss.backward()
            optimizer.step()

            rollout_clone_loss_total += float(rollout_clone_loss.detach().cpu())
            action_margin_loss_total += float(action_margin_loss.detach().cpu())
            bc_loss_total += float(bc_loss.detach().cpu())
            reference_kl_total += float(reference_kl.detach().cpu())

        rollout_clone_loss_value = rollout_clone_loss_total / batch_count
        action_margin_loss_value = action_margin_loss_total / batch_count
        bc_loss_value = bc_loss_total / batch_count
        reference_kl_value = reference_kl_total / batch_count

        epoch_onnx = output_dir / "current-policy.onnx"
        export_onnx_model(model, epoch_onnx)
        eval_payload = evaluate_deterministic(args, epoch_onnx, output_dir, epoch)
        regression_evals = [
            (
                spec,
                evaluate_deterministic(args, epoch_onnx, output_dir, epoch, spec, f"regression-{index:02d}"),
            )
            for index, spec in enumerate(regression_specs, start=1)
        ]
        regression_successes = sum(1 for _, payload in regression_evals if bool(payload.get("Success", False)))
        epoch_score = aggregate_eval_score(args.task, eval_payload, regression_evals, args.target_first_selection)
        update_accepted, gate_rejection_reason = should_accept_regression_gated_update(
            epoch_score,
            accepted_score,
            eval_payload,
            accepted_eval,
            regression_successes,
            accepted_regression_successes,
            args,
        )
        if update_accepted:
            accepted_score = epoch_score
            accepted_eval = eval_payload
            accepted_regression_successes = regression_successes
            accepted_state = clone_model_state(model)
        elif args.regression_gated_updates:
            model.load_state_dict(accepted_state)
            if args.reset_optimizer_on_reject:
                optimizer = create_optimizer(model, args.learning_rate)

        if update_accepted and epoch_score > best_score:
            best_eval = eval_payload
            best_regression_evals = regression_evals
            best_score = epoch_score
            best_state = clone_model_state(model)

        metrics.append(
            DistillationEpochMetrics(
                epoch=epoch,
                rollout_clone_loss=rollout_clone_loss_value,
                action_margin_loss=action_margin_loss_value,
                bc_loss=bc_loss_value,
                reference_kl=reference_kl_value,
                eval_success=bool(eval_payload["Success"]),
                eval_terminal_reason=str(eval_payload["TerminalReason"]),
                eval_min_objective_distance=float(eval_payload["MinObjectiveDistance"]),
                eval_final_objective_distance=float(eval_payload["FinalObjectiveDistance"]),
                eval_max_stuck_ticks=float(eval_payload["MaxStuckTicks"]),
                eval_ticks_elapsed=int(eval_payload["TicksElapsed"]),
                regression_successes=regression_successes,
                regression_count=len(regression_specs),
                update_accepted=update_accepted,
                gate_rejection_reason=gate_rejection_reason,
            )
        )
        print(
            f"epoch={epoch} rollout_clone_loss={metrics[-1].rollout_clone_loss:.4f} "
            f"action_margin_loss={metrics[-1].action_margin_loss:.4f} "
            f"bc_loss={metrics[-1].bc_loss:.4f} reference_kl={metrics[-1].reference_kl:.4f} "
            f"eval_success={metrics[-1].eval_success} eval_terminal_reason={metrics[-1].eval_terminal_reason} "
            f"eval_min_distance={metrics[-1].eval_min_objective_distance:.1f} "
            f"eval_final_distance={metrics[-1].eval_final_objective_distance:.1f} "
            f"eval_max_stuck={metrics[-1].eval_max_stuck_ticks:.1f} "
            f"regression_successes={metrics[-1].regression_successes}/{metrics[-1].regression_count} "
            f"update_accepted={metrics[-1].update_accepted} "
            f"gate_rejection_reason={metrics[-1].gate_rejection_reason}"
        )

    checkpoint_path = output_dir / "model.pt"
    torch.save(best_state, checkpoint_path)
    model.load_state_dict(best_state)
    onnx_path = output_dir / "model.onnx"
    export_onnx_model(model, onnx_path)

    with (output_dir / "metrics.json").open("w", encoding="utf-8") as handle:
        json.dump([asdict(item) for item in metrics], handle, indent=2)
    with (output_dir / "selection-summary.json").open("w", encoding="utf-8") as handle:
        json.dump(
            {
                "baseline_eval": evaluate_deterministic(args, baseline_onnx, output_dir, -1),
                "best_eval": best_eval,
                "best_regression_evals": [
                    {
                        "spec": asdict(spec),
                        "eval": payload,
                    }
                    for spec, payload in best_regression_evals
                ],
                "best_score": list(best_score),
                "loaded_rollout_count": len(rollouts),
                "filtered_rollout_count": len(filtered_rollouts),
                "selected_rollout_count": len(top_rollouts),
                "top_k_per_rollout_context": args.top_k_per_rollout_context,
                "clone_sample_count": int(clone_features.shape[0]),
                "segment_mode": args.segment_mode,
                "teacher_selection_key": args.teacher_selection_key,
                "segment_summaries": segment_summaries,
                "source_rollouts": [rollout["_source_path"] for rollout in top_rollouts],
            },
            handle,
            indent=2,
        )

    print(f"saved checkpoint={checkpoint_path}")
    print(f"saved onnx={onnx_path}")


if __name__ == "__main__":
    main()
