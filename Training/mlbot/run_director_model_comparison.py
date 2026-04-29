from __future__ import annotations

import argparse
import json
import subprocess
import sys
from pathlib import Path
from typing import Any


DEFAULT_MODEL_SUITE = Path("Training/mlbot/config/director_model_suite.json")
DEFAULT_LEGACY_MATRIX = Path("Training/mlbot/config/director_legacy_scout_matrix.json")
DEFAULT_V5_MATRIX = Path("Training/mlbot/config/v5_regression_matrix.json")


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description="Run the director old-vs-V5 MLBot comparison suite.")
    parser.add_argument("--rollout-project", default="MLBot.Tools/OpenGarrison.MLBot.Tools.csproj")
    parser.add_argument("--model-suite", default=str(DEFAULT_MODEL_SUITE))
    parser.add_argument("--legacy-matrix", default=str(DEFAULT_LEGACY_MATRIX))
    parser.add_argument("--v5-matrix", default=str(DEFAULT_V5_MATRIX))
    parser.add_argument("--output-dir", required=True)
    parser.add_argument("--rollout-no-build", action="store_true")
    parser.add_argument("--include-overrides-enabled", action="store_true")
    return parser.parse_args()


def load_json(path: Path) -> dict[str, Any]:
    with path.open("r", encoding="utf-8") as handle:
        payload = json.load(handle)
    if not isinstance(payload, dict):
        raise ValueError(f"expected JSON object: {path}")
    return payload


def load_models(path: Path) -> list[dict[str, str]]:
    payload = load_json(path)
    models: list[dict[str, str]] = []
    for item in payload.get("models", []):
        name = str(item["name"])
        model_path = str(item["path"])
        if not Path(model_path).exists():
            raise FileNotFoundError(f"model not found for {name}: {model_path}")
        models.append({"name": name, "path": model_path})
    if not models:
        raise ValueError(f"model suite has no models: {path}")
    return models


def run_matrix(
    args: argparse.Namespace,
    models: list[dict[str, str]],
    matrix_path: Path,
    output_dir: Path,
    disable_policy_overrides: bool,
) -> Path:
    output_dir.mkdir(parents=True, exist_ok=True)
    command = [
        sys.executable,
        str(Path(__file__).with_name("evaluate_policy_matrix.py")),
        "--rollout-project",
        args.rollout_project,
        "--output-dir",
        str(output_dir),
        "--scenario-file",
        str(matrix_path),
    ]
    if args.rollout_no_build:
        command.append("--rollout-no-build")
    if disable_policy_overrides:
        command.append("--disable-policy-overrides")
    for model in models:
        command.extend(["--model", f"{model['name']}={model['path']}"])

    completed = subprocess.run(command, check=False, text=True, encoding="utf-8")
    if completed.returncode != 0:
        raise RuntimeError(f"matrix failed with exit code {completed.returncode}: {' '.join(command)}")
    return output_dir / "matrix-summary.json"


def summarize_matrix(summary_path: Path) -> dict[str, Any]:
    payload = load_json(summary_path)
    results = payload.get("results", [])
    by_model: dict[str, dict[str, Any]] = {}
    for item in results:
        model_name = item["model"]["name"]
        model_summary = by_model.setdefault(
            model_name,
            {
                "successes": 0,
                "total": 0,
                "scenario_results": [],
            },
        )
        model_summary["total"] += 1
        if item.get("success"):
            model_summary["successes"] += 1
        model_summary["scenario_results"].append(item)
    return {
        "summary_path": str(summary_path),
        "by_model": by_model,
    }


def write_markdown_report(
    report_path: Path,
    sections: list[tuple[str, bool, dict[str, Any]]],
) -> None:
    lines: list[str] = [
        "# Director MLBot Model Comparison",
        "",
        "This report separates legacy Scout evidence from the current V5 world-truth matrix.",
        "A success in AttackIntel means intel pickup. ReturnIntel requires a score. CaptureObjective requires a capture.",
        "",
        "Important caveat: historical `start_node_id` values are preserved in the legacy matrix config, but current `MLBot.Environment` does not apply `StartNodeId` to episode placement. Use this report for current-code behavior, not as a byte-for-byte replay of old matrices.",
        "",
    ]

    for title, overrides_disabled, section in sections:
        lines.append(f"## {title}")
        lines.append("")
        lines.append(f"Policy overrides: {'disabled' if overrides_disabled else 'enabled'}")
        lines.append("")
        lines.append("| Model | Successes | Failed Scenarios |")
        lines.append("|---|---:|---|")
        for model_name, model_summary in sorted(section["by_model"].items()):
            total = int(model_summary["total"])
            successes = int(model_summary["successes"])
            failed = [
                result["scenario"]["name"]
                for result in model_summary["scenario_results"]
                if not result.get("success")
            ]
            failed_text = ", ".join(failed) if failed else "-"
            lines.append(f"| `{model_name}` | {successes}/{total} | {failed_text} |")
        lines.append("")
        lines.append("<details>")
        lines.append("<summary>Scenario Details</summary>")
        lines.append("")
        lines.append("| Model | Scenario | Success | Terminal | Pickup | Score | Capture | Min Nav | Final Nav |")
        lines.append("|---|---|---:|---|---:|---:|---:|---:|---:|")
        for model_name, model_summary in sorted(section["by_model"].items()):
            for result in model_summary["scenario_results"]:
                scenario_name = result["scenario"]["name"]
                lines.append(
                    f"| `{model_name}` | `{scenario_name}` | {str(bool(result.get('success'))).lower()} "
                    f"| {result.get('terminal_reason')} "
                    f"| {format_optional(result.get('pickup_tick'))} "
                    f"| {format_optional(result.get('score_tick'))} "
                    f"| {format_optional(result.get('capture_tick'))} "
                    f"| {float(result.get('min_navigation_distance', 0.0)):.1f} "
                    f"| {float(result.get('final_navigation_distance', 0.0)):.1f} |"
                )
        lines.append("")
        lines.append("</details>")
        lines.append("")
        lines.append(f"Raw summary: `{section['summary_path']}`")
        lines.append("")

    report_path.parent.mkdir(parents=True, exist_ok=True)
    report_path.write_text("\n".join(lines) + "\n", encoding="utf-8")


def format_optional(value: Any) -> str:
    return "" if value is None else str(value)


def main() -> None:
    args = parse_args()
    output_root = Path(args.output_dir)
    models = load_models(Path(args.model_suite))

    sections: list[tuple[str, bool, dict[str, Any]]] = []
    runs = [
        ("Legacy Scout Matrix", Path(args.legacy_matrix), True, "legacy_overrides_disabled"),
        ("V5 World-Truth Matrix", Path(args.v5_matrix), True, "v5_overrides_disabled"),
    ]
    if args.include_overrides_enabled:
        runs.extend(
            [
                ("Legacy Scout Matrix", Path(args.legacy_matrix), False, "legacy_overrides_enabled"),
                ("V5 World-Truth Matrix", Path(args.v5_matrix), False, "v5_overrides_enabled"),
            ]
        )

    for title, matrix_path, disable_overrides, run_name in runs:
        summary_path = run_matrix(
            args,
            models,
            matrix_path,
            output_root / run_name,
            disable_policy_overrides=disable_overrides,
        )
        sections.append((title, disable_overrides, summarize_matrix(summary_path)))

    report_path = output_root / "director-comparison-report.md"
    write_markdown_report(report_path, sections)
    print(f"saved report={report_path}")


if __name__ == "__main__":
    main()
