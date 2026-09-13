"""Content-verified incremental browser assets and release archives.

The cache is outside wwwroot. A failed rebuild never marks its inputs current.
Only Content/ and Plugins/ are managed by the asset synchronizer.
"""
from contextlib import contextmanager
from pathlib import Path
import argparse
import hashlib
import json
import os
import shutil
import subprocess
import tempfile

GENERATED_ROOTS = ("Content", "Plugins")
REQUIRED_CONTENT = ("ConsoleFont.xnb", "MenuFont.xnb", "Grayscale.xnb", "FlamingLogo.xnb")
EXCLUDED_DIRECTORIES = {"bin", "obj", ".git", "node_modules", "__pycache__"}


def file_hash(path):
    with path.open("rb") as stream:
        return hashlib.file_digest(stream, "sha256").hexdigest()


def linked_path(path):
    return path.is_symlink() or getattr(path, "is_junction", lambda: False)()


def files_under(root, excluded=()):
    if not root.exists():
        return
    if linked_path(root):
        raise ValueError(f"Refusing linked build directory: {root}")
    for directory, children, names in os.walk(root):
        children[:] = sorted(name for name in children if name not in excluded)
        for name in children + names:
            path = Path(directory, name)
            if linked_path(path):
                raise ValueError(f"Refusing linked build path: {path}")
        for name in sorted(names):
            yield Path(directory, name)


def tree_hashes(root):
    return {path.relative_to(root).as_posix(): file_hash(path) for path in files_under(root)}


def write_if_changed(path, content):
    if path.is_file() and path.read_bytes() == content:
        return False
    path.parent.mkdir(parents=True, exist_ok=True)
    temporary = path.with_name(path.name + ".tmp")
    try:
        temporary.write_bytes(content)
        os.replace(temporary, path)
    finally:
        temporary.unlink(missing_ok=True)
    return True


def write_json(path, value):
    return write_if_changed(path, (json.dumps(value, indent=2, sort_keys=True) + "\n").encode())


def read_json(path):
    try:
        return json.loads(path.read_text(encoding="utf-8"))
    except (OSError, ValueError):
        return None


def contained_path(root, relative):
    candidate = root / relative
    resolved = candidate.resolve()
    if not resolved.is_relative_to(root.resolve()) or resolved == root.resolve():
        raise ValueError(f"Build path escapes its output root: {relative}")
    # Check the lexical path too: an internal link must not redirect cleanup.
    for parent in (candidate, *candidate.parents):
        if linked_path(parent):
            raise ValueError(f"Refusing linked build path: {parent}")
        if parent == root:
            break
    return candidate


@contextmanager
def cache_lock(cache):
    """Use an OS lock, released even when a build process is interrupted."""
    cache.mkdir(parents=True, exist_ok=True)
    with (cache / "build.lock").open("a+b") as lock:
        if os.fstat(lock.fileno()).st_size == 0:
            lock.write(b"\0")
            lock.flush()
        lock.seek(0)
        try:
            if os.name == "nt":
                import msvcrt
                msvcrt.locking(lock.fileno(), msvcrt.LK_NBLCK, 1)
            else:
                import fcntl
                fcntl.flock(lock, fcntl.LOCK_EX | fcntl.LOCK_NB)
        except OSError as error:
            raise RuntimeError(f"Another browser asset build is using {cache}") from error
        try:
            yield
        finally:
            lock.seek(0)
            if os.name == "nt":
                msvcrt.locking(lock.fileno(), msvcrt.LK_UNLCK, 1)
            else:
                fcntl.flock(lock, fcntl.LOCK_UN)


