#nullable enable

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using System;
using System.Globalization;
using OpenGarrison.Core;

namespace OpenGarrison.Client;

public partial class Game1
{
    private void UpdateTeamSelect(MouseState mouse)
    {
        if (!_teamSelectOpen)
        {
            _teamSelectHoverIndex = -1;
            if (_teamSelectAlpha > 0.01f)
            {
                _teamSelectAlpha = AdvanceClosingAlpha(_teamSelectAlpha, 0.01f);
            }

            if (_teamSelectPanelY > -120f)
            {
                _teamSelectPanelY = MathF.Max(-120f, _teamSelectPanelY - ScaleLegacyUiDistance(15f));
            }

            return;
        }

        if (_teamSelectAlpha < 0.99f)
        {
            _teamSelectAlpha = AdvanceOpeningAlpha(_teamSelectAlpha, 0.01f, 0.99f);
        }

        if (_teamSelectPanelY < 120f)
        {
            _teamSelectPanelY = MathF.Min(120f, _teamSelectPanelY + ScaleLegacyUiDistance(15f));
        }

        var panelLeft = (ViewportWidth / 2f) - 400f;
        _teamSelectHoverIndex = GetTeamSelectHoverIndex(mouse.X, mouse.Y, panelLeft);
        var clickPressed = mouse.LeftButton == ButtonState.Pressed && _previousMouse.LeftButton != ButtonState.Pressed;
        if (!clickPressed || _teamSelectHoverIndex < 0)
        {
            return;
        }

        ApplyTeamSelection(_teamSelectHoverIndex);
    }

    private void DrawTeamSelectHud()
    {
        var viewportWidth = ViewportWidth;
        var viewportHeight = ViewportHeight;
        var panelLeft = (viewportWidth / 2f) - 400f;
        var alpha = Math.Clamp(_teamSelectAlpha, 0.01f, 0.99f);
        _spriteBatch.Draw(_pixel, new Rectangle(0, 0, viewportWidth, viewportHeight), Color.Black * MathF.Min(0.8f, alpha));
        TryDrawScreenSprite("TeamSelectS", 0, new Vector2(viewportWidth / 2f, _teamSelectPanelY), Color.White * alpha, Vector2.One);

        if (_teamSelectHoverIndex < 0 || _teamSelectPanelY < 120f)
        {
            return;
        }

        var drawX = GetTeamSelectDrawX(_teamSelectHoverIndex);
        var lines = GetTeamSelectDescription(_teamSelectHoverIndex);
        var balance = GetTeamBalance();
        if (_teamSelectHoverIndex != 1 && _teamSelectHoverIndex != 4)
        {
            var doorFrame = _teamSelectHoverIndex switch
            {
                0 => 2,
                2 => balance == PlayerTeam.Red ? 3 : 0,
                3 => balance == PlayerTeam.Blue ? 4 : 1,
                _ => -1,
            };
            if (doorFrame >= 0 && doorFrame != 3 && doorFrame != 4)
            {
                TryDrawScreenSprite("TeamDoorS", doorFrame, new Vector2(panelLeft + drawX, 48f), Color.White, Vector2.One);
                TryDrawScreenSprite("DoorTopLightUpS", doorFrame, new Vector2(panelLeft + drawX + 16f, 0f), Color.White, Vector2.One);
            }
        }

        if (_teamSelectHoverIndex == 1)
        {
            TryDrawScreenSprite("TVLightUpS", 0, new Vector2(panelLeft + drawX, 118f), Color.White, Vector2.One);
        }

        float[] lineY = [80f, 100f, 120f, 130f, 140f];
        for (var index = 0; index < lines.Length; index += 1)
        {
            DrawBitmapFontText(lines[index], new Vector2(panelLeft + 495f, lineY[index]), Color.White * alpha, 1f);
        }

        DrawBitmapFontText(GetTeamCount(PlayerTeam.Red).ToString(CultureInfo.InvariantCulture), new Vector2(panelLeft + 284f, 26f), Color.Black * alpha, 1f);
        DrawBitmapFontText(GetTeamCount(PlayerTeam.Blue).ToString(CultureInfo.InvariantCulture), new Vector2(panelLeft + 396f, 26f), Color.Black * alpha, 1f);
    }

