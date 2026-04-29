from __future__ import annotations

import argparse
import json
import subprocess
import sys
from datetime import datetime, timezone
from pathlib import Path
from typing import Any

from evaluate_policy_matrix import evaluate_success_criterion, load_scenario_file, parse_model_spec


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description="Export successful policy rollouts as V6 teacher demo files.")
    parser.add_argument("--rollout-project", required=True)
    parser.add_argument("--output-root", required=True)
    parser.add_argument("--model", action="append", required=True, help="name=path")
    parser.add_argument("--scenario-file", action="append", required=True)
    parser.add_argument("--config", action="append", default=[], help="Director stack JSON to apply while exporting teacher rollouts.")
    parser.add_argument("--rollout-no-build", action="store_true")
    parser.add_argument("--disable-policy-overrides", action="store_true")
    parser.add_argument("--include-failures", action="store_true")
    parser.add_argument("--return-finalizer-model", default="")
    parser.add_argument("--return-finalizer-engage-distance", type=float, default=192.0)
    parser.add_argument("--return-finalizer-map", default="")
    parser.add_argument("--return-finalizer-team", default="")
    parser.add_argument("--return-finalizer-class", default="Scout")
    parser.add_argument("--return-finalizer-after-options", action="store_true")
    parser.add_argument("--return-replay-bank", default="")
    parser.add_argument("--return-replay-engage-distance", type=float, default=320.0)
    parser.add_argument("--return-replay-max-score", type=float, default=0.0)
    parser.add_argument("--return-chunk-model", default="")
    parser.add_argument("--return-chunk-engage-distance", type=float, default=0.0)
    parser.add_argument("--return-chunk-commit-ticks", type=int, default=0)
    parser.add_argument(
        "--return-chunk-spec",
        action="append",
        default=[],
        help=(
            "Chunk option as model|engage_distance|commit_ticks|map_filter|team_filter|class_filter"
            "[|min_rel_x|max_rel_x|min_rel_y|max_rel_y]. Later specs wrap earlier specs."
        ),
    )
    parser.add_argument("--return-chunk-selector-model", default="")
    parser.add_argument(
        "--return-selected-chunk-spec",
        action="append",
        default=[],
        help="Selector chunk option as model|commit_ticks. Option 0 is the base policy; options 1..N follow this order.",
    )
    parser.add_argument("--return-hierarchical-chunk-model", default="")
    parser.add_argument("--return-hierarchical-chunk-commit-ticks", default="")
    parser.add_argument(
        "--task-option-spec",
        action="append",
        default=[],
        help="Task option as model|phase|engage_distance|map_filter|team_filter|class_filter. Later specs wrap earlier specs.",
    )
    parser.add_argument(
        "--task-chunk-spec",
        action="append",
        default=[],
        help=(
            "Task chunk option as model|phase|engage_distance|commit_ticks|map_filter|team_filter|class_filter"
            "[|min_rel_x|max_rel_x|min_rel_y|max_rel_y|requires_carrying_intel|min_engage_distance]."
        ),
    )
    return parser.parse_args()


def load_stack_args(config_paths: list[str]) -> list[str]:
    stack_args: list[str] = []
    for raw_path in config_paths:
        path = Path(raw_path)
        config = json.loads(path.read_text(encoding="utf-8"))
        if config.get("disable_policy_overrides"):
            stack_args.append("--disable-policy-overrides")
        if config.get("return_hierarchical_chunk_model"):
            stack_args.extend(["--return-hierarchical-chunk-model", str(config["return_hierarchical_chunk_model"])])
        if config.get("return_hierarchical_chunk_commit_ticks"):
            raw_ticks = ",".join(str(item) for item in config["return_hierarchical_chunk_commit_ticks"])
            stack_args.extend(["--return-hierarchical-chunk-commit-ticks", raw_ticks])
        for spec in config.get("task_options", []):
            stack_args.extend(["--task-option-spec", str(spec)])
        for spec in config.get("task_chunks", []):
            stack_args.extend(["--task-chunk-spec", str(spec)])
        for spec in config.get("return_chunks", []):
            stack_args.extend(["--return-chunk-spec", str(spec)])
    return stack_args


