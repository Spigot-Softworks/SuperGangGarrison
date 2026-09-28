using OpenGarrison.Client;
using OpenGarrison.Core;
using Xunit;

namespace OpenGarrison.PluginHost.Tests;

public sealed class ManagerCompositionTests
{
    [Fact]
    public void ManagersResolveAsRegisteredInstances()
    {
        var container = new ClientServiceContainer();
        var audio = new AudioManager(null!);
        var input = new InputManager(null!);
        var menu = new MenuManager(null!);
        var session = new SessionManager(null!);
        var hosting = new HostingManager(null!);
        var plugin = new PluginManager(null!);
        var gameplay = new GameplayManager(null!);
        var hud = new HudManager(null!);

        container.Register(audio);
        container.Register(input);
        container.Register(menu);
        container.Register(session);
        container.Register(hosting);
        container.Register(plugin);
        container.Register(gameplay);
        container.Register(hud);

        Assert.Same(audio, container.Get<AudioManager>());
        Assert.Same(input, container.Get<InputManager>());
        Assert.Same(menu, container.Get<MenuManager>());
        Assert.Same(session, container.Get<SessionManager>());
        Assert.Same(hosting, container.Get<HostingManager>());
        Assert.Same(plugin, container.Get<PluginManager>());
        Assert.Same(gameplay, container.Get<GameplayManager>());
        Assert.Same(hud, container.Get<HudManager>());
    }
}
