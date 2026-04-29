from __future__ import annotations

import argparse
import json
from pathlib import Path
from typing import Any

import numpy as np
import torch
import torch.nn.functional as F

from mlbot_dataset import encode_action, vectorize_observation
from train_behavior_cloning import BehaviorCloningModel, load_checkpoint_state_for_model


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser()
    parser.add_argument("--checkpoint", required=True)
    parser.add_argument("--rollout-path", action="append", default=[])
    parser.add_argument("--rollout-dir", action="append", default=[])
    parser.add_argument("--out", default="")
    parser.add_argument("--max-tick", type=int, default=0)
    parser.add_argument("--top-mismatches", type=int, default=40)
    parser.add_argument("--device", default="cpu")
    return parser.parse_args()


def discover_rollouts(args: argparse.Namespace) -> list[Path]:
    paths: list[Path] = []
    for raw_path in args.rollout_path:
        path = Path(raw_path)
        if path.is_file():
            paths.append(path)

    for raw_dir in args.rollout_dir:
        directory = Path(raw_dir)
        if directory.is_dir():
            paths.extend(sorted(directory.rglob("episode-*.json")))

    unique: list[Path] = []
    seen: set[Path] = set()
    for path in paths:
        resolved = path.resolve()
        if resolved in seen:
            continue
        seen.add(resolved)
        unique.append(path)
    return unique


def sigmoid(value: float) -> float:
    return float(1.0 / (1.0 + np.exp(-value)))


def summarize_values(values: list[float]) -> dict[str, float]:
    if not values:
        return {"count": 0.0, "mean": 0.0, "min": 0.0, "p10": 0.0, "median": 0.0, "p90": 0.0, "max": 0.0}

    array = np.asarray(values, dtype=np.float32)
    return {
        "count": float(array.size),
        "mean": float(array.mean()),
        "min": float(array.min()),
        "p10": float(np.percentile(array, 10)),
        "median": float(np.percentile(array, 50)),
        "p90": float(np.percentile(array, 90)),
        "max": float(array.max()),
    }


def action_signature(action: dict[str, Any]) -> str:
    return f"move={action['MoveDirection']} jump={bool(action['Jump'])} crouch={bool(action['Crouch'])}"


