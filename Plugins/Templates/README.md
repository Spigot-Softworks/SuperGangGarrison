# Plugin Templates

These templates demonstrate plugin manifests and host APIs.

- `ClientClr` is the baseline manifest for packaged C# client plugins.
- `ClientLua.RandomBackgrounds` is a runnable Lua client plugin template.
- `ClientLua.ShowPing` is a runnable Lua HUD and scoreboard plugin template.
- `ClientLua.LowHealthIndicator` is a runnable Lua client update and audio plugin template.
- `ClientLua.CameraShake` is a runnable Lua world-sound and camera-offset plugin template.
- `ClientLua.DamageIndicator` is a runnable Lua local-damage HUD/audio plugin template.
- `ClientLua.MoreAnimations` is a runnable Lua dead-body animation plugin template.
- `ClientLua.BubbleWheel` is a runnable Lua bubble-menu override template.
- `ClientLua.TeamOnlyMinimap` is a runnable Lua minimap HUD plugin template.
- `ServerClr` is the baseline manifest for packaged C# server plugins.
- `ServerLua` is a runnable Lua server plugin template.
- `ServerLua.ChatVoting` is a runnable Lua native vote-kind extension template.
- `ServerLua.GameplayAbility` is a runnable Lua server gameplay ability template
  with a Spacebar action, plugin-owned cooldown HUD state, and hidden passive
  cooldown ticking.
- `ServerLua.PrimaryWeapon` is a runnable Lua server primary weapon template
  that registers a custom weapon behavior, weapon item, and loadout.

Ready-to-run packaged example installs live separately under `Plugins/Packaged/`
so release packages can ship examples alongside the game.

Supported APIs include:

- Client Lua supports lifecycle, menu integration, main-menu overrides,
  HUD/scoreboard drawing, lightweight audio playback, and options/config-backed
  presentation plugins.
- Server Lua supports lifecycle, gameplay/map/chat/client events,
  semantic server events, bounded admin actions, replicated state, plugin
  messaging, registered gameplay abilities, and registered primary weapon
  behaviors.
