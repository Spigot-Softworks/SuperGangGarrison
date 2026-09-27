"""Check GG2 custom PNG transfer through the local browser gateway."""

import asyncio
import argparse
import hashlib
import importlib.util
import pathlib
import struct
import urllib.request

import websockets

REPO = pathlib.Path(__file__).resolve().parents[2]
SPEC = importlib.util.spec_from_file_location("gg2_gateway_smoke", pathlib.Path(__file__).with_name("gg2-gateway-smoke.py"))
assert SPEC and SPEC.loader
SMOKE = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(SMOKE)
PNG = (REPO / "Core/Content/StockMaps/Gg2/koth_corinth.png").read_bytes()
HASH = hashlib.md5(PNG).hexdigest()
MAP = "koth_corinth"


def short(value: str) -> bytes:
    encoded = value.encode("latin1")
    return bytes([len(encoded)]) + encoded


async def fake_gg2(reader: asyncio.StreamReader, writer: asyncio.StreamWriter, lifetime: float = 5) -> None:
    try:
        greeting = await reader.readexactly(17)
        assert greeting[0] == 0
        writer.write(bytes([0]) + short("Gateway map test") + short(MAP) + short(HASH)
                     + bytes([0]) + struct.pack("<H", 0))
        await writer.drain()
        command = await reader.readexactly(1)
        if command == bytes([45]):  # DOWNLOAD_MAP (unless already cached)
            writer.write(struct.pack("<I", len(PNG)) + PNG)
            await writer.drain()
            command = await reader.readexactly(1)
        assert command == bytes([60])  # RESERVE_SLOT
        await reader.readexactly((await reader.readexactly(1))[0])
        writer.write(bytes([60]))
        await writer.drain()
        assert await reader.readexactly(1) == bytes([1])  # PLAYER_JOIN
        full_update = (
            bytes([8]) + struct.pack("<H", 0) + bytes([0])
            + struct.pack("<HH", 0, 0) + bytes([3, 0, 0, 10])
            + struct.pack("<HHH", 0, 5400, 5400)
            + bytes([255, 255]) + struct.pack("<H", 0) + bytes(10)
        )
        writer.write(bytes([44, 0, 1, 7]) + short(MAP) + short(HASH)
                     + full_update + bytes([1]) + short("GatewaySmoke"))
        await writer.drain()
        await asyncio.sleep(lifetime)
    finally:
        writer.close()
        try:
            await writer.wait_closed()
        except ConnectionError:
            pass


async def main() -> None:
    server = await asyncio.start_server(fake_gg2, "127.0.0.1", 0)
    port = server.sockets[0].getsockname()[1]
    try:
        async with websockets.connect(f"ws://127.0.0.1:8768/api/gg2/ws/127.0.0.1/{port}",
                                      max_size=16 * 1024 * 1024) as socket:
            await socket.send(SMOKE.hello())
            while True:
                payload = await asyncio.wait_for(socket.recv(), timeout=20)
                if isinstance(payload, bytes) and payload[:2] == bytes([0, 2]):
                    welcome = SMOKE.decode_welcome(payload)
                    break
        assert welcome["level"] == "gg2_" + HASH, welcome
        assert welcome["map_hash"] == HASH, welcome
        assert welcome["is_custom"] and welcome["map_url"], welcome
        with urllib.request.urlopen(welcome["map_url"], timeout=5) as response:
            assert response.read() == PNG
        print(f"GG2 custom map gateway passed: {welcome['level']}")
    finally:
        server.close()
        await server.wait_closed()


async def serve() -> None:
    server = await asyncio.start_server(
        lambda reader, writer: fake_gg2(reader, writer, lifetime=60), "127.0.0.1", 0)
    print(server.sockets[0].getsockname()[1], flush=True)
    async with server:
        await server.serve_forever()


if __name__ == "__main__":
    parser = argparse.ArgumentParser()
    parser.add_argument("--serve", action="store_true")
    args = parser.parse_args()
    asyncio.run(serve() if args.serve else main())
