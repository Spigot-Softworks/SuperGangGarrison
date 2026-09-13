#nullable enable
using System;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using OpenGarrison.ClientShared;

namespace OpenGarrison.Client;

public partial class Game1
{
    private string[] GetPeerLobbyButtons()
    {
        var local = _peerRoomSession?.State?.Players?.FirstOrDefault(player => player.Slot == _peerRoomSession.Connection.Grant.Slot);
        return [local?.Ready == true ? "Not Ready" : "Ready Up", "Start", "Copy Room Code",
            _practiceCoOpMenu ? $"Team: {local?.Team ?? "Red"}" : "Difficulty",
            _practiceCoOpMenu ? "Edit Settings" : "Standard / Hardcore", "Leave Room"];
    }
    private (Rectangle Panel, Rectangle[] Buttons, float Scale) GetPeerLobbyLayout()
    {
        var scale = Math.Min(1.5f, Math.Min(ViewportWidth / 650f, ViewportHeight / 470f));
        var panel = new Rectangle((int)((ViewportWidth - 590 * scale) / 2), (int)((ViewportHeight - 430 * scale) / 2),
            (int)(590 * scale), (int)(430 * scale));
        var buttons = new Rectangle[6];
        for (var i = 0; i < buttons.Length; i++)
            buttons[i] = new(panel.X + (int)((25 + i % 2 * 280) * scale), panel.Y + (int)((272 + i / 2 * 48) * scale),
                (int)(260 * scale), (int)(36 * scale));
        return (panel, buttons, scale);
    }
    private void UpdatePeerLobby(KeyboardState keyboard, MouseState mouse)
    {
        if (IsKeyPressed(keyboard, Keys.Escape) || IsControllerMenuBackPressed()) { ExitPeerLobby(); return; }
        var layout = GetPeerLobbyLayout();
        var horizontal = 0; var vertical = 0;
        if (IsKeyPressed(keyboard, Keys.Left)) horizontal = -1;
        else if (IsKeyPressed(keyboard, Keys.Right)) horizontal = 1;
        else if (IsKeyPressed(keyboard, Keys.Up)) vertical = -1;
        else if (IsKeyPressed(keyboard, Keys.Down)) vertical = 1;
        if (TryConsumeControllerMenuNavigation(out var x, out var y)) { horizontal += x; vertical += y; }
        if (horizontal != 0 || vertical != 0) _lastToDieMenuHoverIndex = Math.Clamp(_lastToDieMenuHoverIndex + horizontal + 2 * vertical, 0, 5);
        if (ShouldUseMouseMenuHover(mouse))
            for (var i = 0; i < 6; i++) if (layout.Buttons[i].Contains(mouse.Position)) _lastToDieMenuHoverIndex = i;
        if (IsKeyPressed(keyboard, Keys.Enter) || IsControllerMenuConfirmPressed()
            || (mouse.LeftButton == ButtonState.Pressed && _previousMouse.LeftButton != ButtonState.Pressed
                && layout.Buttons.Any(button => button.Contains(mouse.Position))))
            ActivatePeerLobbyButton(_lastToDieMenuHoverIndex);
    }
    private void ActivatePeerLobbyButton(int index)
    {
        var room = _peerRoomSession;
        if (room?.State?.Settings is not { } settings) return;
        var local = room.State.Players!.First(player => player.Slot == room.Connection.Grant.Slot);
        switch (index)
        {
            case 0: room.Connection.Send(new("ready", Ready: !local.Ready)); break;
            case 1: if (room.IsOwner) room.Connection.Send(new("start")); else _menuStatusMessage = "The host starts the match."; break;
            case 2:
                if (OperatingSystem.IsBrowser() && BrowserPreferenceStore.CopyText is { } copy)
                    _managedRoomCopyTask = copy(_hostedLastToDieRoomCode);
                else _menuStatusMessage = TrySetClipboardText(_hostedLastToDieRoomCode) ? "Room code copied." : "Room code: " + _hostedLastToDieRoomCode;
                break;
            case 3:
                if (_practiceCoOpMenu) room.Connection.Send(new("team", Team: local.Team == "Red" ? "Blue" : "Red"));
                else goto case 4;
                break;
            case 4:
                if (_practiceCoOpMenu) EditPeerPracticeSettings();
                else if (room.IsOwner) room.Connection.Send(new("settings", Revision: room.State.Revision,
                    Settings: settings with { Difficulty = settings.Difficulty == "standard" ? "hardcore" : "standard" }));
                break;
            case 5: ExitPeerLobby(); break;
        }
    }
    private void ExitPeerLobby()
    {
        LeavePeerRoom(); _networkClient.Disconnect();
        _lastToDieMenuPage = LastToDieMenuPage.CoOp;
        _menuStatusMessage = string.Empty;
    }
    private void DrawPeerLobby()
    {
        var (panel, buttons, scale) = GetPeerLobbyLayout();
        _spriteBatch.Draw(_pixel, new Rectangle(0, 0, ViewportWidth, ViewportHeight), Color.Black * .85f);
        _spriteBatch.Draw(_pixel, panel, new Color(31, 29, 27));
        void Label(string text, float x, float y, Color color, float size = 1f) => DrawShadowedMenuBitmapFontText(
            text, new Vector2(panel.X + x * scale, panel.Y + y * scale), color, scale * size);
        var state = _peerRoomSession?.State;
        Label(_practiceCoOpMenu ? "Practice Co-op" : "Last To Die Co-op", 25, 18, Color.White, 1.25f);
        Label($"Room: {_hostedLastToDieRoomCode}    Players: {state?.Players?.Length ?? 1}/4", 25, 51, Color.Gold);
        for (byte slot = 1; slot <= 4; slot++)
        {
            var player = state?.Players?.FirstOrDefault(player => player.Slot == slot);
            var name = player?.Name ?? "Waiting for player...";
            if (name.Length > 20) name = name[..20];
            Label($"{slot}. {name}{(slot == 1 ? " (Host)" : "")}", 25, 89 + (slot - 1) * 32, Color.White, .9f);
            if (_practiceCoOpMenu && player is not null)
                Label(player.Team, 325, 89 + (slot - 1) * 32, player.Team == "Red" ? Color.Salmon : Color.LightBlue, .8f);
            Label(player is null ? "Empty" : !player.Connected ? "Reconnecting" : player.Ready ? "Ready" : "Not Ready",
                400, 89 + (slot - 1) * 32, player?.Ready == true ? Color.LightGreen : Color.Gold, .85f);
        }
        if (state?.Settings is { } settings)
        {
            Label(_practiceCoOpMenu ? $"{settings.Map} - {settings.TickRate} Hz - {settings.TimeLimitMinutes} min - {settings.CaptureLimit} caps"
                : $"Difficulty: {settings.Difficulty}", 25, 226, Color.White, .83f);
            if (_practiceCoOpMenu) Label($"Bots: {settings.RedBots} red / {settings.BlueBots} blue - Respawn: {settings.RespawnSeconds}s", 25, 247, Color.White, .8f);
        }
        var labels = GetPeerLobbyButtons();
        for (var i = 0; i < buttons.Length; i++) DrawMenuButtonScaled(buttons[i], labels[i], i == _lastToDieMenuHoverIndex, scale * .9f);
        if (!string.IsNullOrWhiteSpace(_menuStatusMessage))
            DrawShadowedMenuBitmapFontText(_menuStatusMessage.Length > 75 ? _menuStatusMessage[..75] : _menuStatusMessage,
                new Vector2(panel.Left, panel.Bottom + 8 * scale), Color.White, scale * .75f);
    }
}