def main() -> int:
    args = parse_args()
    device = torch.device(args.device)
    model = BehaviorCloningModel().to(device)
    model.load_state_dict(load_checkpoint_state_for_model(model, Path(args.checkpoint)))
    model.eval()

    paths = discover_rollouts(args)
    if not paths:
        raise ValueError("no rollout json files matched the provided paths")

    records: list[dict[str, Any]] = []
    move_correct = 0
    jump_correct = 0
    crouch_correct = 0
    target_move_margins: list[float] = []
    target_jump_margins: list[float] = []
    target_crouch_margins: list[float] = []
    move_mismatch_records: list[dict[str, Any]] = []
    jump_mismatch_records: list[dict[str, Any]] = []
    by_action: dict[str, int] = {}

    with torch.no_grad():
        for path in paths:
            with path.open("r", encoding="utf-8") as handle:
                rollout = json.load(handle)

            for step in rollout.get("Steps", []):
                tick = int(step.get("Tick", 0))
                if args.max_tick > 0 and tick > args.max_tick:
                    continue

                features = vectorize_observation(step["Observation"])
                obs = torch.from_numpy(features).to(device).unsqueeze(0)
                move_logits_tensor, binary_logits_tensor, _ = model(obs)
                move_logits = move_logits_tensor.squeeze(0).detach().cpu()
                binary_logits = binary_logits_tensor.squeeze(0).detach().cpu()
                move_target, binary_target_np, _ = encode_action(step)
                binary_target = torch.from_numpy(binary_target_np)

                move_prediction = int(move_logits.argmax().item())
                jump_prediction = bool(binary_logits[0].item() > 0.0)
                crouch_prediction = bool(binary_logits[1].item() > 0.0)
                target_move_logit = float(move_logits[move_target].item())
                other_move_logits = move_logits.clone()
                other_move_logits[move_target] = float("-inf")
                move_margin = target_move_logit - float(other_move_logits.max().item())
                signed_binary_logits = torch.where(binary_target > 0.5, binary_logits, -binary_logits)
                jump_margin = float(signed_binary_logits[0].item())
                crouch_margin = float(signed_binary_logits[1].item())

                move_correct += int(move_prediction == move_target)
                jump_correct += int(jump_prediction == bool(binary_target[0].item() > 0.5))
                crouch_correct += int(crouch_prediction == bool(binary_target[1].item() > 0.5))
                target_move_margins.append(move_margin)
                target_jump_margins.append(jump_margin)
                target_crouch_margins.append(crouch_margin)
                signature = action_signature(step["Action"])
                by_action[signature] = by_action.get(signature, 0) + 1

                record = {
                    "source_path": str(path),
                    "tick": tick,
                    "bot_x": float(step["Observation"].get("BotX", 0.0)),
                    "bot_y": float(step["Observation"].get("BotY", 0.0)),
                    "velocity_x": float(step["Observation"].get("VelocityX", 0.0)),
                    "velocity_y": float(step["Observation"].get("VelocityY", 0.0)),
                    "is_grounded": bool(step["Observation"].get("IsGrounded", False)),
                    "waypoint_relative_x": float(step["Observation"].get("Waypoint", {}).get("RelativeX", 0.0)),
                    "waypoint_relative_y": float(step["Observation"].get("Waypoint", {}).get("RelativeY", 0.0)),
                    "waypoint_distance": float(step["Observation"].get("Waypoint", {}).get("Distance", 0.0)),
                    "target_move": int(step["Action"]["MoveDirection"]),
                    "predicted_move": move_prediction - 1,
                    "target_jump": bool(step["Action"]["Jump"]),
                    "predicted_jump": jump_prediction,
                    "target_crouch": bool(step["Action"]["Crouch"]),
                    "predicted_crouch": crouch_prediction,
                    "move_logits": [float(value) for value in move_logits.tolist()],
                    "jump_logit": float(binary_logits[0].item()),
                    "jump_probability": sigmoid(float(binary_logits[0].item())),
                    "crouch_logit": float(binary_logits[1].item()),
                    "crouch_probability": sigmoid(float(binary_logits[1].item())),
                    "target_move_margin": move_margin,
                    "target_jump_margin": jump_margin,
                    "target_crouch_margin": crouch_margin,
                }
                records.append(record)
                if move_prediction != move_target:
                    move_mismatch_records.append(record)
                if jump_prediction != bool(binary_target[0].item() > 0.5):
                    jump_mismatch_records.append(record)

    total = max(1, len(records))
    move_mismatch_records.sort(key=lambda item: item["target_move_margin"])
    jump_mismatch_records.sort(key=lambda item: item["target_jump_margin"])
    summary = {
        "checkpoint": str(Path(args.checkpoint)),
        "rollout_count": len(paths),
        "sample_count": len(records),
        "move_accuracy": move_correct / total,
        "jump_accuracy": jump_correct / total,
        "crouch_accuracy": crouch_correct / total,
        "target_move_margin": summarize_values(target_move_margins),
        "target_jump_margin": summarize_values(target_jump_margins),
        "target_crouch_margin": summarize_values(target_crouch_margins),
        "action_counts": dict(sorted(by_action.items(), key=lambda item: item[1], reverse=True)),
        "worst_move_mismatches": move_mismatch_records[: args.top_mismatches],
        "worst_jump_mismatches": jump_mismatch_records[: args.top_mismatches],
    }

    if args.out:
        output_path = Path(args.out)
        output_path.parent.mkdir(parents=True, exist_ok=True)
        with output_path.open("w", encoding="utf-8") as handle:
            json.dump(summary, handle, indent=2)

    print(
        f"rollouts={summary['rollout_count']} samples={summary['sample_count']} "
        f"move_accuracy={summary['move_accuracy']:.3f} jump_accuracy={summary['jump_accuracy']:.3f} "
        f"move_margin_mean={summary['target_move_margin']['mean']:.3f} "
        f"jump_margin_mean={summary['target_jump_margin']['mean']:.3f}"
    )
    if move_mismatch_records:
        worst = move_mismatch_records[0]
        print(
            "worst_move "
            f"tick={worst['tick']} target={worst['target_move']} predicted={worst['predicted_move']} "
            f"margin={worst['target_move_margin']:.3f} pos=({worst['bot_x']:.1f},{worst['bot_y']:.1f}) "
            f"wp=({worst['waypoint_relative_x']:.1f},{worst['waypoint_relative_y']:.1f})"
        )
    if jump_mismatch_records:
        worst = jump_mismatch_records[0]
        print(
            "worst_jump "
            f"tick={worst['tick']} target={worst['target_jump']} predicted={worst['predicted_jump']} "
            f"margin={worst['target_jump_margin']:.3f} prob={worst['jump_probability']:.3f} "
            f"pos=({worst['bot_x']:.1f},{worst['bot_y']:.1f})"
        )
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
