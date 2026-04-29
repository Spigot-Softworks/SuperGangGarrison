from __future__ import annotations

import argparse
import json
from collections import defaultdict
from pathlib import Path
from typing import Any

import numpy as np
import torch
import torch.nn as nn
import torch.nn.functional as F

from build_outcome_policy_dataset import action_score, iter_outcome_samples
from mlbot_dataset import DISTANCE_SCALE, FEATURE_COUNT, vectorize_observation
from train_return_chunk_head import ReturnChunkModel, mirror_observation


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description="Train a chunk head from mlbot-outcome-v1 best macro-action probes.")
    parser.add_argument("--data-path", action="append", default=[])
    parser.add_argument("--data-dir", action="append", default=[])
    parser.add_argument("--output-dir", required=True)
    parser.add_argument("--task-phase", default="AttackIntel")
    parser.add_argument("--team", default="")
    parser.add_argument("--class-id", default="")
    parser.add_argument("--horizon", type=int, default=16)
    parser.add_argument("--jump-hold-ticks", type=int, default=8)
    parser.add_argument("--epochs", type=int, default=60)
    parser.add_argument("--batches-per-epoch", type=int, default=64)
    parser.add_argument("--batch-size", type=int, default=256)
    parser.add_argument("--learning-rate", type=float, default=2e-4)
    parser.add_argument("--min-objective-distance", type=float, default=0.0)
    parser.add_argument("--max-objective-distance", type=float, default=0.0)
    parser.add_argument("--min-objective-relative-x", type=float, default=None)
    parser.add_argument("--max-objective-relative-x", type=float, default=None)
    parser.add_argument("--min-objective-relative-y", type=float, default=None)
    parser.add_argument("--max-objective-relative-y", type=float, default=None)
    parser.add_argument("--jump-positive-weight", type=float, default=3.0)
    parser.add_argument("--min-selected-score", type=float, default=-1.0e9)
    parser.add_argument("--min-best-margin", type=float, default=0.0)
    parser.add_argument("--require-success", action="store_true")
    parser.add_argument(
        "--action-name",
        action="append",
        default=[],
        help="Only train from outcome samples whose ActionName matches one of these values.",
    )
    parser.add_argument("--max-samples", type=int, default=0)
    parser.add_argument("--mirror-augment", action="store_true")
    parser.add_argument("--mirror-center-x", type=float, default=0.0)
    parser.add_argument("--seed", type=int, default=1337)
    parser.add_argument("--cpu", action="store_true")
    return parser.parse_args()


def mirror_outcome_sample(sample: dict[str, Any], center_x: float) -> dict[str, Any]:
    mirrored = dict(sample)
    mirrored["StartObservation"] = mirror_observation(sample["StartObservation"], center_x)
    if "MoveDirection" in mirrored:
        mirrored["MoveDirection"] = -int(mirrored.get("MoveDirection", 0))
    move_directions = mirrored.get("MoveDirections")
    if isinstance(move_directions, list):
        mirrored["MoveDirections"] = [-int(value) for value in move_directions]
    return mirrored


def discover_paths(args: argparse.Namespace) -> list[Path]:
    paths = [Path(path) for path in args.data_path]
    for raw_dir in args.data_dir:
        directory = Path(raw_dir)
        if directory.is_dir():
            paths.extend(sorted(directory.rglob("*.json")))

    unique: list[Path] = []
    seen: set[Path] = set()
    for path in paths:
        if not path.is_file():
            continue
        resolved = path.resolve()
        if resolved in seen:
            continue
        seen.add(resolved)
        unique.append(resolved)
    return unique


