#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using OpenGarrison.Core;
using Microsoft.Xna.Framework;

namespace OpenGarrison.Client;

public partial class Game1
{
    private const int ConsoleHistoryLimit = 8;
    private bool _consoleOpen;
    private string _consoleInput = string.Empty;
    private readonly List<string> _consoleHistory = new();

    private bool TryHandleEnemyDummyConsoleCommand(string commandText)
    {
        if (RejectOnlineDummyCommand())
        {
            return true;
        }

        var parts = commandText.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0)
        {
            return false;
        }

        var command = parts[0];
        if (command == "dummy_name")
        {
            if (parts.Length >= 2)
            {
                var name = commandText[command.Length..].Trim();
                _world.SetEnemyPlayerName(name);
                AddConsoleLine($"training dummy name set to {_world.EnemyPlayer.DisplayName}");
            }
            else
            {
                AddConsoleLine($"training dummy name is {_world.EnemyPlayer.DisplayName}");
            }

            return true;
        }

        return false;
    }

    private bool RejectOnlineDummyCommand()
    {
        if (!_networkClient.IsConnected)
        {
            return false;
        }

        AddConsoleLine("training dummy commands are offline-only while connected to a server.");
        return true;
    }

    private void ExecuteConsoleCommand()
    {
        var commandText = _consoleInput.Trim();
        AddConsoleLine($"> {commandText}");
        _consoleInput = string.Empty;
        if (string.IsNullOrWhiteSpace(commandText))
        {
            return;
        }

        var parts = commandText.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var command = parts[0].ToLowerInvariant();
        switch (command)
        {
            case "help":
                AddConsoleLine("help, clear, connect <host> [port], disconnect, net_delay <ms>, net_diag <on|off|status|clear|export>, bot_diag <on|off|status|clear>, spawn_dummy (offline training), despawn_dummy (offline training), spawn_friendly_dummy (offline support), despawn_friendly_dummy (offline support), set_name <text>, set_dummy_name <text> (offline training), set_friendly_name <text> (offline support), set_friendly_dummy_hp <n> (offline support), killme, respawn_me, build_sentry, destroy_sentry, give_intel, drop_intel, set_hp <n>, set_ammo <n>, set_class <scout|engineer|pyro|soldier|demoman|heavy|sniper|medic|spy|quote>, load_map <map>, teleport <x> <y>, fill_uber, show_import, show_engineer, show_medic");
                break;
            case "clear":
                _consoleHistory.Clear();
                break;
            case "connect":
                if (parts.Length >= 2)
                {
                    var host = parts[1];
                    var port = 8190;
                    if (parts.Length >= 3 && !int.TryParse(parts[2], out port))
                    {
                        AddConsoleLine("usage: connect <host> [port]");
                        break;
                    }

                    if (TryConnectToServer(host, port, addConsoleFeedback: true))
                    {
                        _menuStatusMessage = string.Empty;
                    }
                }
                else
                {
                    AddConsoleLine("usage: connect <host> [port]");
                }
                break;
            case "disconnect":
                ReturnToMainMenu(IsPracticeSessionActive ? "Practice ended." : "network disconnected");
                break;
            case "net_delay":
                if (TryParseSingleInt(parts, out var latencyMs) && latencyMs >= 0)
                {
                    _networkClient.SetSimulatedLatency(latencyMs);
                    AddConsoleLine($"simulated latency set to {latencyMs}ms");
                }
                else
                {
                    AddConsoleLine("usage: net_delay <ms>");
                }
                break;
            case "net_diag":
                if (parts.Length < 2)
                {
                    PrintNetworkDiagnosticsStatus();
                    break;
                }

                switch (parts[1].ToLowerInvariant())
                {
                    case "on":
                        EnableNetworkDiagnostics();
                        break;
                    case "off":
                        DisableNetworkDiagnostics();
                        break;
                    case "status":
                        PrintNetworkDiagnosticsStatus();
                        break;
                    case "clear":
                        ClearNetworkDiagnosticsHistory();
                        break;
                    case "export":
                        ExportNetworkDiagnosticsHistory();
                        break;
                    default:
                        AddConsoleLine("usage: net_diag <on|off|status|clear|export>");
                        break;
                }

                break;
            case "bot_diag":
                if (parts.Length < 2)
                {
                    PrintBotDiagnosticsStatus();
                    break;
                }

                switch (parts[1].ToLowerInvariant())
                {
                    case "on":
                        EnableBotDiagnostics();
                        break;
                    case "off":
                        DisableBotDiagnostics();
                        break;
                    case "status":
                        PrintBotDiagnosticsStatus();
                        break;
                    case "clear":
                        ClearBotDiagnosticsHistory();
                        break;
                    default:
                        AddConsoleLine("usage: bot_diag <on|off|status|clear>");
                        break;
                }

                break;
            case "spawn_dummy":
                if (RejectOnlineDummyCommand())
                {
                    break;
                }
                _world.SpawnEnemyDummy();
                AddConsoleLine("training dummy spawned");
                break;
            case "despawn_dummy":
                if (RejectOnlineDummyCommand())
                {
                    break;
                }
                _world.DespawnEnemyDummy();
                AddConsoleLine("training dummy despawned");
                break;
            case "spawn_friendly_dummy":
                if (RejectOnlineDummyCommand())
                {
                    break;
                }
                _world.SpawnFriendlyDummy();
                AddConsoleLine("support dummy spawned");
                break;
            case "despawn_friendly_dummy":
                if (RejectOnlineDummyCommand())
                {
                    break;
                }
                _world.DespawnFriendlyDummy();
                AddConsoleLine("support dummy despawned");
                break;
            case "set_friendly_dummy_hp":
                if (RejectOnlineDummyCommand())
                {
                    break;
                }
                if (TryParseSingleInt(parts, out var friendlyHp))
                {
                    _world.SetFriendlyDummyHealth(friendlyHp);
                    AddConsoleLine($"support dummy hp set to {_world.FriendlyDummy.Health}");
                }
                else
                {
                    AddConsoleLine("usage: set_friendly_dummy_hp <n>");
                }
                break;
            case "set_name":
                if (parts.Length >= 2)
                {
                    var name = commandText[command.Length..].Trim();
                    SetLocalPlayerNameFromSettings(name);
                    AddConsoleLine($"local player name set to {_world.LocalPlayer.DisplayName}");
                }
                else
                {
                    AddConsoleLine("usage: set_name <text>");
                }
                break;
            case "set_dummy_name":
                if (RejectOnlineDummyCommand())
                {
                    break;
                }
                if (parts.Length >= 2)
                {
                    var name = commandText[command.Length..].Trim();
                    _world.SetEnemyPlayerName(name);
                    AddConsoleLine($"training dummy name set to {_world.EnemyPlayer.DisplayName}");
                }
                else
                {
                    AddConsoleLine("usage: set_dummy_name <text>");
                }
                break;
            case "set_friendly_name":
                if (RejectOnlineDummyCommand())
                {
                    break;
                }
                if (parts.Length >= 2)
                {
                    var name = commandText[command.Length..].Trim();
                    _world.SetFriendlyDummyName(name);
                    AddConsoleLine($"support dummy name set to {_world.FriendlyDummy.DisplayName}");
                }
                else
                {
                    AddConsoleLine("usage: set_friendly_name <text>");
                }
                break;
            case "killme":
                _world.ForceKillLocalPlayer();
                AddConsoleLine("local player killed");
                break;
            case "respawn_me":
                _world.ForceRespawnLocalPlayer();
                AddConsoleLine("local player respawned");
                break;
            case "build_sentry":
                AddConsoleLine(_world.TryBuildLocalSentry() ? "sentry build started" : "could not build sentry");
                break;
            case "destroy_sentry":
                AddConsoleLine(_world.TryDestroyLocalSentry() ? "sentry destroyed" : "no owned sentry to destroy");
                break;
            case "give_intel":
                AddConsoleLine(_world.ForceGiveEnemyIntelToLocalPlayer() ? "enemy intel granted" : "could not grant enemy intel");
                break;
            case "drop_intel":
                _world.ForceDropLocalIntel();
                AddConsoleLine("drop intel requested");
                break;
            case "set_hp":
                if (TryParseSingleInt(parts, out var health))
                {
                    _world.SetLocalHealth(health);
                    AddConsoleLine($"hp set to {Math.Clamp(health, 0, _world.LocalPlayer.MaxHealth)}");
                }
                else
                {
                    AddConsoleLine("usage: set_hp <n>");
                }
                break;
            case "set_ammo":
                if (TryParseSingleInt(parts, out var ammo))
                {
                    _world.SetLocalAmmo(ammo);
                    AddConsoleLine($"ammo set to {Math.Clamp(ammo, 0, _world.LocalPlayer.MaxShells)}");
                }
                else
                {
                    AddConsoleLine("usage: set_ammo <n>");
                }
                break;
            case "set_class":
                if (parts.Length >= 2 && TryParsePlayerClass(parts[1], out var playerClass))
                {
                    AddConsoleLine(_world.TrySetLocalClass(playerClass)
                        ? $"class set to {_world.LocalPlayer.ClassName}"
                        : $"class already {_world.LocalPlayer.ClassName}");
                }
                else
                {
                    AddConsoleLine("usage: set_class <scout|engineer|pyro|soldier|demoman|heavy|sniper|medic|spy|quote>");
                }
                break;
            case "load_map":
                if (parts.Length >= 2)
                {
                    AddConsoleLine(_world.TryLoadLevel(parts[1])
                        ? $"loaded map {_world.Level.Name}"
                        : $"usage: load_map <{string.Join("|", SimpleLevelFactory.GetAvailableSourceLevels().Select(entry => entry.Name.ToLowerInvariant()))}>");
                }
                else
                {
                    AddConsoleLine($"usage: load_map <{string.Join("|", SimpleLevelFactory.GetAvailableSourceLevels().Select(entry => entry.Name.ToLowerInvariant()))}>");
                }
                break;
            case "teleport":
                if (parts.Length >= 3
                    && float.TryParse(parts[1], out var x)
                    && float.TryParse(parts[2], out var y))
                {
                    _world.TeleportLocalPlayer(x, y);
                    AddConsoleLine($"teleported to ({_world.LocalPlayer.X:F1}, {_world.LocalPlayer.Y:F1})");
                }
                else
                {
                    AddConsoleLine("usage: teleport <x> <y>");
                }
                break;
            case "show_import":
                AddConsoleLine(_world.GetImportSummary());
                break;
            case "show_engineer":
                AddConsoleLine(_world.GetEngineerSummary());
                break;
            case "show_medic":
                AddConsoleLine(_world.GetMedicSummary());
                break;
            case "fill_uber":
                AddConsoleLine(_world.TryFillLocalMedicUber() ? "medic uber filled" : "local player is not medic");
                break;
            default:
                if (TryHandleEnemyDummyConsoleCommand(commandText))
                {
                    break;
                }

                AddConsoleLine($"unknown command: {command}");
                break;
        }
    }

    private static bool TryParseSingleInt(string[] parts, out int value)
    {
        value = 0;
        return parts.Length >= 2 && int.TryParse(parts[1], out value);
    }

    private static bool TryParsePlayerClass(string value, out PlayerClass playerClass)
    {
        playerClass = value.ToLowerInvariant() switch
        {
            "engineer" or "engi" => PlayerClass.Engineer,
            "pyro" => PlayerClass.Pyro,
            "soldier" or "solly" => PlayerClass.Soldier,
            "demoman" or "demo" => PlayerClass.Demoman,
            "heavy" => PlayerClass.Heavy,
            "sniper" => PlayerClass.Sniper,
            "medic" => PlayerClass.Medic,
            "spy" => PlayerClass.Spy,
            "scout" => PlayerClass.Scout,
            "quote" => PlayerClass.Quote,
            _ => default,
        };

        return value.Equals("scout", StringComparison.OrdinalIgnoreCase)
            || value.Equals("engineer", StringComparison.OrdinalIgnoreCase)
            || value.Equals("engi", StringComparison.OrdinalIgnoreCase)
            || value.Equals("pyro", StringComparison.OrdinalIgnoreCase)
            || value.Equals("soldier", StringComparison.OrdinalIgnoreCase)
            || value.Equals("solly", StringComparison.OrdinalIgnoreCase)
            || value.Equals("demoman", StringComparison.OrdinalIgnoreCase)
            || value.Equals("demo", StringComparison.OrdinalIgnoreCase)
            || value.Equals("heavy", StringComparison.OrdinalIgnoreCase)
            || value.Equals("sniper", StringComparison.OrdinalIgnoreCase)
            || value.Equals("medic", StringComparison.OrdinalIgnoreCase)
            || value.Equals("spy", StringComparison.OrdinalIgnoreCase)
            || value.Equals("quote", StringComparison.OrdinalIgnoreCase);
    }

    private void DrawConsoleOverlay()
    {
        var overlayRectangle = new Rectangle(18, 18, ViewportWidth - 36, 180);
        _spriteBatch.Draw(_pixel, overlayRectangle, new Color(10, 14, 18, 210));
        _spriteBatch.Draw(_pixel, new Rectangle(overlayRectangle.X, overlayRectangle.Y, overlayRectangle.Width, 2), new Color(245, 215, 120));

        var linePosition = new Vector2(overlayRectangle.X + 12, overlayRectangle.Y + 10);
        foreach (var line in _consoleHistory)
        {
            _spriteBatch.DrawString(_consoleFont, line, linePosition, new Color(230, 232, 235));
            linePosition.Y += 18f;
        }

        var promptText = $"> {_consoleInput}_";
        _spriteBatch.DrawString(
            _consoleFont,
            promptText,
            new Vector2(overlayRectangle.X + 12, overlayRectangle.Bottom - 30),
            new Color(255, 245, 190));
    }

    private void AddConsoleLine(string line)
    {
        if (string.IsNullOrWhiteSpace(line))
        {
            return;
        }

        _consoleHistory.Add(line);
        while (_consoleHistory.Count > ConsoleHistoryLimit)
        {
            _consoleHistory.RemoveAt(0);
        }
    }
}
