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

from mlbot_dataset import DISTANCE_SCALE, FEATURE_COUNT, vectorize_observation


BINARY_ACTIONS = ("Jump", "Crouch", "FirePrimary", "FireSecondary", "DropIntel")


class ReturnHierarchicalChunkModel(nn.Module):
    def __init__(
        self,
        option_count: int,
        horizon: int,
        input_size: int = FEATURE_COUNT,
        hidden_size: int = 256,
        separate_option_backbones: bool = False,
    ) -> None:
        super().__init__()
        self.option_count = option_count
        self.horizon = horizon
        self.separate_option_backbones = separate_option_backbones
        self.selector_backbone = nn.Sequential(
            nn.Linear(input_size, hidden_size),
            nn.ReLU(),
            nn.Linear(hidden_size, hidden_size),
            nn.ReLU(),
        )
        self.option_head = nn.Linear(hidden_size, option_count + 1)
        if separate_option_backbones:
            self.option_backbones = nn.ModuleList(
                [
                    nn.Sequential(
                        nn.Linear(input_size, hidden_size),
                        nn.ReLU(),
                        nn.Linear(hidden_size, hidden_size),
                        nn.ReLU(),
                    )
                    for _ in range(option_count)
                ]
            )
            self.move_heads = nn.ModuleList([nn.Linear(hidden_size, horizon * 3) for _ in range(option_count)])
            self.binary_heads = nn.ModuleList([nn.Linear(hidden_size, horizon * 5) for _ in range(option_count)])
            self.aim_heads = nn.ModuleList([nn.Linear(hidden_size, horizon * 2) for _ in range(option_count)])
        else:
            self.chunk_backbone = nn.Sequential(
                nn.Linear(input_size, hidden_size),
                nn.ReLU(),
                nn.Linear(hidden_size, hidden_size),
                nn.ReLU(),
            )
            self.move_head = nn.Linear(hidden_size, option_count * horizon * 3)
            self.binary_head = nn.Linear(hidden_size, option_count * horizon * 5)
            self.aim_head = nn.Linear(hidden_size, option_count * horizon * 2)

    def forward(self, obs: torch.Tensor) -> tuple[torch.Tensor, torch.Tensor, torch.Tensor, torch.Tensor]:
        selector_hidden = self.selector_backbone(obs)
        batch = obs.shape[0]
        option_logits = self.option_head(selector_hidden)
        if self.separate_option_backbones:
            move_outputs = []
            binary_outputs = []
            aim_outputs = []
            for option_index in range(self.option_count):
                hidden = self.option_backbones[option_index](obs)
                move_outputs.append(self.move_heads[option_index](hidden).reshape(batch, self.horizon, 3))
                binary_outputs.append(self.binary_heads[option_index](hidden).reshape(batch, self.horizon, 5))
                aim_outputs.append(torch.tanh(self.aim_heads[option_index](hidden).reshape(batch, self.horizon, 2)))
            move_logits = torch.stack(move_outputs, dim=1)
            binary_logits = torch.stack(binary_outputs, dim=1)
            aim = torch.stack(aim_outputs, dim=1)
        else:
            hidden = self.chunk_backbone(obs)
            move_logits = self.move_head(hidden).reshape(batch, self.option_count, self.horizon, 3)
            binary_logits = self.binary_head(hidden).reshape(batch, self.option_count, self.horizon, 5)
            aim = torch.tanh(self.aim_head(hidden).reshape(batch, self.option_count, self.horizon, 2))
        return option_logits, move_logits, binary_logits, aim


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
    parser = argparse.ArgumentParser(description="Train one ReturnIntel selector plus option-specific chunk heads.")
    parser.add_argument("--output-dir", required=True)
    parser.add_argument("--rollout-path", action="append", default=[])
    parser.add_argument("--rollout-dir", action="append", default=[])
    parser.add_argument("--rollout-glob", default="*.json")
    parser.add_argument(
        "--option-only-rollout-path",
        action="append",
        default=[],
        help="Use these rollout states for option labels, but do not train chunk actions from them.",
    )
    parser.add_argument(
        "--label-spec",
        action="append",
        default=[],
        help=(
            "Option label as option_index|map|team|class|engage_distance"
            "[|min_rel_x|max_rel_x|min_rel_y|max_rel_y]. Later specs take priority."
        ),
    )
    parser.add_argument("--horizon", type=int, default=8)
    parser.add_argument("--epochs", type=int, default=64)
    parser.add_argument("--batches-per-epoch", type=int, default=80)
    parser.add_argument("--batch-size", type=int, default=512)
    parser.add_argument("--learning-rate", type=float, default=2e-4)
    parser.add_argument("--hidden-size", type=int, default=256)
    parser.add_argument("--separate-option-backbones", action="store_true")
    parser.add_argument("--seed", type=int, default=1337)
    parser.add_argument("--success-only", action=argparse.BooleanOptionalAction, default=True)
    parser.add_argument("--return-only", action=argparse.BooleanOptionalAction, default=True)
    parser.add_argument("--balanced-option-loss", action=argparse.BooleanOptionalAction, default=True)
    parser.add_argument("--option-loss-coef", type=float, default=1.0)
    parser.add_argument("--chunk-loss-coef", type=float, default=1.0)
    parser.add_argument(
        "--option-sample-weight",
        action="append",
        default=[],
        help="Multiply chunk sample weight for an option, formatted option_index=weight.",
    )
    parser.add_argument("--jump-sample-weight", type=float, default=1.0)
    parser.add_argument("--stuck-sample-weight", type=float, default=1.0)
    parser.add_argument("--stuck-ticks-threshold", type=float, default=0.0)
    parser.add_argument("--jump-positive-weight", type=float, default=1.0)
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
        engage_distance=float(parts[4]),
        min_objective_relative_x=parse_optional_float(parts, 5),
        max_objective_relative_x=parse_optional_float(parts, 6),
        min_objective_relative_y=parse_optional_float(parts, 7),
        max_objective_relative_y=parse_optional_float(parts, 8),
    )


