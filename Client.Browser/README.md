# OpenGarrison Browser

This project hosts OpenGarrison in a browser. Commands below run from the repository root.

## Status

- Blazor WebAssembly and KNI run local Practice and multiplayer in the browser.
- Browser release output is AOT-only. Non-AOT browser publish paths are unsupported and intentionally fail.
- Shared `Core`, `Protocol`, gameplay content, and client runtime are reused from the main repo.
- `Core/Content` is mirrored into `wwwroot/Content` during build/publish for browser-hosted asset access.
- Browser smoke launches practice, verifies frame-pump performance, and checks keyboard capture.

## Generated Files

The browser build generates large local outputs that should not be committed:

- `Client.Browser/wwwroot/Content/`
- `artifacts/browser-publish-aot/`
- `Tests/BrowserSmoke/node_modules/`
- `Tests/BrowserSmoke/artifacts/`

The source of truth for content remains under `Core/Content` and the browser atlas/build tools under `Tools/`.

## Build, Dist, And Serve

Install the browser workload once per machine:

```powershell
dotnet workload install wasm-tools
```

Build the browser host in the only supported configuration:

```powershell
dotnet build .\Client.Browser\OpenGarrison.Client.Browser.csproj -c Release -p:OpenGarrisonBrowserAot=true
```

Create the deployable AOT dist artifact:

```powershell
powershell -ExecutionPolicy Bypass -File .\Tools\Browser\publish-browser.ps1
```

Serve the deployable output from the repo root:

```powershell
$wwwroot = Resolve-Path .\artifacts\browser-publish-aot\wwwroot -ErrorAction Stop
if (!(Test-Path (Join-Path $wwwroot 'index.html'))) { throw "Missing browser dist index.html. Run .\Tools\Browser\publish-browser.ps1 first." }
python -m http.server 5014 --directory $wwwroot
```

Open:

```text
http://127.0.0.1:5014/
```

Only `artifacts/browser-publish-aot/wwwroot` is deployable. `dotnet build` alone is not enough for this static server command; run the publish script first.

## Smoke Test

Install the smoke-test dependencies and Chromium once, then run the test:

```powershell
npm ci --prefix .\Tests\BrowserSmoke
npm exec --prefix .\Tests\BrowserSmoke -- playwright install chromium
node .\Tests\BrowserSmoke\smoke.mjs
```

For an already-running static AOT artifact server:

```powershell
$env:OG_BROWSER_SMOKE_URL='http://127.0.0.1:5014'
$env:OG_BROWSER_SMOKE_SKIP_SERVER='1'
node .\Tests\BrowserSmoke\smoke.mjs
```

## Networking Status

Dedicated-server connections use binary WebSocket transport in the browser.
Player-hosted Practice and Last to Die rooms use WebRTC with an authenticated
WebSocket relay fallback. See [room deployment](../services/opengarrison-api/deploy/browser-edition/README.md).

Completed Last to Die runs are saved in IndexedDB and uploaded for replay verification
when the account service is available. Verified solo and co-op results share the same
leaderboard. Clearing site data deletes any recordings still waiting to upload.
