"""Check native readiness and owner cleanup against an isolated local API fixture."""
import argparse
import json
from pathlib import Path
import secrets
import time
import urllib.request
from urllib.parse import urlparse
import uuid

parser = argparse.ArgumentParser(description=__doc__)
parser.add_argument("--api", default="http://127.0.0.1:8010")
parser.add_argument("--release", required=True)
args = parser.parse_args()
assert urlparse(args.api).hostname in ("localhost", "127.0.0.1"), "Use a local test fixture"
release = json.loads(Path(args.release).read_text())
code = "".join(secrets.choice("ABCDEFGHJKLMNPQRSTUVWXYZ23456789") for _ in range(8))
payload = dict(clientId=str(uuid.uuid4()), clientSecret=secrets.token_hex(32),
    friendCode=f"OG2-{code[:4]}-{code[4:]}", displayName="Readiness test",
    requestId=uuid.uuid4().hex, maximumPlayers=1, difficulty="standard",
    protocolVersion=release["protocolVersion"], buildVersion=release["buildVersion"], contentId=release["contentId"])
def post(operation):
    request = urllib.request.Request(args.api + "/api/private-rooms/" + operation,
        json.dumps(payload).encode(), {"Content-Type": "application/json"})
    with urllib.request.urlopen(request, timeout=10) as response:
        return json.load(response)
started = time.monotonic()
room = post("create")
payload["roomId"] = room["roomId"]
try:
    while room["status"] in ("starting", "queued") and time.monotonic() - started < 140:
        time.sleep(0.5)
        room = post("status")
    assert room["status"] == "ready", {key: room[key] for key in ("status", "message")}
    assert room["contentId"] == release["contentId"]
    assert room["protocolVersion"] == release["protocolVersion"]
    print(json.dumps({"readySeconds": round(time.monotonic() - started, 2), "roomId": room["roomId"]}), flush=True)
finally:
    post("leave")
for attempt in range(20):
    room = post("status")
    if room["status"] == "closed": break
    time.sleep(0.5)
assert room["status"] == "closed", room["status"]
print("Owner departure closed the room.")
