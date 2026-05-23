#nullable enable

using System;
using System.Collections.Generic;

namespace OpenGarrison.Client;

internal sealed record HudElementDefinition(
    string Id,
    string RendererId,
    int Layer,
    bool Editable = true);

internal sealed record HudElementInstance(
    string Id,
    string RendererId,
    int Layer);

internal sealed class HudElementContext
{
    private readonly HudElementRegistry _registry;

    internal HudElementContext(Game1 game, HudElementRegistry registry)
    {
        Game = game;
        _registry = registry;
    }

    public Game1 Game { get; }

    public bool TryCreateInstance(string id, out HudElementInstance instance)
    {
        return _registry.TryCreateInstance(id, out instance);
    }

    public void AddIfRegistered(List<HudElementInstance> elements, string id)
    {
        if (TryCreateInstance(id, out var instance))
        {
            elements.Add(instance);
        }
    }
}

internal sealed class HudElementRenderContext
{
    internal HudElementRenderContext(Game1 game)
    {
        Game = game;
    }

    public Game1 Game { get; }
}

internal interface IHudElementProvider
{
    void Collect(HudElementContext context, List<HudElementInstance> elements);
}

internal interface IHudElementRenderer
{
    void Draw(HudElementRenderContext context, HudElementInstance element);
}

internal sealed class HudElementRegistry
{
    private readonly Dictionary<string, HudElementDefinition> _definitionsById = new(StringComparer.Ordinal);
    private readonly Dictionary<string, IHudElementRenderer> _renderersById = new(StringComparer.Ordinal);
    private readonly List<IHudElementProvider> _providers = [];

    public void RegisterDefinition(HudElementDefinition definition)
    {
        _definitionsById[definition.Id] = definition;
    }

    public void RegisterProvider(IHudElementProvider provider)
    {
        _providers.Add(provider);
    }

    public void RegisterRenderer(string rendererId, IHudElementRenderer renderer)
    {
        _renderersById[rendererId] = renderer;
    }

    public void Collect(Game1 game, List<HudElementInstance> elements)
    {
        elements.Clear();
        var context = new HudElementContext(game, this);
        foreach (var provider in _providers)
        {
            provider.Collect(context, elements);
        }
    }

    public void Draw(Game1 game, HudElementInstance element)
    {
        if (!_renderersById.TryGetValue(element.RendererId, out var renderer))
        {
            return;
        }

        renderer.Draw(new HudElementRenderContext(game), element);
    }

    internal bool TryCreateInstance(string id, out HudElementInstance instance)
    {
        if (!_definitionsById.TryGetValue(id, out var definition))
        {
            instance = default!;
            return false;
        }

        instance = new HudElementInstance(definition.Id, definition.RendererId, definition.Layer);
        return true;
    }
}

internal sealed class DelegateHudElementProvider(Action<HudElementContext, List<HudElementInstance>> collect) : IHudElementProvider
{
    public void Collect(HudElementContext context, List<HudElementInstance> elements)
    {
        collect(context, elements);
    }
}

internal sealed class DelegateHudElementRenderer(Action<HudElementRenderContext, HudElementInstance> draw) : IHudElementRenderer
{
    public void Draw(HudElementRenderContext context, HudElementInstance element)
    {
        draw(context, element);
    }
}
