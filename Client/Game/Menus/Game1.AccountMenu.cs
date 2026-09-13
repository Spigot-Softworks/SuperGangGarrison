#nullable enable

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using OpenGarrison.ClientShared;

namespace OpenGarrison.Client;

public partial class Game1
{
    private enum AccountOperationKind
    {
        None,
        Refresh,
        Protect,
        ShortenCode,
        Login,
    }

    private enum AccountDialogKind
    {
        Login,
        ConfirmRecoveryReplacement,
    }

    private Task<AccountProfileResponse>? _accountOperationTask;
    private AccountOperationKind _accountOperationKind;
    private bool _accountDialogOpen;
    private AccountDialogKind _accountDialogKind;
    private string _accountLoginFriendCode = string.Empty;
    private string _accountLoginRecoveryKey = string.Empty;
    private int _accountDialogSelection;

    private bool IsAccountOperationPending => _accountOperationTask is not null;

    private bool CanShortenAccountFriendCode
    {
        get
        {
            if (!ClientIdentityDocument.TryNormalizeFriendCode(_clientIdentity.FriendCode, out var normalized))
            {
                return false;
            }

            return normalized.Replace("OG2-", string.Empty, StringComparison.OrdinalIgnoreCase)
                .Replace("-", string.Empty, StringComparison.Ordinal)
                .Length > 8;
        }
    }

    private string GetAccountRecoveryKeyDisplay()
        => string.IsNullOrWhiteSpace(_accountRecoveryKey)
            ? _accountIsProtected ? "Stored securely (not shown)" : "Not created"
            : _accountRecoveryKey;

    private string GetAccountStatusDisplay()
        => string.IsNullOrWhiteSpace(_accountStatusMessage) ? "Ready" : _accountStatusMessage;

    private void BeginAccountProfileRefresh(bool silent)
    {
        if (IsAccountOperationPending)
        {
            return;
        }

        if (!silent)
        {
            _accountStatusMessage = "Refreshing account...";
        }

        StartAccountOperation(AccountOperationKind.Refresh, _presenceClient.GetAccountProfileAsync(_clientIdentity));
    }

    private void BeginProtectAccount()
    {
        if (IsAccountOperationPending)
        {
            return;
        }

        if (_accountIsProtected)
        {
            _accountDialogKind = AccountDialogKind.ConfirmRecoveryReplacement;
            _accountDialogSelection = 0;
            _accountDialogOpen = true;
            return;
        }

        StartProtectAccount();
    }

    private void StartProtectAccount()
    {
        CloseAccountDialog();
        _accountStatusMessage = "Creating recovery key...";
        StartAccountOperation(AccountOperationKind.Protect, _presenceClient.ProtectAccountAsync(_clientIdentity));
    }

    private void BeginShortenAccountFriendCode()
    {
        if (IsAccountOperationPending)
        {
            return;
        }

        if (!CanShortenAccountFriendCode)
        {
            _accountStatusMessage = "This account already has an 8-character friend code.";
            return;
        }

        _accountStatusMessage = "Creating short friend code...";
        StartAccountOperation(AccountOperationKind.ShortenCode, _presenceClient.ShortenFriendCodeAsync(_clientIdentity));
    }

    private void OpenAccountLoginDialog()
    {
        if (IsAccountOperationPending)
        {
            return;
        }

        _accountDialogKind = AccountDialogKind.Login;
        _accountLoginFriendCode = string.Empty;
        _accountLoginRecoveryKey = string.Empty;
        _accountDialogSelection = 0;
        _accountDialogOpen = true;
    }

    private void CloseAccountDialog()
    {
        _accountDialogOpen = false;
        _accountDialogSelection = 0;
    }

    private void StartAccountOperation(AccountOperationKind kind, Task<AccountProfileResponse> task)
    {
        _accountOperationKind = kind;
        _accountOperationTask = task;
    }

