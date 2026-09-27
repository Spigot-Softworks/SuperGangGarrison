using System;
using Microsoft.Xna.Framework.Input;

namespace OpenGarrison.Client.Plugins;

/// <summary>
/// A labeled choice value for a choice option item.
/// </summary>
/// <param name="Value">The option value.</param>
/// <param name="Label">The label shown for the value.</param>
public sealed record ClientPluginChoiceOptionValue(int Value, string Label);

/// <summary>
/// A section of plugin-contributed options shown in the options menu.
/// </summary>
/// <param name="Title">The section title.</param>
/// <param name="Items">The option items in the section.</param>
public sealed record ClientPluginOptionsSection(
    string Title,
    IReadOnlyList<ClientPluginOptionItem> Items);

/// <summary>
/// A single plugin-contributed option menu item.
/// </summary>
/// <param name="label">The option label.</param>
public abstract class ClientPluginOptionItem(string label)
{
    /// <summary>
    /// Gets the option label.
    /// </summary>
    public string Label { get; } = label;

    /// <summary>
    /// Gets the label describing the current value.
    /// </summary>
    /// <returns>The current value label.</returns>
    public abstract string GetValueLabel();

    /// <summary>
    /// Activates the option (for example cycles to the next value).
    /// </summary>
    public abstract void Activate();
}

/// <summary>
/// A boolean plugin option toggled between enabled and disabled.
/// </summary>
/// <param name="label">The option label.</param>
/// <param name="getter">Reads the current value.</param>
/// <param name="setter">Writes the new value.</param>
/// <param name="trueLabel">The label shown when enabled.</param>
/// <param name="falseLabel">The label shown when disabled.</param>
public sealed class ClientPluginBooleanOptionItem(
    string label,
    Func<bool> getter,
    Action<bool> setter,
    string trueLabel = "Enabled",
    string falseLabel = "Disabled") : ClientPluginOptionItem(label)
{
    /// <inheritdoc />
    public override string GetValueLabel()
    {
        return getter() ? trueLabel : falseLabel;
    }

    /// <inheritdoc />
    public override void Activate()
    {
        setter(!getter());
    }
}

/// <summary>
/// A plugin option that cycles through a fixed set of labeled choices.
/// </summary>
/// <param name="label">The option label.</param>
/// <param name="getter">Reads the current value.</param>
/// <param name="setter">Writes the new value.</param>
/// <param name="choices">The available choices.</param>
public sealed class ClientPluginChoiceOptionItem(
    string label,
    Func<int> getter,
    Action<int> setter,
    IReadOnlyList<ClientPluginChoiceOptionValue> choices) : ClientPluginOptionItem(label)
{
    /// <inheritdoc />
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

    /// <inheritdoc />
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

/// <summary>
/// A plugin option that cycles an integer through a ranged sequence.
/// </summary>
/// <param name="label">The option label.</param>
/// <param name="getter">Reads the current value.</param>
/// <param name="setter">Writes the new value.</param>
/// <param name="minValue">The minimum value.</param>
/// <param name="maxValue">The maximum value.</param>
/// <param name="step">The step size when cycling.</param>
/// <param name="valueLabelFormatter">Formats the value label, or null for the default.</param>
public sealed class ClientPluginIntegerOptionItem(
    string label,
    Func<int> getter,
    Action<int> setter,
    int minValue,
    int maxValue,
    int step = 1,
    Func<int, string>? valueLabelFormatter = null) : ClientPluginOptionItem(label)
{
    /// <inheritdoc />
    public override string GetValueLabel()
    {
        var value = getter();
        return valueLabelFormatter is null
            ? value.ToString(System.Globalization.CultureInfo.InvariantCulture)
            : valueLabelFormatter(value);
    }

    /// <inheritdoc />
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

/// <summary>
/// A plugin option bound to a keyboard key, rebound through key capture.
/// </summary>
/// <param name="label">The option label.</param>
/// <param name="getter">Reads the current key.</param>
/// <param name="setter">Writes the new key.</param>
/// <param name="valueLabelFormatter">Formats the key label, or null for the default.</param>
public sealed class ClientPluginKeyOptionItem(
    string label,
    Func<Keys> getter,
    Action<Keys> setter,
    Func<Keys, string>? valueLabelFormatter = null) : ClientPluginOptionItem(label)
{
    /// <summary>
    /// Gets the currently bound key.
    /// </summary>
    /// <returns>The bound key.</returns>
    public Keys GetKey()
    {
        return getter();
    }

    /// <summary>
    /// Sets the bound key.
    /// </summary>
    /// <param name="key">The key to bind.</param>
    public void SetKey(Keys key)
    {
        setter(key);
    }

    /// <inheritdoc />
    public override string GetValueLabel()
    {
        var key = getter();
        return valueLabelFormatter is null
            ? key.ToString()
            : valueLabelFormatter(key);
    }

    /// <inheritdoc />
    public override void Activate()
    {
    }
}
