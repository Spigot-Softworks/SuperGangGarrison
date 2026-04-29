from __future__ import annotations

import argparse
import json
from pathlib import Path
from typing import Any

import numpy as np
import torch
import torch.nn as nn
import torch.nn.functional as F

from mlbot_dataset import FEATURE_COUNT, vectorize_observation
from build_outcome_policy_dataset import iter_outcome_samples


ACTION_FEATURE_COUNT = 4
CONTINUOUS_TARGETS = (
    "DeltaX",
    "DeltaY",
    "DeltaVelocityX",
    "DeltaVelocityY",
    "ObjectiveDistanceDelta",
    "MinObjectiveDistanceDelta",
    "MaxVerticalGain",
    "UpwardLandingProgress",
)
BINARY_TARGETS = (
    "EndGrounded",
    "BecameAirborne",
    "LandedAfterAirborne",
    "HitWall",
    "Success",
    "EndedNearUpwardLanding",
    "ProductiveLanding",
    "LandedHigher",
    "ObjectiveImproved",
    "WastedJump",
    "LocalLoop",
)
DELTA_POSITION_SCALE = 512.0
DELTA_VELOCITY_SCALE = 512.0
OBJECTIVE_DELTA_SCALE = 512.0
VERTICAL_GAIN_SCALE = 256.0


class OutcomePredictor(nn.Module):
    def __init__(self, input_size: int = FEATURE_COUNT + ACTION_FEATURE_COUNT, hidden_size: int = 256) -> None:
        super().__init__()
        self.backbone = nn.Sequential(
            nn.Linear(input_size, hidden_size),
            nn.ReLU(),
            nn.Linear(hidden_size, hidden_size),
            nn.ReLU(),
        )
        self.continuous_head = nn.Linear(hidden_size, len(CONTINUOUS_TARGETS))
        self.binary_head = nn.Linear(hidden_size, len(BINARY_TARGETS))

    def forward(self, inputs: torch.Tensor) -> tuple[torch.Tensor, torch.Tensor]:
        hidden = self.backbone(inputs)
        return self.continuous_head(hidden), self.binary_head(hidden)


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description="Train an auxiliary short-horizon action outcome predictor.")
    parser.add_argument("--data-path", action="append", default=[])
    parser.add_argument("--data-dir", action="append", default=[])
    parser.add_argument("--output-dir", required=True)
    parser.add_argument("--epochs", type=int, default=40)
    parser.add_argument("--batch-size", type=int, default=512)
    parser.add_argument("--learning-rate", type=float, default=3e-4)
    parser.add_argument("--validation-split", type=float, default=0.1)
    parser.add_argument("--hidden-size", type=int, default=256)
    parser.add_argument("--seed", type=int, default=1337)
    parser.add_argument("--cpu", action="store_true")
    return parser.parse_args()


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


def action_features(sample: dict[str, Any]) -> np.ndarray:
    return np.asarray(
        [
            float(sample.get("MoveDirection", 0)),
            1.0 if sample.get("Jump", False) else 0.0,
            1.0 if sample.get("Crouch", False) else 0.0,
            min(1.0, float(sample.get("HorizonTicks", 0)) / 120.0),
        ],
        dtype=np.float32,
    )


def continuous_targets(sample: dict[str, Any]) -> np.ndarray:
    scales = np.asarray(
        [
            DELTA_POSITION_SCALE,
            DELTA_POSITION_SCALE,
            DELTA_VELOCITY_SCALE,
            DELTA_VELOCITY_SCALE,
            OBJECTIVE_DELTA_SCALE,
            OBJECTIVE_DELTA_SCALE,
            VERTICAL_GAIN_SCALE,
            VERTICAL_GAIN_SCALE,
        ],
        dtype=np.float32,
    )
    values = np.asarray([float(sample.get(name, 0.0)) for name in CONTINUOUS_TARGETS], dtype=np.float32)
    return np.clip(values / scales, -4.0, 4.0)


