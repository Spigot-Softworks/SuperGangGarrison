#nullable enable

using static OpenGarrison.Client.Game1;

namespace OpenGarrison.Client;

public sealed class GameplayManager
{
    private readonly IGameplayContext _context;

    public GameplayManager(IGameplayContext context)
    {
        _context = context;
        Frame = new FrameController(context);
        Bootstrap = new BootstrapController(context);
        InputUpdate = new GameplayInputUpdateController(context);
        PresentationUpdate = new GameplayPresentationUpdateController(context);
        WorldDraw = new GameplayWorldDrawController(context);
        OverlayDraw = new GameplayOverlayDrawController(context);
        Update = new GameplayUpdateController(context);
        Draw = new GameplayDrawController(context);
        Gameplay = new GameplayController(context);
        ScreenState = new GameplayScreenStateController(context);
        PresentationState = new GameplayPresentationStateController(context);
        ImpactEffects = new GameplayImpactEffectsController(context);
        GoreEffects = new GameplayGoreEffectsController(context);
        SmokeEffects = new GameplaySmokeEffectsController(context);
        MaterialEffects = new GameplayMaterialEffectsController(context);
        VisualEvents = new GameplayVisualEventController(context);
        SessionState = new GameplaySessionStateController(context);
        OnlineSession = new OnlineSessionController(context);
        OfflineSession = new OfflineSessionController(context);
        Session = new GameplaySessionController(context);
        OverlayState = new GameplayOverlayStateController(context);
        Reset = new GameplayResetController(context);
        Overlay = new GameplayOverlayController(context);
    }

    public FrameController Frame { get; }

    public BootstrapController Bootstrap { get; }

    public GameplayInputUpdateController InputUpdate { get; }

    public GameplayPresentationUpdateController PresentationUpdate { get; }

    public GameplayWorldDrawController WorldDraw { get; }

    public GameplayOverlayDrawController OverlayDraw { get; }

    public GameplayUpdateController Update { get; }

    public GameplayDrawController Draw { get; }

    public GameplayController Gameplay { get; }

    public GameplayScreenStateController ScreenState { get; }

    public GameplayPresentationStateController PresentationState { get; }

    public GameplayImpactEffectsController ImpactEffects { get; }

    public GameplayGoreEffectsController GoreEffects { get; }

    public GameplaySmokeEffectsController SmokeEffects { get; }

    public GameplayMaterialEffectsController MaterialEffects { get; }

    public GameplayVisualEventController VisualEvents { get; }

    public GameplaySessionStateController SessionState { get; }

    public OnlineSessionController OnlineSession { get; }

    public OfflineSessionController OfflineSession { get; }

    public GameplaySessionController Session { get; }

    public GameplayOverlayStateController OverlayState { get; }

    public GameplayResetController Reset { get; }

    public GameplayOverlayController Overlay { get; }
}
