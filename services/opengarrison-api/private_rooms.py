"""Typed managed rooms. Each admitted player gets an independent native peer."""
from __future__ import annotations

import asyncio
import logging
import os
import re
import secrets
import time
import uuid
import anyio
from urllib.parse import quote

from fastapi import HTTPException, Request, WebSocket, WebSocketDisconnect
from pydantic import BaseModel, Field
from websockets.asyncio.client import connect as connect_websocket
from websockets.exceptions import ConnectionClosed, InvalidHandshake

from private_room_store import RoomCapacityError, RoomStore, load_release


class RoomAdmissionLogFilter(logging.Filter):
    """Uvicorn logs WebSocket URLs even with HTTP access logging disabled."""
    def filter(self, record):
        message = record.getMessage()
        redacted = re.sub(r"(/api/(?:private|peer)-rooms/ws/[^\s?]+\?[^\s]*?token=)[^&\s\"']+", r"\1[redacted]", message)
        if redacted != message:
            record.msg, record.args = redacted, ()
        return True


class RoomRequest(BaseModel):
    clientId: str = Field(min_length=1, max_length=64)
    clientSecret: str = Field(min_length=1, max_length=256)
    friendCode: str = Field(min_length=1, max_length=64)
    displayName: str = Field(default="Player", max_length=64)
    requestId: str = Field(default="", max_length=64)
    roomId: str = Field(default="", max_length=64)
    code: str = Field(default="", max_length=64)
    maximumPlayers: int = Field(default=2, ge=1, le=2)
    difficulty: str = Field(default="standard", pattern="^(standard|hardcore)$")
    protocolVersion: int = Field(default=0, ge=0)
    buildVersion: str = Field(default="", max_length=128)
    contentId: str = Field(default="", max_length=128)


