"""Build the restricted AOT website and lightweight player-hosted room API update."""
from pathlib import Path
import argparse
import hashlib
import json
import os
import re
import shutil
import subprocess
import tarfile
import zipfile
from browser_build_cache import cached_archive, write_if_changed, write_json

REPO = Path(__file__).resolve().parents[2]


def run(*args):
    print("Running:", *args, flush=True)
    subprocess.run([str(arg) for arg in args], cwd=REPO, check=True)


def fingerprint():
    digest = hashlib.sha256()
    files = []
    for directory in ("Core", "Protocol"):
        for path in source_files(REPO / directory, {"bin", "obj"}):
            if "Content" in path.parts or path.suffix == ".cs":
                files.append(path)
    for path in sorted(files):
        digest.update(path.relative_to(REPO).as_posix().encode())
        digest.update(b"\0")
        digest.update(hashlib.sha256(path.read_bytes()).digest())
    return digest.hexdigest()


def source_files(root, excluded):
    for directory, children, names in os.walk(root):
        children[:] = sorted(child for child in children if child not in excluded)
        for name in sorted(names):
            yield Path(directory) / name


def source_fingerprint():
    digest = hashlib.sha256()
    for name in ("Directory.Build.props", "Directory.Build.targets", "global.json", "NuGet.Config", "nuget.config"):
        path = REPO / name
        if path.is_file():
            digest.update(name.encode() + b"\0" + hashlib.sha256(path.read_bytes()).digest())
    for directory in ("Client", "Client.Shared", "Client.Browser", "Core", "Protocol", "Networking", "Server", "SessionRuntime", "Plugins", "Tools/Browser", "Tools/BrowserAssetBuilder", "services/opengarrison-api"):
        for path in source_files(REPO / directory, {"bin", "obj", "Content", "Packaged", ".venv", "__pycache__", "updates", "deploy"}):
            relative = path.relative_to(REPO)
            if relative.as_posix().startswith("Client.Browser/wwwroot/Plugins/"):
                continue
            if path.suffix not in (".cs", ".csproj", ".props", ".targets", ".config", ".js", ".html", ".css", ".lua", ".json", ".py", ".ps1", ".txt"):
                continue
            digest.update(relative.as_posix().encode() + b"\0")
            digest.update(hashlib.sha256(path.read_bytes()).digest())
    return digest.hexdigest()


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--version", default="0.7.6-browser.20260905")
    parser.add_argument("--output", default="artifacts/browser-practice-ltd")
    parser.add_argument("--artifact-root", default=str(REPO / "artifacts"))
    parser.add_argument("--build-root", default="", help="Persistent compiler/asset cache; defaults to the publish script's environment/local configuration or .build/browser-cache")
    parser.add_argument("--service-origin", default="https://api.superganggarrison.com")
    parser.add_argument("--skip-browser", action="store_true", help="Repackage an already verified matching AOT publish")
    args = parser.parse_args()
    root = (REPO / args.output).resolve()
    artifacts = Path(args.artifact_root).resolve()
    if not root.is_relative_to(artifacts) or root == artifacts:
        raise ValueError("Output must be a child of the specified artifact root")
    root.mkdir(parents=True, exist_ok=True)
    content_id = fingerprint()
    protocol = int(re.search(r"Current\s*=\s*(\d+)", (REPO / "Protocol/ProtocolVersion.cs").read_text())[1])
    release = dict(edition="PracticeAndLastToDie", aot=True, buildVersion=args.version,
                   protocolVersion=protocol, contentId=content_id, roomServiceOrigin=args.service_origin,
                   roomModel="player-hosted", maximumRoomPlayers=4, sourceId=source_fingerprint())
    backend = root / "backend"
    backend.mkdir(parents=True, exist_ok=True)
    api = backend / "api"
    api.mkdir(exist_ok=True)
    for name in ("app.py", "reward_authority.py", "run_verification.py", "run_verification_worker.py", "private_rooms.py", "peer_rooms.py", "private_room_store.py", "room_worker.py", "requirements.txt"):
        shutil.copy2(REPO / "services/opengarrison-api" / name, api / name)
    shutil.copytree(REPO / "services/opengarrison-api/deploy/browser-edition", backend / "deploy", dirs_exist_ok=True)
    shutil.copy2(REPO / "LICENSE", backend / "LICENSE")
    write_json(backend / "room-release.json", release)
    browser = root / "browser-publish"
    if not args.skip_browser:
        run("pwsh", "-NoProfile", "-File", REPO / "Tools/Browser/publish-browser.ps1",
            "-Output", browser, "-ArtifactRoot", artifacts,
            "-BuildRoot", args.build_root, "-Version", args.version, "-Edition", "PracticeAndLastToDie",
            "-ContentId", content_id, "-RoomServiceOrigin", args.service_origin)
        write_json(browser / "wwwroot/release.json", release)
    elif json.loads((browser / "wwwroot/release.json").read_text()) != release:
        raise ValueError("Existing AOT publish does not match current release inputs")
    run("python", REPO / "Tools/Browser/verify-map-bundle.py", browser / "wwwroot")
    shutil.copy2(REPO / "LICENSE", browser / "wwwroot/LICENSE")
    shutil.copy2(REPO / "services/opengarrison-api/deploy/browser-edition/README.md", root / "DEPLOYMENT.md")
    if source_fingerprint() != release["sourceId"] or fingerprint() != content_id:
        raise ValueError("Release inputs changed during the build; rebuild before packaging")
    archive_cache = root / ".package-cache"
    def create_website_archive(destination):
        with zipfile.ZipFile(destination, "w", zipfile.ZIP_DEFLATED, compresslevel=6) as archive:
            for path in sorted((browser / "wwwroot").rglob("*")):
                if path.is_file():
                    archive.write(path, path.relative_to(browser / "wwwroot").as_posix())
    cached_archive(browser / "wwwroot", root / "superganggarrison-browser-aot.zip",
                   archive_cache / "website.json", create_website_archive)
    def unix_mode(info):
        info.mode = 0o755 if info.isdir() or info.name.endswith("/server/OG2.Server") or info.name.endswith(".sh") else 0o644
        info.uid = info.gid = 0
        info.uname = info.gname = "root"
        return info
    def create_api_archive(destination):
        with tarfile.open(destination, "w:gz") as archive:
            archive.add(backend, arcname="opengarrison-rooms", filter=unix_mode)
    cached_archive(backend, root / "superganggarrison-room-api-update.tar.gz",
                   archive_cache / "api.json", create_api_archive)
    hashes = []
    for path in sorted(root.glob("superganggarrison-*")):
        hashes.append(f"{hashlib.sha256(path.read_bytes()).hexdigest()}  {path.name}")
    write_if_changed(root / "SHA256SUMS.txt", ("\n".join(hashes) + "\n").encode())
    write_json(root / "release.json", release)
    print(f"Upload-ready artifacts: {root}", flush=True)


if __name__ == "__main__":
    main()
