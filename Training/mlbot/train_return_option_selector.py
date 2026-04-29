from __future__ import annotations

import argparse
import json
from dataclasses import asdict, dataclass
from pathlib import Path
from typing import Any

import numpy as np
import torch
import torch.nn as nn
import torch.nn.functional as F

from mlbot_dataset import FEATURE_COUNT, vectorize_observation


class ReturnOptionSelectorModel(nn.Module):
    def __init__(self, option_count: int, input_size: int = FEATURE_COUNT, hidden_size: int = 128) -> None:
        super().__init__()
        self.backbone = nn.Sequential(
            nn.Linear(input_size, hidden_size),
            nn.ReLU(),
            nn.Linear(hidden_size, hidden_size),
            nn.ReLU(),
        )
        self.option_head = nn.Linear(hidden_size, option_count)

    def forward(self, obs: torch.Tensor) -> torch.Tensor:
        return self.option_head(self.backbone(obs))


@dataclass(frozen=True)
class LabelSpec:
    option_index: int
    level_name: str | None
    team: str | None
    class_id: str | None
    engage_distance: float
    min_objective_relative_x: float | None = None
    max_objective_relative_x: float | None = None
    min_objective_relative_y: float | None = None
    max_objective_relative_y: float | None = None


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description="Train a ReturnIntel option selector over base/chunk choices.")
    parser.add_argument("--output-dir", required=True)
    parser.add_argument("--rollout-path", action="append", default=[])
    parser.add_argument("--rollout-dir", action="append", default=[])
    parser.add_argument("--rollout-glob", default="*.json")
    parser.add_argument(
        "--label-spec",
        action="append",
        default=[],
        help=(
            "Selector label as option_index|map|team|class|engage_distance"
            "[|min_rel_x|max_rel_x|min_rel_y|max_rel_y]. Later specs take priority."
        ),
    )
    parser.add_argument("--epochs", type=int, default=40)
    parser.add_argument("--batches-per-epoch", type=int, default=64)
    parser.add_argument("--batch-size", type=int, default=512)
    parser.add_argument("--learning-rate", type=float, default=3e-4)
    parser.add_argument("--hidden-size", type=int, default=128)
    parser.add_argument("--seed", type=int, default=1337)
    parser.add_argument("--return-only", action=argparse.BooleanOptionalAction, default=True)
    parser.add_argument("--success-only", action=argparse.BooleanOptionalAction, default=True)
    parser.add_argument("--balanced-loss", action=argparse.BooleanOptionalAction, default=True)
    parser.add_argument("--cpu", action="store_true")
    return parser.parse_args()


def parse_label_spec(raw_value: str) -> LabelSpec:
    parts = raw_value.split("|")
    if len(parts) < 5:
        raise ValueError("--label-spec must contain at least option_index|map|team|class|engage_distance")
    return LabelSpec(
        option_index=int(parts[0]),
        level_name=none_if_blank(parts[1]),
        team=none_if_blank(parts[2]),
        class_id=none_if_blank(parts[3]),
        engage_distance=max(0.0, float(parts[4])) if parts[4].strip() else 0.0,
        min_objective_relative_x=parse_optional_float(parts, 5),
        max_objective_relative_x=parse_optional_float(parts, 6),
        min_objective_relative_y=parse_optional_float(parts, 7),
        max_objective_relative_y=parse_optional_float(parts, 8),
    )


def none_if_blank(value: str) -> str | None:
    return value.strip() or None


def parse_optional_float(parts: list[str], index: int) -> float | None:
    if index >= len(parts) or not parts[index].strip():
        return None
    return float(parts[index])


def discover_rollout_paths(args: argparse.Namespace) -> list[Path]:
    paths = [Path(raw_path) for raw_path in args.rollout_path if Path(raw_path).is_file()]
    for raw_dir in args.rollout_dir:
        directory = Path(raw_dir)
        if directory.is_dir():
            paths.extend(sorted(directory.rglob(args.rollout_glob)))

    unique: list[Path] = []
    seen: set[Path] = set()
    for path in paths:
        resolved = path.resolve()
        if resolved in seen:
            continue
        seen.add(resolved)
        unique.append(resolved)
    return unique


def can_use_selector(observation: dict[str, Any]) -> bool:
    return (
        observation.get("TaskPhase") == "ReturnIntel"
        and bool(observation.get("IsCarryingIntel", False))
        and bool(observation.get("Objective", {}).get("HasObjective", False))
    )


def label_for_observation(observation: dict[str, Any], label_specs: list[LabelSpec]) -> int:
    if not can_use_selector(observation):
        return 0
    for spec in reversed(label_specs):
        if not matches_spec(observation, spec):
            continue
        return spec.option_index
    return 0


def matches_spec(observation: dict[str, Any], spec: LabelSpec) -> bool:
    objective = observation.get("Objective", {})
    if spec.level_name and str(observation.get("LevelName", "")).casefold() != spec.level_name.casefold():
        return False
    if spec.team and str(observation.get("Team", "")).casefold() != spec.team.casefold():
        return False
    if spec.class_id and str(observation.get("ClassId", "")).casefold() != spec.class_id.casefold():
        return False
    if spec.engage_distance > 0.0 and float(observation.get("ObjectiveDistance", 0.0)) > spec.engage_distance:
        return False
    relative_x = float(objective.get("RelativeX", 0.0))
    relative_y = float(objective.get("RelativeY", 0.0))
    if spec.min_objective_relative_x is not None and relative_x < spec.min_objective_relative_x:
        return False
    if spec.max_objective_relative_x is not None and relative_x > spec.max_objective_relative_x:
        return False
    if spec.min_objective_relative_y is not None and relative_y < spec.min_objective_relative_y:
        return False
    if spec.max_objective_relative_y is not None and relative_y > spec.max_objective_relative_y:
        return False
    return True


