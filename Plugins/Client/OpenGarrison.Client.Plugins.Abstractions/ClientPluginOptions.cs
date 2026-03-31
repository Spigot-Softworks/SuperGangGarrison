using System;
using Microsoft.Xna.Framework.Input;

namespace OpenGarrison.Client.Plugins;

public sealed record ClientPluginChoiceOptionValue(int Value, string Label);

public sealed record ClientPluginOptionsSection(
    string Title,
    IReadOnlyList<ClientPluginOptionItem> Items);

public abstract class ClientPluginOptionItem(string label)
{
    public string Label { get; } = label;

    public abstract string GetValueLabel();

    public abstract void Activate();
}

public sealed class ClientPluginBooleanOptionItem(
    string label,
    Func<bool> getter,
    Action<bool> setter,
    string trueLabel = "Enabled",
    string falseLabel = "Disabled") : ClientPluginOptionItem(label)
{
    public override string GetValueLabel()
    {
        return getter() ? trueLabel : falseLabel;
    }

    public override void Activate()
    {
        setter(!getter());
    }
}

public sealed class ClientPluginChoiceOptionItem(
    string label,
    Func<int> getter,
    Action<int> setter,
    IReadOnlyList<ClientPluginChoiceOptionValue> choices) : ClientPluginOptionItem(label)
{
    public override string GetValueLabel()
    {
        var value = getter();
        for (var index = 0; index < choices.Count; index += 1)
        {
            if (choices[index].Value == value)
            {
                return choices[index].Label;
            }
        }

        return value.ToString(System.Globalization.CultureInfo.InvariantCulture);
    }

    public override void Activate()
    {
        if (choices.Count == 0)
        {
            return;
        }

        var current = getter();
        for (var index = 0; index < choices.Count; index += 1)
        {
            if (choices[index].Value == current)
            {
                setter(choices[(index + 1) % choices.Count].Value);
                return;
            }
        }

        setter(choices[0].Value);
    }
}

public sealed class ClientPluginIntegerOptionItem(
    string label,
    Func<int> getter,
    Action<int> setter,
    int minValue,
    int maxValue,
    int step = 1,
    Func<int, string>? valueLabelFormatter = null) : ClientPluginOptionItem(label)
{
    public override string GetValueLabel()
    {
        var value = getter();
        return valueLabelFormatter is null
            ? value.ToString(System.Globalization.CultureInfo.InvariantCulture)
            : valueLabelFormatter(value);
    }

    public override void Activate()
    {
        if (maxValue < minValue)
        {
            return;
        }

        var current = int.Clamp(getter(), minValue, maxValue);
        var normalizedStep = Math.Max(1, step);
        var next = current + normalizedStep;
        if (next > maxValue)
        {
            next = minValue;
        }

        setter(next);
    }
}

public sealed class ClientPluginKeyOptionItem(
    string label,
    Func<Keys> getter,
    Action<Keys> setter,
    Func<Keys, string>? valueLabelFormatter = null) : ClientPluginOptionItem(label)
{
    public Keys GetKey()
    {
        return getter();
    }

    public void SetKey(Keys key)
    {
        setter(key);
    }

    public override string GetValueLabel()
    {
        var key = getter();
        return valueLabelFormatter is null
            ? key.ToString()
            : valueLabelFormatter(key);
    }

    public override void Activate()
    {
    }
}
