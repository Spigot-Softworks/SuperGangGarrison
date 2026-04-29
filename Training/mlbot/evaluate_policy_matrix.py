from __future__ import annotations

import argparse
import json
import subprocess
from dataclasses import asdict, dataclass
from pathlib import Path
from typing import Any


@dataclass(frozen=True)
class ModelSpec:
    name: str
    path: str


@dataclass(frozen=True)
class ScenarioSpec:
    name: str
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
    success_criterion: str = "terminal_success"


@dataclass
class MatrixResult:
    model: ModelSpec
    scenario: ScenarioSpec
    success: bool
    raw_terminal_success: bool
    success_criterion: str
    terminal_reason: str
    ticks_elapsed: int
    pickup_tick: int | None
    score_tick: int | None
    capture_tick: int | None
    min_navigation_distance: float
    final_navigation_distance: float
    trace_path: str


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description="Evaluate MLBot policies across a regression scenario matrix.")
    parser.add_argument("--rollout-project", required=True)
    parser.add_argument("--output-dir", required=True)
    parser.add_argument("--model", action="append", required=True, help="name=path")
    parser.add_argument(
        "--scenario",
        action="append",
        default=[],
        help=(
            "name,map,team,class,task,ticks,start_node_id, or "
            "name,map,team,class,task,ticks,start_x,start_y[,carrying_intel[,start_vx,start_vy]]."
        ),
    )
    parser.add_argument(
        "--scenario-file",
        action="append",
        default=[],
        help="JSON file with a top-level 'scenarios' array using ScenarioSpec field names.",
    )
    parser.add_argument("--rollout-no-build", action="store_true")
    parser.add_argument("--disable-policy-overrides", action="store_true")
    parser.add_argument("--return-finalizer-model", default="")
    parser.add_argument("--return-finalizer-engage-distance", type=float, default=0.0)
    parser.add_argument("--return-finalizer-map", default="")
    parser.add_argument("--return-finalizer-team", default="")
    parser.add_argument("--return-finalizer-class", default="")
    parser.add_argument("--return-finalizer-after-options", action="store_true")
    parser.add_argument("--return-replay-bank", default="")
    parser.add_argument("--return-replay-engage-distance", type=float, default=0.0)
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
        help="Selector chunk option as model|commit_ticks. Option id 0 is base; ids 1..N follow this order.",
    )
    parser.add_argument("--return-hierarchical-chunk-model", default="")
    parser.add_argument(
        "--return-hierarchical-chunk-commit-ticks",
        default="",
        help="Comma/pipe/semicolon-separated commit ticks for hierarchical chunk options 1..N.",
    )
    parser.add_argument(
        "--task-option-spec",
        action="append",
        default=[],
        help="Filtered task option as model|phase|engage_distance|map_filter|team_filter|class_filter.",
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
    parser.add_argument("--stop-on-failure", action="store_true")
    return parser.parse_args()


def parse_model_spec(raw_value: str) -> ModelSpec:
    name, separator, path = raw_value.partition("=")
    if not separator or not name.strip():
        raise ValueError("--model must be formatted as name=path; use name= for the built-in direct policy")
    return ModelSpec(name=name.strip(), path=path.strip())


def parse_scenario_spec(raw_value: str) -> ScenarioSpec:
    parts = [part.strip() for part in raw_value.split(",")]
    if len(parts) not in (6, 7, 8, 9, 11) or any(not part for part in parts[:6]):
        raise ValueError(
            "--scenario must be formatted as name,map,team,class,task,ticks[,start_node_id] "
            "or name,map,team,class,task,ticks,start_x,start_y[,carrying_intel[,start_vx,start_vy]]"
        )
    if len(parts) in (8, 9, 11):
        return ScenarioSpec(
            name=parts[0],
            level_name=parts[1],
            team=parts[2],
            class_id=parts[3],
            task=parts[4],
            ticks=int(parts[5]),
            start_x=float(parts[6]),
            start_y=float(parts[7]),
            carrying_intel=parse_optional_bool(parts[8]) if len(parts) >= 9 else None,
            start_vx=float(parts[9]) if len(parts) == 11 else None,
            start_vy=float(parts[10]) if len(parts) == 11 else None,
        )

    return ScenarioSpec(
        name=parts[0],
        level_name=parts[1],
        team=parts[2],
        class_id=parts[3],
        task=parts[4],
        ticks=int(parts[5]),
        start_node_id=int(parts[6]) if len(parts) == 7 and parts[6] else -1,
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


def load_scenario_file(path: Path) -> list[ScenarioSpec]:
    with path.open("r", encoding="utf-8") as handle:
        payload: dict[str, Any] = json.load(handle)
    scenarios: list[ScenarioSpec] = []
    for item in payload.get("scenarios", []):
        scenarios.append(
            ScenarioSpec(
                name=str(item["name"]),
                level_name=str(item["level_name"]),
                team=str(item["team"]),
                class_id=str(item["class_id"]),
                task=str(item["task"]),
                ticks=int(item["ticks"]),
                start_node_id=int(item.get("start_node_id", -1)),
                start_x=float(item["start_x"]) if item.get("start_x") is not None else None,
                start_y=float(item["start_y"]) if item.get("start_y") is not None else None,
                start_vx=float(item["start_vx"]) if item.get("start_vx") is not None else None,
                start_vy=float(item["start_vy"]) if item.get("start_vy") is not None else None,
                carrying_intel=(
                    bool(item["carrying_intel"])
                    if item.get("carrying_intel") is not None
                    else None
                ),
                success_criterion=str(item.get("success_criterion", "terminal_success")),
            )
        )
    return scenarios


def append_world_start_options(command: list[str], scenario: ScenarioSpec) -> None:
    if scenario.start_x is not None and scenario.start_y is not None:
        command.extend(["--start-x", str(scenario.start_x), "--start-y", str(scenario.start_y)])
    elif scenario.start_x is not None or scenario.start_y is not None:
        raise ValueError(f"scenario {scenario.name} must provide both start_x and start_y")

    if scenario.start_vx is not None and scenario.start_vy is not None:
        command.extend(["--start-vx", str(scenario.start_vx), "--start-vy", str(scenario.start_vy)])
    elif scenario.start_vx is not None or scenario.start_vy is not None:
        raise ValueError(f"scenario {scenario.name} must provide both start_vx and start_vy")

    if scenario.carrying_intel is True:
        command.append("--carrying-intel")
    elif scenario.carrying_intel is False:
        command.append("--no-carrying-intel")


def run_eval(args: argparse.Namespace, model: ModelSpec, scenario: ScenarioSpec, output_dir: Path) -> MatrixResult:
    trace_path = output_dir / f"{model.name}__{scenario.name}.json"
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
            scenario.level_name,
            "--team",
            scenario.team,
            "--class",
            scenario.class_id,
            "--task",
            scenario.task,
            "--ticks",
            str(scenario.ticks),
            "--json-out",
            str(trace_path),
        ]
    )
    if model.path.strip().lower() not in {"", "direct", "direct-objective"}:
        command.extend(["--model", model.path])
    if scenario.start_node_id >= 0:
        command.extend(["--start-node-id", str(scenario.start_node_id)])
    append_world_start_options(command, scenario)
    if args.disable_policy_overrides:
        command.append("--disable-policy-overrides")
    if args.return_finalizer_model:
        command.extend(["--return-finalizer-model", args.return_finalizer_model])
        command.extend(["--return-finalizer-engage-distance", str(args.return_finalizer_engage_distance)])
        if args.return_finalizer_map:
            command.extend(["--return-finalizer-map", args.return_finalizer_map])
        if args.return_finalizer_team:
            command.extend(["--return-finalizer-team", args.return_finalizer_team])
        if args.return_finalizer_class:
            command.extend(["--return-finalizer-class", args.return_finalizer_class])
        if args.return_finalizer_after_options:
            command.append("--return-finalizer-after-options")
    if args.return_replay_bank:
        command.extend(["--return-replay-bank", args.return_replay_bank])
        command.extend(["--return-replay-engage-distance", str(args.return_replay_engage_distance)])
        if args.return_replay_max_score > 0.0:
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
    for spec in args.task_chunk_spec:
        command.extend(["--task-chunk-spec", spec])
    for spec in args.task_option_spec:
        command.extend(["--task-option-spec", spec])

    completed = subprocess.run(command, check=False, capture_output=True, text=True, encoding="utf-8")
    if completed.returncode != 0:
        raise RuntimeError(
            "evaluation failed\n"
            f"command={' '.join(command)}\nstdout={completed.stdout}\nstderr={completed.stderr}"
        )

    with trace_path.open("r", encoding="utf-8") as handle:
        payload: dict[str, Any] = json.load(handle)

    raw_terminal_success = bool(payload["Success"])
    success = evaluate_success_criterion(payload, scenario.success_criterion)
    return MatrixResult(
        model=model,
        scenario=scenario,
        success=success,
        raw_terminal_success=raw_terminal_success,
        success_criterion=scenario.success_criterion,
        terminal_reason=str(payload["TerminalReason"]),
        ticks_elapsed=int(payload["TicksElapsed"]),
        pickup_tick=payload.get("PickupTick"),
        score_tick=payload.get("ScoreTick"),
        capture_tick=payload.get("CaptureTick"),
        min_navigation_distance=float(payload.get("MinNavigationDistance", payload["MinObjectiveDistance"])),
        final_navigation_distance=float(payload.get("FinalNavigationDistance", payload["FinalObjectiveDistance"])),
        trace_path=str(trace_path),
    )


