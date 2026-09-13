"""Player-hosted rooms: authenticated seats, signaling and an optional packet relay.

This service never creates a game process or runs gameplay. Each guest has its
own seat and transport generation; only the owner can edit or start a room.
"""
from __future__ import annotations

import asyncio
from dataclasses import dataclass, field
import json
import logging
import os
import secrets
import time
import uuid
from urllib.parse import quote

from fastapi import HTTPException, Request, WebSocket, WebSocketDisconnect
from pydantic import BaseModel, Field, ValidationError


class PracticeSettings(BaseModel):
    map: str = Field(default="Harvest", min_length=1, max_length=64, pattern=r"^[\w -]+$")
    mapArea: int = Field(default=1, ge=1, le=32)
    tickRate: int = Field(default=30, ge=30, le=120)
    timeLimitMinutes: int = Field(default=15, ge=5, le=60)
    captureLimit: int = Field(default=5, ge=1, le=10)
    respawnSeconds: int = Field(default=5, ge=0, le=15)
    redBots: int = Field(default=0, ge=0, le=9)
    blueBots: int = Field(default=1, ge=0, le=9)
    specialAbilities: bool = True
    difficulty: str = Field(default="standard", pattern="^(standard|hardcore)$")

    def checked(self):
        if self.tickRate not in (30, 60, 120) or self.respawnSeconds not in (0, 3, 5, 10, 15):
            raise ValueError("Unsupported tick rate or respawn time.")
        return self.model_dump()


class PeerRoomRequest(BaseModel):
    clientId: str = Field(min_length=1, max_length=64)
    clientSecret: str = Field(min_length=1, max_length=256)
    friendCode: str = Field(min_length=1, max_length=64)
    displayName: str = Field(default="Player", max_length=48)
    requestId: str = Field(min_length=1, max_length=64)
    code: str = Field(default="", max_length=8)
    kind: str = Field(default="LastToDie", pattern="^(LastToDie|Practice)$")
    protocolVersion: int = Field(ge=1)
    contentId: str = Field(min_length=1, max_length=128)
    settings: PracticeSettings = Field(default_factory=PracticeSettings)


@dataclass
class Seat:
    slot: int
    client: str
    name: str
    token: str = field(default_factory=lambda: secrets.token_urlsafe(32))
    socket: WebSocket | None = None
    send_lock: asyncio.Lock = field(default_factory=asyncio.Lock)
    ready: bool = False
    team: str = "Red"
    seen: float = field(default_factory=time.monotonic)
    window: float = field(default_factory=time.monotonic)
    messages: int = 0
    relay_bytes: int = 0


@dataclass
class Room:
    code: str
    owner: str
    request_id: str
    kind: str
    protocol: int
    content: str
    settings: dict
    seats: dict[int, Seat]
    revision: int = 1
    generation: int = 1
    phase: str = "Lobby"


