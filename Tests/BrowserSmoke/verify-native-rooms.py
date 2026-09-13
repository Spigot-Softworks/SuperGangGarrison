"""Verify native room distributions against their manifests and browser content identity."""
import argparse
import hashlib
import json
from pathlib import Path, PurePosixPath
import tarfile
import zipfile

parser = argparse.ArgumentParser(description=__doc__)
parser.add_argument('directory')
args = parser.parse_args()
root = Path(args.directory).resolve()
release = json.loads((root / 'backend/room-release.json').read_text())
results = []
for name, system in [('OpenGarrison-Windows-x64.zip', 'Windows'), ('OpenGarrison-Linux-x64.tar.gz', 'Linux')]:
    path = root / 'native' / name
    if path.suffix == '.zip':
        archive = zipfile.ZipFile(path)
        members = {v.filename: v for v in archive.infolist() if not v.is_dir()}
        read = archive.read
    else:
        archive = tarfile.open(path)
        members = {v.name.removeprefix('./'): v for v in archive.getmembers() if v.isfile()}
        read = lambda name: archive.extractfile(members[name]).read()
    with archive:
        for name in members:
            item = PurePosixPath(name)
            assert not item.is_absolute() and '..' not in item.parts, name
        manifest = json.loads(read('package-manifest.json'))
        assert manifest['version'] == '0.7.6-playerhosted.20260906.3'
        for entry in manifest['files']:
            payload = read(entry['path'])
            assert len(payload) == entry['size'], entry['path']
            assert hashlib.sha256(payload).hexdigest() == entry['sha256'], entry['path']
        client = read('app/OG2.dll')
        assert b'OpenGarrisonRoomContentId' in client and release['contentId'].encode() in client
        assert b'OpenGarrisonRoomServiceOrigin' in client and release['roomServiceOrigin'].encode() in client
        assert 'app/OpenGarrison.SessionRuntime.dll' in members
        assert 'app/SIPSorcery.dll' in members
        if system == 'Windows': assert read('app/OG2.Game.exe')[:2] == b'MZ'
        else:
            assert read('app/OG2.Game')[:4] == b'\x7fELF'
            assert members['app/OG2.Game'].mode & 0o111
            assert 'app/libmsquic.so.2' in members
        results.append(dict(platform=system, files=len(manifest['files']), contentId=release['contentId'],
                            archive=path.name, sha256=hashlib.sha256(path.read_bytes()).hexdigest()))
print(json.dumps(dict(passed=True, packages=results), indent=2))
