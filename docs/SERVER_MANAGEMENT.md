# Server management identities and player titles

Dedicated servers use `server-management.json` beside `server.ini`. The server creates a disabled example the first time it starts. A different file can be selected with `--management-config <path>`.

```json
{
  "schemaVersion": 1,
  "players": [
    {
      "enabled": true,
      "friendCode": "OG2-ABCD-EFGH",
      "role": "Owner",
      "permissions": ["FullAccess"],
      "title": {
        "text": "[Owner]",
        "color": "rainbow"
      }
    },
    {
      "enabled": true,
      "friendCode": "OG2-MNPQ-RSTU",
      "role": "Moderator",
      "title": {
        "text": "[Moderator]",
        "color": "#55AAFF"
      }
    }
  ]
}
```

The built-in role defaults are:

- `Owner`, `Admin`, or `Administrator`: full access.
- `Moderator` or `Mod`: view server state, manage players, and manage the match.
- `Special`: no command permissions; useful for a title-only entry.

An explicit `permissions` list replaces the role default. Supported values are `ViewServerState`, `ManagePlayers`, `ManageMatch`, `ManageServerConfiguration`, `ManagePlugins`, `ManageScheduler`, and `FullAccess`.

Title colors accept `#RRGGBB`, `RRGGBB`, the short `#RGB` form, or `rainbow`. Titles are shown with player names in chat, the scoreboard, and overhead nameplates.

## Identity security

A friend code sent in the initial game connection or a later profile update is only a claim and never grants a role or title. The desktop client automatically authenticates to the SuperGanggarrison API, obtains a short-lived gameplay token, and sends it to the game server. The server validates that token directly with the API, requires its authenticated client ID to match the active network session, and uses the canonical friend code returned for that account. Configured permissions stop working as soon as the verified gameplay session expires or is cleared.