def matches_filters(observation: dict[str, Any], args: argparse.Namespace) -> bool:
    if str(observation.get("TaskPhase", "")).casefold() != str(args.task_phase).casefold():
        return False
    if args.team and str(observation.get("Team", "")).casefold() != args.team.casefold():
        return False
    if args.class_id and str(observation.get("ClassId", "")).casefold() != args.class_id.casefold():
        return False
    objective = observation.get("Objective", {})
    if not objective.get("HasObjective", False):
        return False
    distance = float(observation.get("ObjectiveDistance", 0.0))
    if args.min_objective_distance > 0.0 and distance < args.min_objective_distance:
        return False
    if args.max_objective_distance > 0.0 and distance > args.max_objective_distance:
        return False
    relative_x = float(objective.get("RelativeX", 0.0))
    relative_y = float(objective.get("RelativeY", 0.0))
    if args.min_objective_relative_x is not None and relative_x < args.min_objective_relative_x:
        return False
    if args.max_objective_relative_x is not None and relative_x > args.max_objective_relative_x:
        return False
    if args.min_objective_relative_y is not None and relative_y < args.min_objective_relative_y:
        return False
    if args.max_objective_relative_y is not None and relative_y > args.max_objective_relative_y:
        return False
    return True


def best_samples(paths: list[Path], args: argparse.Namespace) -> tuple[list[dict[str, Any]], dict[str, Any]]:
    groups: dict[tuple[str, int, int], list[dict[str, Any]]] = defaultdict(list)
    action_name_filter = {str(name).casefold() for name in args.action_name if str(name).strip()}
    scanned = 0
    accepted = 0
    file_counts: dict[str, int] = {}
    for path in paths:
        with path.open("r", encoding="utf-8-sig") as handle:
            document = json.load(handle)
        if document.get("SchemaVersion") not in {"mlbot-outcome-v1", "mlbot-outcome-compact-v1"}:
            continue
        local_count = 0
        for sample in iter_outcome_samples(document, path):
            scanned += 1
            if args.require_success and not bool(sample.get("Success", False)):
                continue
            if action_name_filter and str(sample.get("ActionName", "")).casefold() not in action_name_filter:
                continue
            observation = sample.get("StartObservation")
            if not isinstance(observation, dict) or not matches_filters(observation, args):
                continue
            key = (
                str(sample.get("SourcePath", path)),
                int(sample.get("SourceTick", 0)),
                int(sample.get("HorizonTicks", document.get("HorizonTicks", 0))),
            )
            groups[key].append(sample)
            accepted += 1
            local_count += 1
        if local_count:
            file_counts[str(path)] = local_count

    selected: list[dict[str, Any]] = []
    rejected_low_score = 0
    rejected_low_margin = 0
    for candidates in groups.values():
        if not candidates:
            continue
        ranked = sorted(candidates, key=action_score, reverse=True)
        best_score = action_score(ranked[0])
        if len(ranked) > 1 and best_score - action_score(ranked[1]) < float(args.min_best_margin):
            rejected_low_margin += 1
            continue
        if best_score < float(args.min_selected_score):
            rejected_low_score += 1
            continue
        selected.append(ranked[0])
    if args.max_samples > 0 and len(selected) > args.max_samples:
        rng = np.random.default_rng(args.seed)
        indices = rng.choice(len(selected), size=args.max_samples, replace=False)
        selected = [selected[int(index)] for index in indices]

    action_counts: dict[str, int] = {}
    for sample in selected:
        action_name = str(sample.get("ActionName", "unknown"))
        action_counts[action_name] = action_counts.get(action_name, 0) + 1

    summary = {
        "scanned_samples": scanned,
        "accepted_action_samples": accepted,
        "anchor_horizon_groups": len(groups),
        "selected_samples": len(selected),
        "rejected_low_score": rejected_low_score,
        "rejected_low_margin": rejected_low_margin,
        "min_selected_score": float(args.min_selected_score),
        "min_best_margin": float(args.min_best_margin),
        "require_success": bool(args.require_success),
        "action_name_filter": sorted(action_name_filter),
        "file_counts": file_counts,
        "action_counts": action_counts,
    }
    return selected, summary


def aim_target(observation: dict[str, Any]) -> np.ndarray:
    objective = observation.get("Objective", {})
    bot_x = float(observation.get("BotX", 0.0))
    bot_y = float(observation.get("BotY", 0.0))
    aim_x = float(objective.get("WorldX", bot_x))
    aim_y = float(objective.get("WorldY", bot_y))
    return np.asarray(
        [
            np.clip((aim_x - bot_x) / DISTANCE_SCALE, -1.0, 1.0),
            np.clip((aim_y - bot_y) / DISTANCE_SCALE, -1.0, 1.0),
        ],
        dtype=np.float32,
    )


