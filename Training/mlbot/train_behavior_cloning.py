from __future__ import annotations

import argparse
import json
import random
import subprocess
from dataclasses import asdict, dataclass
from pathlib import Path
from typing import Any

import numpy as np
import torch
import torch.nn as nn
import torch.nn.functional as F
from torch.utils.data import DataLoader, TensorDataset

from mlbot_dataset import (
    FEATURE_COUNT,
    DatasetBatch,
    build_dataset_filter,
    parse_csv_filter,
    load_behavior_cloning_dataset,
)
from train_outcome_predictor import (
    ACTION_FEATURE_COUNT,
    BINARY_TARGETS as OUTCOME_BINARY_TARGETS,
    CONTINUOUS_TARGETS as OUTCOME_CONTINUOUS_TARGETS,
    discover_paths as discover_outcome_paths,
    load_dataset as load_outcome_dataset,
)


class BehaviorCloningModel(nn.Module):
    def __init__(self, input_size: int = FEATURE_COUNT, hidden_size: int = 256) -> None:
        super().__init__()
        self.backbone = nn.Sequential(
            nn.Linear(input_size, hidden_size),
            nn.ReLU(),
            nn.Linear(hidden_size, hidden_size),
            nn.ReLU(),
        )
        self.move_head = nn.Linear(hidden_size, 3)
        self.binary_head = nn.Linear(hidden_size, 5)
        self.aim_head = nn.Linear(hidden_size, 2)

    def forward(self, obs: torch.Tensor) -> tuple[torch.Tensor, torch.Tensor, torch.Tensor]:
        hidden = self.backbone(obs)
        return self.move_head(hidden), self.binary_head(hidden), torch.tanh(self.aim_head(hidden))


class TaskConditionedBehaviorCloningModel(nn.Module):
    TaskFeatureStartIndex = 8
    TaskHeadCount = 4

    def __init__(self, input_size: int = FEATURE_COUNT, hidden_size: int = 256) -> None:
        super().__init__()
        self.backbone = nn.Sequential(
            nn.Linear(input_size, hidden_size),
            nn.ReLU(),
            nn.Linear(hidden_size, hidden_size),
            nn.ReLU(),
        )
        self.move_heads = nn.ModuleList(nn.Linear(hidden_size, 3) for _ in range(self.TaskHeadCount))
        self.binary_heads = nn.ModuleList(nn.Linear(hidden_size, 5) for _ in range(self.TaskHeadCount))
        self.aim_heads = nn.ModuleList(nn.Linear(hidden_size, 2) for _ in range(self.TaskHeadCount))

    def forward(self, obs: torch.Tensor) -> tuple[torch.Tensor, torch.Tensor, torch.Tensor]:
        hidden = self.backbone(obs)
        task_weights = obs[:, self.TaskFeatureStartIndex : self.TaskFeatureStartIndex + self.TaskHeadCount].clamp(0.0, 1.0)
        task_weight_sum = task_weights.sum(dim=1, keepdim=True).clamp_min(1.0)
        task_weights = task_weights / task_weight_sum

        move_logits_by_task = torch.stack([head(hidden) for head in self.move_heads], dim=1)
        binary_logits_by_task = torch.stack([head(hidden) for head in self.binary_heads], dim=1)
        aim_by_task = torch.stack([torch.tanh(head(hidden)) for head in self.aim_heads], dim=1)
        move_logits = (move_logits_by_task * task_weights.unsqueeze(-1)).sum(dim=1)
        binary_logits = (binary_logits_by_task * task_weights.unsqueeze(-1)).sum(dim=1)
        aim = (aim_by_task * task_weights.unsqueeze(-1)).sum(dim=1)
        return move_logits, binary_logits, aim


class TaskConditionedMlpHeadBehaviorCloningModel(nn.Module):
    TaskFeatureStartIndex = TaskConditionedBehaviorCloningModel.TaskFeatureStartIndex
    TaskHeadCount = TaskConditionedBehaviorCloningModel.TaskHeadCount

    def __init__(self, input_size: int = FEATURE_COUNT, hidden_size: int = 256) -> None:
        super().__init__()
        self.backbone = nn.Sequential(
            nn.Linear(input_size, hidden_size),
            nn.ReLU(),
            nn.Linear(hidden_size, hidden_size),
            nn.ReLU(),
        )
        self.move_heads = nn.ModuleList(self._build_head(hidden_size, 3) for _ in range(self.TaskHeadCount))
        self.binary_heads = nn.ModuleList(self._build_head(hidden_size, 5) for _ in range(self.TaskHeadCount))
        self.aim_heads = nn.ModuleList(self._build_head(hidden_size, 2) for _ in range(self.TaskHeadCount))

    @staticmethod
    def _build_head(hidden_size: int, output_size: int) -> nn.Sequential:
        return nn.Sequential(
            nn.Linear(hidden_size, hidden_size),
            nn.ReLU(),
            nn.Linear(hidden_size, output_size),
        )

    def forward(self, obs: torch.Tensor) -> tuple[torch.Tensor, torch.Tensor, torch.Tensor]:
        hidden = self.backbone(obs)
        task_weights = obs[:, self.TaskFeatureStartIndex : self.TaskFeatureStartIndex + self.TaskHeadCount].clamp(0.0, 1.0)
        task_weight_sum = task_weights.sum(dim=1, keepdim=True).clamp_min(1.0)
        task_weights = task_weights / task_weight_sum

        move_logits_by_task = torch.stack([head(hidden) for head in self.move_heads], dim=1)
        binary_logits_by_task = torch.stack([head(hidden) for head in self.binary_heads], dim=1)
        aim_by_task = torch.stack([torch.tanh(head(hidden)) for head in self.aim_heads], dim=1)
        move_logits = (move_logits_by_task * task_weights.unsqueeze(-1)).sum(dim=1)
        binary_logits = (binary_logits_by_task * task_weights.unsqueeze(-1)).sum(dim=1)
        aim = (aim_by_task * task_weights.unsqueeze(-1)).sum(dim=1)
        return move_logits, binary_logits, aim