def none_if_blank(value: str) -> str | None:
    return None if not value.strip() else value.strip()


def parse_optional_float(parts: list[str], index: int) -> float | None:
    if index >= len(parts) or not parts[index].strip():
        return None
    return float(parts[index])


def discover_rollout_paths(args: argparse.Namespace) -> list[Path]:
    paths = [Path(raw_path) for raw_path in args.rollout_path if Path(raw_path).is_file()]
    paths.extend(Path(raw_path) for raw_path in args.option_only_rollout_path if Path(raw_path).is_file())
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


def load_rollout(path: Path) -> dict[str, Any] | None:
    with path.open("r", encoding="utf-8") as handle:
        payload = json.load(handle)
    if not isinstance(payload, dict):
        return None
    steps = payload.get("Steps")
    if not isinstance(steps, list) or not steps:
        return None
    return payload


def observation_string(observation: dict[str, Any], key: str) -> str:
    value = observation.get(key, "")
    return str(value)


def option_label(observation: dict[str, Any], specs: list[LabelSpec]) -> int:
    if observation.get("TaskPhase") != "ReturnIntel" or not observation.get("IsCarryingIntel", False):
        return 0
    if not observation.get("Objective", {}).get("HasObjective", False):
        return 0
    distance = float(observation.get("ObjectiveDistance", 0.0))
    objective = observation.get("Objective", {})
    relative_x = float(objective.get("RelativeX", 0.0))
    relative_y = float(objective.get("RelativeY", 0.0))
    label = 0
    for spec in specs:
        if spec.level_name is not None and observation_string(observation, "LevelName").lower() != spec.level_name.lower():
            continue
        if spec.team is not None and observation_string(observation, "Team").lower() != spec.team.lower():
            continue
        if spec.class_id is not None and observation_string(observation, "ClassId").lower() != spec.class_id.lower():
            continue
        if spec.engage_distance > 0.0 and distance > spec.engage_distance:
            continue
        if spec.min_objective_relative_x is not None and relative_x < spec.min_objective_relative_x:
            continue
        if spec.max_objective_relative_x is not None and relative_x > spec.max_objective_relative_x:
            continue
        if spec.min_objective_relative_y is not None and relative_y < spec.min_objective_relative_y:
            continue
        if spec.max_objective_relative_y is not None and relative_y > spec.max_objective_relative_y:
            continue
        label = spec.option_index
    return label


def move_direction_to_index(move_direction: int) -> int:
    return max(0, min(2, int(move_direction) + 1))


def action_binary_vector(action: dict[str, Any]) -> np.ndarray:
    return np.asarray([1.0 if action.get(name, False) else 0.0 for name in BINARY_ACTIONS], dtype=np.float32)


