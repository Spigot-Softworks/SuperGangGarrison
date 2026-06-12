# OpenGarrison API

Small Linux-hosted API for:

- public server discovery at `/api/servers`
- anonymous client registration at `/api/client/register`
- friend presence at `/api/presence`
- static updater files served by the reverse proxy under `/updates/`

The service uses SQLite and expects a reverse proxy such as Caddy or nginx in front of it.
Server discovery rows include `protocolVersion`, `buildVersion`, `releaseChannel`, and
`compatibilityKey` so stable and beta builds can share the registry without appearing to
incompatible clients.

## Environment

```text
OPENGARRISON_API_DB=/var/lib/opengarrison-api/opengarrison.db
OPENGARRISON_API_CORS_ORIGINS=https://unkind-dev.com,https://www.unkind-dev.com
OPENGARRISON_REGISTRY_TOKEN=optional-admin-token
```

## Run Locally

```bash
python3 -m venv .venv
. .venv/bin/activate
pip install -r requirements.txt
uvicorn app:app --host 127.0.0.1 --port 8008
```