def install_private_rooms(api):
    for name in ("uvicorn.error", "uvicorn.access"):
        logger = logging.getLogger(name)
        if not any(isinstance(item, RoomAdmissionLogFilter) for item in logger.filters):
            logger.addFilter(RoomAdmissionLogFilter())

    def store():
        return RoomStore(api.db_path())

    @api.app.on_event("startup")
    async def recover_gateway_connections():
        # This service runs one ASGI worker. No socket survives a process restart.
        store().update("UPDATE private_room_seats SET connection_id='' WHERE connection_id!=''")

    def release():
        if os.environ.get("OPENGARRISON_ROOMS_ENABLED") != "1":
            raise HTTPException(503, {"code": "hosting_unavailable"})
        try:
            return load_release()
        except (OSError, ValueError, KeyError):
            raise HTTPException(503, {"code": "hosting_unavailable"}) from None

    def authenticate(payload: RoomRequest):
        try:
            # Match the Guid "N" representation persisted by the desktop
            # identity document and the rest of the account API.
            client_id = uuid.UUID(payload.clientId).hex
        except ValueError:
            raise HTTPException(400, "Invalid client identity") from None
        friend = api.normalize_friend_code(payload.friendCode)
        if not friend:
            raise HTTPException(400, "Invalid friend code")
        with api.connect_db() as db:
            # Avoid a deferred read-to-write upgrade racing the room worker.
            db.execute("BEGIN IMMEDIATE")
            account_id = api.verify_client(db, client_id, friend, payload.clientSecret, payload.displayName)
            friend = api.get_primary_friend_code(db, account_id)
        return client_id, friend

    def compatible(payload, version):
        if payload.protocolVersion != version["protocolVersion"] or payload.contentId != version["contentId"]:
            raise HTTPException(426, "Incompatible game content or protocol")

    def describe(room, client_id, request, seat=None):
        scheme, authority = api.relay_public_origin(request)
        endpoint = ""
        expires = ""
        if seat is not None:
            ws_scheme = "wss64" if scheme == "wss" else "ws64"
            endpoint = f"{ws_scheme}://{authority}/api/private-rooms/ws/{room['id']}/{seat['slot']}?token={quote(seat['token'], safe='')}"
            expires = api.iso_from_seconds(int(seat["expires"]))
        return dict(roomId=room["id"], roomCode=room["code"] or "", kind="LastToDie", status=room["status"],
                    protocolVersion=room["protocol_version"], buildVersion=room["build_version"], contentId=room["content_id"],
                    maximumPlayers=room["maximum_players"], isOwner=room["owner"] == client_id,
                    endpoint=endpoint, expiresAtIso=expires, message=room["message"])

    @api.app.post("/api/private-rooms/create")
    def create_room(payload: RoomRequest, request: Request):
        version = release()
        api.enforce_relay_room_lookup_rate_limit(request)
        owner, friend = authenticate(payload)
        compatible(payload, version)
        if not payload.requestId:
            raise HTTPException(400, "A creation idempotency key is required")
        capacity = max(1, min(64, int(os.environ.get("OPENGARRISON_ROOM_CAPACITY", "2"))))
        try:
            room = store().create(owner, friend, payload.model_dump(), version, capacity)
        except RoomCapacityError as error:
            raise HTTPException(503, {"code": error.code, "roomId": error.room_id}) from None
        except ValueError:
            raise HTTPException(409, "Creation request key was reused with different settings") from None
        return describe(room, owner, request)

    @api.app.post("/api/private-rooms/status")
    def room_status(payload: RoomRequest, request: Request):
        owner, _ = authenticate(payload)
        rooms = store().rows("SELECT * FROM private_rooms WHERE id=? AND owner=?", (payload.roomId, owner))
        if not rooms:
            raise HTTPException(404, "Allocation not found")
        return describe(rooms[0], owner, request)

    @api.app.post("/api/private-rooms/join")
    def join_room(payload: RoomRequest, request: Request):
        release()
        api.enforce_relay_room_lookup_rate_limit(request)
        client_id, _ = authenticate(payload)
        now = time.time()
        room_store = store()
        with room_store.connect(write=True) as db:
            if db.execute("SELECT 1 FROM private_room_cancelled_requests WHERE client_id=? AND request_id=?", (client_id, payload.requestId)).fetchone():
                raise HTTPException(409, "Join request was cancelled")
            if payload.roomId:
                room = db.execute("SELECT * FROM private_rooms WHERE id=?", (payload.roomId,)).fetchone()
            elif payload.code.upper().startswith("OG2"):
                friend = api.normalize_friend_code(payload.code)
                room = db.execute("SELECT * FROM private_rooms WHERE owner_friend=? AND maximum_players=2 AND status='ready' ORDER BY created DESC LIMIT 1", (friend,)).fetchone()
            else:
                code = api.normalize_relay_room_code(payload.code)
                room = db.execute("SELECT * FROM private_rooms WHERE code=?", (code,)).fetchone()
            if room is None or (room["maximum_players"] == 1 and room["owner"] != client_id):
                raise HTTPException(404, "Room not found")
            compatible(payload, {"protocolVersion": room["protocol_version"], "contentId": room["content_id"]})
            if room["status"] != "ready" or room["worker_lease"] < now:
                raise HTTPException(409, "Room is not ready")
            seat = db.execute("SELECT * FROM private_room_seats WHERE room_id=? AND client_id=?", (room["id"], client_id)).fetchone()
            if seat is None:
                if room["phase"] != "Lobby" or db.execute("SELECT 1 FROM private_room_seats WHERE room_id=? AND slot=2", (room["id"],)).fetchone():
                    raise HTTPException(409, "Room is full or already playing")
                db.execute("INSERT INTO private_room_seats (room_id,slot,client_id,request_id,token,expires,last_seen) VALUES (?,2,?,?,?,?,?)",
                           (room["id"], client_id, payload.requestId, secrets.token_urlsafe(32), now + 43200, now))
            else:
                db.execute("UPDATE private_room_seats SET expires=?, request_id=? WHERE room_id=? AND client_id=?", (now + 43200, payload.requestId, room["id"], client_id))
            seat = db.execute("SELECT * FROM private_room_seats WHERE room_id=? AND client_id=?", (room["id"], client_id)).fetchone()
            db.execute("UPDATE private_rooms SET last_activity=? WHERE id=?", (now, room["id"]))
        return describe(room, client_id, request, seat)

    @api.app.post("/api/private-rooms/cancel")
    def cancel_room(payload: RoomRequest, request: Request):
        client_id, friend = authenticate(payload)
        room_store = store()
        room_store.update("INSERT OR IGNORE INTO private_room_cancelled_requests VALUES (?,?,?)", (client_id, payload.requestId, time.time()))
        if payload.code or payload.roomId:
            # A lookup can finish after Cancel; release only this request's guest reservation.
            room_store.update("DELETE FROM private_room_seats WHERE client_id=? AND request_id=? AND slot=2 AND connection_id=''", (client_id, payload.requestId))
            return empty_response(payload)
        if not payload.requestId:
            raise HTTPException(400, "Request id is required")
        version = dict(protocolVersion=payload.protocolVersion, buildVersion=payload.buildVersion, contentId=payload.contentId)
        room = room_store.create(client_id, friend, payload.model_dump(), version, 0, cancelled=True)
        return describe(room, client_id, request)

    @api.app.post("/api/private-rooms/leave")
    def leave_room(payload: RoomRequest, request: Request):
        client_id, _ = authenticate(payload)
        with store().connect(write=True) as db:
            room = db.execute("SELECT * FROM private_rooms WHERE id=?", (payload.roomId,)).fetchone()
            if room is None:
                return empty_response(payload)
            if room["owner"] == client_id:
                db.execute("UPDATE private_rooms SET status='closing', message='The owner left the room.', updated=? WHERE id=? AND status IN ('queued','starting','ready')", (time.time(), room["id"]))
            else:
                db.execute("UPDATE private_room_seats SET expires=0 WHERE room_id=? AND client_id=?", (room["id"], client_id))
            room = db.execute("SELECT * FROM private_rooms WHERE id=?", (room["id"],)).fetchone()
        return describe(room, client_id, request)

    @api.app.websocket("/api/private-rooms/ws/{room_id}/{slot}")
    async def room_socket(websocket: WebSocket, room_id: str, slot: int, token: str = ""):
        room_store = await asyncio.to_thread(store)
        connection_id = secrets.token_hex(16)
        def claim_seat():
            now = time.time()
            with room_store.connect(write=True) as db:
                room = db.execute("SELECT * FROM private_rooms WHERE id=?", (room_id,)).fetchone()
                seat = db.execute("SELECT * FROM private_room_seats WHERE room_id=? AND slot=?", (room_id, slot)).fetchone()
                valid = room is not None and seat is not None and room["status"] == "ready" and room["worker_lease"] >= now
                valid = valid and seat["expires"] > now and bool(token) and secrets.compare_digest(token, seat["token"])
                valid = valid and not seat["connection_id"]
                if valid:
                    db.execute("UPDATE private_room_seats SET connection_id=?, last_seen=? WHERE room_id=? AND slot=?", (connection_id, now, room_id, slot))
            return room, seat, valid
        room, seat, valid = await asyncio.to_thread(claim_seat)
        if not valid:
            await websocket.close(code=4403, reason="Room admission is expired, occupied or unavailable.")
            return
        try:
            async with connect_websocket(f"ws://127.0.0.1:{room['port']}/opengarrison/ws64",
                                         additional_headers={"X-OG-Room-Token": room["server_token"], "X-OG-Client-Id": seat["client_id"], "X-OG-Player-Slot": str(slot)},
                                         max_size=4 * 1024 * 1024, max_queue=16, open_timeout=8, close_timeout=3) as upstream:
                await websocket.accept()

                async def to_server():
                    while True:
                        message = await websocket.receive()
                        if message["type"] == "websocket.disconnect":
                            return
                        data = message.get("bytes")
                        if data is None or len(data) > 4 * 1024 * 1024:
                            await websocket.close(code=1009)
                            return
                        await upstream.send(data)

                async def to_client():
                    async for data in upstream:
                        if not isinstance(data, bytes):
                            raise ValueError("Native protocol requires binary messages")
                        await websocket.send_bytes(data)

                def refresh_seat():
                    now = time.time()
                    with room_store.connect(write=True) as db:
                        current = db.execute("SELECT status,worker_lease FROM private_rooms WHERE id=?", (room_id,)).fetchone()
                        grant = db.execute("SELECT expires,connection_id FROM private_room_seats WHERE room_id=? AND slot=?", (room_id, slot)).fetchone()
                        if current is None or current["status"] != "ready" or current["worker_lease"] < now or grant is None or grant["expires"] <= now or grant["connection_id"] != connection_id:
                            return False
                        db.execute("UPDATE private_room_seats SET last_seen=? WHERE room_id=? AND slot=? AND connection_id=?", (now, room_id, slot, connection_id))
                        db.execute("UPDATE private_rooms SET last_activity=? WHERE id=?", (now, room_id))
                    return True

                async def heartbeat():
                    while True:
                        await asyncio.sleep(5)
                        if not await asyncio.to_thread(refresh_seat): return

                tasks = [asyncio.create_task(action()) for action in (to_server, to_client, heartbeat)]
                try:
                    await asyncio.wait(tasks, return_when=asyncio.FIRST_COMPLETED)
                finally:
                    for task in tasks:
                        task.cancel()
                    await asyncio.gather(*tasks, return_exceptions=True)
        except (ConnectionClosed, WebSocketDisconnect, OSError, TimeoutError, InvalidHandshake):
            pass
        finally:
            # ASGI disconnect cancellation must not skip releasing the seat;
            # otherwise a reconnect is rejected as an already occupied slot.
            with anyio.move_on_after(10, shield=True):
                await asyncio.to_thread(room_store.update, "UPDATE private_room_seats SET connection_id='', last_seen=? WHERE room_id=? AND slot=? AND connection_id=?", (time.time(), room_id, slot, connection_id))
            try:
                await websocket.close(code=1001, reason="Room connection ended.")
            except RuntimeError:
                pass


def empty_response(payload):
    return dict(roomId=payload.roomId, roomCode="", kind="LastToDie", status="cancelled", protocolVersion=payload.protocolVersion,
                buildVersion=payload.buildVersion, contentId=payload.contentId, maximumPlayers=payload.maximumPlayers,
                isOwner=False, endpoint="", expiresAtIso="", message="")