class OutcomeAuxiliaryHeads(nn.Module):
    def __init__(self, hidden_size: int = 256) -> None:
        super().__init__()
        self.net = nn.Sequential(
            nn.Linear(hidden_size + ACTION_FEATURE_COUNT, hidden_size),
            nn.ReLU(),
            nn.Linear(hidden_size, hidden_size),
            nn.ReLU(),
        )
        self.continuous_head = nn.Linear(hidden_size, len(OUTCOME_CONTINUOUS_TARGETS))
        self.binary_head = nn.Linear(hidden_size, len(OUTCOME_BINARY_TARGETS))

    def forward(self, hidden: torch.Tensor, action_features: torch.Tensor) -> tuple[torch.Tensor, torch.Tensor]:
        features = torch.cat([hidden, action_features], dim=1)
        auxiliary = self.net(features)
        return self.continuous_head(auxiliary), self.binary_head(auxiliary)


@dataclass
class TrainingMetrics:
    epoch: int
    train_loss: float
    val_loss: float
    move_accuracy: float
    binary_accuracy: float
    aim_mae: float


@dataclass
class RolloutSelectionMetrics:
    success: bool
    ticks_elapsed: int
    total_reward: float
    terminal_reason: str
    pickup_tick: int | None
    score_tick: int | None
    max_stuck_ticks: float
    min_objective_distance: float
    final_objective_distance: float
    final_phase: str
    likely_issue: str = ""
    stall_tick: int | None = None
    stall_window_zero_velocity_ticks: int = 0
    stall_window_jump_count: int = 0
    stall_window_idle_count: int = 0

    @property
    def score_key(self) -> tuple[float, ...]:
        idle_stall_penalty = 1.0 if self.likely_issue == "idle_after_progress_stall" else 0.0
        low_obstacle_penalty = 1.0 if self.likely_issue == "blocked_at_low_obstacle_without_jump" else 0.0
        wall_loop_penalty = 1.0 if self.likely_issue == "wall_jump_loop_without_progress" else 0.0
        local_loop_penalty = 1.0 if self.likely_issue == "local_loop_without_progress" else 0.0
        return (
            1.0 if self.success else 0.0,
            1.0 if self.score_tick is not None else 0.0,
            1.0 if self.pickup_tick is not None else 0.0,
            -idle_stall_penalty,
            -low_obstacle_penalty,
            -wall_loop_penalty,
            -local_loop_penalty,
            -float(self.min_objective_distance),
            -float(self.final_objective_distance),
            -float(self.max_stuck_ticks),
            -float(self.stall_window_zero_velocity_ticks),
            float(self.stall_window_jump_count),
            -float(self.stall_window_idle_count),
            float(self.total_reward),
            -float(self.ticks_elapsed),
        )


def evaluate(model: BehaviorCloningModel, data_loader: DataLoader, device: torch.device) -> tuple[float, float, float, float]:
    model.eval()
    ce_loss = nn.CrossEntropyLoss()
    bce_loss = nn.BCEWithLogitsLoss()
    l1_loss = nn.L1Loss()

    total_loss = 0.0
    total_examples = 0
    total_move_correct = 0
    total_binary_correct = 0
    total_binary_count = 0
    total_aim_error = 0.0

    with torch.no_grad():
        for obs, move_target, binary_target, aim_target in data_loader:
            obs = obs.to(device)
            move_target = move_target.to(device)
            binary_target = binary_target.to(device)
            aim_target = aim_target.to(device)

            move_logits, binary_logits, aim = model(obs)
            loss = (
                ce_loss(move_logits, move_target)
                + bce_loss(binary_logits, binary_target)
                + l1_loss(aim, aim_target)
            )

            batch_size = obs.shape[0]
            total_loss += loss.item() * batch_size
            total_examples += batch_size

            total_move_correct += (move_logits.argmax(dim=1) == move_target).sum().item()
            binary_predictions = (binary_logits > 0).float()
            total_binary_correct += (binary_predictions == binary_target).sum().item()
            total_binary_count += binary_target.numel()
            total_aim_error += torch.abs(aim - aim_target).sum().item()

    return (
        total_loss / max(1, total_examples),
        total_move_correct / max(1, total_examples),
        total_binary_correct / max(1, total_binary_count),
        total_aim_error / max(1, total_examples * 2),
    )


def build_move_class_weights(move_targets: torch.Tensor, train_indices: np.ndarray, device: torch.device) -> torch.Tensor:
    selected_targets = move_targets[torch.from_numpy(train_indices)]
    counts = torch.bincount(selected_targets, minlength=3).float().clamp_min(1.0)
    weights = counts.sum() / (counts * counts.numel())
    weights = weights / weights.mean().clamp_min(1e-6)
    return weights.to(device)


def build_binary_positive_weights(
    binary_targets: torch.Tensor,
    train_indices: np.ndarray,
    device: torch.device,
    maximum_weight: float,
) -> torch.Tensor:
    selected_targets = binary_targets[torch.from_numpy(train_indices)].float()
    positives = selected_targets.sum(dim=0).clamp_min(1.0)
    negatives = (selected_targets.shape[0] - positives).clamp_min(1.0)
    weights = negatives / positives
    weights = torch.clamp(weights, min=1.0, max=max(1.0, maximum_weight))
    return weights.to(device)


def parse_binary_positive_weights(value: str, device: torch.device) -> torch.Tensor | None:
    if not value.strip():
        return None

    pieces = [piece.strip() for piece in value.split(",")]
    if len(pieces) != 5:
        raise ValueError("--binary-positive-weights must contain five comma-separated values")

    return torch.tensor([max(1.0, float(piece)) for piece in pieces], dtype=torch.float32, device=device)


def merge_counts(batches: list[DatasetBatch], field_name: str) -> dict[str, int]:
    merged: dict[str, int] = {}
    for batch in batches:
        for key, value in getattr(batch, field_name).items():
            merged[key] = merged.get(key, 0) + int(value)
    return merged


