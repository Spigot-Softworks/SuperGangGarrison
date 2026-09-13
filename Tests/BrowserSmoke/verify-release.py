"""Verify transfer archives, matching room metadata and safe archive layout."""
import argparse
import hashlib
import gzip
import io
import json
from pathlib import Path, PurePosixPath
import tarfile
import zipfile

parser = argparse.ArgumentParser(description=__doc__)
parser.add_argument("directory")
args = parser.parse_args()
root = Path(args.directory).resolve()
for line in (root / "SHA256SUMS.txt").read_text().splitlines():
    expected, name = line.split(maxsplit=1)
    path = (root / name.strip().lstrip("*")).resolve()
    assert path.is_relative_to(root) and path != root
    assert hashlib.file_digest(path.open("rb"), "sha256").hexdigest() == expected, path.name
with zipfile.ZipFile(root / "superganggarrison-browser-aot.zip") as archive:
    assert archive.testzip() is None
    for name in archive.namelist():
        path = PurePosixPath(name)
        assert not path.is_absolute() and ".." not in path.parts
        assert "backend" not in path.parts and not name.endswith(".env")
    web = json.loads(archive.read("release.json"))
    assert web["aot"] and web["edition"] == "PracticeAndLastToDie"
    assert web["roomServiceOrigin"] == "https://api.superganggarrison.com"
    assert archive.read("index.html") == (root / "browser-publish/wwwroot/index.html").read_bytes()
    # The browser client uses the compiled fonts/effect even though its C#
    # library build does not invoke the desktop content compiler.
    for required in ("ConsoleFont.xnb", "MenuFont.xnb", "Grayscale.xnb", "FlamingLogo.xnb"):
        content = archive.read("Content/" + required)
        assert content.startswith(b"XNB"), f"Missing or invalid compiled content: {required}"
        assert content == (root / "browser-publish/wwwroot/Content" / required).read_bytes()
    native_wasm = [entry for entry in archive.infolist() if entry.filename.startswith("_framework/dotnet.native.") and entry.filename.endswith(".wasm")]
    assert len(native_wasm) == 1 and native_wasm[0].file_size > 10_000_000, "AOT native code is missing"
    with zipfile.ZipFile(io.BytesIO(archive.read("Content/_browser-runtime-assets.zip"))) as runtime:
        graphs = [name for name in runtime.namelist() if name.endswith(".og2nav.bin")]
        assert len(graphs) > 0
        for name in graphs:
            encoded = runtime.read(name)
            assert encoded[:9] == b"GOG2\x04\x00\x00\x00\x02", name
            decoded = gzip.decompress(encoded[17:])
            assert len(decoded) == int.from_bytes(encoded[9:17], "little")
            assert decoded[:8] == b"GOG2\x03\x00\x00\x00"
api_only = web.get("roomModel") == "player-hosted"
with tarfile.open(root / ("superganggarrison-room-api-update.tar.gz" if api_only else "superganggarrison-room-services-linux-x64.tar.gz")) as archive:
    members = {item.name: item for item in archive.getmembers()}
    for name in members:
        path = PurePosixPath(name)
        assert not path.is_absolute() and ".." not in path.parts
    prefix = "opengarrison-rooms/"
    backend = json.load(archive.extractfile(prefix + "room-release.json"))
    assert web == backend, "Web and room API releases differ"
    if api_only:
        assert backend["maximumRoomPlayers"] == 4
        assert prefix + "api/peer_rooms.py" in members
        assert not any(name.startswith(prefix + "server/") for name in members)
        assert b"peer_rooms" in archive.extractfile(prefix + "api/app.py").read()
    else:
        executable = members[prefix + backend["serverExecutable"]]
        assert executable.mode & 0o111, "Native server is not executable"
        assert archive.extractfile(executable).read(4) == b"\x7fELF"
print(json.dumps({"passed": True, "release": web}, indent=2))
