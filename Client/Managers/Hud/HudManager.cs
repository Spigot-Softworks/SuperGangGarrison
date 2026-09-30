#nullable enable

using static OpenGarrison.Client.Game1;

namespace OpenGarrison.Client;

public sealed class HudManager
{
    private readonly IHudContext _context;

    public HudManager(IHudContext context)
    {
        _context = context;
        LocalStatus = new GameplayLocalStatusHudController(context);
        Medic = new GameplayMedicHudController(context);
        Engineer = new GameplayEngineerHudController(context);
        Aim = new GameplayAimHudController(context);
        PlayerName = new GameplayPlayerNameHudController(context);
        Editor = new HudEditorController(context);
        CustomBubbleEditor = new CustomBubbleEditorController(context);
    }

    public GameplayLocalStatusHudController LocalStatus { get; }

    public GameplayMedicHudController Medic { get; }

    public GameplayEngineerHudController Engineer { get; }

    public GameplayAimHudController Aim { get; }

    public GameplayPlayerNameHudController PlayerName { get; }

    public HudEditorController Editor { get; }

    public CustomBubbleEditorController CustomBubbleEditor { get; }
}
