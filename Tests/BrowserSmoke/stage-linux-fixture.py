"""Stage a release on Linux storage for representative local room smoke tests."""
import argparse
import json
from pathlib import Path
import subprocess
import tarfile
import tempfile

parser = argparse.ArgumentParser(description=__doc__)
parser.add_argument("--backend", required=True)
parser.add_argument("--destination", required=True)
args = parser.parse_args()
backend = Path(args.backend).resolve()
if not (backend / "room-release.json").is_file():
    raise ValueError("A completed backend release is required")
if not args.destination.startswith("/var/tmp/og2-browser-release-") or ".." in args.destination:
    raise ValueError("Use an isolated /var/tmp/og2-browser-release-* directory")
with tempfile.TemporaryDirectory(prefix="og2-native-stage-") as temporary:
    archive = Path(temporary) / "backend.tar"
    with tarfile.open(archive, "w") as output:
        output.add(backend, arcname="backend")
    linux_archive = subprocess.check_output(["wsl", "-d", "Ubuntu", "--exec", "wslpath", "-a", archive.as_posix()], text=True).strip()
    script = """import os, pathlib, sys, tarfile
destination=pathlib.Path(sys.argv[2])
destination.mkdir(exist_ok=False)
with tarfile.open(sys.argv[1]) as archive: archive.extractall(destination, filter='data')
os.chmod(destination / 'backend/server/OG2.Server', 0o755)
print(destination / 'backend/room-release.json')
"""
    subprocess.run(["wsl", "-d", "Ubuntu", "--exec", "python3", "-", linux_archive, args.destination], input=script, text=True, check=True)
