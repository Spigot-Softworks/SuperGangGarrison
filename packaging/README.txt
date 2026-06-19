
windows
- run the client with `Super Gang Garrison.exe`; it starts the updater so client updates can be checked before the game opens
- game, server, content, maps, and plugin files live under `app`
- launch `Super Gang Garrison.exe` from this folder for normal play so updates are checked

linux/macOS
- run the client with `./OG2`; it starts the updater so client updates can be checked before the game opens
- game, server, content, maps, and plugin files live under `app`
- if extracted app files are not marked executable on first launch, run `chmod +x OG2 app/OG2.Game app/OG2.Server app/OG2.ServerLauncher`
- server command-line options can be passed to `app/OG2.Server`, for example `app/OG2.Server --websocket-port 8191 --public-host server.example.com --public-websocket-url wss://server.example.com/opengarrison/ws`
- linux audio uses the system OpenAL library; if audio is unavailable the client will continue with sound disabled

config files
- app/config/OpenGarrison.ini
- app/config/controls.OpenGarrison
- app/config/sampleMapRotation.txt
