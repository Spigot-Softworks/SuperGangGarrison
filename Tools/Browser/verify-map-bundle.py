"""Verify every staged map asset is present, byte-for-byte, in the browser runtime bundle."""
import argparse
import hashlib
from pathlib import Path
import zipfile


def verify(root):
    content = root / "Content"
    expected = [p for p in (content / "StockMaps").rglob("*")
                if p.is_file() and p.suffix.lower() in {".png", ".json", ".ogg"}]
    if not expected:
        raise ValueError("No staged stock map assets found")
    failures = []
    with zipfile.ZipFile(content / "_browser-runtime-assets.zip") as bundle:
        names = set(bundle.namelist())
        for path in sorted(expected):
            name = path.relative_to(root).as_posix()
            if name not in names:
                failures.append(f"Missing: {name}")
            elif hashlib.sha256(bundle.read(name)).digest() != hashlib.sha256(path.read_bytes()).digest():
                failures.append(f"Content mismatch: {name}")
    if failures:
        raise ValueError("\n".join(failures))
    print(f"Browser map bundle verified: {len(expected)} staged map assets", flush=True)


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("wwwroot", type=Path)
    verify(parser.parse_args().wwwroot.resolve())