def build_targets(sample: dict[str, Any], args: argparse.Namespace) -> tuple[np.ndarray, np.ndarray, np.ndarray]:
    move_index = max(0, min(2, int(sample.get("MoveDirection", 0)) + 1))
    move_directions = sample.get("MoveDirections", [])
    if isinstance(move_directions, list) and move_directions:
        moves = np.asarray(
            [max(0, min(2, int(move_directions[min(index, len(move_directions) - 1)]) + 1)) for index in range(args.horizon)],
            dtype=np.int64,
        )
    else:
        moves = np.full(args.horizon, move_index, dtype=np.int64)
    binary = np.zeros((args.horizon, 5), dtype=np.float32)
    jump_sequence = sample.get("JumpSequence", [])
    if isinstance(jump_sequence, list) and jump_sequence:
        for index in range(args.horizon):
            binary[index, 0] = 1.0 if bool(jump_sequence[min(index, len(jump_sequence) - 1)]) else 0.0
    elif bool(sample.get("Jump", False)):
        binary[: min(args.horizon, max(1, args.jump_hold_ticks)), 0] = 1.0
    if bool(sample.get("Crouch", False)):
        binary[:, 1] = 1.0
    aim = np.repeat(aim_target(sample["StartObservation"])[np.newaxis, :], args.horizon, axis=0)
    return moves, binary, aim.astype(np.float32)


def build_dataset(args: argparse.Namespace) -> tuple[torch.Tensor, torch.Tensor, torch.Tensor, torch.Tensor, dict[str, Any]]:
    selected, summary = best_samples(discover_paths(args), args)
    if not selected:
        raise ValueError("no outcome-policy chunk samples matched the requested filters")

    features: list[np.ndarray] = []
    moves: list[np.ndarray] = []
    binary: list[np.ndarray] = []
    aim: list[np.ndarray] = []
    for sample in selected:
        features.append(vectorize_observation(sample["StartObservation"]).astype(np.float32))
        move_target, binary_target, aim_target_value = build_targets(sample, args)
        moves.append(move_target)
        binary.append(binary_target)
        aim.append(aim_target_value)
        if args.mirror_augment:
            if args.mirror_center_x <= 0.0:
                raise ValueError("--mirror-center-x must be positive when --mirror-augment is enabled")
            mirrored_sample = mirror_outcome_sample(sample, args.mirror_center_x)
            features.append(vectorize_observation(mirrored_sample["StartObservation"]).astype(np.float32))
            move_target, binary_target, aim_target_value = build_targets(mirrored_sample, args)
            moves.append(move_target)
            binary.append(binary_target)
            aim.append(aim_target_value)

    summary.update(
        {
            "mirror_augment": bool(args.mirror_augment),
            "mirror_center_x": float(args.mirror_center_x),
            "mirrored_sample_count": len(selected) if args.mirror_augment else 0,
            "horizon": args.horizon,
            "jump_hold_ticks": args.jump_hold_ticks,
            "task_phase": args.task_phase,
            "team": args.team,
            "class_id": args.class_id,
            "min_objective_distance": args.min_objective_distance,
            "max_objective_distance": args.max_objective_distance,
            "min_objective_relative_x": args.min_objective_relative_x,
            "max_objective_relative_x": args.max_objective_relative_x,
            "min_objective_relative_y": args.min_objective_relative_y,
            "max_objective_relative_y": args.max_objective_relative_y,
        }
    )
    return (
        torch.tensor(np.stack(features), dtype=torch.float32),
        torch.tensor(np.stack(moves), dtype=torch.long),
        torch.tensor(np.stack(binary), dtype=torch.float32),
        torch.tensor(np.stack(aim), dtype=torch.float32),
        summary,
    )


def sample_batch(
    features: torch.Tensor,
    moves: torch.Tensor,
    binary: torch.Tensor,
    aim: torch.Tensor,
    batch_size: int,
) -> tuple[torch.Tensor, torch.Tensor, torch.Tensor, torch.Tensor]:
    indices = torch.randint(0, features.shape[0], (batch_size,), device=features.device)
    return features[indices], moves[indices], binary[indices], aim[indices]


