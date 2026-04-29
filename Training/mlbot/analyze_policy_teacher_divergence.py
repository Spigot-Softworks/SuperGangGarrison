from __future__ import annotations

import argparse
import json
from pathlib import Path
from typing import Any

import numpy as np
import torch
import torch.nn.functional as F

from mlbot_dataset import vectorize_observation
from train_behavior_cloning import (
    BehaviorCloningModel,
    TaskConditionedBehaviorCloningModel,
    load_checkpoint_state_for_model,
)


BINARY_ACTIONS = ("Jump", "Crouch", "FirePrimary", "FireSecondary", "DropIntel")


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(
        description="Compare a trained policy checkpoint against a successful rollout teacher."
    )
    parser.add_argument("--model", required=True, help="PyTorch policy checkpoint.")
    parser.add_argument("--rollout", required=True, help="Rollout JSON to use as the teacher trace.")
    parser.add_argument("--output", required=True, help="Path to write divergence summary JSON.")
    parser.add_argument("--start-tick", type=int, default=0)
    parser.add_argument("--end-tick", type=int, default=0)
    parser.add_argument("--top-runs", type=int, default=12)
    parser.add_argument("--window-size", type=int, default=100)
    parser.add_argument("--task-conditioned-heads", action="store_true")
    parser.add_argument("--cpu", action="store_true")
    return parser.parse_args()


def create_model(args: argparse.Namespace) -> torch.nn.Module:
    if args.task_conditioned_heads:
        return TaskConditionedBehaviorCloningModel()
    return BehaviorCloningModel()


def move_direction_to_index(move_direction: int) -> int:
    return move_direction + 1


def move_index_to_direction(move_index: int) -> int:
    return move_index - 1


def action_binary_vector(action: dict[str, Any]) -> np.ndarray:
    return np.asarray([1.0 if action.get(name, False) else 0.0 for name in BINARY_ACTIONS], dtype=np.float32)


def observation_snapshot(observation: dict[str, Any]) -> dict[str, Any]:
    waypoint = observation.get("Waypoint", {})
    return {
        "bot_x": observation.get("BotX"),
        "bot_y": observation.get("BotY"),
        "objective_distance": observation.get("ObjectiveDistance"),
        "waypoint_distance": waypoint.get("Distance"),
        "waypoint_relative_x": waypoint.get("RelativeX"),
        "waypoint_relative_y": waypoint.get("RelativeY"),
        "has_waypoint": waypoint.get("HasWaypoint"),
        "is_final_waypoint": waypoint.get("IsFinalWaypoint"),
        "wall_contact_left": observation.get("WallContactLeft"),
        "wall_contact_right": observation.get("WallContactRight"),
        "left_clearance": observation.get("LeftClearance"),
        "right_clearance": observation.get("RightClearance"),
    }


def load_rollout_steps(path: Path, start_tick: int, end_tick: int) -> list[dict[str, Any]]:
    with path.open("r", encoding="utf-8") as handle:
        rollout = json.load(handle)

    steps = list(rollout.get("Steps", []))
    if start_tick > 0:
        steps = [step for step in steps if int(step.get("Tick", 0)) >= start_tick]
    if end_tick > 0:
        steps = [step for step in steps if int(step.get("Tick", 0)) <= end_tick]
    if not steps:
        raise ValueError("no rollout steps matched the requested tick range")
    return steps


def contiguous_runs(rows: list[dict[str, Any]]) -> list[dict[str, Any]]:
    runs: list[dict[str, Any]] = []
    active: dict[str, Any] | None = None

    for row in rows:
        if not row["any_mismatch"]:
            if active is not None:
                runs.append(active)
                active = None
            continue

        if active is None:
            active = {
                "start_tick": row["tick"],
                "end_tick": row["tick"],
                "length": 1,
                "move_mismatches": 1 if row["move_mismatch"] else 0,
                "jump_mismatches": 1 if row["binary_mismatches"].get("Jump", False) else 0,
                "first": row,
                "last": row,
            }
        else:
            active["end_tick"] = row["tick"]
            active["length"] += 1
            active["move_mismatches"] += 1 if row["move_mismatch"] else 0
            active["jump_mismatches"] += 1 if row["binary_mismatches"].get("Jump", False) else 0
            active["last"] = row

    if active is not None:
        runs.append(active)
    return runs