def merge_dataset_batches(batches: list[DatasetBatch]) -> DatasetBatch:
    if not batches:
        raise ValueError("at least one dataset batch is required")
    if len(batches) == 1:
        return batches[0]

    return DatasetBatch(
        features=np.concatenate([batch.features for batch in batches], axis=0).astype(np.float32),
        move_targets=np.concatenate([batch.move_targets for batch in batches], axis=0).astype(np.int64),
        binary_targets=np.concatenate([batch.binary_targets for batch in batches], axis=0).astype(np.float32),
        aim_targets=np.concatenate([batch.aim_targets for batch in batches], axis=0).astype(np.float32),
        resolved_phase_labels=np.concatenate([batch.resolved_phase_labels for batch in batches], axis=0),
        requested_phase_labels=np.concatenate([batch.requested_phase_labels for batch in batches], axis=0),
        team_labels=np.concatenate([batch.team_labels for batch in batches], axis=0),
        class_labels=np.concatenate([batch.class_labels for batch in batches], axis=0),
        map_labels=np.concatenate([batch.map_labels for batch in batches], axis=0),
        capture_kind_labels=np.concatenate([batch.capture_kind_labels for batch in batches], axis=0),
        corrected_flags=np.concatenate([batch.corrected_flags for batch in batches], axis=0).astype(bool),
        sample_count=sum(batch.sample_count for batch in batches),
        file_count=sum(batch.file_count for batch in batches),
        phase_counts=merge_counts(batches, "phase_counts"),
        class_counts=merge_counts(batches, "class_counts"),
        team_counts=merge_counts(batches, "team_counts"),
        map_counts=merge_counts(batches, "map_counts"),
        capture_kind_counts=merge_counts(batches, "capture_kind_counts"),
        corrected_count=sum(batch.corrected_count for batch in batches),
    )


def build_group_labels(dataset_batch: DatasetBatch, strategy: str) -> np.ndarray:
    sample_count = dataset_batch.sample_count
    if strategy == "none":
        return np.full(sample_count, "all", dtype=np.str_)

    group_parts: list[np.ndarray] = []
    if strategy in {"team_phase", "team_phase_capture", "team_phase_capture_override"}:
        group_parts.extend((dataset_batch.team_labels, dataset_batch.resolved_phase_labels))
    if strategy in {"team_phase_capture", "team_phase_capture_override"}:
        group_parts.append(dataset_batch.capture_kind_labels)
    if strategy == "team_phase_capture_override":
        override_labels = np.where(dataset_batch.corrected_flags, "override", "base")
        group_parts.append(override_labels.astype(np.str_))

    if not group_parts:
        return np.full(sample_count, "all", dtype=np.str_)

    labels = group_parts[0].astype(np.str_)
    for part in group_parts[1:]:
        labels = np.char.add(np.char.add(labels, "|"), part.astype(np.str_))
    return labels


def select_balanced_indices(dataset_batch: DatasetBatch, args: argparse.Namespace) -> np.ndarray:
    all_indices = np.arange(dataset_batch.sample_count, dtype=np.int64)
    if args.balance_groups == "none" and args.max_samples_per_group <= 0:
        return all_indices

    group_labels = build_group_labels(dataset_batch, args.balance_groups)
    rng = np.random.default_rng(args.seed)
    selected_batches: list[np.ndarray] = []

    for group_label in np.unique(group_labels):
        group_indices = all_indices[group_labels == group_label]
        if group_indices.size == 0:
            continue

        rng.shuffle(group_indices)
        if args.max_samples_per_group > 0:
            group_indices = group_indices[: args.max_samples_per_group]
        selected_batches.append(group_indices)

    if not selected_batches:
        raise ValueError("sampling strategy removed every sample from the dataset")

    selected = np.concatenate(selected_batches)
    rng.shuffle(selected)
    return selected


def stratified_split_indices(
    dataset_batch: DatasetBatch,
    args: argparse.Namespace,
    selected_indices: np.ndarray,
) -> tuple[np.ndarray, np.ndarray]:
    rng = np.random.default_rng(args.seed)
    group_labels = build_group_labels(dataset_batch, args.split_groups)
    train_indices: list[np.ndarray] = []
    val_indices: list[np.ndarray] = []

    for group_label in np.unique(group_labels[selected_indices]):
        group_indices = selected_indices[group_labels[selected_indices] == group_label]
        if group_indices.size == 0:
            continue

        shuffled = group_indices.copy()
        rng.shuffle(shuffled)
        val_count = max(1, int(round(shuffled.size * args.validation_split)))
        if val_count >= shuffled.size and shuffled.size > 1:
            val_count = shuffled.size - 1
        val_indices.append(shuffled[:val_count])
        train_indices.append(shuffled[val_count:])

    train_joined = np.concatenate([batch for batch in train_indices if batch.size > 0], dtype=np.int64)
    val_joined = np.concatenate([batch for batch in val_indices if batch.size > 0], dtype=np.int64)

    if train_joined.size == 0 or val_joined.size == 0:
        raise ValueError("stratified split produced an empty training or validation set")

    return train_joined, val_joined


def load_checkpoint_state(path: Path) -> dict[str, Any]:
    state = torch.load(path, map_location="cpu")
    if not isinstance(state, dict):
        raise ValueError(f"checkpoint did not contain a state dict: {path}")
    return state


def load_checkpoint_state_for_model(model: nn.Module, path: Path) -> dict[str, Any]:
    state = load_checkpoint_state(path)
    target_state = model.state_dict()
    adapted_state: dict[str, Any] = {}
    for key, target_value in target_state.items():
        source_key = key
        if source_key not in state:
            source_key = map_shared_head_key_to_task_head_key(key)
        if source_key not in state:
            mlp_value = initialize_mlp_task_head_parameter_from_linear_checkpoint(key, target_value, state)
            if mlp_value is not None:
                adapted_state[key] = mlp_value
                continue
            raise ValueError(f"checkpoint is missing parameter {key}: {path}")

        source_value = state[source_key]
        if not isinstance(source_value, torch.Tensor):
            adapted_state[key] = source_value
            continue

        if tuple(source_value.shape) == tuple(target_value.shape):
            adapted_state[key] = source_value
            continue

        if key == "backbone.0.weight" and source_value.ndim == 2 and target_value.ndim == 2:
            if source_value.shape[0] == target_value.shape[0] and source_value.shape[1] < target_value.shape[1]:
                padded_value = torch.zeros_like(target_value)
                padded_value[:, : source_value.shape[1]] = source_value
                adapted_state[key] = padded_value
                print(
                    "adapted checkpoint input layer "
                    f"from {source_value.shape[1]} to {target_value.shape[1]} features"
                )
                continue

        raise ValueError(
            f"checkpoint parameter shape mismatch for {key}: "
            f"source={tuple(source_value.shape)} target={tuple(target_value.shape)} path={path}"
        )

    return adapted_state