def train(args: argparse.Namespace) -> None:
    torch.manual_seed(args.seed)
    np.random.seed(args.seed)
    output_dir = Path(args.output_dir)
    output_dir.mkdir(parents=True, exist_ok=True)
    device = torch.device("cuda" if torch.cuda.is_available() and not args.cpu else "cpu")

    features, moves, binary, aim, dataset_summary = build_dataset(args)
    features = features.to(device)
    moves = moves.to(device)
    binary = binary.to(device)
    aim = aim.to(device)

    model = ReturnChunkModel(args.horizon).to(device)
    optimizer = torch.optim.AdamW(model.parameters(), lr=args.learning_rate, weight_decay=1e-4)
    binary_positive_weights = torch.ones(5, dtype=torch.float32, device=device)
    binary_positive_weights[0] = max(1.0, float(args.jump_positive_weight))
    metrics: list[dict[str, Any]] = []

    for epoch in range(1, args.epochs + 1):
        model.train()
        total_loss = 0.0
        for _ in range(args.batches_per_epoch):
            obs_batch, move_batch, binary_batch, aim_batch = sample_batch(
                features,
                moves,
                binary,
                aim,
                args.batch_size,
            )
            optimizer.zero_grad(set_to_none=True)
            move_logits, binary_logits, aim_output = model(obs_batch)
            move_loss = F.cross_entropy(move_logits.reshape(-1, 3), move_batch.reshape(-1))
            binary_loss = F.binary_cross_entropy_with_logits(
                binary_logits,
                binary_batch,
                pos_weight=binary_positive_weights.view(1, 1, -1),
            )
            aim_loss = F.l1_loss(aim_output, aim_batch)
            loss = move_loss + binary_loss + aim_loss
            loss.backward()
            optimizer.step()
            total_loss += float(loss.item())

        with torch.no_grad():
            move_logits, binary_logits, aim_output = model(features)
            move_accuracy = float((move_logits.argmax(dim=2) == moves).float().mean().item())
            jump_accuracy = float(((binary_logits[:, :, 0] > 0.0).float() == binary[:, :, 0]).float().mean().item())
            aim_mae = float(torch.abs(aim_output - aim).mean().item())
        metrics.append(
            {
                "epoch": epoch,
                "loss": total_loss / max(1, args.batches_per_epoch),
                "move_accuracy": move_accuracy,
                "jump_accuracy": jump_accuracy,
                "aim_mae": aim_mae,
            }
        )
        print(
            f"epoch={epoch} loss={metrics[-1]['loss']:.4f} "
            f"move_acc={move_accuracy:.3f} jump_acc={jump_accuracy:.3f} aim_mae={aim_mae:.4f}"
        )

    checkpoint_path = output_dir / "chunk_model.pt"
    torch.save(model.state_dict(), checkpoint_path)
    model.eval()
    dummy = torch.zeros(1, FEATURE_COUNT, dtype=torch.float32, device=device)
    onnx_path = output_dir / "chunk_model.onnx"
    torch.onnx.export(
        model,
        dummy,
        onnx_path,
        input_names=["obs"],
        output_names=["chunk_move_logits", "chunk_binary_logits", "chunk_aim"],
        dynamic_axes={
            "obs": {0: "batch"},
            "chunk_move_logits": {0: "batch"},
            "chunk_binary_logits": {0: "batch"},
            "chunk_aim": {0: "batch"},
        },
        opset_version=17,
        dynamo=False,
    )
    with (output_dir / "dataset-summary.json").open("w", encoding="utf-8") as handle:
        json.dump(dataset_summary, handle, indent=2)
    with (output_dir / "metrics.json").open("w", encoding="utf-8") as handle:
        json.dump(metrics, handle, indent=2)
    print(f"saved checkpoint={checkpoint_path}")
    print(f"saved onnx={onnx_path}")
    print(f"saved dataset_summary={output_dir / 'dataset-summary.json'}")


def main() -> None:
    train(parse_args())


if __name__ == "__main__":
    main()