def binary_targets(sample: dict[str, Any]) -> np.ndarray:
    return np.asarray([1.0 if sample.get(name, False) else 0.0 for name in BINARY_TARGETS], dtype=np.float32)


def load_dataset(paths: list[Path]) -> tuple[torch.Tensor, torch.Tensor, torch.Tensor, dict[str, Any]]:
    inputs: list[np.ndarray] = []
    continuous: list[np.ndarray] = []
    binary: list[np.ndarray] = []
    action_counts: dict[str, int] = {}
    file_counts: dict[str, int] = {}

    for path in paths:
        with path.open("r", encoding="utf-8-sig") as handle:
            document = json.load(handle)
        samples = iter_outcome_samples(document, path)
        if not isinstance(samples, list):
            continue
        accepted = 0
        for sample in samples:
            observation = sample.get("StartObservation")
            if not isinstance(observation, dict):
                continue
            inputs.append(np.concatenate([vectorize_observation(observation), action_features(sample)]).astype(np.float32))
            continuous.append(continuous_targets(sample))
            binary.append(binary_targets(sample))
            action_name = str(sample.get("ActionName", "unknown"))
            action_counts[action_name] = action_counts.get(action_name, 0) + 1
            accepted += 1
        if accepted > 0:
            file_counts[str(path)] = accepted

    if not inputs:
        raise ValueError("no outcome samples found")

    summary = {
        "sample_count": len(inputs),
        "file_counts": file_counts,
        "action_counts": action_counts,
        "continuous_targets": list(CONTINUOUS_TARGETS),
        "binary_targets": list(BINARY_TARGETS),
    }
    return (
        torch.tensor(np.stack(inputs), dtype=torch.float32),
        torch.tensor(np.stack(continuous), dtype=torch.float32),
        torch.tensor(np.stack(binary), dtype=torch.float32),
        summary,
    )


def split_indices(sample_count: int, validation_split: float, seed: int) -> tuple[np.ndarray, np.ndarray]:
    rng = np.random.default_rng(seed)
    indices = np.arange(sample_count)
    rng.shuffle(indices)
    val_count = max(1, int(sample_count * max(0.0, min(0.5, validation_split)))) if sample_count > 1 else 0
    return indices[val_count:], indices[:val_count]


def make_batch(
    inputs: torch.Tensor,
    continuous: torch.Tensor,
    binary: torch.Tensor,
    indices: np.ndarray,
    batch_size: int,
) -> tuple[torch.Tensor, torch.Tensor, torch.Tensor]:
    selected = torch.from_numpy(np.random.choice(indices, size=batch_size, replace=len(indices) < batch_size)).long()
    return inputs[selected], continuous[selected], binary[selected]


def evaluate(
    model: OutcomePredictor,
    inputs: torch.Tensor,
    continuous: torch.Tensor,
    binary: torch.Tensor,
    indices: np.ndarray,
) -> dict[str, float]:
    if len(indices) == 0:
        return {
            "continuous_mae": 0.0,
            "binary_accuracy": 0.0,
            "delta_position_mae_px": 0.0,
            "objective_delta_mae_px": 0.0,
        }
    model.eval()
    selected = torch.from_numpy(indices).long().to(inputs.device)
    with torch.no_grad():
        cont_pred, bin_logits = model(inputs[selected])
        cont_target = continuous[selected]
        bin_target = binary[selected]
        cont_mae = torch.abs(cont_pred - cont_target).mean()
        bin_acc = ((bin_logits > 0.0) == (bin_target > 0.5)).float().mean()
        delta_position_mae = torch.abs(cont_pred[:, :2] - cont_target[:, :2]).mean() * DELTA_POSITION_SCALE
        objective_delta_mae = torch.abs(cont_pred[:, 4] - cont_target[:, 4]).mean() * OBJECTIVE_DELTA_SCALE
    return {
        "continuous_mae": float(cont_mae.cpu()),
        "binary_accuracy": float(bin_acc.cpu()),
        "delta_position_mae_px": float(delta_position_mae.cpu()),
        "objective_delta_mae_px": float(objective_delta_mae.cpu()),
    }


