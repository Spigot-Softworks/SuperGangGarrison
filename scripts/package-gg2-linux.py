"""Publish the GG2-only OpenGarrison Linux client as a self-contained tarball.

Run from any directory: python scripts/package-gg2-linux.py
The output is dist/OpenGarrison-GG2-linux-x64.tar.gz.
"""

from __future__ import annotations

import os
import argparse
from pathlib import Path
import shutil
import subprocess
import tarfile


REPO = Path(__file__).resolve().parent.parent
DIST = (REPO / "dist").resolve()
PAYLOAD = (DIST / "OpenGarrison-GG2-linux-x64").resolve()
APP = PAYLOAD / "app"
ARCHIVE = (DIST / "OpenGarrison-GG2-linux-x64.tar.gz").resolve()
NAVIGATION_DIRECTORIES = (
    "BotBrainNav",
    "BotBrainCorridors",
    "BotBrainOg2Nav",
    "BotBrainProofGraphs",
    "BotBrainTapes",
    "BotNavScoreRoutes",
    "TraversalLab",
)
BROWSER_ONLY_FILES = (
    "_browser-bootstrap-assets.zip",
    "_browser-runtime-assets.zip",
    "_browser-pack-assets.zip",
    "_browser-pack-definition.json",
)
RUNTIME_DIAGNOSTICS = ("createdump", "libmscordaccore.so", "libmscordbi.so")


def checked_output_path(path: Path) -> Path:
    resolved = path.resolve()
    if resolved == DIST or DIST not in resolved.parents:
        raise RuntimeError(f"Refusing to modify a path outside the package directory: {resolved}")
    return resolved


def remove_old_output(path: Path) -> None:
    path = checked_output_path(path)
    if path.is_dir():
        shutil.rmtree(path)
    elif path.exists():
        path.unlink()


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--reuse-publish", action="store_true", help="Package the existing publish output")
    args = parser.parse_args()
    DIST.mkdir(parents=True, exist_ok=True)
    if not args.reuse_publish:
        remove_old_output(PAYLOAD)
    remove_old_output(ARCHIVE)
    if not args.reuse_publish:
        APP.mkdir(parents=True)
    elif (PAYLOAD / "OpenGarrison.dll").is_file():
        # Upgrade an existing flat publish directory without publishing again.
        APP.mkdir(exist_ok=True)
        for path in PAYLOAD.iterdir():
            if path.name not in {"app", "README.txt"}:
                shutil.move(str(path), str(APP / path.name))

    command = [
        "dotnet", "publish", str(REPO / "Client/OpenGarrison.Client.csproj"),
        "-c", "Gg2Linux", "-r", "linux-x64", "--self-contained", "true",
        "-p:OpenGarrisonGg2Only=true", "-p:EnableMGCBItems=true",
        "-p:RunAnalyzers=false", "-p:UseSharedCompilation=false",
        "-o", str(APP), "-v:q",
    ]
    if not args.reuse_publish:
        print("Publishing GG2-only OpenGarrison for linux-x64...", flush=True)
        subprocess.run(command, cwd=REPO, check=True)

    content = APP / "Content"
    for name in NAVIGATION_DIRECTORIES:
        remove_old_output(content / name)
    removed_bytes = 0
    for name in BROWSER_ONLY_FILES:
        for path in content.rglob(name):
            removed_bytes += path.stat().st_size
            remove_old_output(path)
    for path in APP.rglob("*.pdb"):
        removed_bytes += path.stat().st_size
        remove_old_output(path)
    for name in RUNTIME_DIAGNOSTICS:
        path = APP / name
        if path.is_file():
            removed_bytes += path.stat().st_size
            remove_old_output(path)
    print(f"Removed {removed_bytes / (1024 * 1024):.1f} MiB of browser and debug-only files", flush=True)

    required = (
        APP / "OpenGarrison",
        APP / "OpenGarrison.dll",
        APP / "OpenGarrison.runtimeconfig.json",
        content / "ConsoleFont.xnb",
        content / "MenuFont.xnb",
        content / "Grayscale.xnb",
        content / "FlamingLogo.xnb",
        content / "Browser/Manifests/gamemaker-atlas-manifest.json",
        content / "Gameplay/stock.gg2/classes/scout.json",
    )
    missing = [str(path) for path in required if not path.is_file()]
    if missing:
        raise RuntimeError(f"GG2 package is missing required files: {missing}")
    if '"OpenGarrisonLogoS"' not in (content / "Browser/Manifests/gamemaker-atlas-manifest.json").read_text(encoding="utf-8"):
        raise RuntimeError("GG2 logo is missing from the packaged runtime atlas")

    native_names = {path.name.lower() for path in APP.rglob("*.so*") if path.is_file()}
    if not any("sdl2" in name for name in native_names):
        raise RuntimeError("Linux SDL2 native dependency is missing from the publish output")
    if not any("openal" in name for name in native_names):
        raise RuntimeError("Linux OpenAL native dependency is missing from the publish output")

    launcher = PAYLOAD / "OpenGarrison"
    launcher.write_text(
        "#!/bin/sh\n"
        "set -eu\n"
        "SCRIPT_DIR=$(CDPATH= cd -- \"$(dirname -- \"$0\")\" && pwd)\n"
        "cd \"$SCRIPT_DIR/app\"\n"
        "if [ -n \"${LD_LIBRARY_PATH:-}\" ]; then\n"
        "    export LD_LIBRARY_PATH=\"$PWD:$LD_LIBRARY_PATH\"\n"
        "else\n"
        "    export LD_LIBRARY_PATH=\"$PWD\"\n"
        "fi\n"
        "exec ./OpenGarrison \"$@\"\n",
        encoding="utf-8",
        newline="\n",
    )
    launcher.chmod(0o755)
    (APP / "OpenGarrison").chmod(0o755)

    readme = PAYLOAD / "README.txt"
    readme.write_text(
        "OpenGarrison GG2-only client for Linux x64\n"
        "\n"
        "Extract this archive, then run ./OpenGarrison from its top-level directory.\n"
        "Game files and bundled dependencies are in the app directory.\n"
        "The .NET runtime, SDL2, and OpenAL are included. A working graphics\n"
        "driver and display/audio system are still required on the host.\n"
        "Play opens the public GG2 server list; this edition cannot join SGG servers.\n",
        encoding="utf-8",
    )

    with tarfile.open(ARCHIVE, "w:gz") as tar:
        for path in sorted((PAYLOAD, *PAYLOAD.rglob("*"))):
            relative = path.relative_to(PAYLOAD)
            arcname = Path("OpenGarrison") / relative
            info = tar.gettarinfo(str(path), arcname=str(arcname).replace(os.sep, "/"))
            info.uid = info.gid = 0
            info.uname = info.gname = "root"
            info.mode = 0o755 if path.is_dir() or path in {launcher, APP / "OpenGarrison"} else 0o644
            if path.is_file():
                with path.open("rb") as source:
                    tar.addfile(info, source)
            else:
                tar.addfile(info)

    print(f"Created {ARCHIVE} ({ARCHIVE.stat().st_size / (1024 * 1024):.1f} MiB)")


if __name__ == "__main__":
    main()
