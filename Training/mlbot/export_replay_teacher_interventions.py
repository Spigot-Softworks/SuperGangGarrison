from __future__ import annotations

import argparse
import json
import subprocess
from pathlib import Path
from typing import Any

from evaluate_policy_matrix import ScenarioSpec, load_scenario_file


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(
        description="Export verified DAgger intervention rollouts using a teacher policy with optional replay or chunk options."
    )
    parser.add_argument("--rollout-project", required=True)
    parser.add_argument("--scenario-file", action="append", required=True)
    parser.add_argument("--output-dir", required=True)
    parser.add_argument("--student-model", required=True)
    parser.add_argument("--teacher-model", required=True)
    parser.add_argument("--teacher-return-replay-bank", default="")
    parser.add_argument("--teacher-return-replay-engage-distance", type=float, default=3000.0)
    parser.add_argument("--teacher-return-replay-max-score", type=float, default=2.0)
    parser.add_argument("--teacher-replay-bank", default="")
    parser.add_argument("--teacher-replay-task", default="ReturnIntel")
    parser.add_argument("--teacher-replay-engage-distance", type=float, default=3000.0)
    parser.add_argument("--teacher-replay-max-score", type=float, default=2.0)
    parser.add_argument(
        "--teacher-replay-requires-carrying-intel",
        action=argparse.BooleanOptionalAction,
        default=True,
    )
    parser.add_argument("--teacher-return-finalizer-model", default="")
    parser.add_argument("--teacher-return-finalizer-engage-distance", type=float, default=0.0)
    parser.add_argument("--teacher-return-finalizer-map", default="")
    parser.add_argument("--teacher-return-finalizer-team", default="")
    parser.add_argument("--teacher-return-finalizer-class", default="")
    parser.add_argument("--teacher-return-finalizer-after-options", action="store_true")
    parser.add_argument("--teacher-return-chunk-model", default="")
    parser.add_argument("--teacher-return-chunk-engage-distance", type=float, default=0.0)
    parser.add_argument("--teacher-return-chunk-commit-ticks", type=int, default=0)
    parser.add_argument(
        "--teacher-return-chunk-spec",
        action="append",
        default=[],
        help=(
            "Chunk option as model|engage_distance|commit_ticks|map_filter|team_filter|class_filter"
            "[|min_rel_x|max_rel_x|min_rel_y|max_rel_y]. Later specs wrap earlier specs."
        ),
    )
    parser.add_argument("--teacher-return-chunk-selector-model", default="")
    parser.add_argument(
        "--teacher-return-selected-chunk-spec",
        action="append",
        default=[],
        help="Selector chunk option as model|commit_ticks. Option 0 is the base policy; options 1..N follow this order.",
    )
    parser.add_argument("--teacher-return-hierarchical-chunk-model", default="")
    parser.add_argument("--teacher-return-hierarchical-chunk-commit-ticks", default="")
    parser.add_argument(
        "--teacher-task-option-spec",
        action="append",
        default=[],
        help="Teacher task option as model|phase|engage_distance|map_filter|team_filter|class_filter. Later specs wrap earlier specs.",
    )
    parser.add_argument("--rollout-no-build", action="store_true")
    parser.add_argument("--disable-policy-overrides", action="store_true")
    parser.add_argument("--include-attack", action="store_true")
    parser.add_argument("--intervention-horizon", type=int, default=3600)
    parser.add_argument("--pre-intervention-label-window", type=int, default=120)
    parser.add_argument("--trigger-navigation-distance", type=float, default=3000.0)
    parser.add_argument("--trigger-min-tick-padding", type=int, default=0)
    parser.add_argument(
        "--trigger-requires-carrying-intel",
        action=argparse.BooleanOptionalAction,
        default=True,
        help="Wait until the student is carrying intel before invoking the replay teacher.",
    )
    parser.add_argument("--buffer-risk-only", action="store_true")
    return parser.parse_args()


