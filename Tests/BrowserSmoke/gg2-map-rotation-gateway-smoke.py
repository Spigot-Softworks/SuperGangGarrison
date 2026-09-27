"""Verify a GG2 map rotation resets the translated local slot to spectator."""

import asyncio
import argparse
import hashlib
import pathlib
import sys
import urllib.request

import websockets

ROOT = pathlib.Path(__file__).resolve().parent
sys.path.insert(0, str(ROOT))
import importlib.util

spec = importlib.util.spec_from_file_location("gg2_gateway_smoke", ROOT / "gg2-gateway-smoke.py")
assert spec and spec.loader
smoke = importlib.util.module_from_spec(spec)
spec.loader.exec_module(smoke)


async def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("--custom", action="store_true")
    custom = parser.parse_args().custom
    fake = await asyncio.create_subprocess_exec(
        sys.executable, str(ROOT / "gg2-map-rotation-fake.py"),
        *(["--custom"] if custom else []),
        stdout=asyncio.subprocess.PIPE, stderr=asyncio.subprocess.PIPE)
    try:
        assert fake.stdout
        port = int((await fake.stdout.readline()).strip())
        slots = []
        welcome = None
        async with websockets.connect(
            f"ws://127.0.0.1:8768/api/gg2/ws/127.0.0.1/{port}",
            max_size=16 * 1024 * 1024,
        ) as socket:
            await socket.send(smoke.hello())
            async with asyncio.timeout(15):
                while True:
                    payload = await socket.recv()
                    if not isinstance(payload, bytes) or len(payload) < 2:
                        continue
                    if payload[:2] == bytes([0, 2]):
                        welcome = smoke.decode_welcome(payload)
                    elif payload[:2] == bytes([0, 8]):
                        slots.append(payload[2])
                        if len(slots) >= 2:
                            break
        assert welcome and welcome["level"] == "gg2_stock_koth_corinth", welcome
        assert slots[0] == 1, slots
        assert slots[1] >= 128, slots
        if custom:
            png = (ROOT.parents[1] / "Core/Content/StockMaps/Gg2/koth_harvest.png").read_bytes()
            digest = hashlib.md5(png).hexdigest()
            with urllib.request.urlopen(f"http://127.0.0.1:8768/api/gg2/maps/{digest}.png", timeout=5) as response:
                assert response.read() == png
        print(f"GG2 map rotation reset playable slot {slots[0]} to spectator slot {slots[1]}")
    finally:
        fake.terminate()
        await fake.wait()


if __name__ == "__main__":
    asyncio.run(main())