def summarize_windows(rows: list[dict[str, Any]], window_size: int) -> list[dict[str, Any]]:
    if window_size <= 0:
        return []

    windows: dict[int, list[dict[str, Any]]] = {}
    for row in rows:
        bucket = (int(row["tick"]) // window_size) * window_size
        windows.setdefault(bucket, []).append(row)

    summaries: list[dict[str, Any]] = []
    for start_tick, bucket_rows in sorted(windows.items()):
        count = len(bucket_rows)
        move_matches = sum(1 for row in bucket_rows if not row["move_mismatch"])
        exact_matches = sum(1 for row in bucket_rows if not row["any_mismatch"])
        jump_matches = sum(1 for row in bucket_rows if not row["binary_mismatches"].get("Jump", False))
        summaries.append(
            {
                "start_tick": start_tick,
                "end_tick": start_tick + window_size - 1,
                "steps": count,
                "move_accuracy": move_matches / count,
                "jump_accuracy": jump_matches / count,
                "exact_action_accuracy": exact_matches / count,
            }
        )
    return summaries


def main() -> None:
    args = parse_args()
    device = torch.device("cuda" if torch.cuda.is_available() and not args.cpu else "cpu")

    model = create_model(args).to(device)
    model.load_state_dict(load_checkpoint_state_for_model(model, Path(args.model)))
    model.eval()

    steps = load_rollout_steps(Path(args.rollout), args.start_tick, args.end_tick)
    features = np.stack([vectorize_observation(step["Observation"]) for step in steps]).astype(np.float32)
    observations = torch.from_numpy(features).to(device)

    with torch.no_grad():
        move_logits, binary_logits, _ = model(observations)
        move_probs = F.softmax(move_logits, dim=1).cpu().numpy()
        predicted_move_indices = move_logits.argmax(dim=1).cpu().numpy()
        predicted_binary = (binary_logits > 0).cpu().numpy()

    rows: list[dict[str, Any]] = []
    move_match_count = 0
    binary_match_counts = {name: 0 for name in BINARY_ACTIONS}
    exact_match_count = 0

    for index, step in enumerate(steps):
        action = step["Action"]
        teacher_move_index = move_direction_to_index(int(action["MoveDirection"]))
        predicted_move_index = int(predicted_move_indices[index])
        teacher_binary = action_binary_vector(action)
        binary_mismatches = {
            name: bool(predicted_binary[index][binary_index] != (teacher_binary[binary_index] > 0.5))
            for binary_index, name in enumerate(BINARY_ACTIONS)
        }
        move_mismatch = predicted_move_index != teacher_move_index
        any_mismatch = move_mismatch or any(binary_mismatches.values())

        if not move_mismatch:
            move_match_count += 1
        for binary_index, name in enumerate(BINARY_ACTIONS):
            if not binary_mismatches[name]:
                binary_match_counts[name] += 1
        if not any_mismatch:
            exact_match_count += 1

        rows.append(
            {
                "tick": int(step.get("Tick", index + 1)),
                "move_mismatch": move_mismatch,
                "binary_mismatches": binary_mismatches,
                "any_mismatch": any_mismatch,
                "teacher_move": move_index_to_direction(teacher_move_index),
                "predicted_move": move_index_to_direction(predicted_move_index),
                "predicted_move_probability": float(move_probs[index][predicted_move_index]),
                "teacher_move_probability": float(move_probs[index][teacher_move_index]),
                "teacher_jump": bool(action.get("Jump", False)),
                "predicted_jump": bool(predicted_binary[index][0]),
                "observation": observation_snapshot(step["Observation"]),
            }
        )

    runs = contiguous_runs(rows)
    runs.sort(
        key=lambda run: (
            int(run["length"]),
            int(run["move_mismatches"]),
            int(run["jump_mismatches"]),
        ),
        reverse=True,
    )

    evaluated_count = len(rows)
    output = {
        "model": str(Path(args.model).resolve()),
        "rollout": str(Path(args.rollout).resolve()),
        "evaluated_steps": evaluated_count,
        "start_tick": rows[0]["tick"],
        "end_tick": rows[-1]["tick"],
        "move_accuracy": move_match_count / evaluated_count,
        "binary_accuracy": {
            name: binary_match_counts[name] / evaluated_count
            for name in BINARY_ACTIONS
        },
        "exact_action_accuracy": exact_match_count / evaluated_count,
        "top_disagreement_runs": runs[: max(0, args.top_runs)],
        "window_summaries": summarize_windows(rows, args.window_size),
    }

    output_path = Path(args.output)
    output_path.parent.mkdir(parents=True, exist_ok=True)
    with output_path.open("w", encoding="utf-8") as handle:
        json.dump(output, handle, indent=2)

    print(
        f"evaluated_steps={evaluated_count} move_accuracy={output['move_accuracy']:.3f} "
        f"exact_action_accuracy={output['exact_action_accuracy']:.3f} output={output_path}"
    )


if __name__ == "__main__":
    main()
