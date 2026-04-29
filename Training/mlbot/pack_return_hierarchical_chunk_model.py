from __future__ import annotations

import argparse
import json
from pathlib import Path

import torch
import torch.nn as nn

from mlbot_dataset import FEATURE_COUNT
from train_return_chunk_head import ReturnChunkModel
from train_return_option_selector import ReturnOptionSelectorModel


class PackedReturnHierarchicalChunkModel(nn.Module):
    def __init__(self, selector: nn.Module, experts: list[ReturnChunkModel], horizon: int) -> None:
        super().__init__()
        self.selector = selector
        self.experts = nn.ModuleList(experts)
        self.horizon = horizon

    def forward(self, obs: torch.Tensor) -> tuple[torch.Tensor, torch.Tensor, torch.Tensor, torch.Tensor]:
        option_logits = self.selector(obs)
        move_outputs = []
        binary_outputs = []
        aim_outputs = []
        for expert in self.experts:
            move_logits, binary_logits, aim = expert(obs)
            move_outputs.append(pad_horizon(move_logits, self.horizon))
            binary_outputs.append(pad_horizon(binary_logits, self.horizon))
            aim_outputs.append(pad_horizon(aim, self.horizon))
        return (
            option_logits,
            torch.stack(move_outputs, dim=1),
            torch.stack(binary_outputs, dim=1),
            torch.stack(aim_outputs, dim=1),
        )


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description="Pack a selector and chunk experts into one hierarchical ReturnIntel ONNX.")
    parser.add_argument("--output-dir", required=True)
    parser.add_argument("--selector-checkpoint", required=True)
    parser.add_argument("--selector-hidden-size", type=int, default=128)
    parser.add_argument("--chunk-checkpoint", action="append", required=True)
    parser.add_argument("--horizon", type=int, default=0)
    return parser.parse_args()


def infer_chunk_horizon(checkpoint_path: Path) -> int:
    state = torch.load(checkpoint_path, map_location="cpu")
    move_weight = state.get("move_head.weight")
    if not isinstance(move_weight, torch.Tensor):
        raise ValueError(f"could not infer chunk horizon from {checkpoint_path}")
    return int(move_weight.shape[0] // 3)


def load_selector(checkpoint_path: Path, hidden_size: int) -> ReturnOptionSelectorModel:
    state = torch.load(checkpoint_path, map_location="cpu")
    option_count = int(state["option_head.weight"].shape[0])
    selector = ReturnOptionSelectorModel(option_count=option_count, hidden_size=hidden_size)
    selector.load_state_dict(state)
    selector.eval()
    return selector


def load_chunk(checkpoint_path: Path) -> ReturnChunkModel:
    horizon = infer_chunk_horizon(checkpoint_path)
    state = torch.load(checkpoint_path, map_location="cpu")
    hidden_size = int(state["backbone.0.weight"].shape[0])
    chunk = ReturnChunkModel(horizon=horizon, hidden_size=hidden_size)
    chunk.load_state_dict(state)
    chunk.eval()
    return chunk


def pad_horizon(values: torch.Tensor, horizon: int) -> torch.Tensor:
    if values.shape[1] == horizon:
        return values
    if values.shape[1] > horizon:
        return values[:, :horizon]
    padding_shape = list(values.shape)
    padding_shape[1] = horizon - values.shape[1]
    padding = torch.zeros(*padding_shape, dtype=values.dtype, device=values.device)
    return torch.cat([values, padding], dim=1)


def main() -> None:
    args = parse_args()
    output_dir = Path(args.output_dir)
    output_dir.mkdir(parents=True, exist_ok=True)
    selector = load_selector(Path(args.selector_checkpoint), args.selector_hidden_size)
    experts = [load_chunk(Path(raw_path)) for raw_path in args.chunk_checkpoint]
    horizon = args.horizon if args.horizon > 0 else max(expert.horizon for expert in experts)
    model = PackedReturnHierarchicalChunkModel(selector, experts, horizon)
    model.eval()
    onnx_path = output_dir / "hierarchical_chunk_model.onnx"
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
    metadata = {
        "selector_checkpoint": args.selector_checkpoint,
        "chunk_checkpoints": args.chunk_checkpoint,
        "horizon": horizon,
        "option_count": len(experts),
    }
    with (output_dir / "pack-summary.json").open("w", encoding="utf-8") as handle:
        json.dump(metadata, handle, indent=2)
    print(f"saved onnx={onnx_path}")


if __name__ == "__main__":
    main()