def initialize_mlp_task_head_parameter_from_linear_checkpoint(
    key: str,
    target_value: torch.Tensor,
    source_state: dict[str, Any],
) -> torch.Tensor | None:
    parts = key.split(".")
    if len(parts) != 4 or parts[0] not in {"move_heads", "binary_heads", "aim_heads"}:
        return None
    _, task_index, layer_index, suffix = parts
    if not task_index.isdigit() or layer_index not in {"0", "2"} or suffix not in {"weight", "bias"}:
        return None

    if layer_index == "0":
        if suffix == "weight":
            if target_value.ndim != 2 or target_value.shape[0] != target_value.shape[1]:
                return None
            return torch.eye(target_value.shape[0], dtype=target_value.dtype, device=target_value.device)
        return torch.zeros_like(target_value)

    linear_key = f"{parts[0]}.{task_index}.{suffix}"
    if linear_key not in source_state:
        linear_key = map_shared_head_key_to_task_head_key(linear_key)
    source_value = source_state.get(linear_key)
    if isinstance(source_value, torch.Tensor) and tuple(source_value.shape) == tuple(target_value.shape):
        return source_value
    return None


def copy_shared_policy_head_to_task_head(model: nn.Module, source_state: dict[str, Any], task_head_index: int) -> None:
    target_state = model.state_dict()
    updates: dict[str, Any] = {}
    for task_head_prefix, shared_head_prefix in (
        ("move_heads", "move_head"),
        ("binary_heads", "binary_head"),
        ("aim_heads", "aim_head"),
    ):
        for suffix in ("weight", "bias"):
            source_key = f"{shared_head_prefix}.{suffix}"
            target_key = f"{task_head_prefix}.{task_head_index}.{suffix}"
            if source_key not in source_state or target_key not in target_state:
                raise ValueError(f"cannot copy {source_key} to {target_key}")
            source_value = source_state[source_key]
            target_value = target_state[target_key]
            if not isinstance(source_value, torch.Tensor) or tuple(source_value.shape) != tuple(target_value.shape):
                raise ValueError(
                    f"policy head shape mismatch for {source_key} -> {target_key}: "
                    f"source={getattr(source_value, 'shape', None)} target={target_value.shape}"
                )
            updates[target_key] = source_value

    target_state.update(updates)
    model.load_state_dict(target_state)


def map_shared_head_key_to_task_head_key(key: str) -> str:
    for task_head_prefix, shared_head_prefix in (
        ("move_heads.", "move_head."),
        ("binary_heads.", "binary_head."),
        ("aim_heads.", "aim_head."),
    ):
        if key.startswith(task_head_prefix):
            parts = key.split(".", 2)
            if len(parts) == 3 and parts[1].isdigit():
                return f"{shared_head_prefix}{parts[2]}"
    return key


def export_onnx_model(model: nn.Module, onnx_path: Path) -> None:
    model.eval()
    dummy_input = torch.zeros(1, FEATURE_COUNT, dtype=torch.float32)
    torch.onnx.export(
        model,
        dummy_input,
        onnx_path,
        input_names=["obs"],
        output_names=["move_logits", "binary_logits", "aim"],
        dynamic_axes={"obs": {0: "batch"}, "move_logits": {0: "batch"}, "binary_logits": {0: "batch"}, "aim": {0: "batch"}},
        opset_version=17,
        dynamo=False,
    )


def outcome_auxiliary_enabled(args: argparse.Namespace) -> bool:
    return args.outcome_aux_coef > 0.0 and (args.outcome_data_path or args.outcome_data_dir)


def load_outcome_auxiliary_tensors(
    args: argparse.Namespace,
    device: torch.device,
) -> tuple[torch.Tensor, torch.Tensor, torch.Tensor, dict[str, Any]] | None:
    if not outcome_auxiliary_enabled(args):
        return None

    outcome_args = argparse.Namespace(
        data_path=args.outcome_data_path,
        data_dir=args.outcome_data_dir,
    )
    paths = discover_outcome_paths(outcome_args)
    if not paths:
        raise ValueError("outcome auxiliary training was requested but no outcome dataset files were found")
    inputs, continuous, binary, summary = load_outcome_dataset(paths)
    summary = dict(summary)
    summary["input_size"] = int(inputs.shape[1])
    summary["outcome_aux_coef"] = float(args.outcome_aux_coef)
    return inputs.to(device), continuous.to(device), binary.to(device), summary


def sample_outcome_auxiliary_batch(
    outcome_inputs: torch.Tensor,
    outcome_continuous: torch.Tensor,
    outcome_binary: torch.Tensor,
    batch_size: int,
) -> tuple[torch.Tensor, torch.Tensor, torch.Tensor, torch.Tensor]:
    indices = torch.randint(0, outcome_inputs.shape[0], (batch_size,), device=outcome_inputs.device)
    batch = outcome_inputs[indices]
    return (
        batch[:, :FEATURE_COUNT],
        batch[:, FEATURE_COUNT:],
        outcome_continuous[indices],
        outcome_binary[indices],
    )


def compute_outcome_auxiliary_loss(
    model: nn.Module,
    outcome_heads: OutcomeAuxiliaryHeads,
    outcome_inputs: torch.Tensor,
    outcome_continuous: torch.Tensor,
    outcome_binary: torch.Tensor,
    batch_size: int,
) -> torch.Tensor:
    observations, action_features, continuous_targets, binary_targets = sample_outcome_auxiliary_batch(
        outcome_inputs,
        outcome_continuous,
        outcome_binary,
        batch_size,
    )
    hidden = model.backbone(observations)
    continuous_pred, binary_logits = outcome_heads(hidden, action_features)
    return F.smooth_l1_loss(continuous_pred, continuous_targets) + F.binary_cross_entropy_with_logits(
        binary_logits,
        binary_targets,
    )


