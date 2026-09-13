#nullable enable

using OpenGarrison.Server;
using OpenGarrison.Server.Plugins;
using Xunit;

namespace OpenGarrison.PluginHost.Tests;

public sealed class PluginVoteRegistryTests
{
    [Fact]
    public void RegistrationsAreOwnerScopedAndShortIdsMustBeUnambiguous()
    {
        var registry = new PluginVoteRegistry();
        Assert.True(registry.TryRegister("plugin.one", Registration("restart"), out var firstError), firstError);
        Assert.True(registry.TryRegister("plugin.two", Registration("restart"), out var secondError), secondError);

        Assert.False(registry.TryResolvePublic("restart", out _, out var ambiguousError));
        Assert.Contains("ambiguous", ambiguousError, StringComparison.OrdinalIgnoreCase);
        Assert.True(registry.TryResolvePublic("plugin.one:restart", out var resolved, out var resolveError), resolveError);
        Assert.Equal("plugin.one", resolved.OwnerPluginId);
        Assert.Equal("plugin.one:restart", resolved.GlobalId);
    }

    [Fact]
    public void DuplicateInvalidAndStaleRegistrationsAreRejected()
    {
        var registry = new PluginVoteRegistry();
        var original = Registration("valid-id");
        Assert.True(registry.TryRegister("plugin.one", original, out _));
        Assert.True(registry.TryGetOwned("plugin.one", "valid-id", out var registered));
        Assert.True(registry.IsCurrent(registered));

        Assert.False(registry.TryRegister("plugin.one", original, out var duplicateError));
        Assert.Contains("already registered", duplicateError, StringComparison.OrdinalIgnoreCase);
        Assert.False(registry.TryRegister("plugin.one", Registration("not valid!"), out var invalidError));
        Assert.Contains("Vote IDs", invalidError, StringComparison.Ordinal);
        Assert.False(registry.TryRegister(
            "plugin.two",
            Registration("control") with { DisplayName = "Bad\nName" },
            out var controlError));
        Assert.Contains("single-line", controlError, StringComparison.OrdinalIgnoreCase);
        Assert.False(registry.TryRegister(
            "plugin.two",
            Registration("unicode") with { DisplayName = new string('\uD800', 1) },
            out var unicodeError));
        Assert.Contains("UTF-8", unicodeError, StringComparison.OrdinalIgnoreCase);

        Assert.True(registry.RemoveOwner("plugin.one"));
        Assert.False(registry.IsCurrent(registered));
        Assert.Empty(registry.GetCatalog());
    }

    private static OpenGarrisonServerVoteRegistration Registration(string id)
        => new(
            id,
            "Restart Round",
            "Restarts the round.",
            OpenGarrisonServerVoteTargetKind.None,
            static _ => true);
}
