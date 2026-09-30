#nullable enable

using static OpenGarrison.Client.Game1;

namespace OpenGarrison.Client;

public sealed class InputManager
{
    private readonly IInputContext _context;

    public InputManager(IInputContext context)
    {
        _context = context;
        WindowTextInput = new WindowTextInputController(context);
        MenuTextInput = new MenuTextInputController(context);
        NetworkPromptTextInput = new NetworkPromptTextInputController(context);
        ChatTextInput = new ChatTextInputController(context);
        ConsoleTextInput = new ConsoleTextInputController(context);
    }

    public WindowTextInputController WindowTextInput { get; }

    public MenuTextInputController MenuTextInput { get; }

    public NetworkPromptTextInputController NetworkPromptTextInput { get; }

    public ChatTextInputController ChatTextInput { get; }

    public ConsoleTextInputController ConsoleTextInput { get; }
}