def run_rollout(
    args: argparse.Namespace,
    stack_args: list[str],
    model_name: str,
    model_path: str,
    scenario: Any,
    output_path: Path,
) -> dict[str, Any]:
    command = ["dotnet", "run", "--project", args.rollout_project]
    if args.rollout_no_build:
        command.append("--no-build")
    command.extend(
        [
            "--",
            "export-rollout",
            "--map",
            scenario.level_name,
            "--team",
            scenario.team,
            "--class",
            scenario.class_id,
            "--task",
            scenario.task,
            "--ticks",
            str(scenario.ticks),
            "--model",
            model_path,
            "--out",
            str(output_path),
        ]
    )
    if scenario.start_x is not None and scenario.start_y is not None:
        command.extend(["--start-x", str(scenario.start_x), "--start-y", str(scenario.start_y)])
    if scenario.start_vx is not None and scenario.start_vy is not None:
        command.extend(["--start-vx", str(scenario.start_vx), "--start-vy", str(scenario.start_vy)])
    if scenario.carrying_intel is True:
        command.append("--carrying-intel")
    elif scenario.carrying_intel is False:
        command.append("--no-carrying-intel")
    if args.disable_policy_overrides:
        command.append("--disable-policy-overrides")
    command.extend(stack_args)
    if args.return_finalizer_model:
        command.extend(["--return-finalizer-model", args.return_finalizer_model])
        command.extend(["--return-finalizer-engage-distance", str(args.return_finalizer_engage_distance)])
        if args.return_finalizer_map:
            command.extend(["--return-finalizer-map", args.return_finalizer_map])
        if args.return_finalizer_team:
            command.extend(["--return-finalizer-team", args.return_finalizer_team])
        command.extend(["--return-finalizer-class", args.return_finalizer_class])
        if args.return_finalizer_after_options:
            command.append("--return-finalizer-after-options")
    if args.return_replay_bank:
        command.extend(["--return-replay-bank", args.return_replay_bank])
        command.extend(["--return-replay-engage-distance", str(args.return_replay_engage_distance)])
        if args.return_replay_max_score > 0:
            command.extend(["--return-replay-max-score", str(args.return_replay_max_score)])
    if args.return_chunk_model:
        command.extend(["--return-chunk-model", args.return_chunk_model])
        command.extend(["--return-chunk-engage-distance", str(args.return_chunk_engage_distance)])
        if args.return_chunk_commit_ticks > 0:
            command.extend(["--return-chunk-commit-ticks", str(args.return_chunk_commit_ticks)])
    for spec in args.return_chunk_spec:
        command.extend(["--return-chunk-spec", spec])
    if args.return_chunk_selector_model:
        command.extend(["--return-chunk-selector-model", args.return_chunk_selector_model])
    for spec in args.return_selected_chunk_spec:
        command.extend(["--return-selected-chunk-spec", spec])
    if args.return_hierarchical_chunk_model:
        command.extend(["--return-hierarchical-chunk-model", args.return_hierarchical_chunk_model])
    if args.return_hierarchical_chunk_commit_ticks:
        command.extend(["--return-hierarchical-chunk-commit-ticks", args.return_hierarchical_chunk_commit_ticks])
    for spec in args.task_option_spec:
        command.extend(["--task-option-spec", spec])
    for spec in args.task_chunk_spec:
        command.extend(["--task-chunk-spec", spec])

    completed = subprocess.run(command, check=False, capture_output=True, text=True, encoding="utf-8")
    if completed.returncode != 0:
        raise RuntimeError(
            "teacher rollout failed\n"
            f"model={model_name} scenario={scenario.name}\n"
            f"command={' '.join(command)}\nstdout={completed.stdout}\nstderr={completed.stderr}"
        )
    with output_path.open("r", encoding="utf-8") as handle:
        payload: dict[str, Any] = json.load(handle)
    return payload


def rollout_to_trace_payload(rollout: dict[str, Any]) -> dict[str, Any]:
    steps = rollout.get("Steps", [])
    pickup_tick = None
    score_tick = None
    capture_tick = None
    for step in steps:
        observation = step.get("Observation", {})
        next_observation = step.get("NextObservation", {})
        tick = step.get("Tick")
        if pickup_tick is None and not observation.get("IsCarryingIntel", False) and next_observation.get("IsCarryingIntel", False):
            pickup_tick = tick
        if score_tick is None and observation.get("IsCarryingIntel", False) and not next_observation.get("IsCarryingIntel", False) and step.get("IsSuccess"):
            score_tick = tick
        if capture_tick is None and step.get("IsSuccess") and str(step.get("TerminalReason", "")).lower() == "captured":
            capture_tick = tick
    return {
        "Success": bool(rollout.get("Success")),
        "TerminalReason": rollout.get("TerminalReason", ""),
        "PickupTick": pickup_tick,
        "ScoreTick": score_tick,
        "CaptureTick": capture_tick,
    }