    private static int GetTeamSelectHoverIndex(int mouseX, int mouseY, float panelLeft)
    {
        var regions = new (float Left, float Right, float Top, float Bottom)[]
        {
            (40f, 127f, 48f, 223f),
            (156f, 193f, 118f, 151f),
            (228f, 315f, 48f, 223f),
            (340f, 427f, 48f, 223f),
        };

        for (var index = 0; index < regions.Length; index += 1)
        {
            var region = regions[index];
            if (mouseX > panelLeft + region.Left && mouseX < panelLeft + region.Right && mouseY > region.Top && mouseY < region.Bottom)
            {
                return index;
            }
        }

        return -1;
    }

    private void ApplyTeamSelection(int hoverIndex)
    {
        if (hoverIndex == 1)
        {
            if (_networkClient.IsConnected)
            {
                _networkClient.QueueSpectateSelection();
                _teamSelectOpen = false;
                _classSelectOpen = false;
                _menuStatusMessage = "Switching to spectator mode...";
            }
            else
            {
                _menuStatusMessage = GetOfflineSpectateUnavailableMessage();
            }

            return;
        }

        var balance = GetTeamBalance();
        if ((hoverIndex == 2 && balance == PlayerTeam.Red) || (hoverIndex == 3 && balance == PlayerTeam.Blue))
        {
            return;
        }

        var selectedTeam = hoverIndex switch
        {
            0 => GetAutoSelectedTeam(balance),
            2 => PlayerTeam.Red,
            3 => PlayerTeam.Blue,
            _ => GetAutoSelectedTeam(balance),
        };
        _pendingClassSelectTeam = selectedTeam;

        if (_networkClient.IsConnected)
        {
            _networkClient.QueueTeamSelection(selectedTeam);
            if (_networkClient.IsSpectator)
            {
                _teamSelectOpen = false;
                _classSelectOpen = false;
                _menuStatusMessage = selectedTeam switch
                {
                    PlayerTeam.Red => "Joining RED team...",
                    PlayerTeam.Blue => "Joining BLU team...",
                    _ => "Joining team...",
                };
                return;
            }
        }
        else
        {
            _world.SetLocalPlayerTeam(selectedTeam);
            ApplyPracticeTeamSelection(selectedTeam);
        }

        _teamSelectOpen = false;
        _classSelectOpen = true;
    }

    private PlayerTeam GetAutoSelectedTeam(PlayerTeam? balance)
    {
        if (!balance.HasValue)
        {
            return _visualRandom.Next(2) == 0 ? PlayerTeam.Red : PlayerTeam.Blue;
        }

        return balance == PlayerTeam.Red ? PlayerTeam.Blue : PlayerTeam.Red;
    }

    private PlayerTeam? GetTeamBalance()
    {
        var redSuperiority = 0;
        foreach (var player in EnumerateRemotePlayersForView())
        {
            if (player.Team == PlayerTeam.Red)
            {
                redSuperiority += 1;
            }
            else if (player.Team == PlayerTeam.Blue)
            {
                redSuperiority -= 1;
            }
        }

        if (redSuperiority > 0)
        {
            return PlayerTeam.Red;
        }

        if (redSuperiority < 0)
        {
            return PlayerTeam.Blue;
        }

        return null;
    }

    private int GetTeamCount(PlayerTeam team)
    {
        var count = 0;
        if (!_networkClient.IsSpectator && !_world.LocalPlayerAwaitingJoin && _world.LocalPlayer.Team == team)
        {
            count += 1;
        }

        foreach (var player in EnumerateRemotePlayersForView())
        {
            if (player.Team == team)
            {
                count += 1;
            }
        }

        return count;
    }

    private static int GetTeamSelectDrawX(int hoverIndex)
    {
        return hoverIndex switch
        {
            0 => 40,
            1 => 156,
            2 => 228,
            3 => 340,
            _ => 40,
        };
    }

    private static string[] GetTeamSelectDescription(int hoverIndex)
    {
        return hoverIndex switch
        {
            0 => ["Auto Select", string.Empty, "Let us place you on the team", "that needs you the most!", string.Empty],
            1 => ["Spectate", string.Empty, "Watch the match without", "taking a player slot.", string.Empty],
            2 => ["RED", "Respectable Elucidation Division", "A private company dedicated to", "illicit information acquisition", "and other shady activities."],
            3 => ["BLU", "Bolstered Locks Unlimited", "The leading name in freelance", "security and use of brute force", "in property protection."],
            _ => [string.Empty, string.Empty, string.Empty, string.Empty, string.Empty],
        };
    }
}
