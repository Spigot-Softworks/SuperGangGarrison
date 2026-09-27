"""Local GG2 server that rotates stock maps without sending a team-change packet."""

import asyncio
import argparse
import hashlib
import pathlib
import struct


PNG = (pathlib.Path(__file__).resolve().parents[2]
       / "Core/Content/StockMaps/Gg2/koth_harvest.png").read_bytes()
HASH = hashlib.md5(PNG).hexdigest()


def short(value: str) -> bytes:
    encoded = value.encode("latin1")
    return bytes([len(encoded)]) + encoded


def quick_update(character: bool) -> bytes:
    if not character:
        return bytes([9, 1, 0])
    return (bytes([9, 1, 1, 0]) + struct.pack("<H", 0) + bytes([0])
            + struct.pack("<HHbbBBB", 2500, 2200, 0, 0, 125, 6, 0))


async def handle(reader: asyncio.StreamReader, writer: asyncio.StreamWriter,
                 state: dict[str, bool], custom: bool) -> None:
    try:
        assert (await reader.readexactly(17))[0] == 0
        if state["rotated"] and custom:
            writer.write(bytes([0]) + short("Map rotation test") + short("koth_harvest")
                         + short(HASH) + bytes([0]) + struct.pack("<H", 0))
            await writer.drain()
            if await reader.readexactly(1) == bytes([45]):
                writer.write(struct.pack("<I", len(PNG)) + PNG)
                await writer.drain()
            return
        writer.write(bytes([0]) + short("Map rotation test") + short("koth_corinth")
                     + short("") + bytes([0]) + struct.pack("<H", 0))
        await writer.drain()
        assert await reader.readexactly(1) == bytes([60])
        await reader.readexactly((await reader.readexactly(1))[0])
        writer.write(bytes([60]))
        await writer.drain()
        assert await reader.readexactly(1) == bytes([1])
        full_update = (bytes([8]) + struct.pack("<H", 0) + bytes([0])
                       + struct.pack("<HH", 0, 0) + bytes([3, 0, 0, 10])
                       + struct.pack("<HHH", 0, 5400, 5400)
                       + bytes([255, 255]) + struct.pack("<H", 0) + bytes(10))
        writer.write(bytes([44, 0, 1, 7]) + short("koth_corinth") + short("")
                     + full_update + bytes([1]) + short("RotationSmoke")
                     + bytes([3, 0, 0]))  # Local player joins Red.
        await writer.drain()
        for _ in range(45):
            writer.write(quick_update(True))
            await writer.drain()
            await asyncio.sleep(0.1)
        # Area 1 resets teams inside GG2's client; no PLAYER_CHANGETEAM follows.
        state["rotated"] = True
        writer.write(bytes([14]) + short("koth_harvest") + bytes([0, 1])
                     + bytes([7]) + short("koth_harvest") + short(HASH if custom else ""))
        await writer.drain()
        for _ in range(240):
            writer.write(quick_update(False))
            await writer.drain()
            await asyncio.sleep(0.1)
    except (ConnectionError, asyncio.IncompleteReadError):
        pass
    finally:
        writer.close()
        try:
            await writer.wait_closed()
        except ConnectionError:
            pass


async def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("--custom", action="store_true")
    custom = parser.parse_args().custom
    state = {"rotated": False}
    server = await asyncio.start_server(
        lambda reader, writer: handle(reader, writer, state, custom), "127.0.0.1", 0)
    print(server.sockets[0].getsockname()[1], flush=True)
    async with server:
        await server.serve_forever()


if __name__ == "__main__":
    asyncio.run(main())