def train(args: argparse.Namespace) -> None:
    torch.manual_seed(args.seed)
    np.random.seed(args.seed)
    output_dir = Path(args.output_dir)
    output_dir.mkdir(parents=True, exist_ok=True)

    paths = discover_paths(args)
    inputs, continuous, binary, dataset_summary = load_dataset(paths)
    train_indices, val_indices = split_indices(inputs.shape[0], args.validation_split, args.seed)
    device = torch.device("cuda" if torch.cuda.is_available() and not args.cpu else "cpu")
    inputs = inputs.to(device)
    continuous = continuous.to(device)
    binary = binary.to(device)
    model = OutcomePredictor(hidden_size=args.hidden_size).to(device)
    optimizer = torch.optim.AdamW(model.parameters(), lr=args.learning_rate, weight_decay=1e-4)
    metrics: list[dict[str, Any]] = []

    batches_per_epoch = max(1, int(np.ceil(len(train_indices) / max(1, args.batch_size))))
    for epoch in range(1, args.epochs + 1):
        model.train()
        total_loss = 0.0
        for _ in range(batches_per_epoch):
            batch_inputs, batch_continuous, batch_binary = make_batch(
                inputs,
                continuous,
                binary,
                train_indices,
                args.batch_size,
            )
            optimizer.zero_grad(set_to_none=True)
            cont_pred, bin_logits = model(batch_inputs)
            continuous_loss = F.smooth_l1_loss(cont_pred, batch_continuous)
            binary_loss = F.binary_cross_entropy_with_logits(bin_logits, batch_binary)
            loss = continuous_loss + binary_loss
            loss.backward()
            optimizer.step()
            total_loss += float(loss.detach().cpu())

        train_eval = evaluate(model, inputs, continuous, binary, train_indices)
        val_eval = evaluate(model, inputs, continuous, binary, val_indices)
        metric = {
            "epoch": epoch,
            "loss": total_loss / batches_per_epoch,
            "train": train_eval,
            "validation": val_eval,
        }
        metrics.append(metric)
        print(
            f"epoch={epoch} loss={metric['loss']:.4f} "
            f"val_cont_mae={val_eval['continuous_mae']:.4f} "
            f"val_bin_acc={val_eval['binary_accuracy']:.3f} "
            f"val_delta_pos_mae_px={val_eval['delta_position_mae_px']:.1f} "
            f"val_obj_delta_mae_px={val_eval['objective_delta_mae_px']:.1f}"
        )

    checkpoint_path = output_dir / "outcome_model.pt"
    torch.save(model.state_dict(), checkpoint_path)
    model.eval()
    dummy = torch.zeros(1, FEATURE_COUNT + ACTION_FEATURE_COUNT, dtype=torch.float32, device=device)
    onnx_path = output_dir / "outcome_model.onnx"
    torch.onnx.export(
        model,
        dummy,
        onnx_path,
        input_names=["obs_action"],
        output_names=["continuous", "binary_logits"],
        dynamic_axes={
            "obs_action": {0: "batch"},
            "continuous": {0: "batch"},
            "binary_logits": {0: "batch"},
        },
        opset_version=17,
        dynamo=False,
    )
    with (output_dir / "metrics.json").open("w", encoding="utf-8") as handle:
        json.dump(metrics, handle, indent=2)
    dataset_summary.update(
        {
            "train_sample_count": int(len(train_indices)),
            "validation_sample_count": int(len(val_indices)),
            "input_size": FEATURE_COUNT + ACTION_FEATURE_COUNT,
        }
    )
    with (output_dir / "dataset-summary.json").open("w", encoding="utf-8") as handle:
        json.dump(dataset_summary, handle, indent=2)
    print(f"saved checkpoint={checkpoint_path}")
    print(f"saved onnx={onnx_path}")


if __name__ == "__main__":
    train(parse_args())