def asset_inputs(repo, sdk_version=""):
    inputs = {"format": 1, "sourceRoot": str(repo), "sdkVersion": sdk_version, "files": {}}
    # Include the builder's complete source dependency graph. Gameplay changes
    # in Core can affect catalogs/serialization, so invalidation is conservative.
    directories = (
        "Core", "Client.Shared", "Protocol", "Plugins/GameplayModding.Abstractions",
        "Tools/BrowserAssetBuilder", "Plugins/Packaged/Client", "Maps", "Docking",
    )
    for name in directories:
        for path in files_under(repo / name, EXCLUDED_DIRECTORIES):
            inputs["files"][path.relative_to(repo).as_posix()] = file_hash(path)
    for name in ("Directory.Build.props", "Directory.Build.targets", "Directory.Packages.props",
                 "global.json", "NuGet.Config", "nuget.config", "Tools/Browser/browser_build_cache.py"):
        path = repo / name
        if path.is_file():
            inputs["files"][name] = file_hash(path)
    bot_names = repo / "Client/practice-bot-names.txt"
    if bot_names.is_file():
        inputs["files"]["Client/practice-bot-names.txt"] = file_hash(bot_names)
    compiled = repo / "Client/Content/bin/DesktopGL/Content"
    for name in REQUIRED_CONTENT:
        path = compiled / name
        if not path.is_file() or path.read_bytes()[:3] != b"XNB":
            raise ValueError(f"Required compiled browser content is missing or invalid: {path}")
    for path in files_under(compiled):
        inputs["files"][path.relative_to(repo).as_posix()] = file_hash(path)
    return inputs


def generated_hashes(wwwroot):
    return {f"{name}/{relative}": digest for name in GENERATED_ROOTS
            for relative, digest in tree_hashes(wwwroot / name).items()}


def synchronize_tree(source, destination, expected):
    """Copy only changed bytes and prune obsolete files inside owned roots."""
    copied = deleted = 0
    for name, digest in expected.items():
        target = contained_path(destination, name)
        original = contained_path(source, name)
        if target.is_file() and file_hash(target) == digest:
            continue
        target.parent.mkdir(parents=True, exist_ok=True)
        shutil.copyfile(original, target)
        # Do not restore an old source timestamp: MSBuild must notice edits,
        # including same-size replacements and files restored from old exports.
        copied += 1
    for name in GENERATED_ROOTS:
        root = contained_path(destination, name)
        for target in list(files_under(root)):
            if target.relative_to(destination).as_posix() not in expected:
                target.unlink()
                deleted += 1
    return copied, deleted


def build_assets(repo, staging, cache):
    output = staging / "Content"
    for source in (repo / "Core/Content", repo / "Client/Content/bin/DesktopGL/Content"):
        for path in files_under(source, {"bin", "obj"}):
            if path.suffix.lower() in (".cs", ".mgcb"):
                continue
            target = contained_path(output, path.relative_to(source))
            target.parent.mkdir(parents=True, exist_ok=True)
            shutil.copy2(path, target)
    subprocess.run([
        "dotnet", "run", "--project", str(repo / "Tools/BrowserAssetBuilder/OpenGarrison.Tools.BrowserAssetBuilder.csproj"),
        f"-p:OpenGarrisonBuildRoot={cache / 'tool-build'}", "-p:RunAnalyzers=false", "--",
        str(output), str(repo / "Plugins/Packaged/Client"), f"--repo-root={repo}",
    ], cwd=repo, check=True)