def action_aim_target(source_observation: dict[str, Any], action: dict[str, Any]) -> np.ndarray:
    bot_x = float(source_observation.get("BotX", 0.0))
    bot_y = float(source_observation.get("BotY", 0.0))
    aim_x = float(action.get("AimWorldX", bot_x))
    aim_y = float(action.get("AimWorldY", bot_y))
    return np.asarray(
        [
            np.clip((aim_x - bot_x) / DISTANCE_SCALE, -1.0, 1.0),
            np.clip((aim_y - bot_y) / DISTANCE_SCALE, -1.0, 1.0),
        ],
        dtype=np.float32,
    )


def build_dataset(
    args: argparse.Namespace,
    specs: list[LabelSpec],
) -> tuple[torch.Tensor, torch.Tensor, torch.Tensor, torch.Tensor, torch.Tensor, torch.Tensor, torch.Tensor, dict[str, Any]]:
    features: list[np.ndarray] = []
    option_targets: list[int] = []
    move_targets: list[np.ndarray] = []
    binary_targets: list[np.ndarray] = []
    aim_targets: list[np.ndarray] = []
    sample_weights: list[float] = []
    chunk_weights: list[float] = []
    source_counts: dict[str, int] = {}
    option_only_paths = {Path(raw_path).resolve() for raw_path in args.option_only_rollout_path if Path(raw_path).is_file()}
    option_sample_weights = parse_option_sample_weights(args.option_sample_weight)

    for path in discover_rollout_paths(args):
        rollout = load_rollout(path)
        if rollout is None:
            continue
        if args.success_only and not bool(rollout.get("Success", False)):
            continue
        steps = rollout["Steps"]
        source_sample_count = 0
        option_only = path.resolve() in option_only_paths
        for index, step in enumerate(steps):
            observation = step.get("Observation", {})
            if args.return_only and observation.get("TaskPhase") != "ReturnIntel":
                continue
            label = option_label(observation, specs)
            feature = vectorize_observation(observation).astype(np.float32)
            chunk_moves = np.zeros(args.horizon, dtype=np.int64)
            chunk_binary = np.zeros((args.horizon, 5), dtype=np.float32)
            chunk_aim = np.zeros((args.horizon, 2), dtype=np.float32)
            for offset in range(args.horizon):
                action_index = min(index + offset, len(steps) - 1)
                action = steps[action_index].get("Action", {})
                chunk_moves[offset] = move_direction_to_index(int(action.get("MoveDirection", 0)))
                chunk_binary[offset, :] = action_binary_vector(action)
                chunk_aim[offset, :] = action_aim_target(observation, action)
            weight = 1.0
            if label > 0 and args.jump_sample_weight > 1.0 and np.any(chunk_binary[:, 0] > 0.5):
                weight *= args.jump_sample_weight
            if (
                label > 0
                and args.stuck_sample_weight > 1.0
                and args.stuck_ticks_threshold > 0.0
                and float(observation.get("StuckTicks", 0.0)) >= args.stuck_ticks_threshold
            ):
                weight *= args.stuck_sample_weight
            if label in option_sample_weights:
                weight *= option_sample_weights[label]
            features.append(feature)
            option_targets.append(label)
            move_targets.append(chunk_moves)
            binary_targets.append(chunk_binary)
            aim_targets.append(chunk_aim)
            sample_weights.append(weight)
            chunk_weights.append(0.0 if option_only else weight)
            source_sample_count += 1
        if source_sample_count > 0:
            source_counts[str(path)] = source_sample_count

    if not features:
        raise ValueError("no hierarchical chunk samples were found")
    label_counts = {str(index): int(np.sum(np.asarray(option_targets) == index)) for index in range(max(option_targets) + 1)}
    summary = {
        "sample_count": len(features),
        "source_counts": source_counts,
        "label_counts": label_counts,
        "horizon": args.horizon,
        "label_specs": [asdict(spec) for spec in specs],
        "sample_weight_mean": float(np.mean(sample_weights)),
        "sample_weight_max": float(np.max(sample_weights)),
        "option_only_rollouts": [str(path) for path in sorted(option_only_paths)],
        "option_sample_weights": {str(key): value for key, value in sorted(option_sample_weights.items())},
    }
    return (
        torch.tensor(np.stack(features), dtype=torch.float32),
        torch.tensor(np.asarray(option_targets), dtype=torch.long),
        torch.tensor(np.stack(move_targets), dtype=torch.long),
        torch.tensor(np.stack(binary_targets), dtype=torch.float32),
        torch.tensor(np.stack(aim_targets), dtype=torch.float32),
        torch.tensor(sample_weights, dtype=torch.float32),
        torch.tensor(chunk_weights, dtype=torch.float32),
        summary,
    )


