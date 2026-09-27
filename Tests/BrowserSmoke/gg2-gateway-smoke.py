"""Exercise the GG2 gateway against one advertised public server."""

import argparse
import asyncio
import json
import struct
import urllib.request

import websockets


def hello(name: str = "GatewaySmoke") -> bytes:
    encoded = name.encode("utf-8")
    return (
        bytes([0, 1])  # Uncompressed SGG Hello.
        + struct.pack("<H", len(encoded)) + encoded
        + struct.pack("<iQHHB", 105, 0, 0, 0, 0)
        + bytes(16)
    )


def read_string(payload: bytes, offset: int) -> tuple[str, int]:
    length = struct.unpack_from("<H", payload, offset)[0]
    offset += 2
    return payload[offset:offset + length].decode("utf-8"), offset + length


def decode_welcome(payload: bytes) -> dict:
    assert payload[:2] == bytes([0, 2]), f"Expected uncompressed Welcome, got {payload[:2]!r}"
    server, offset = read_string(payload, 2)
    version, tick_rate = struct.unpack_from("<ii", payload, offset)
    offset += 8
    level, offset = read_string(payload, offset)
    slot = payload[offset]
    offset += 1
    max_players = struct.unpack_from("<i", payload, offset)[0]
    offset += 4
    is_custom = bool(payload[offset])
    offset += 1
    map_url, offset = read_string(payload, offset)
    map_hash, offset = read_string(payload, offset)
    return dict(server=server, version=version, tick_rate=tick_rate, level=level,
                slot=slot, max_players=max_players, is_custom=is_custom,
                map_url=map_url, map_hash=map_hash)


async def run(origin: str) -> None:
    with urllib.request.urlopen(origin + "/api/gg2/servers", timeout=10) as response:
        servers = json.load(response)["servers"]
    candidates = [server for server in servers if server["isCompatible"]
                  and not server["isPrivate"] and server["players"] < server["slots"]]
    if not candidates:
        raise RuntimeError("No joinable GG2 server is currently advertised")
    target = min(candidates, key=lambda server: server["players"])
    ws_origin = origin.replace("http://", "ws://").replace("https://", "wss://")
    url = f"{ws_origin}/api/gg2/ws/{target['host']}/{target['port']}"
    async with websockets.connect(url, max_size=16 * 1024 * 1024, open_timeout=10) as socket:
        await socket.send(hello())
        while True:
            payload = await asyncio.wait_for(socket.recv(), timeout=20)
            if isinstance(payload, bytes) and payload[:2] == bytes([0, 2]):
                welcome = decode_welcome(payload)
                assert welcome["version"] == 105
                assert welcome["is_custom"]
                print(json.dumps(dict(target=target["name"], welcome=welcome), indent=2))
                return


if __name__ == "__main__":
    parser = argparse.ArgumentParser()
    parser.add_argument("--origin", default="http://127.0.0.1:8768")
    args = parser.parse_args()
    asyncio.run(run(args.origin))
