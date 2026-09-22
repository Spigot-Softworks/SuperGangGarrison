# Documentation

## Guides

- [Codebase orientation](CODEBASE_ORIENTATION.md): projects, runtime flow, and configuration.
- [Server management](SERVER_MANAGEMENT.md): verified administrators, permissions, and player titles.
- [Voice chat and Jukebox](design/VOICE_CHAT_JUKEBOX.md): controls, playlists, and audio configuration.
- [Server registry](server-registry/README.md): discovery and dedicated-server advertising.
- [Browser client](../Client.Browser/README.md): build, publish, and smoke tests.
- [API service](../services/opengarrison-api/README.md): local setup and backend configuration.
- [Player-hosted rooms](../services/opengarrison-api/deploy/browser-edition/README.md): browser/native deployment.
- [Bot runtime](../Core/BotBrain/README.md): live navigation and retired components.
- [Plugin user guide](../Plugins/USER_GUIDE.md) and [authoring guide](../Plugins/AUTHORING_GUIDE.md).
- [Gameplay authoring](../Plugins/GAMEPLAY_ABILITIES.md): stock data and plugin abilities.
- [Packaging](../packaging/DISTRO_QUICKSTART.txt): release commands and output layout.

## Maintaining documentation

Use repository-relative links to shared files. If a command generates an output,
document the command and identify the path as generated. Personal checkout paths,
local test logs, and work-session notes do not belong in shared guides.

The ignore rules deliberately keep local notes out of Git. When adding a shared
guide under `docs/`, add a matching exception to `.gitignore` and include the guide
in the same change as any links to it.