def install_peer_rooms(api):
    rooms: dict[str, Room] = {}
    api.peer_rooms = rooms
    cancelled: dict[tuple[str, str], float] = {}

    def authenticate(payload):
        try:
            # Desktop identities are persisted as Guid "N" strings (32 hex
            # characters). Keep room authentication on that same canonical
            # representation so an already-registered device is not mistaken
            # for a second device trying to claim its friend code.
            client = uuid.UUID(payload.clientId).hex
        except ValueError:
            raise HTTPException(400, "Invalid client identity") from None
        friend = api.normalize_friend_code(payload.friendCode)
        if not friend:
            raise HTTPException(400, "Invalid friend code")
        with api.connect_db() as db:
            db.execute("BEGIN IMMEDIATE")
            api.verify_client(db, client, friend, payload.clientSecret, payload.displayName)
        return client

    def snapshot(room):
        return dict(type="state", code=room.code, kind=room.kind, phase=room.phase,
                    revision=room.revision, generation=room.generation, maximumPlayers=4,
                    settings=room.settings, players=[dict(slot=s.slot, clientId=s.client,
                    name=s.name, connected=s.socket is not None, ready=s.ready, team=s.team)
                    for s in sorted(room.seats.values(), key=lambda s: s.slot)])

    async def send(seat, value):
        socket = seat.socket
        if socket is None:
            return
        try:
            async with asyncio.timeout(3), seat.send_lock:
                if isinstance(value, bytes):
                    await socket.send_bytes(value)
                else:
                    await socket.send_json(value)
        except (TimeoutError, RuntimeError, OSError, WebSocketDisconnect):
            if seat.socket is socket:
                seat.socket = None
                seat.seen = time.monotonic()
            try:
                await socket.close(1013, "Connection stalled.")
            except (RuntimeError, OSError):
                pass

    async def publish(room):
        await asyncio.gather(*(send(seat, snapshot(room)) for seat in list(room.seats.values())))

    async def close_room(room, reason):
        if rooms.get(room.code) is not room:
            return
        del rooms[room.code]
        await asyncio.gather(*(send(seat, dict(type="closed", reason=reason)) for seat in room.seats.values()))
        for seat in list(room.seats.values()):
            if seat.socket:
                try:
                    await seat.socket.close(1000, reason)
                except (RuntimeError, OSError):
                    pass
                seat.socket = None

    async def prune():
        now = time.monotonic()
        for key, expiry in list(cancelled.items()):
            if expiry < now:
                del cancelled[key]
        for room in list(rooms.values()):
            owner = room.seats[1]
            if owner.socket is None and now - owner.seen > 45:
                await close_room(room, "The host left the room.")
                continue
            if room.phase == "Lobby":
                expired = [s.slot for s in room.seats.values() if s.slot > 1 and s.socket is None and now - s.seen > 30]
                for slot in expired:
                    del room.seats[slot]
                if expired:
                    room.revision += 1
                    await publish(room)

    def grant(room, seat, request):
        scheme, authority = api.relay_public_origin(request)
        return dict(code=room.code, slot=seat.slot, kind=room.kind, protocolVersion=room.protocol,
                    contentId=room.content, endpoint=f"{scheme}://{authority}/api/peer-rooms/ws/{room.code}/{seat.slot}?token={quote(seat.token, safe='')}",
                    iceServers=json.loads(os.environ.get("OPENGARRISON_ICE_SERVERS", '[{"urls":"stun:stun.l.google.com:19302"}]')))

    @api.app.post("/api/peer-rooms/create")
    async def create(payload: PeerRoomRequest, request: Request):
        api.enforce_relay_room_lookup_rate_limit(request)
        client = await asyncio.to_thread(authenticate, payload)
        try:
            settings = payload.settings.checked()
        except ValueError as error:
            raise HTTPException(400, str(error)) from None
        await prune()
        if (client, payload.requestId) in cancelled:
            raise HTTPException(409, "Room request cancelled.")
        for room in rooms.values():
            if room.owner == client:
                if (room.request_id == payload.requestId and room.kind == payload.kind
                    and room.protocol == payload.protocolVersion and room.content == payload.contentId
                    and room.settings == settings):
                    return grant(room, room.seats[1], request)
                raise HTTPException(409, "Leave your current room before creating another.")
        limit = max(1, int(os.environ.get("OPENGARRISON_PEER_ROOM_LIMIT", "4096")))
        if len(rooms) >= limit:
            raise HTTPException(503, "The room service is busy. Try again shortly.")
        alphabet = api.RELAY_ROOM_CODE_ALPHABET
        for _ in range(128):
            code = "".join(secrets.choice(alphabet) for _ in range(4))
            if code not in rooms:
                break
        else:
            raise HTTPException(503, "No room code available.")
        seat = Seat(1, client, payload.displayName)
        room = Room(code, client, payload.requestId, payload.kind, payload.protocolVersion, payload.contentId, settings, {1: seat})
        rooms[code] = room
        return grant(room, seat, request)

    @api.app.post("/api/peer-rooms/join")
    async def join(payload: PeerRoomRequest, request: Request):
        api.enforce_relay_room_lookup_rate_limit(request)
        client = await asyncio.to_thread(authenticate, payload)
        await prune()
        if (client, payload.requestId) in cancelled:
            raise HTTPException(409, "Room request cancelled.")
        room = rooms.get(payload.code.upper())
        if room is None or room.kind != payload.kind:
            raise HTTPException(404, "Room not found for this mode.")
        if room.protocol != payload.protocolVersion or room.content != payload.contentId:
            raise HTTPException(426, "The room uses a different game version.")
        seat = next((s for s in room.seats.values() if s.client == client), None)
        if seat is None:
            if room.phase != "Lobby" and room.kind == "LastToDie":
                raise HTTPException(409, "This Last to Die run has already started.")
            slot = next((slot for slot in range(2, 5) if slot not in room.seats), None)
            if slot is None:
                raise HTTPException(409, "The room is full.")
            seat = Seat(slot, client, payload.displayName)
            room.seats[slot] = seat
            room.revision += 1
            await publish(room)
        return grant(room, seat, request)

    @api.app.post("/api/peer-rooms/leave")
    async def leave(payload: PeerRoomRequest, request: Request):
        api.enforce_relay_room_lookup_rate_limit(request)
        client = await asyncio.to_thread(authenticate, payload)
        room = rooms.get(payload.code.upper())
        if room is not None:
            seat = next((s for s in room.seats.values() if s.client == client), None)
            if seat is not None:
                if seat.slot == 1:
                    await close_room(room, "The host left the room.")
                else:
                    del room.seats[seat.slot]
                    room.revision += 1
                    socket = seat.socket
                    seat.socket = None
                    if socket is not None:
                        try:
                            await socket.close(1000, "You left the room.")
                        except (RuntimeError, OSError):
                            pass
                    await publish(room)
        return {"left": True}

    @api.app.post("/api/peer-rooms/cancel")
    async def cancel(payload: PeerRoomRequest, request: Request):
        api.enforce_relay_room_lookup_rate_limit(request)
        client = await asyncio.to_thread(authenticate, payload)
        await prune()
        cancelled[(client, payload.requestId)] = time.monotonic() + 60
        for room in list(rooms.values()):
            if room.owner == client and room.request_id == payload.requestId:
                await close_room(room, "The host cancelled the room.")
            elif room.code == payload.code.upper():
                removed = [slot for slot, seat in room.seats.items() if slot != 1 and seat.client == client and seat.socket is None]
                for slot in removed:
                    del room.seats[slot]
                if removed:
                    room.revision += 1
                    await publish(room)
        return {"cancelled": True}

    @api.app.websocket("/api/peer-rooms/ws/{code}/{slot}")
    async def socket(websocket: WebSocket, code: str, slot: int, token: str = ""):
        room = rooms.get(code)
        seat = room.seats.get(slot) if room else None
        if seat is None or not token or not secrets.compare_digest(seat.token, token) or seat.socket is not None:
            await websocket.close(4403, "Room admission is expired or occupied.")
            return
        await websocket.accept()
        seat.socket = websocket
        seat.ready = False
        await publish(room)
        try:
            while rooms.get(code) is room and seat.socket is websocket:
                message = await websocket.receive()
                if rooms.get(code) is not room or room.seats.get(slot) is not seat or seat.socket is not websocket:
                    break
                if message["type"] == "websocket.disconnect":
                    break
                now = time.monotonic()
                seat.seen = now
                if now - seat.window >= 1:
                    seat.window, seat.messages, seat.relay_bytes = now, 0, 0
                data = message.get("bytes")
                if data is not None:
                    seat.relay_bytes += len(data)
                    if len(data) < 2 or len(data) > 4 * 1024 * 1024 + 1 or seat.relay_bytes > 8 * 1024 * 1024:
                        await websocket.close(1009, "Game relay limit exceeded.")
                        break
                    target = data[0]
                    if (slot == 1 and target in (2, 3, 4)) or (slot != 1 and target == 1):
                        if target in room.seats:
                            await send(room.seats[target], bytes([slot]) + data[1:])
                    continue
                text = message.get("text", "")
                seat.messages += 1
                if len(text) > 65536 or seat.messages > 128:
                    await websocket.close(1009, "Room command limit exceeded.")
                    break
                try:
                    command = json.loads(text)
                    if not isinstance(command, dict):
                        raise ValueError("Expected a room command.")
                    kind = command.get("type")
                    if kind == "ping":
                        await send(seat, dict(type="pong"))
                        await prune()
                        continue
                    if kind == "signal":
                        if not isinstance(command.get("data"), str):
                            raise ValueError("Expected a signaling message.")
                        target = int(command.get("target", 0))
                        if target in room.seats and ((slot == 1 and target > 1) or (slot > 1 and target == 1)):
                            await send(room.seats[target], dict(type="signal", source=slot, data=command.get("data")))
                        continue
                    if kind == "leave":
                        if slot == 1:
                            await close_room(room, "The host left the room.")
                        else:
                            del room.seats[slot]
                            room.revision += 1
                            await publish(room)
                        break
                    if kind == "ready" and room.phase == "Lobby":
                        seat.ready = command.get("ready") is True
                    elif kind == "team" and room.kind == "Practice" and room.phase == "Lobby" and command.get("team") in ("Red", "Blue"):
                        seat.team = command["team"]
                        seat.ready = False
                    elif kind == "settings" and slot == 1 and room.phase == "Lobby":
                        if command.get("revision") != room.revision:
                            await send(seat, dict(type="error", reason="The lobby changed. Try applying the settings again."))
                            continue
                        room.settings = PracticeSettings(**command["settings"]).checked()
                        for member in room.seats.values():
                            member.ready = False
                    elif kind == "start" and slot == 1 and room.phase == "Lobby":
                        if not all(s.socket is not None and s.ready for s in room.seats.values()):
                            await send(seat, dict(type="error", reason="Everyone in the lobby must be connected and ready."))
                            continue
                        room.phase = "Playing"
                        room.generation += 1
                    elif kind == "lobby" and slot == 1:
                        room.phase = "Lobby"
                        room.generation += 1
                        for member in room.seats.values():
                            member.ready = False
                    else:
                        await send(seat, dict(type="error", reason="That action is unavailable."))
                        continue
                    room.revision += 1
                    await publish(room)
                except (ValueError, TypeError, KeyError, ValidationError):
                    await send(seat, dict(type="error", reason="Invalid room command."))
        except (WebSocketDisconnect, RuntimeError, OSError):
            pass
        finally:
            if seat.socket is websocket:
                seat.socket = None
                seat.seen = time.monotonic()
                seat.ready = False
                if rooms.get(code) is room:
                    room.revision += 1
                    await publish(room)
