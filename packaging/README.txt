
windows
- run the client with `Super Gang Garrison.exe`; it starts the updater so client updates can be checked before the game opens
- game, server, content, maps, and plugin files live under `app`
- launch `Super Gang Garrison.exe` from this folder for normal play so updates are checked
- the Windows package is framework-dependent; install the .NET 10 runtime and ASP.NET Core runtime before launching the client/server
- the Windows launcher checks the packaged server's ASP.NET Core version requirement and offers the official Microsoft download page when the compatible runtime is missing
- Windows MsQuic is supplied by supported .NET runtimes; Windows 11 or Windows Server 2022 (or newer) is required for QUIC

linux/macOS
- run the client with `./OG2`; it starts the updater so client updates can be checked before the game opens
- game, server, content, maps, and plugin files live under `app`
- if extracted app files are not marked executable on first launch, run `chmod +x OG2 app/OG2.Game app/OG2.Server app/OG2.ServerLauncher`
- server command-line options can be passed to `app/OG2.Server`, for example `app/OG2.Server --websocket-port 8191 --public-host server.example.com --public-websocket-url wss://server.example.com/opengarrison/ws`
- linux audio uses the system OpenAL library; if audio is unavailable the client will continue with sound disabled
- Linux release archives are self-contained for .NET and bundle the x64 `libmsquic` runtime used by protocol-64 QUIC. Linux package builders must install `libmsquic` 2.2 or newer from the Microsoft package repository or distribution repository; for Debian/Ubuntu, use `sudo apt-get install libmsquic`
- Linux still uses host OpenSSL, libnuma, and OpenAL libraries. If those are unavailable, QUIC or audio may be disabled while protocol-64 WebSocket remains usable
- to enable the protocol-64 QUIC listener, set `OPENGARRISON_QUIC_PORT` and configure the existing PKCS#12 WebSocket certificate and password; allow UDP on that port

config files
- app/config/OpenGarrison.ini
- app/config/controls.OpenGarrison
- app/config/sampleMapRotation.txt
