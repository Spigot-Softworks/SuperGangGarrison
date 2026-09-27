"""Read and cache the public GG2 lobby for browser clients."""

from __future__ import annotations

import asyncio
import struct
import time

LOBBY_HOST = "ganggarrison.com"
LOBBY_PORT = 29944
LIST_REQUEST = bytes.fromhex(
    "297d0df4430cbf61640a640897eaef57"
    "1ccf16b1436d856f504dcc1af306aaa7"
)
PROTOCOL_ID = bytes.fromhex("b31c220942569a19d0efc71c5373bd75")
MAX_SERVERS = 256
MAX_BLOCK_BYTES = 100_000
CACHE_SECONDS = 15

_cached_servers: list[dict] | None = None
_cache_expires_at = 0.0
_fetch_lock = asyncio.Lock()


def parse_server_block(block: bytes) -> dict | None:
    offset = 0

    def take(length: int) -> bytes:
        nonlocal offset
        if length < 0 or length > len(block) - offset:
            raise ValueError("truncated GG2 lobby block")
        value = block[offset:offset + length]
        offset += length
        return value

    def u16() -> int:
        return struct.unpack(">H", take(2))[0]

    try:
        transport = take(1)[0]
        port = u16()
        address = ".".join(str(part) for part in take(4))
        take(18)  # Lobby server ID.
        slots, players, bots, flags, info_count = (u16() for _ in range(5))
        name, map_name, game, version = "Unknown server", "-", "gg2", ""
        compatible_protocol = False
        for _ in range(info_count):
            key = take(take(1)[0]).decode("utf-8", errors="replace")
            value = take(u16())
            if key == "protocol_id":
                compatible_protocol = value == PROTOCOL_ID
            elif key == "name":
                name = value.decode("utf-8", errors="replace")
            elif key == "map":
                map_name = value.decode("utf-8", errors="replace")
            elif key == "game_short":
                game = value.decode("utf-8", errors="replace")
            elif key == "game_ver":
                version = value.decode("utf-8", errors="replace")
        return {
            "host": address,
            "port": port,
            "name": name.strip() or "Unknown server",
            "map": map_name.strip() or "-",
            "game": game.strip(),
            "version": version.strip(),
            "players": players,
            "bots": bots,
            "slots": slots,
            "isPrivate": bool(flags & 1),
            "isCompatible": transport == 0 and port != 0 and compatible_protocol,
        }
    except (ValueError, IndexError, struct.error):
        return None


async def _read_lobby() -> list[dict]:
    reader, writer = await asyncio.open_connection(LOBBY_HOST, LOBBY_PORT)
    try:
        writer.write(LIST_REQUEST)
        await writer.drain()
        count = struct.unpack(">I", await reader.readexactly(4))[0]
        if count > MAX_SERVERS:
            raise ValueError("GG2 lobby advertised too many servers")
        servers = []
        for _ in range(count):
            length = struct.unpack(">I", await reader.readexactly(4))[0]
            if not 0 < length <= MAX_BLOCK_BYTES:
                raise ValueError("GG2 lobby sent an invalid server block")
            server = parse_server_block(await reader.readexactly(length))
            if server is not None:
                servers.append(server)
        return servers
    finally:
        writer.close()
        await writer.wait_closed()


async def fetch_servers() -> list[dict]:
    global _cached_servers, _cache_expires_at
    if _cached_servers is not None and time.monotonic() < _cache_expires_at:
        return _cached_servers
    async with _fetch_lock:
        if _cached_servers is not None and time.monotonic() < _cache_expires_at:
            return _cached_servers
        servers = await asyncio.wait_for(_read_lobby(), timeout=6)
        _cached_servers = servers
        _cache_expires_at = time.monotonic() + CACHE_SECONDS
        return servers