def rollout_selection_enabled(args: argparse.Namespace) -> bool:
    return all(
        (
            args.rollout_project,
            args.rollout_map,
            args.rollout_team,
            args.rollout_class,
            args.rollout_task,
        )
    )


def run_rollout_evaluation(
    model: BehaviorCloningModel,
    output_dir: Path,
    epoch: int,
    args: argparse.Namespace,
) -> RolloutSelectionMetrics | None:
    if not rollout_selection_enabled(args):
        return None

    rollout_dir = output_dir / "rollout-selection"
    rollout_dir.mkdir(parents=True, exist_ok=True)
    onnx_path = rollout_dir / f"epoch-{epoch:02d}.onnx"
    trace_json_path = rollout_dir / f"epoch-{epoch:02d}-trace.json"
    rollout_json_path = rollout_dir / f"epoch-{epoch:02d}-rollout.json"
    diagnostic_json_path = rollout_dir / f"epoch-{epoch:02d}-diagnostic.json"

    export_onnx_model(model, onnx_path)
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
            args.rollout_map,
            "--team",
            args.rollout_team,
            "--class",
            args.rollout_class,
            "--task",
            args.rollout_task,
            "--ticks",
            str(args.rollout_ticks),
            "--model",
            str(onnx_path),
            "--json-out",
            str(trace_json_path),
        ]
    )
    append_world_start_options(
        command,
        args.rollout_start_x,
        args.rollout_start_y,
        args.rollout_start_vx,
        args.rollout_start_vy,
        args.rollout_carrying_intel,
    )
    if args.disable_policy_overrides:
        command.append("--disable-policy-overrides")

    completed = subprocess.run(
        command,
        check=False,
        capture_output=True,
        text=True,
        encoding="utf-8",
    )
    if completed.returncode != 0:
        raise RuntimeError(
            "rollout evaluation failed\n"
            f"command={' '.join(command)}\n"
            f"stdout={completed.stdout}\n"
            f"stderr={completed.stderr}"
        )

    with trace_json_path.open("r", encoding="utf-8") as handle:
        payload = json.load(handle)

    export_command = [
        "dotnet",
        "run",
        "--project",
        args.rollout_project,
    ]
    if args.rollout_no_build:
        export_command.append("--no-build")
    export_command.extend(
        [
            "--",
            "export-rollout",
            "--map",
            args.rollout_map,
            "--team",
            args.rollout_team,
            "--class",
            args.rollout_class,
            "--task",
            args.rollout_task,
            "--ticks",
            str(args.rollout_ticks),
            "--model",
            str(onnx_path),
            "--out",
            str(rollout_json_path),
        ]
    )
    append_world_start_options(
        export_command,
        args.rollout_start_x,
        args.rollout_start_y,
        args.rollout_start_vx,
        args.rollout_start_vy,
        args.rollout_carrying_intel,
    )
    if args.disable_policy_overrides:
        export_command.append("--disable-policy-overrides")

    export_completed = subprocess.run(
        export_command,
        check=False,
        capture_output=True,
        text=True,
        encoding="utf-8",
    )
    if export_completed.returncode != 0:
        raise RuntimeError(
            "rollout export failed\n"
            f"command={' '.join(export_command)}\n"
            f"stdout={export_completed.stdout}\n"
            f"stderr={export_completed.stderr}"
        )

    analyze_command = [
        "dotnet",
        "run",
        "--project",
        args.rollout_project,
    ]
    if args.rollout_no_build:
        analyze_command.append("--no-build")
    analyze_command.extend(
        [
            "--",
            "analyze-rollout",
            "--in",
            str(rollout_json_path),
            "--out",
            str(diagnostic_json_path),
        ]
    )
    analyze_completed = subprocess.run(
        analyze_command,
        check=False,
        capture_output=True,
        text=True,
        encoding="utf-8",
    )
    if analyze_completed.returncode != 0:
        raise RuntimeError(
            "rollout analysis failed\n"
            f"command={' '.join(analyze_command)}\n"
            f"stdout={analyze_completed.stdout}\n"
            f"stderr={analyze_completed.stderr}"
        )

    with diagnostic_json_path.open("r", encoding="utf-8") as handle:
        diagnostic = json.load(handle)

    return RolloutSelectionMetrics(
        success=bool(payload["Success"]),
        ticks_elapsed=int(payload["TicksElapsed"]),
        total_reward=float(payload["TotalReward"]),
        terminal_reason=str(payload["TerminalReason"]),
        pickup_tick=int(payload["PickupTick"]) if payload.get("PickupTick") is not None else None,
        score_tick=int(payload["ScoreTick"]) if payload.get("ScoreTick") is not None else None,
        max_stuck_ticks=float(payload["MaxStuckTicks"]),
        min_objective_distance=float(payload["MinObjectiveDistance"]),
        final_objective_distance=float(payload["FinalObjectiveDistance"]),
        final_phase=str(payload["FinalPhase"]),
        likely_issue=str(diagnostic.get("LikelyIssue", "")),
        stall_tick=int(diagnostic["StallTick"]) if diagnostic.get("StallTick") is not None else None,
        stall_window_zero_velocity_ticks=int(diagnostic.get("StallWindowZeroVelocityTicks", 0)),
        stall_window_jump_count=int(diagnostic.get("StallWindowJumpCount", 0)),
        stall_window_idle_count=int(diagnostic.get("StallWindowMoveCounts", {}).get("0", 0)),
    )


