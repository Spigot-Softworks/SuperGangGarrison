"""Local smoke fixture: real API, worker and approved native room server, isolated from user data."""
import argparse
import asyncio
import json
import os
from pathlib import Path
import signal
import subprocess
import sys
import tempfile


async def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--release", required=True)
    parser.add_argument("--port", type=int, default=8009)
    parser.add_argument("--host", default="127.0.0.1")
    args = parser.parse_args()
    repo = Path(__file__).resolve().parents[2]
    api = repo / "services/opengarrison-api"
    with tempfile.TemporaryDirectory(prefix="og2-browser-rooms-") as data:
        environment = os.environ.copy()
        environment.update(OPENGARRISON_API_DB=str(Path(data) / "api.db"), OPENGARRISON_ROOMS_ENABLED="1",
            OPENGARRISON_ROOM_RELEASE_MANIFEST=str(Path(args.release).resolve()), OPENGARRISON_ROOM_DATA_ROOT=str(Path(data) / "rooms"),
            OPENGARRISON_ROOM_CAPACITY="4", OPENGARRISON_ROOM_FIRST_PORT="32000",
            OPENGARRISON_RELAY_PUBLIC_BASE_URL="https://api.superganggarrison.com",
            OPENGARRISON_API_BASE_URL=f"http://{args.host}:{args.port}",
            OPENGARRISON_API_CORS_ORIGINS="http://127.0.0.1:5015,http://localhost:5015,https://superganggarrison.com")
        print(json.dumps({"data": data, "apiPort": args.port, "pid": os.getpid()}), flush=True)
        options = {"cwd": api, "env": environment}
        if os.name == "nt": options["creationflags"] = 0x08000000
        processes = [await asyncio.create_subprocess_exec(sys.executable, "-m", "uvicorn", "app:app", "--host", args.host, "--port", str(args.port), "--no-access-log", **options),
                     await asyncio.create_subprocess_exec(sys.executable, "room_worker.py", **options)]
        stop = asyncio.Event()
        if os.name != "nt":
            for sig in (signal.SIGTERM, signal.SIGINT):
                asyncio.get_running_loop().add_signal_handler(sig, stop.set)
        try:
            await stop.wait()
        finally:
            for process in reversed(processes):
                if process.returncode is None:
                    process.terminate()
                    try: await asyncio.wait_for(process.wait(), 20)
                    except asyncio.TimeoutError:
                        process.kill()
                        await process.wait()


if __name__ == "__main__":
    asyncio.run(main())