def parse_option_sample_weights(raw_values: list[str]) -> dict[int, float]:
    weights: dict[int, float] = {}
    for raw_value in raw_values:
        key, separator, value = raw_value.partition("=")
        if not separator:
            raise ValueError("--option-sample-weight must be formatted as option_index=weight")
        weights[int(key)] = max(0.0, float(value))
    return weights


def sample_batch(dataset_size: int, batch_size: int) -> torch.Tensor:
    return torch.randint(0, dataset_size, (batch_size,))


def weighted_mean(values: torch.Tensor, weights: torch.Tensor) -> torch.Tensor:
    while weights.ndim < values.ndim:
        weights = weights.unsqueeze(-1)
    return (values * weights).sum() / weights.sum().clamp_min(1e-6)


def train(args: argparse.Namespace) -> None:
    specs = [parse_label_spec(raw_value) for raw_value in args.label_spec]
    if not specs:
        raise ValueError("at least one --label-spec is required")
    option_count = max(spec.option_index for spec in specs)
    torch.manual_seed(args.seed)
    np.random.seed(args.seed)
    output_dir = Path(args.output_dir)
    output_dir.mkdir(parents=True, exist_ok=True)
    device = torch.device("cuda" if torch.cuda.is_available() and not args.cpu else "cpu")
    features, options, moves, binary, aim, weights, chunk_weights, dataset_summary = build_dataset(args, specs)
    features = features.to(device)
    options = options.to(device)
    moves = moves.to(device)
    binary = binary.to(device)
    aim = aim.to(device)
    weights = weights.to(device)
    chunk_weights = chunk_weights.to(device)

    model = ReturnHierarchicalChunkModel(
        option_count,
        args.horizon,
        hidden_size=args.hidden_size,
        separate_option_backbones=args.separate_option_backbones,
    ).to(device)
    optimizer = torch.optim.AdamW(model.parameters(), lr=args.learning_rate, weight_decay=1e-4)
    option_class_weights = None
    if args.balanced_option_loss:
        counts = torch.bincount(options, minlength=option_count + 1).float().to(device).clamp_min(1.0)
        option_class_weights = counts.sum() / (counts * float(option_count + 1))
    binary_positive_weights = torch.ones(5, dtype=torch.float32, device=device)
    binary_positive_weights[0] = max(1.0, float(args.jump_positive_weight))
    metrics: list[dict[str, Any]] = []

    for epoch in range(1, args.epochs + 1):
        model.train()
        total_loss = 0.0
        total_option_loss = 0.0
        total_chunk_loss = 0.0
        for _ in range(max(1, args.batches_per_epoch)):
            indices = sample_batch(features.shape[0], args.batch_size).to(device)
            batch_features = features[indices]
            batch_options = options[indices]
            batch_moves = moves[indices]
            batch_binary = binary[indices]
            batch_aim = aim[indices]
            batch_weights = weights[indices]
            batch_chunk_weights = chunk_weights[indices]
            optimizer.zero_grad(set_to_none=True)
            option_logits, move_logits, binary_logits, aim_pred = model(batch_features)
            option_loss = F.cross_entropy(option_logits, batch_options, weight=option_class_weights)
            chunk_mask = (batch_options > 0) & (batch_chunk_weights > 0.0)
            if torch.any(chunk_mask):
                option_indices = (batch_options[chunk_mask] - 1).clamp_min(0)
                row_indices = torch.arange(option_indices.shape[0], device=device)
                selected_moves = move_logits[chunk_mask][row_indices, option_indices]
                selected_binary = binary_logits[chunk_mask][row_indices, option_indices]
                selected_aim = aim_pred[chunk_mask][row_indices, option_indices]
                selected_weights = batch_chunk_weights[chunk_mask]
                move_loss_by_frame = F.cross_entropy(
                    selected_moves.reshape(-1, 3),
                    batch_moves[chunk_mask].reshape(-1),
                    reduction="none",
                ).reshape(selected_moves.shape[:2])
                move_loss = weighted_mean(move_loss_by_frame.mean(dim=1), selected_weights)
                binary_loss_by_channel = F.binary_cross_entropy_with_logits(
                    selected_binary,
                    batch_binary[chunk_mask],
                    reduction="none",
                )
                binary_loss_by_channel = torch.where(
                    batch_binary[chunk_mask] > 0.5,
                    binary_loss_by_channel * binary_positive_weights.view(1, 1, -1),
                    binary_loss_by_channel,
                )
                binary_loss = weighted_mean(binary_loss_by_channel.mean(dim=(1, 2)), selected_weights)
                aim_loss = weighted_mean(F.l1_loss(selected_aim, batch_aim[chunk_mask], reduction="none").mean(dim=(1, 2)), selected_weights)
                chunk_loss = move_loss + binary_loss + aim_loss
            else:
                chunk_loss = option_logits.sum() * 0.0
            loss = (args.option_loss_coef * option_loss) + (args.chunk_loss_coef * chunk_loss)
            loss.backward()
            optimizer.step()
            total_loss += float(loss.detach().cpu())
            total_option_loss += float(option_loss.detach().cpu())
            total_chunk_loss += float(chunk_loss.detach().cpu())

        denominator = max(1, args.batches_per_epoch)
        model.eval()
        with torch.no_grad():
            option_logits, move_logits, binary_logits, aim_pred = model(features)
            option_accuracy = float((option_logits.argmax(dim=1) == options).float().mean().detach().cpu())
            chunk_mask = (options > 0) & (chunk_weights > 0.0)
            if torch.any(chunk_mask):
                option_indices = (options[chunk_mask] - 1).clamp_min(0)
                row_indices = torch.arange(option_indices.shape[0], device=device)
                selected_moves = move_logits[chunk_mask][row_indices, option_indices]
                selected_binary = binary_logits[chunk_mask][row_indices, option_indices]
                selected_aim = aim_pred[chunk_mask][row_indices, option_indices]
                move_accuracy = float((selected_moves.argmax(dim=2) == moves[chunk_mask]).float().mean().detach().cpu())
                binary_accuracy = float(((selected_binary > 0.0) == (binary[chunk_mask] > 0.5)).float().mean().detach().cpu())
                aim_mae = float(torch.abs(selected_aim - aim[chunk_mask]).mean().detach().cpu())
            else:
                move_accuracy = 0.0
                binary_accuracy = 0.0
                aim_mae = 0.0
        metric = {
            "epoch": epoch,
            "loss": total_loss / denominator,
            "option_loss": total_option_loss / denominator,
            "chunk_loss": total_chunk_loss / denominator,
            "option_accuracy": option_accuracy,
            "move_accuracy": move_accuracy,
            "binary_accuracy": binary_accuracy,
            "aim_mae": aim_mae,
        }
        metrics.append(metric)
        print(
            f"epoch={epoch} loss={metric['loss']:.4f} option_acc={option_accuracy:.3f} "
            f"move_acc={move_accuracy:.3f} binary_acc={binary_accuracy:.3f} aim_mae={aim_mae:.4f}"
        )

    checkpoint_path = output_dir / "hierarchical_chunk_model.pt"
    onnx_path = output_dir / "hierarchical_chunk_model.onnx"
    torch.save(model.state_dict(), checkpoint_path)
    export_onnx(model, onnx_path, option_count, args.horizon)
    with (output_dir / "metrics.json").open("w", encoding="utf-8") as handle:
        json.dump(metrics, handle, indent=2)
    with (output_dir / "dataset-summary.json").open("w", encoding="utf-8") as handle:
        json.dump(dataset_summary, handle, indent=2)
    print(f"saved checkpoint={checkpoint_path}")
    print(f"saved onnx={onnx_path}")


def export_onnx(model: ReturnHierarchicalChunkModel, onnx_path: Path, option_count: int, horizon: int) -> None:
    model.eval()
    dummy_input = torch.zeros(1, FEATURE_COUNT, dtype=torch.float32)
    torch.onnx.export(
        model,
        dummy_input,
        onnx_path,
        input_names=["obs"],
        output_names=["option_logits", "option_chunk_move_logits", "option_chunk_binary_logits", "option_chunk_aim"],
        dynamic_axes={
            "obs": {0: "batch"},
            "option_logits": {0: "batch"},
            "option_chunk_move_logits": {0: "batch"},
            "option_chunk_binary_logits": {0: "batch"},
            "option_chunk_aim": {0: "batch"},
        },
        opset_version=17,
        dynamo=False,
    )
    metadata = {"option_count": option_count, "horizon": horizon}
    with onnx_path.with_suffix(".metadata.json").open("w", encoding="utf-8") as handle:
        json.dump(metadata, handle, indent=2)


if __name__ == "__main__":
    train(parse_args())