def append_world_start_options(
    command: list[str],
    start_x: float | None,
    start_y: float | None,
    start_vx: float | None,
    start_vy: float | None,
    carrying_intel: bool | None,
) -> None:
    if start_x is not None and start_y is not None:
        command.extend(["--start-x", str(start_x), "--start-y", str(start_y)])
    elif start_x is not None or start_y is not None:
        raise ValueError("world-truth rollout fixtures require both --rollout-start-x and --rollout-start-y")

    if start_vx is not None and start_vy is not None:
        command.extend(["--start-vx", str(start_vx), "--start-vy", str(start_vy)])
    elif start_vx is not None or start_vy is not None:
        raise ValueError("world-truth rollout fixtures require both --rollout-start-vx and --rollout-start-vy")

    if carrying_intel is True:
        command.append("--carrying-intel")
    elif carrying_intel is False:
        command.append("--no-carrying-intel")


def train(args: argparse.Namespace) -> None:
    random.seed(args.seed)
    np.random.seed(args.seed)
    torch.manual_seed(args.seed)

    dataset_filter = build_dataset_filter(
        resolved_phases=parse_csv_filter(args.resolved_phases),
        requested_phases=parse_csv_filter(args.requested_phases),
        class_ids=parse_csv_filter(args.class_ids),
        teams=parse_csv_filter(args.teams),
        map_names=parse_csv_filter(args.maps),
        capture_kinds=parse_csv_filter(args.capture_kinds),
        corrected_only=args.corrected_only,
        success_only=args.success_only,
        carrying_intel=args.carrying_intel_filter,
        corrected_upweight=args.corrected_upweight,
    )
    data_roots = [Path(args.data_root), *(Path(path) for path in args.extra_data_root)]
    dataset_batches = [load_behavior_cloning_dataset(root, dataset_filter) for root in data_roots]
    dataset_batch = merge_dataset_batches(dataset_batches)
    selected_indices = select_balanced_indices(dataset_batch, args)
    train_indices, val_indices = stratified_split_indices(dataset_batch, args, selected_indices)

    observations = torch.from_numpy(dataset_batch.features)
    move_targets = torch.from_numpy(dataset_batch.move_targets)
    binary_targets = torch.from_numpy(dataset_batch.binary_targets)
    aim_targets = torch.from_numpy(dataset_batch.aim_targets)
    dataset = TensorDataset(observations, move_targets, binary_targets, aim_targets)
    train_dataset = torch.utils.data.Subset(dataset, train_indices.tolist())
    val_dataset = torch.utils.data.Subset(dataset, val_indices.tolist())

    train_loader = DataLoader(train_dataset, batch_size=args.batch_size, shuffle=True)
    val_loader = DataLoader(val_dataset, batch_size=args.batch_size, shuffle=False)

    device = torch.device("cuda" if torch.cuda.is_available() and not args.cpu else "cpu")
    model_class = select_model_class(args)
    model = model_class(hidden_size=args.hidden_size).to(device)
    if args.fine_tune_from:
        checkpoint_state = load_checkpoint_state_for_model(model, Path(args.fine_tune_from))
        model.load_state_dict(checkpoint_state)

    outcome_auxiliary = load_outcome_auxiliary_tensors(args, device)
    outcome_heads = OutcomeAuxiliaryHeads(args.hidden_size).to(device) if outcome_auxiliary is not None else None
    optimizer_parameters = list(model.parameters())
    if outcome_heads is not None:
        optimizer_parameters.extend(outcome_heads.parameters())
    optimizer = torch.optim.AdamW(optimizer_parameters, lr=args.learning_rate, weight_decay=1e-4)
    if args.weighted_action_loss and args.weighted_move_loss:
        move_class_weights = build_move_class_weights(move_targets, train_indices, device)
    else:
        move_class_weights = None

    if args.weighted_action_loss:
        binary_positive_weights = parse_binary_positive_weights(args.binary_positive_weights, device)
        if binary_positive_weights is None:
            binary_positive_weights = build_binary_positive_weights(
                binary_targets,
                train_indices,
                device,
                args.binary_positive_weight_max,
            )
        print(
            "weighted_action_loss "
            f"move_weights={([round(float(value), 3) for value in move_class_weights.detach().cpu()] if move_class_weights is not None else 'unweighted')} "
            f"binary_pos_weights={[round(float(value), 3) for value in binary_positive_weights.detach().cpu()]}"
        )
    else:
        binary_positive_weights = None

    ce_loss = nn.CrossEntropyLoss(weight=move_class_weights)
    bce_loss = nn.BCEWithLogitsLoss(pos_weight=binary_positive_weights)
    l1_loss = nn.L1Loss()

    output_dir = Path(args.output_dir)
    output_dir.mkdir(parents=True, exist_ok=True)
    metrics_history: list[TrainingMetrics] = []
    dataset_summary = build_dataset_summary(dataset_batch, selected_indices, train_indices, val_indices, args)
    if outcome_auxiliary is not None:
        dataset_summary["outcome_auxiliary"] = outcome_auxiliary[3]
    print_dataset_summary(dataset_summary)

    best_val_loss = float("inf")
    best_val_state = None
    best_rollout = None
    best_rollout_state = None
    for epoch in range(1, args.epochs + 1):
        model.train()
        total_train_loss = 0.0
        total_train_examples = 0
        for obs, move_target, binary_target, aim_target in train_loader:
            obs = obs.to(device)
            move_target = move_target.to(device)
            binary_target = binary_target.to(device)
            aim_target = aim_target.to(device)

            optimizer.zero_grad(set_to_none=True)
            move_logits, binary_logits, aim = model(obs)
            loss = (
                ce_loss(move_logits, move_target)
                + bce_loss(binary_logits, binary_target)
                + l1_loss(aim, aim_target)
            )
            if outcome_auxiliary is not None and outcome_heads is not None:
                outcome_inputs, outcome_continuous, outcome_binary, _ = outcome_auxiliary
                outcome_loss = compute_outcome_auxiliary_loss(
                    model,
                    outcome_heads,
                    outcome_inputs,
                    outcome_continuous,
                    outcome_binary,
                    args.outcome_batch_size,
                )
                loss = loss + (float(args.outcome_aux_coef) * outcome_loss)
            loss.backward()
            optimizer.step()

            batch_size = obs.shape[0]
            total_train_loss += loss.item() * batch_size
            total_train_examples += batch_size

        train_loss = total_train_loss / max(1, total_train_examples)
        val_loss, move_accuracy, binary_accuracy, aim_mae = evaluate(model, val_loader, device)
        metrics = TrainingMetrics(epoch, train_loss, val_loss, move_accuracy, binary_accuracy, aim_mae)
        metrics_history.append(metrics)
        print(
            f"epoch={epoch} train_loss={train_loss:.4f} val_loss={val_loss:.4f} "
            f"move_acc={move_accuracy:.3f} binary_acc={binary_accuracy:.3f} aim_mae={aim_mae:.4f}"
        )

        if val_loss < best_val_loss:
            best_val_loss = val_loss
            best_val_state = {key: value.detach().cpu().clone() for key, value in model.state_dict().items()}

        if rollout_selection_enabled(args) and (epoch % max(1, args.rollout_eval_every) == 0):
            rollout_metrics = run_rollout_evaluation(model, output_dir, epoch, args)
            print(
                "rollout "
                f"epoch={epoch} success={rollout_metrics.success} "
                f"terminal_reason={rollout_metrics.terminal_reason} "
                f"ticks={rollout_metrics.ticks_elapsed} "
                f"pickup_tick={rollout_metrics.pickup_tick} "
                f"score_tick={rollout_metrics.score_tick} "
                f"min_objective_distance={rollout_metrics.min_objective_distance:.1f} "
                f"max_stuck={rollout_metrics.max_stuck_ticks:.1f} "
                f"likely_issue={rollout_metrics.likely_issue or 'none'} "
                f"reward={rollout_metrics.total_reward:.2f}"
            )
            if best_rollout is None or rollout_metrics.score_key > best_rollout.score_key:
                best_rollout = rollout_metrics
                best_rollout_state = {key: value.detach().cpu().clone() for key, value in model.state_dict().items()}

    selected_state = best_rollout_state if best_rollout_state is not None else best_val_state
    if selected_state is None:
        raise RuntimeError("training did not produce a model state")

    checkpoint_path = output_dir / "model.pt"
    torch.save(selected_state, checkpoint_path)

    model.load_state_dict(selected_state)
    onnx_path = output_dir / "model.onnx"
    export_onnx_model(model, onnx_path)

    metrics_path = output_dir / "metrics.json"
    with metrics_path.open("w", encoding="utf-8") as handle:
        json.dump([asdict(item) for item in metrics_history], handle, indent=2)

    summary_path = output_dir / "dataset-summary.json"
    with summary_path.open("w", encoding="utf-8") as handle:
        json.dump(dataset_summary, handle, indent=2)

    selection_summary_path = output_dir / "selection-summary.json"
    with selection_summary_path.open("w", encoding="utf-8") as handle:
        json.dump(
            {
                "selection_mode": "rollout" if best_rollout is not None else "validation_loss",
                "best_validation_loss": best_val_loss,
                "best_rollout": asdict(best_rollout) if best_rollout is not None else None,
            },
            handle,
            indent=2,
        )

    print(f"saved checkpoint={checkpoint_path}")
    print(f"saved onnx={onnx_path}")
    print(f"saved metrics={metrics_path}")
    print(f"saved dataset_summary={summary_path}")
    print(f"saved selection_summary={selection_summary_path}")


