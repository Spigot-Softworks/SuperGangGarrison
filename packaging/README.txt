
windows
- run the client with `OG2.exe`; it starts the updater so client updates can be checked before the game opens
- the actual game binary is `OG2.Game.exe`; launch `OG2.exe` for normal play so updates are checked

linux/macOS
- run the client with `./OG2` when app files are executable, or `sh run-client.sh`; it starts the updater so client updates can be checked before the game opens
- the actual game binary is `OG2.Game`; launch `./OG2` or `sh run-client.sh` for normal play so updates are checked
- if extracted app files are not marked executable on first launch, run `sh run-client.sh`, `sh run-server.sh`, or `sh run-server-launcher.sh`
- the helper scripts will fix the executable bit for the packaged binaries in place and then start them
- server command-line options pass through the helper script, for example `sh run-server.sh --websocket-port 8191 --public-host server.example.com --public-websocket-url wss://server.example.com/opengarrison/ws`
- linux audio uses the system OpenAL library; if audio is unavailable the client will continue with sound disabled

config files
- config/OpenGarrison.ini
- config/controls.OpenGarrison
- config/sampleMapRotation.txt