def evaluate_success_criterion(payload: dict[str, Any], criterion: str) -> bool:
    normalized = criterion.strip().lower().replace("-", "_")
    terminal_reason = str(payload.get("TerminalReason", "")).lower()
    pickup_tick = payload.get("PickupTick")
    score_tick = payload.get("ScoreTick")
    capture_tick = payload.get("CaptureTick")
    terminal_success = bool(payload.get("Success"))

    if normalized in {"", "terminal", "terminal_success"}:
        return terminal_success
    if normalized in {"attack_pickup", "pickup", "intel_pickup"}:
        return pickup_tick is not None or terminal_reason == "picked_up_intel"
    if normalized in {"return_score", "score", "scored"}:
        return score_tick is not None or terminal_reason == "scored"
    if normalized in {"full_score", "ctf_score", "attack_return_score"}:
        return score_tick is not None or terminal_reason in {"scored", "completed_primary_objective"}
    if normalized in {"capture", "cap", "capture_hold", "koth_capture_hold"}:
        return capture_tick is not None or terminal_reason == "captured"
    raise ValueError(f"unknown success_criterion: {criterion}")


def main() -> None:
    args = parse_args()
    output_dir = Path(args.output_dir)
    output_dir.mkdir(parents=True, exist_ok=True)
    models = [parse_model_spec(raw_value) for raw_value in args.model]
    scenarios = [parse_scenario_spec(raw_value) for raw_value in args.scenario]
    for raw_path in args.scenario_file:
        scenarios.extend(load_scenario_file(Path(raw_path)))
    if not scenarios:
        raise ValueError("at least one --scenario or --scenario-file is required")

    results: list[MatrixResult] = []
    for model in models:
        for scenario in scenarios:
            result = run_eval(args, model, scenario, output_dir)
            results.append(result)
            print(
                f"model={model.name} scenario={scenario.name} success={result.success} "
                f"criterion={result.success_criterion} raw_success={result.raw_terminal_success} "
                f"terminal={result.terminal_reason} ticks={result.ticks_elapsed} "
                f"pickup={result.pickup_tick} score={result.score_tick} capture={result.capture_tick} "
                f"min_nav={result.min_navigation_distance:.1f} final_nav={result.final_navigation_distance:.1f}"
            )
            if args.stop_on_failure and not result.success:
                raise SystemExit(1)

    summary = {
        "models": [asdict(model) for model in models],
        "scenarios": [asdict(scenario) for scenario in scenarios],
        "results": [asdict(result) for result in results],
    }
    with (output_dir / "matrix-summary.json").open("w", encoding="utf-8") as handle:
        json.dump(summary, handle, indent=2)


if __name__ == "__main__":
    main()