def build_dataset_summary(
    dataset_batch: DatasetBatch,
    selected_indices: np.ndarray,
    train_indices: np.ndarray,
    val_indices: np.ndarray,
    args: argparse.Namespace,
) -> dict[str, Any]:
    return {
        "sample_count": dataset_batch.sample_count,
        "selected_sample_count": int(selected_indices.size),
        "train_sample_count": int(train_indices.size),
        "validation_sample_count": int(val_indices.size),
        "file_count": dataset_batch.file_count,
        "phase_counts": dataset_batch.phase_counts,
        "class_counts": dataset_batch.class_counts,
        "team_counts": dataset_batch.team_counts,
        "map_counts": dataset_batch.map_counts,
        "capture_kind_counts": dataset_batch.capture_kind_counts,
        "corrected_count": dataset_batch.corrected_count,
        "selected_group_counts": summarize_selected_groups(dataset_batch, selected_indices, args.balance_groups),
        "split_group_counts": {
            "train": summarize_selected_groups(dataset_batch, train_indices, args.split_groups),
            "validation": summarize_selected_groups(dataset_batch, val_indices, args.split_groups),
        },
        "filters": {
            "data_root": args.data_root,
            "extra_data_root": args.extra_data_root,
            "resolved_phases": list(parse_csv_filter(args.resolved_phases)),
            "requested_phases": list(parse_csv_filter(args.requested_phases)),
            "class_ids": list(parse_csv_filter(args.class_ids)),
            "teams": list(parse_csv_filter(args.teams)),
            "maps": list(parse_csv_filter(args.maps)),
            "capture_kinds": list(parse_csv_filter(args.capture_kinds)),
            "corrected_only": args.corrected_only,
            "carrying_intel_filter": args.carrying_intel_filter,
            "corrected_upweight": args.corrected_upweight,
            "balance_groups": args.balance_groups,
            "split_groups": args.split_groups,
            "max_samples_per_group": args.max_samples_per_group,
            "fine_tune_from": args.fine_tune_from,
            "rollout_project": args.rollout_project,
            "rollout_map": args.rollout_map,
            "rollout_team": args.rollout_team,
            "rollout_class": args.rollout_class,
            "rollout_task": args.rollout_task,
            "rollout_ticks": args.rollout_ticks,
            "rollout_start_x": args.rollout_start_x,
            "rollout_start_y": args.rollout_start_y,
            "rollout_start_vx": args.rollout_start_vx,
            "rollout_start_vy": args.rollout_start_vy,
            "rollout_carrying_intel": args.rollout_carrying_intel,
            "rollout_eval_every": args.rollout_eval_every,
            "disable_policy_overrides": args.disable_policy_overrides,
            "task_conditioned_heads": args.task_conditioned_heads,
            "weighted_action_loss": args.weighted_action_loss,
            "weighted_move_loss": args.weighted_move_loss,
            "binary_positive_weight_max": args.binary_positive_weight_max,
            "binary_positive_weights": args.binary_positive_weights,
            "outcome_data_path": args.outcome_data_path,
            "outcome_data_dir": args.outcome_data_dir,
            "outcome_aux_coef": args.outcome_aux_coef,
            "outcome_batch_size": args.outcome_batch_size,
        },
    }