def sync_assets(repo, wwwroot, cache, builder=build_assets, force=False, sdk_version=""):
    repo, wwwroot, cache = (Path(path).resolve() for path in (repo, wwwroot, cache))
    # Cache cleanup may only affect a separate, explicitly owned directory.
    if cache == wwwroot or cache.is_relative_to(wwwroot) or wwwroot.is_relative_to(cache):
        raise ValueError("Browser asset cache and wwwroot must be separate directories")
    state_path = cache / "assets.json"
    cached_root = cache / "wwwroot"
    with cache_lock(cache):
        inputs = asset_inputs(repo, sdk_version)
        previous = read_json(state_path)
        hit = (not force and isinstance(previous, dict) and previous.get("inputs") == inputs
               and isinstance(previous.get("outputs"), dict) and bool(previous["outputs"])
               and previous["outputs"] == generated_hashes(cached_root))
        if hit:
            outputs = previous["outputs"]
        else:
            print("Browser assets: inputs changed or cache needs repair; rebuilding.", flush=True)
            with tempfile.TemporaryDirectory(prefix="staging-", dir=cache) as temporary:
                staging = Path(temporary)
                builder(repo, staging, cache)
                if asset_inputs(repo, sdk_version) != inputs:
                    raise RuntimeError("Browser asset inputs changed during generation; rerun the build")
                outputs = generated_hashes(staging)
                if not outputs:
                    raise ValueError("Browser asset generation produced no files")
                # Invalidate first. Interrupted synchronization must not create a
                # cache hit next time; the prior published tree is still intact.
                state_path.unlink(missing_ok=True)
                synchronize_tree(staging, cached_root, outputs)
                write_json(state_path, {"inputs": inputs, "outputs": outputs})
        copied, deleted = synchronize_tree(cached_root, wwwroot, outputs)
        print(f"Browser assets: {'cache hit' if hit else 'cache refreshed'}; "
              f"{len(outputs)} verified, {copied} copied, {deleted} stale files removed.", flush=True)
        return {"cacheHit": hit, "files": len(outputs), "copied": copied, "deleted": deleted}


def prune_publish(wwwroot, inventory):
    root = Path(wwwroot).resolve()
    expected = set()
    for line in Path(inventory).read_text(encoding="utf-8-sig").splitlines():
        line = line.strip().replace("\\", "/")
        if not line.startswith("wwwroot/"):
            raise ValueError(f"Unexpected browser publish target: {line}")
        name = line.removeprefix("wwwroot/")
        contained_path(root, name)
        expected.add(name)
    if "index.html" not in expected or not any(name.endswith(".wasm") for name in expected):
        raise ValueError("Incomplete browser publish inventory; refusing to prune output")
    # These are added by package-web-edition.py after dotnet publish completes.
    expected.update(("release.json", "LICENSE"))
    removed = 0
    for path in list(files_under(root)):
        if path.relative_to(root).as_posix() not in expected:
            path.unlink()
            removed += 1
    print(f"Browser publish: removed {removed} obsolete files; retained current output.", flush=True)
    return removed


def cached_archive(root, archive, manifest, create):
    inputs = tree_hashes(root)
    previous = read_json(manifest)
    if (isinstance(previous, dict) and previous.get("inputs") == inputs and archive.is_file()
            and previous.get("archiveHash") == file_hash(archive)):
        print(f"Archive cache hit: {archive.name}", flush=True)
        return False
    temporary = archive.with_name(archive.name + ".tmp")
    try:
        create(temporary)
        if tree_hashes(root) != inputs:
            raise RuntimeError("Archive inputs changed during packaging")
        os.replace(temporary, archive)
        write_json(manifest, {"inputs": inputs, "archiveHash": file_hash(archive)})
    finally:
        temporary.unlink(missing_ok=True)
    return True


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    commands = parser.add_subparsers(dest="command", required=True)
    sync = commands.add_parser("sync-assets")
    sync.add_argument("--repo", required=True, type=Path)
    sync.add_argument("--wwwroot", required=True, type=Path)
    sync.add_argument("--cache", required=True, type=Path)
    sync.add_argument("--force", action="store_true")
    sync.add_argument("--sdk", default="")
    prune = commands.add_parser("prune-publish")
    prune.add_argument("--wwwroot", required=True, type=Path)
    prune.add_argument("--inventory", required=True, type=Path)
    args = parser.parse_args()
    if args.command == "sync-assets":
        sdk = args.sdk or subprocess.check_output(["dotnet", "--version"], cwd=args.repo, text=True).strip()
        sync_assets(args.repo, args.wwwroot, args.cache, force=args.force, sdk_version=sdk)
    else:
        prune_publish(args.wwwroot, args.inventory)


if __name__ == "__main__":
    main()