def build_dataset(args: argparse.Namespace) -> tuple[torch.Tensor, torch.Tensor, dict[str, Any], int]:
    label_specs = [parse_label_spec(raw_value) for raw_value in args.label_spec]
    if not label_specs:
        raise ValueError("at least one --label-spec is required")
    option_count = max(spec.option_index for spec in label_specs) + 1
    features: list[np.ndarray] = []
    labels: list[int] = []
    source_counts: dict[str, int] = {}

    for path in discover_rollout_paths(args):
        with path.open("r", encoding="utf-8") as handle:
            payload = json.load(handle)
        if not isinstance(payload, dict):
            continue
        if args.success_only and not bool(payload.get("Success", False)):
            continue
        steps = payload.get("Steps", [])
        if not isinstance(steps, list):
            continue
        source_sample_count = 0
        for step in steps:
            observation = step.get("Observation", {})
            if args.return_only and not can_use_selector(observation):
                continue
            label = label_for_observation(observation, label_specs)
            features.append(vectorize_observation(observation).astype(np.float32))
            labels.append(label)
            source_sample_count += 1
        if source_sample_count > 0:
            source_counts[str(path)] = source_sample_count

    if not features:
        raise ValueError("no selector samples were found")

    label_counts = {str(index): int(np.sum(np.asarray(labels) == index)) for index in range(option_count)}
    summary = {
        "sample_count": len(features),
        "option_count": option_count,
        "label_counts": label_counts,
        "source_counts": source_counts,
        "label_specs": [asdict(spec) for spec in label_specs],
    }
    return (
        torch.tensor(np.stack(features), dtype=torch.float32),
        torch.tensor(labels, dtype=torch.long),
        summary,
        option_count,
    )


def sample_batch(features: torch.Tensor, labels: torch.Tensor, batch_size: int) -> tuple[torch.Tensor, torch.Tensor]:
    indices = torch.randint(0, features.shape[0], (batch_size,))
    return features[indices], labels[indices]


def train(args: argparse.Namespace) -> None:
    torch.manual_seed(args.seed)
    np.random.seed(args.seed)
    output_dir = Path(args.output_dir)
    output_dir.mkdir(parents=True, exist_ok=True)
    device = torch.device("cuda" if torch.cuda.is_available() and not args.cpu else "cpu")

    features, labels, dataset_summary, option_count = build_dataset(args)
    features = features.to(device)
    labels = labels.to(device)
    model = ReturnOptionSelectorModel(option_count=option_count, hidden_size=args.hidden_size).to(device)
    optimizer = torch.optim.AdamW(model.parameters(), lr=args.learning_rate, weight_decay=1e-4)

    class_weights = None
    if args.balanced_loss:
        counts = torch.bincount(labels, minlength=option_count).float().clamp_min(1.0)
        class_weights = (counts.sum() / (counts * option_count)).to(device)
        class_weights = class_weights / class_weights.mean().clamp_min(1e-6)
        dataset_summary["class_weights"] = [float(value) for value in class_weights.detach().cpu()]

    metrics: list[dict[str, Any]] = []
    for epoch in range(1, args.epochs + 1):
        model.train()
        total_loss = 0.0
        for _ in range(max(1, args.batches_per_epoch)):
            batch_features, batch_labels = sample_batch(features, labels, args.batch_size)
            optimizer.zero_grad(set_to_none=True)
            logits = model(batch_features)
            loss = F.cross_entropy(logits, batch_labels, weight=class_weights)
            loss.backward()
            optimizer.step()
            total_loss += float(loss.detach().cpu())

        with torch.no_grad():
            logits = model(features)
            predictions = logits.argmax(dim=1)
            accuracy = float((predictions == labels).float().mean().detach().cpu())
            per_option_accuracy = {}
            for option_index in range(option_count):
                mask = labels == option_index
                per_option_accuracy[str(option_index)] = (
                    float((predictions[mask] == labels[mask]).float().mean().detach().cpu())
                    if bool(mask.any())
                    else 0.0
                )
        metric = {
            "epoch": epoch,
            "loss": total_loss / max(1, args.batches_per_epoch),
            "accuracy": accuracy,
            "per_option_accuracy": per_option_accuracy,
        }
        metrics.append(metric)
        print(f"epoch={epoch} loss={metric['loss']:.4f} accuracy={accuracy:.3f} per_option={per_option_accuracy}")

    checkpoint_path = output_dir / "selector_model.pt"
    onnx_path = output_dir / "selector_model.onnx"
    torch.save(model.state_dict(), checkpoint_path)
    export_onnx(model, onnx_path)
    with (output_dir / "metrics.json").open("w", encoding="utf-8") as handle:
        json.dump(metrics, handle, indent=2)
    with (output_dir / "dataset-summary.json").open("w", encoding="utf-8") as handle:
        json.dump(dataset_summary, handle, indent=2)
    print(f"saved checkpoint={checkpoint_path}")
    print(f"saved onnx={onnx_path}")


def export_onnx(model: ReturnOptionSelectorModel, onnx_path: Path) -> None:
    model.eval()
    dummy_input = torch.zeros(1, FEATURE_COUNT, dtype=torch.float32)
    torch.onnx.export(
        model,
        dummy_input,
        onnx_path,
        input_names=["obs"],
        output_names=["option_logits"],
        dynamic_axes={
            "obs": {0: "batch"},
            "option_logits": {0: "batch"},
        },
        opset_version=17,
        dynamo=False,
    )


if __name__ == "__main__":
    train(parse_args())
