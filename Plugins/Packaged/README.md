# Packaged Plugins

`Plugins/Packaged` contains ready-to-run example plugin folders that are copied
into packaged runtime outputs under `Plugins/Client/...` and `Plugins/Server/...`
by `scripts/package.ps1`.

- `Client/` contains the packaged Lua client plugins that ship by default in
  runtime distributions.
- `Server/` contains the packaged Lua server plugins that ship by default in
  runtime distributions.

These examples are intentionally separate from `Plugins/Templates/`:

- `Templates/` are authoring starting points.
- `Packaged/` are the runnable distribution plugins.
