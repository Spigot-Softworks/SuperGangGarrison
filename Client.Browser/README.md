# OpenGarrison Browser

This project is the thin browser-specific host for OpenGarrison.

## Status

- Blazor WebAssembly and KNI browser host runs offline practice in-browser.
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

For the AOT smoke path:

```powershell
node .\Tests\BrowserSmoke\smoke.mjs
```

For an already-running static AOT artifact server:

```powershell
$env:OG_BROWSER_SMOKE_URL='http://127.0.0.1:5014'
$env:OG_BROWSER_SMOKE_SKIP_SERVER='1'
node .\Tests\BrowserSmoke\smoke.mjs
```

## Networking Status

Offline practice is the current browser-ready gameplay path. Multiplayer is still blocked on transport work: the existing desktop client/server path is UDP/socket-based, while browsers need a web transport such as WebTransport with a WebSocket fallback decision. The protocol codec is reusable, but the network client/server transport boundary still needs to be abstracted before browser multiplayer should be considered release-ready.
