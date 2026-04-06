# Plugin Templates

These templates describe the manifest shape and host boundary that the current
plugin runtime understands today.

- `ClientClr` is the baseline manifest for packaged C# client plugins.
- `ClientLua.RandomBackgrounds` is a runnable first-pass Lua client plugin template.
- `ClientLua.ShowPing` is a runnable Lua HUD and scoreboard plugin template.
- `ClientLua.LowHealthIndicator` is a runnable Lua client update and audio plugin template.
- `ClientLua.CameraShake` is a runnable Lua world-sound and camera-offset plugin template.
- `ClientLua.DamageIndicator` is a runnable Lua local-damage HUD/audio plugin template.
- `ClientLua.MoreAnimations` is a runnable Lua dead-body animation plugin template.
- `ClientLua.BubbleWheel` is a runnable Lua bubble-menu override template.
- `ClientLua.TeamOnlyMinimap` is a runnable Lua minimap HUD plugin template.
- `ServerClr` is the baseline manifest for packaged C# server plugins.
- `ServerLua` is a runnable first-pass Lua server plugin template.
- `ServerLua.ChatVoting` is a runnable Lua server voting plugin template.

Ready-to-run packaged example installs live separately under `Plugins/Packaged/`
so release packages can ship examples without conflating them with authoring
templates.

The current Lua hosts are intentionally narrow:

- Client Lua currently targets lifecycle, menu integration, main-menu overrides,
  HUD/scoreboard drawing, lightweight audio playback, and options/config-backed
  presentation plugins.
- Server Lua currently targets lifecycle, gameplay/map/chat/client events,
  semantic server events, bounded admin actions, replicated state, and plugin
  messaging.
