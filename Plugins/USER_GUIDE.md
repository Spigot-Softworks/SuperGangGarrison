# OpenGarrison Plugin User Guide

This guide is for players and server hosts who want to install and use plugins.
It does not cover writing plugins.

## What Plugins Do

OpenGarrison plugins add optional features without changing the base game.

There are two plugin types:

- **Client plugins** run on your game client. They can add HUD widgets, sounds,
  menu entries, scoreboard panels, chat helpers, and visual effects.
- **Server plugins** run on a hosted server. They can add server commands,
  voting, admin tools, gameplay rules, map behavior, and server-side effects.

A client plugin only affects your local game unless it talks to a matching
server plugin. A server plugin affects everyone playing on that server.

## Installing Client Plugins

1. Close OpenGarrison.
2. Open the game install folder.
3. Put the plugin folder under:

   ```text
   Plugins/Client/<PluginFolder>/
   ```

4. Make sure the plugin folder contains a `plugin.json` file.
5. Start OpenGarrison.
6. Open **Options -> Other -> Plugin Options**.
7. Select the plugin to enable, disable, or configure it.

Client plugin settings are saved under:

```text
config/plugins/client/<pluginId>/
```

## Installing Server Plugins

1. Stop the server.
2. Open the server install folder.
3. Put the plugin folder under:

   ```text
   Plugins/Server/<PluginFolder>/
   ```

4. Make sure the plugin folder contains a `plugin.json` file.
5. Start the server.

Server plugins load when the server starts. To disable a server plugin, remove
or move its folder out of `Plugins/Server/`, then restart the server.

Server plugin settings are saved under:

```text
config/plugins/server/<pluginId>/
```

## Packaged Plugins

OpenGarrison may ship with packaged plugins already installed. These are normal
plugins that live in the same runtime plugin folders.

Examples include:

- client HUD or visual helper plugins
- server chat voting
- server admin tools

You can disable client packaged plugins from **Plugin Options**. Server packaged
plugins are controlled by the server host.

## Using Plugin Commands

Some server plugins add chat commands. Commands usually start with `!` or `/`.

Examples:

```text
!votemap ctf_truefort
!vote yes
!gt_help
```

Command availability depends on the server and your permissions. Admin commands
may require authentication or server-granted permissions.

## Safety Tips

- Install plugins only from people or servers you trust.
- A client plugin can change your UI and local game presentation.
- A server plugin can change server behavior for every player.
- If a plugin causes errors, disable or remove that plugin first.
- Keep the whole plugin folder together; do not copy only `main.lua` or only
  `plugin.json`.

## Troubleshooting

If a plugin does not show up:

- Check that the folder is in `Plugins/Client/` or `Plugins/Server/`.
- Check that the folder contains `plugin.json`.
- Check that the plugin type matches the folder. Client plugins go in
  `Plugins/Client`; server plugins go in `Plugins/Server`.
- Restart the game or server after installing a plugin.

If a client plugin loads but does nothing:

- Open **Options -> Other -> Plugin Options** and make sure it is enabled.
- Open the plugin detail page and check whether it has its own options.
- Check the game console/log for plugin load errors.

If a server plugin command does not work:

- Check that the server was restarted after installing the plugin.
- Check that you typed the command exactly as the plugin expects.
- Check whether the command requires admin permissions.
- Check the server console/log for plugin load or command errors.

## Removing A Plugin

For a client plugin:

1. Disable it from **Plugin Options**, or close the game.
2. Remove its folder from `Plugins/Client/`.
3. Start the game again.

For a server plugin:

1. Stop the server.
2. Remove its folder from `Plugins/Server/`.
3. Start the server again.

Removing a plugin does not always remove its saved config. If you also want to
remove saved settings, delete that plugin's folder under `config/plugins/`.