def scenario_is_replay_relevant(scenario: ScenarioSpec, include_attack: bool) -> bool:
    criterion = scenario.success_criterion.strip().lower().replace("-", "_")
    task = scenario.task.strip().lower()
    if include_attack and criterion in {"attack_pickup", "pickup", "intel_pickup"}:
        return True
    return task == "returnintel" or criterion in {"return_score", "score", "scored", "full_score", "ctf_score", "attack_return_score"}


def safe_name(value: str) -> str:
    return "".join(ch.lower() if ch.isalnum() else "_" for ch in value).strip("_")


def append_start_options(command: list[str], scenario: ScenarioSpec) -> None:
    if scenario.start_node_id >= 0:
        command.extend(["--start-node-id", str(scenario.start_node_id)])
    if scenario.start_x is not None and scenario.start_y is not None:
        command.extend(["--start-x", str(scenario.start_x), "--start-y", str(scenario.start_y)])
    if scenario.start_vx is not None and scenario.start_vy is not None:
        command.extend(["--start-vx", str(scenario.start_vx), "--start-vy", str(scenario.start_vy)])
    if scenario.carrying_intel is True:
        command.append("--carrying-intel")
    elif scenario.carrying_intel is False:
        command.append("--no-carrying-intel")


def infer_trigger_min_tick(scenario: ScenarioSpec, padding: int) -> int:
    if scenario.task.strip().lower() == "none" and scenario.success_criterion.strip().lower().replace("-", "_") in {
        "full_score",
        "ctf_score",
        "attack_return_score",
    }:
        # Let the base attack policy reach the enemy intel before switching to the return teacher.
        return max(0, padding)
    return 0