    private void UpdateAccountOperation()
    {
        var task = _accountOperationTask;
        if (task is null || !task.IsCompleted)
        {
            return;
        }

        var operation = _accountOperationKind;
        _accountOperationTask = null;
        _accountOperationKind = AccountOperationKind.None;
        if (task.IsCanceled)
        {
            _accountStatusMessage = "Account request canceled.";
            return;
        }

        if (task.IsFaulted)
        {
            _accountStatusMessage = $"Account request failed: {task.Exception?.GetBaseException().Message ?? "request failed"}";
            return;
        }

        var profile = task.Result;
        ApplyAccountProfile(profile);
        if (!string.IsNullOrWhiteSpace(profile.DisplayName))
        {
            SetLocalPlayerNameFromSettings(profile.DisplayName);
        }

        _networkClient.UpdatePlayerProfile(
            _world.LocalPlayer.DisplayName,
            _world.LocalPlayer.BadgeMask,
            _clientIdentity.FriendCode,
            PlayerCardProfile.Serialize(_clientIdentity.PlayerCard));
        _lastSocialPresenceSignature = string.Empty;
        _socialPresenceSecondsUntilHeartbeat = 0d;

        _accountStatusMessage = operation switch
        {
            AccountOperationKind.Protect => $"Recovery key created: {_accountRecoveryKey}. Save it somewhere safe.",
            AccountOperationKind.ShortenCode => $"Friend code updated to {_clientIdentity.FriendCode}; old codes remain valid.",
            AccountOperationKind.Login => $"Signed in as {_clientIdentity.FriendCode}.",
            _ => "Account is up to date.",
        };

        if (operation == AccountOperationKind.Login)
        {
            _accountLoginFriendCode = string.Empty;
            _accountLoginRecoveryKey = string.Empty;
            CloseAccountDialog();
        }

        if (_networkClient.IsConnected && !_networkClient.IsReplayConnection)
        {
            BeginGameplayAccountAttach();
        }
    }

    private void SubmitAccountLogin()
    {
        if (IsAccountOperationPending)
        {
            return;
        }

        if (!ClientIdentityDocument.TryNormalizeFriendCode(_accountLoginFriendCode, out var friendCode))
        {
            _accountStatusMessage = "Enter a valid OG2 friend code.";
            return;
        }

        var recoveryKey = NormalizeRecoveryKeyInput(_accountLoginRecoveryKey);
        if (recoveryKey.Length != 8)
        {
            _accountStatusMessage = "Enter the 8-character recovery key.";
            return;
        }

        _accountStatusMessage = "Signing in...";
        StartAccountOperation(
            AccountOperationKind.Login,
            _presenceClient.LoginAccountAsync(_clientIdentity, friendCode, recoveryKey));
    }

    private void UpdateAccountDialog(KeyboardState keyboard, MouseState mouse)
    {
        if (!_accountDialogOpen)
        {
            return;
        }

        if (IsKeyPressed(keyboard, Keys.Escape) || IsControllerMenuBackPressed())
        {
            CloseAccountDialog();
            return;
        }

        var selectionCount = _accountDialogKind == AccountDialogKind.Login ? 4 : 2;
        if (TryConsumeControllerMenuNavigation(out _, out var verticalStep) && verticalStep != 0)
        {
            _accountDialogSelection = MoveControllerMenuSelection(_accountDialogSelection, selectionCount, verticalStep);
        }
        else if (IsKeyPressed(keyboard, Keys.Up))
        {
            _accountDialogSelection = MoveControllerMenuSelection(_accountDialogSelection, selectionCount, -1);
        }
        else if (IsKeyPressed(keyboard, Keys.Down))
        {
            _accountDialogSelection = MoveControllerMenuSelection(_accountDialogSelection, selectionCount, 1);
        }

        GetAccountDialogLayout(out _, out var fields, out var confirmBounds, out var cancelBounds);
        if (ShouldUseMouseMenuHover(mouse))
        {
            if (_accountDialogKind == AccountDialogKind.Login)
            {
                if (fields[0].Contains(mouse.Position))
                {
                    _accountDialogSelection = 0;
                }
                else if (fields[1].Contains(mouse.Position))
                {
                    _accountDialogSelection = 1;
                }
                else if (confirmBounds.Contains(mouse.Position))
                {
                    _accountDialogSelection = 2;
                }
                else if (cancelBounds.Contains(mouse.Position))
                {
                    _accountDialogSelection = 3;
                }
            }
            else
            {
                _accountDialogSelection = confirmBounds.Contains(mouse.Position)
                    ? 0
                    : cancelBounds.Contains(mouse.Position) ? 1 : _accountDialogSelection;
            }
        }

        var activate = IsKeyPressed(keyboard, Keys.Enter)
            || IsControllerMenuConfirmPressed()
            || (mouse.LeftButton == ButtonState.Pressed && _previousMouse.LeftButton != ButtonState.Pressed);
        if (!activate)
        {
            return;
        }

        if (_accountDialogKind == AccountDialogKind.ConfirmRecoveryReplacement)
        {
            if (_accountDialogSelection == 0)
            {
                StartProtectAccount();
            }
            else
            {
                CloseAccountDialog();
            }

            return;
        }

        if (_accountDialogSelection == 2)
        {
            SubmitAccountLogin();
        }
        else if (_accountDialogSelection == 3)
        {
            CloseAccountDialog();
        }
    }

