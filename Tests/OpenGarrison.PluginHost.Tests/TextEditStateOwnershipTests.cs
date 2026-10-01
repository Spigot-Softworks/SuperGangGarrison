using System.Globalization;
using System.Reflection;
using System.Runtime.CompilerServices;
using OpenGarrison.Client;
using OpenGarrison.Core;
using Xunit;

namespace OpenGarrison.PluginHost.Tests;

public sealed class TextEditStateOwnershipTests
{
    [Fact]
    public void TextEditStatesAreStableDistinctWithinAndBetweenGames()
    {
        var first = CreateGameWithInputManager();
        var second = CreateGameWithInputManager();
        var firstInput = ((IInputContext)first.Game).Input;
        var secondInput = ((IInputContext)second.Game).Input;
        var firstSession = (ISessionContext)first.Game;
        var secondSession = (ISessionContext)second.Game;
        var firstMenu = (IMenuContext)first.Game;
        var secondMenu = (IMenuContext)second.Game;

        var firstStates = GetStates(firstInput);
        var secondStates = GetStates(secondInput);

        Assert.Same(first.InputManager, firstInput);
        Assert.Same(second.InputManager, secondInput);
        AssertDistinct(firstStates);
        AssertDistinct(secondStates);
        var defaultTexts = new[]
        {
            string.Empty,
            string.Empty,
            string.Empty,
            "127.0.0.1",
            OpenGarrisonPreferencesDocument.DefaultServerPort.ToString(CultureInfo.InvariantCulture),
            string.Empty,
            string.Empty,
            string.Empty,
        };
        for (var index = 0; index < firstStates.Length; index++)
        {
            Assert.NotSame(firstStates[index], secondStates[index]);
            Assert.Equal(defaultTexts[index], secondStates[index].Text);
            Assert.Equal(0, secondStates[index].CursorIndex);
            Assert.Equal(0, secondStates[index].SelectionStart);
        }

        Assert.Same(firstInput.NetworkPromptTextInput.Edit, firstSession.PasswordEdit);
        Assert.Same(firstInput.MenuTextInput.ConnectHostEdit, firstSession.ConnectHostEdit);
        Assert.Same(firstInput.MenuTextInput.ConnectPortEdit, firstSession.ConnectPortEdit);
        Assert.Same(firstInput.MenuTextInput.FriendNicknameEdit, firstMenu.FriendNicknameEdit);
        Assert.Same(firstSession.PasswordEdit, firstSession.PasswordEdit);
        Assert.Same(firstSession.ConnectHostEdit, firstSession.ConnectHostEdit);
        Assert.Same(firstSession.ConnectPortEdit, firstSession.ConnectPortEdit);
        Assert.Same(firstMenu.FriendNicknameEdit, firstMenu.FriendNicknameEdit);

        firstSession.PasswordEdit.Text = "secret";
        firstSession.PasswordEdit.CursorIndex = 4;
        firstSession.PasswordEdit.SelectionStart = 1;
        Assert.Same(firstSession.PasswordEdit, firstInput.NetworkPromptTextInput.Edit);
        Assert.Equal("secret", firstInput.NetworkPromptTextInput.Edit.Text);
        Assert.Equal(4, firstInput.NetworkPromptTextInput.Edit.CursorIndex);
        Assert.Equal(1, firstInput.NetworkPromptTextInput.Edit.SelectionStart);

        firstSession.ConnectHostEdit.Text = "example.test";
        firstSession.ConnectHostEdit.CursorIndex = 7;
        firstSession.ConnectHostEdit.SelectionStart = 2;
        Assert.Same(firstSession.ConnectHostEdit, firstInput.MenuTextInput.ConnectHostEdit);
        Assert.Equal("example.test", firstInput.MenuTextInput.ConnectHostEdit.Text);
        Assert.Equal(7, firstInput.MenuTextInput.ConnectHostEdit.CursorIndex);
        Assert.Equal(2, firstInput.MenuTextInput.ConnectHostEdit.SelectionStart);

        firstSession.ConnectPortEdit.Text = "1234";
        firstSession.ConnectPortEdit.CursorIndex = 3;
        firstSession.ConnectPortEdit.SelectionStart = 1;
        Assert.Same(firstSession.ConnectPortEdit, firstInput.MenuTextInput.ConnectPortEdit);
        Assert.Equal("1234", firstInput.MenuTextInput.ConnectPortEdit.Text);
        Assert.Equal(3, firstInput.MenuTextInput.ConnectPortEdit.CursorIndex);
        Assert.Equal(1, firstInput.MenuTextInput.ConnectPortEdit.SelectionStart);

        firstMenu.FriendNicknameEdit.Text = "player";
        firstMenu.FriendNicknameEdit.CursorIndex = 5;
        firstMenu.FriendNicknameEdit.SelectionStart = 2;
        Assert.Same(firstMenu.FriendNicknameEdit, firstInput.MenuTextInput.FriendNicknameEdit);
        Assert.Equal("player", firstInput.MenuTextInput.FriendNicknameEdit.Text);
        Assert.Equal(5, firstInput.MenuTextInput.FriendNicknameEdit.CursorIndex);
        Assert.Equal(2, firstInput.MenuTextInput.FriendNicknameEdit.SelectionStart);

        Assert.Equal("", secondSession.PasswordEdit.Text);
        Assert.Equal(0, secondSession.PasswordEdit.CursorIndex);
        Assert.Equal(0, secondSession.PasswordEdit.SelectionStart);
        Assert.Equal("127.0.0.1", secondSession.ConnectHostEdit.Text);
        Assert.Equal(0, secondSession.ConnectHostEdit.CursorIndex);
        Assert.Equal(0, secondSession.ConnectHostEdit.SelectionStart);
        Assert.Equal(OpenGarrisonPreferencesDocument.DefaultServerPort.ToString(CultureInfo.InvariantCulture), secondSession.ConnectPortEdit.Text);
        Assert.Equal(0, secondSession.ConnectPortEdit.CursorIndex);
        Assert.Equal(0, secondSession.ConnectPortEdit.SelectionStart);
        Assert.Equal("", secondMenu.FriendNicknameEdit.Text);
        Assert.Equal(0, secondMenu.FriendNicknameEdit.CursorIndex);
        Assert.Equal(0, secondMenu.FriendNicknameEdit.SelectionStart);
    }

    private static TextEditState[] GetStates(InputManager input)
    {
        return
        [
            input.ConsoleTextInput.Edit,
            input.NetworkPromptTextInput.Edit,
            input.ChatTextInput.Edit,
            input.MenuTextInput.ConnectHostEdit,
            input.MenuTextInput.ConnectPortEdit,
            input.MenuTextInput.FriendNicknameEdit,
            input.MenuTextInput.FriendCodeEdit,
            input.MenuTextInput.FriendMessageEdit,
        ];
    }

    private static void AssertDistinct(TextEditState[] states)
    {
        for (var left = 0; left < states.Length; left++)
        {
            for (var right = left + 1; right < states.Length; right++)
            {
                Assert.NotSame(states[left], states[right]);
            }
        }
    }

    private static (Game1 Game, InputManager InputManager) CreateGameWithInputManager()
    {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        var game = (Game1)RuntimeHelpers.GetUninitializedObject(typeof(Game1));
        var services = new ClientServiceContainer();
        typeof(Game1).GetField("_services", flags)!.SetValue(game, services);
        var inputManager = new InputManager(game);
        services.Register(inputManager);
        return (game, inputManager);
    }
}