def run_intervention(args: argparse.Namespace, scenario: ScenarioSpec, output_path: Path) -> dict[str, Any]:
    command = ["dotnet", "run", "--project", args.rollout_project]
    if args.rollout_no_build:
        command.append("--no-build")
    command.extend(
        [
            "--",
            "export-intervention-rollout",
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
            "--student-model",
            args.student_model,
            "--teacher-model",
            args.teacher_model,
            "--allow-matching-action-trigger",
            "--trigger-navigation-distance",
            str(args.trigger_navigation_distance),
            "--trigger-regression-after-progress",
            "0",
            "--trigger-waypoint-abs-y",
            "0",
            "--trigger-min-tick",
            str(infer_trigger_min_tick(scenario, args.trigger_min_tick_padding)),
            "--intervention-horizon",
            str(args.intervention_horizon),
            "--pre-intervention-label-window",
            str(args.pre_intervention_label_window),
            "--include-teacher-recovery-steps",
            "--require-terminal-success",
            "--out",
            str(output_path),
        ]
    )
    if args.teacher_return_replay_bank:
        command.extend(
            [
                "--teacher-return-replay-bank",
                args.teacher_return_replay_bank,
                "--teacher-return-replay-engage-distance",
                str(args.teacher_return_replay_engage_distance),
                "--teacher-return-replay-max-score",
                str(args.teacher_return_replay_max_score),
            ]
        )
    if args.teacher_replay_bank:
        command.extend(
            [
                "--teacher-replay-bank",
                args.teacher_replay_bank,
                "--teacher-replay-task",
                args.teacher_replay_task,
                "--teacher-replay-engage-distance",
                str(args.teacher_replay_engage_distance),
                "--teacher-replay-max-score",
                str(args.teacher_replay_max_score),
            ]
        )
        if not args.teacher_replay_requires_carrying_intel:
            command.append("--teacher-no-replay-requires-carrying-intel")
    if args.teacher_return_finalizer_model:
        command.extend(["--teacher-return-finalizer-model", args.teacher_return_finalizer_model])
        command.extend(["--teacher-return-finalizer-engage-distance", str(args.teacher_return_finalizer_engage_distance)])
        if args.teacher_return_finalizer_map:
            command.extend(["--teacher-return-finalizer-map", args.teacher_return_finalizer_map])
        if args.teacher_return_finalizer_team:
            command.extend(["--teacher-return-finalizer-team", args.teacher_return_finalizer_team])
        if args.teacher_return_finalizer_class:
            command.extend(["--teacher-return-finalizer-class", args.teacher_return_finalizer_class])
        if args.teacher_return_finalizer_after_options:
            command.append("--teacher-return-finalizer-after-options")
    if args.teacher_return_chunk_model:
        command.extend(["--teacher-return-chunk-model", args.teacher_return_chunk_model])
        command.extend(["--teacher-return-chunk-engage-distance", str(args.teacher_return_chunk_engage_distance)])
        if args.teacher_return_chunk_commit_ticks > 0:
            command.extend(["--teacher-return-chunk-commit-ticks", str(args.teacher_return_chunk_commit_ticks)])
    for spec in args.teacher_return_chunk_spec:
        command.extend(["--teacher-return-chunk-spec", spec])
    if args.teacher_return_chunk_selector_model:
        command.extend(["--teacher-return-chunk-selector-model", args.teacher_return_chunk_selector_model])
    for spec in args.teacher_return_selected_chunk_spec:
        command.extend(["--teacher-return-selected-chunk-spec", spec])
    if args.teacher_return_hierarchical_chunk_model:
        command.extend(["--teacher-return-hierarchical-chunk-model", args.teacher_return_hierarchical_chunk_model])
    if args.teacher_return_hierarchical_chunk_commit_ticks:
        command.extend(["--teacher-return-hierarchical-chunk-commit-ticks", args.teacher_return_hierarchical_chunk_commit_ticks])
    for spec in args.teacher_task_option_spec:
        command.extend(["--teacher-task-option-spec", spec])
    append_start_options(command, scenario)
    if args.disable_policy_overrides:
        command.append("--disable-policy-overrides")
    if args.trigger_requires_carrying_intel:
        command.append("--trigger-requires-carrying-intel")
    if args.buffer_risk_only:
        command.append("--buffer-risk-only")

    completed = subprocess.run(command, check=False, capture_output=True, text=True, encoding="utf-8")
    if completed.returncode != 0:
        raise RuntimeError(
            "intervention export failed\n"
            f"scenario={scenario.name}\n"
            f"command={' '.join(command)}\nstdout={completed.stdout}\nstderr={completed.stderr}"
        )

    with output_path.open("r", encoding="utf-8") as handle:
        payload: dict[str, Any] = json.load(handle)
    return payload


def main() -> None:
    args = parse_args()
    output_dir = Path(args.output_dir)
    output_dir.mkdir(parents=True, exist_ok=True)
    scenarios: list[ScenarioSpec] = []
    for raw_path in args.scenario_file:
        scenarios.extend(load_scenario_file(Path(raw_path)))

    summary: list[dict[str, Any]] = []
    for scenario in scenarios:
        if not scenario_is_replay_relevant(scenario, args.include_attack):
            summary.append({"scenario": scenario.name, "included": False, "reason": "not_replay_relevant"})
            continue

        output_path = output_dir / f"{safe_name(scenario.name)}.json"
        payload = run_intervention(args, scenario, output_path)
        summary.append(
            {
                "scenario": scenario.name,
                "included": True,
                "path": str(output_path),
                "success": bool(payload.get("Success", False)),
                "terminal_reason": payload.get("TerminalReason", ""),
                "steps": len(payload.get("Steps", [])),
                "intervention_tick": payload.get("InterventionTick"),
                "teacher_recovery_steps": payload.get("TeacherRecoveryStepCount", 0),
            }
        )
        print(
            f"scenario={scenario.name} success={summary[-1]['success']} "
            f"terminal={summary[-1]['terminal_reason']} steps={summary[-1]['steps']}"
        )

    with (output_dir / "intervention-export-summary.json").open("w", encoding="utf-8") as handle:
        json.dump(summary, handle, indent=2)
    print(f"saved summary={output_dir / 'intervention-export-summary.json'}")


if __name__ == "__main__":
    main()