def convert_rollout_to_demo(
    rollout: dict[str, Any],
    model_name: str,
    scenario: Any,
    model_path: str,
    success: bool,
) -> dict[str, Any]:
    steps = rollout.get("Steps", [])
    samples = []
    for index, step in enumerate(steps):
        observation = step["Observation"]
        next_observation = step["NextObservation"]
        samples.append(
            {
                "Tick": index,
                "ResolvedPhase": observation.get("TaskPhase", scenario.task),
                "Observation": observation,
                "Action": step["Action"],
                "HumanAction": None,
                "SuggestedAction": None,
                "UsedHumanOverride": False,
                "NextObservation": next_observation,
                "PickedUpIntel": (not observation.get("IsCarryingIntel", False)) and bool(next_observation.get("IsCarryingIntel", False)),
                "ScoredIntel": bool(observation.get("IsCarryingIntel", False))
                and not bool(next_observation.get("IsCarryingIntel", False))
                and bool(step.get("IsSuccess")),
                "Died": str(step.get("TerminalReason", "")).lower() == "death",
                "EpisodeEnded": bool(step.get("IsTerminal")),
            }
        )

    mode = steps[0]["Observation"].get("Mode", "Unknown") if steps else "Unknown"
    return {
        "Metadata": {
            "SchemaVersion": "mlbot-demo-v6",
            "LevelName": rollout.get("LevelName", scenario.level_name),
            "MapAreaIndex": 1,
            "Mode": mode,
            "Team": rollout.get("Team", scenario.team),
            "ClassId": rollout.get("ClassId", scenario.class_id),
            "RequestedPhase": scenario.task,
            "CaptureKind": "Demonstration",
            "PolicyModelPath": model_path,
            "Label": f"teacher_rollout:{model_name}:{scenario.name}",
            "RecordedAtUtc": datetime.now(timezone.utc).isoformat(),
            "TickCount": len(samples),
            "CaptureMaxTicks": scenario.ticks,
            "ShortCapture": False,
            "Success": success,
            "Outcome": "teacher_success" if success else "teacher_failure",
        },
        "Samples": samples,
    }


def safe_name(value: str) -> str:
    return "".join(ch.lower() if ch.isalnum() else "_" for ch in value).strip("_")


def main() -> None:
    args = parse_args()
    output_root = Path(args.output_root)
    rollout_root = output_root / "rollouts"
    demo_root = output_root / "teacher-demos"
    rollout_root.mkdir(parents=True, exist_ok=True)
    demo_root.mkdir(parents=True, exist_ok=True)

    models = [parse_model_spec(raw) for raw in args.model]
    stack_args = load_stack_args(args.config)
    scenarios = []
    for raw_path in args.scenario_file:
        scenarios.extend(load_scenario_file(Path(raw_path)))
    summary: list[dict[str, Any]] = []

    for model in models:
        for scenario in scenarios:
            stem = f"{safe_name(model.name)}__{safe_name(scenario.name)}"
            rollout_path = rollout_root / f"{stem}.json"
            rollout = run_rollout(args, stack_args, model.name, model.path, scenario, rollout_path)
            trace_payload = rollout_to_trace_payload(rollout)
            success = evaluate_success_criterion(trace_payload, scenario.success_criterion)
            included = success or args.include_failures
            demo_path = demo_root / str(rollout.get("LevelName", scenario.level_name)) / f"{stem}.json"
            if included:
                demo_path.parent.mkdir(parents=True, exist_ok=True)
                demo = convert_rollout_to_demo(rollout, model.name, scenario, model.path, success)
                with demo_path.open("w", encoding="utf-8") as handle:
                    json.dump(demo, handle, indent=2)
            summary.append(
                {
                    "model": model.name,
                    "scenario": scenario.name,
                    "success_criterion": scenario.success_criterion,
                    "success": success,
                    "included": included,
                    "rollout_path": str(rollout_path),
                    "demo_path": str(demo_path) if included else "",
                    **trace_payload,
                }
            )
            print(f"teacher model={model.name} scenario={scenario.name} success={success} included={included}")

    with (output_root / "teacher-export-summary.json").open("w", encoding="utf-8") as handle:
        json.dump(summary, handle, indent=2)
    print(f"saved summary={output_root / 'teacher-export-summary.json'}")


if __name__ == "__main__":
    main()