    private bool TryHandleAccountDialogTextInput(char character)
    {
        if (!_accountDialogOpen || _accountDialogKind != AccountDialogKind.Login || _accountDialogSelection > 1)
        {
            return false;
        }

        ref var value = ref _accountDialogSelection == 0
            ? ref _accountLoginFriendCode
            : ref _accountLoginRecoveryKey;
        switch (character)
        {
            case '\b':
                if (value.Length > 0)
                {
                    value = value[..^1];
                }
                break;
            case '\t':
                _accountDialogSelection = (_accountDialogSelection + 1) % 2;
                break;
            case '\r':
            case '\n':
                if (_accountDialogSelection == 0)
                {
                    _accountDialogSelection = 1;
                }
                else
                {
                    SubmitAccountLogin();
                }
                break;
            default:
                if ((char.IsAsciiLetterOrDigit(character) || character == '-') && value.Length < 20)
                {
                    value += char.ToUpperInvariant(character);
                }
                break;
        }

        return true;
    }

    private void DrawAccountDialog()
    {
        if (!_accountDialogOpen)
        {
            return;
        }

        GetAccountDialogLayout(out var panel, out var fields, out var confirmBounds, out var cancelBounds);
        _spriteBatch.Draw(_pixel, new Rectangle(0, 0, ViewportWidth, ViewportHeight), Color.Black * 0.62f);
        DrawRoundedRectangleOutline(panel, new Color(59, 51, 46), new Color(213, 205, 188), outlineThickness: 2, radius: 8);

        if (_accountDialogKind == AccountDialogKind.ConfirmRecoveryReplacement)
        {
            DrawBitmapFontText("Replace Recovery Key?", new Vector2(panel.X + 20f, panel.Y + 20f), Color.White, 1.1f);
            DrawBitmapFontText("The previous key will stop working.", new Vector2(panel.X + 20f, panel.Y + 62f), new Color(230, 220, 180), 0.88f);
            DrawMenuButtonScaled(confirmBounds, "Replace Key", _accountDialogSelection == 0, 0.9f);
            DrawMenuButtonScaled(cancelBounds, "Cancel", _accountDialogSelection == 1, 0.9f);
            return;
        }

        DrawBitmapFontText("Sign In On This Device", new Vector2(panel.X + 20f, panel.Y + 18f), Color.White, 1.1f);
        DrawBitmapFontText("Friend Code", new Vector2(panel.X + 20f, fields[0].Y - 20f), new Color(230, 220, 180), 0.82f);
        DrawMenuInputBoxScaled(fields[0], _accountLoginFriendCode, _accountDialogSelection == 0, 0.94f, _accountLoginFriendCode.Length, _accountLoginFriendCode.Length);
        DrawBitmapFontText("Recovery Key", new Vector2(panel.X + 20f, fields[1].Y - 20f), new Color(230, 220, 180), 0.82f);
        DrawMenuInputBoxScaled(fields[1], _accountLoginRecoveryKey, _accountDialogSelection == 1, 0.94f, _accountLoginRecoveryKey.Length, _accountLoginRecoveryKey.Length);
        DrawMenuButtonScaled(confirmBounds, IsAccountOperationPending ? "Signing In..." : "Sign In", _accountDialogSelection == 2, 0.9f, !IsAccountOperationPending);
        DrawMenuButtonScaled(cancelBounds, "Cancel", _accountDialogSelection == 3, 0.9f);
    }

    private void GetAccountDialogLayout(
        out Rectangle panel,
        out Rectangle[] fields,
        out Rectangle confirmBounds,
        out Rectangle cancelBounds)
    {
        var width = Math.Min(510, ViewportWidth - 36);
        var height = _accountDialogKind == AccountDialogKind.Login ? 330 : 190;
        panel = new Rectangle((ViewportWidth - width) / 2, (ViewportHeight - height) / 2, width, height);
        fields =
        [
            new Rectangle(panel.X + 20, panel.Y + 86, panel.Width - 40, 40),
            new Rectangle(panel.X + 20, panel.Y + 166, panel.Width - 40, 40),
        ];
        confirmBounds = new Rectangle(panel.X + 20, panel.Bottom - 64, (panel.Width - 52) / 2, 42);
        cancelBounds = new Rectangle(confirmBounds.Right + 12, confirmBounds.Y, confirmBounds.Width, confirmBounds.Height);
    }

    private static string NormalizeRecoveryKeyInput(string value)
        => new(value.Where(char.IsAsciiLetterOrDigit).Select(char.ToUpperInvariant).ToArray());
}