def summarize_selected_groups(dataset_batch: DatasetBatch, indices: np.ndarray, strategy: str) -> dict[str, int]:
    if indices.size == 0:
        return {}

    labels = build_group_labels(dataset_batch, strategy)[indices]
    summary: dict[str, int] = {}
    for label in labels.tolist():
        summary[label] = summary.get(label, 0) + 1
    return dict(sorted(summary.items(), key=lambda item: item[0]))


def print_dataset_summary(summary: dict[str, Any]) -> None:
    print(
        "dataset "
        f"samples={summary['sample_count']} "
        f"selected={summary['selected_sample_count']} "
        f"train={summary['train_sample_count']} "
        f"val={summary['validation_sample_count']} "
        f"files={summary['file_count']} "
        f"phases={summary['phase_counts']} "
        f"classes={summary['class_counts']} "
        f"teams={summary['team_counts']} "
        f"maps={summary['map_counts']} "
        f"capture_kinds={summary['capture_kind_counts']} "
        f"corrected={summary['corrected_count']}"
    )


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser()
    parser.add_argument("--data-root", required=True)
    parser.add_argument(
        "--extra-data-root",
        action="append",
        default=[],
        help="Additional behavior-cloning dataset root merged after filtering.",
    )
    parser.add_argument("--output-dir", required=True)
    parser.add_argument("--epochs", type=int, default=30)
    parser.add_argument("--batch-size", type=int, default=256)
    parser.add_argument("--learning-rate", type=float, default=1e-3)
    parser.add_argument("--hidden-size", type=int, default=256)
    parser.add_argument("--validation-split", type=float, default=0.1)
    parser.add_argument("--seed", type=int, default=1337)
    parser.add_argument("--resolved-phases", default="")
    parser.add_argument("--requested-phases", default="")
    parser.add_argument("--class-ids", default="")
    parser.add_argument("--teams", default="")
    parser.add_argument("--maps", default="")
    parser.add_argument("--capture-kinds", default="")
    parser.add_argument("--corrected-only", action="store_true")
    parser.add_argument("--success-only", action="store_true")
    carrying_group = parser.add_mutually_exclusive_group()
    carrying_group.add_argument("--carrying-intel-only", dest="carrying_intel_filter", action="store_true")
    carrying_group.add_argument("--exclude-carrying-intel", dest="carrying_intel_filter", action="store_false")
    parser.set_defaults(carrying_intel_filter=None)
    parser.add_argument("--corrected-upweight", type=int, default=1)
    parser.add_argument(
        "--balance-groups",
        choices=("none", "team_phase", "team_phase_capture", "team_phase_capture_override"),
        default="team_phase_capture_override",
    )
    parser.add_argument(
        "--split-groups",
        choices=("none", "team_phase", "team_phase_capture", "team_phase_capture_override"),
        default="team_phase_capture_override",
    )
    parser.add_argument("--max-samples-per-group", type=int, default=0)
    parser.add_argument("--fine-tune-from", default="")
    parser.add_argument("--rollout-project", default="")
    parser.add_argument("--rollout-map", default="")
    parser.add_argument("--rollout-team", default="")
    parser.add_argument("--rollout-class", default="")
    parser.add_argument("--rollout-task", default="")
    parser.add_argument("--rollout-ticks", type=int, default=1800)
    parser.add_argument("--rollout-start-x", type=float, default=None)
    parser.add_argument("--rollout-start-y", type=float, default=None)
    parser.add_argument("--rollout-start-vx", type=float, default=None)
    parser.add_argument("--rollout-start-vy", type=float, default=None)
    parser.add_argument("--rollout-carrying-intel", dest="rollout_carrying_intel", action="store_true")
    parser.add_argument("--rollout-no-carrying-intel", dest="rollout_carrying_intel", action="store_false")
    parser.set_defaults(rollout_carrying_intel=None)
    parser.add_argument("--rollout-eval-every", type=int, default=1)
    parser.add_argument("--rollout-no-build", action="store_true")
    parser.add_argument("--disable-policy-overrides", action="store_true")
    parser.add_argument("--weighted-action-loss", action="store_true")
    parser.add_argument("--weighted-move-loss", action="store_true")
    parser.add_argument("--binary-positive-weight-max", type=float, default=4.0)
    parser.add_argument(
        "--binary-positive-weights",
        default="",
        help="Optional comma-separated pos_weight values for jump,crouch,fire_primary,fire_secondary,drop_intel.",
    )
    parser.add_argument(
        "--outcome-data-path",
        action="append",
        default=[],
        help="Optional mlbot-outcome-v1 dataset JSON for training-only auxiliary outcome prediction.",
    )
    parser.add_argument(
        "--outcome-data-dir",
        action="append",
        default=[],
        help="Optional directory containing mlbot-outcome-v1 dataset JSON files.",
    )
    parser.add_argument(
        "--outcome-aux-coef",
        type=float,
        default=0.0,
        help="Auxiliary outcome prediction loss coefficient. 0 disables the auxiliary head.",
    )
    parser.add_argument("--outcome-batch-size", type=int, default=512)
    parser.add_argument(
        "--task-conditioned-heads",
        action="store_true",
        help="Use one shared V5 backbone with task-gated policy heads for a unified model.",
    )
    parser.add_argument(
        "--task-conditioned-mlp-heads",
        action="store_true",
        help="Use nonlinear task-gated heads initialized compatibly from linear task-head checkpoints.",
    )
    parser.add_argument("--cpu", action="store_true")
    return parser.parse_args()


def select_model_class(args: argparse.Namespace) -> type[nn.Module]:
    if getattr(args, "task_conditioned_mlp_heads", False):
        return TaskConditionedMlpHeadBehaviorCloningModel
    if getattr(args, "task_conditioned_heads", False):
        return TaskConditionedBehaviorCloningModel
    return BehaviorCloningModel


if __name__ == "__main__":
    train(parse_args())
