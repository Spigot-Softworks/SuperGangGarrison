#nullable enable

using System.IO;
using OpenGarrison.Core;
using Microsoft.Xna.Framework.Input;

namespace OpenGarrison.Client;

public enum InputBindingKind
{
    Keyboard = 0,
    Mouse = 1,
}

public enum InputMouseButton
{
    Left = 0,
    Middle = 1,
    Right = 2,
    XButton1 = 3,
    XButton2 = 4,
}

public readonly record struct InputBinding(InputBindingKind Kind, Keys Key, InputMouseButton MouseButton)
{
    public static InputBinding FromKey(Keys key)
    {
        return new InputBinding(InputBindingKind.Keyboard, key, InputMouseButton.Left);
    }

    public static InputBinding FromMouse(InputMouseButton mouseButton)
    {
        return new InputBinding(InputBindingKind.Mouse, Keys.None, mouseButton);
    }

    public bool IsKeyboardKey(Keys key)
    {
        return Kind == InputBindingKind.Keyboard && Key == key;
    }
}

public enum WeaponSwapBindingMode
{
    Space = 0,
    MouseSecondary = 1,
    Q = 2,
    Custom = 3,
}

public sealed class InputBindingsSettings
{
    public const string DefaultFileName = "controls.OpenGarrison";
    private const string LegacyFileName = "input.bindings.json";

    public InputBinding MoveLeft { get; set; } = InputBinding.FromKey(Keys.A);

    public InputBinding MoveRight { get; set; } = InputBinding.FromKey(Keys.D);

    public InputBinding MoveUp { get; set; } = InputBinding.FromKey(Keys.W);

    public InputBinding MoveDown { get; set; } = InputBinding.FromKey(Keys.S);

    public InputBinding Taunt { get; set; } = InputBinding.FromKey(Keys.F);

    public InputBinding CallMedic { get; set; } = InputBinding.FromKey(Keys.E);

    public InputBinding UseAbility { get; set; } = InputBinding.FromKey(Keys.Space);

    public InputBinding InteractWeapon { get; set; } = InputBinding.FromKey(Keys.Q);

    public WeaponSwapBindingMode SwapWeaponsBinding { get; set; } = WeaponSwapBindingMode.Space;

    public InputBinding SwapWeaponsCustomKey { get; set; } = InputBinding.FromKey(Keys.Space);

    public InputBinding ShowScoreboard { get; set; } = InputBinding.FromKey(Keys.Tab);

    public InputBinding ChangeTeam { get; set; } = InputBinding.FromKey(Keys.N);

    public InputBinding ChangeClass { get; set; } = InputBinding.FromKey(Keys.M);

    public InputBinding ToggleConsole { get; set; } = InputBinding.FromKey(Keys.OemTilde);

    public InputBinding OpenBubbleMenuZ { get; set; } = InputBinding.FromKey(Keys.Z);

    public InputBinding OpenBubbleMenuX { get; set; } = InputBinding.FromKey(Keys.X);

    public InputBinding OpenBubbleMenuC { get; set; } = InputBinding.FromKey(Keys.C);

    public InputBinding CustomBubble { get; set; } = InputBinding.FromKey(Keys.R);

    public InputBinding ToggleClassMenu
    {
        get => ChangeClass;
        set => ChangeClass = value;
    }

    public static InputBindingsSettings Load(string? path = null)
    {
        if (OperatingSystem.IsBrowser())
        {
            return new InputBindingsSettings();
        }

        var resolvedPath = path ?? RuntimePaths.GetConfigPath(DefaultFileName);
        if (File.Exists(resolvedPath))
        {
            return LoadFromIni(resolvedPath);
        }

        var legacyPath = RuntimePaths.GetConfigPath(LegacyFileName);
        if (File.Exists(legacyPath))
        {
            var legacy = JsonConfigurationFile.LoadOrCreate<LegacyInputBindingsSettings>(legacyPath);
            var migrated = FromLegacy(legacy);
            migrated.Save(resolvedPath);
            return migrated;
        }

        var created = new InputBindingsSettings();
        created.Save(resolvedPath);
        return created;
    }

    public void Save(string? path = null)
    {
        if (OperatingSystem.IsBrowser())
        {
            return;
        }

        var resolvedPath = path ?? RuntimePaths.GetConfigPath(DefaultFileName);
        var document = new IniConfigurationFile();

        document.SetString("Controls", "jump", FormatBinding(MoveUp));
        document.SetString("Controls", "down", FormatBinding(MoveDown));
        document.SetString("Controls", "left", FormatBinding(MoveLeft));
        document.SetString("Controls", "right", FormatBinding(MoveRight));
        document.SetString("Controls", "taunt", FormatBinding(Taunt));
        document.SetString("Controls", "medic", FormatBinding(CallMedic));
        document.SetString("Controls", "secondaryWeapon", FormatBinding(UseAbility));
        document.SetString("Controls", "interactWeapon", FormatBinding(InteractWeapon));
        document.SetString("Controls", "swapWeapons", NormalizeSwapWeaponsBinding(SwapWeaponsBinding).ToString());
        document.SetString("Controls", "swapWeaponsCustomKey", FormatBinding(SwapWeaponsCustomKey));
        document.SetString("Controls", "changeTeam", FormatBinding(ChangeTeam));
        document.SetString("Controls", "changeClass", FormatBinding(ChangeClass));
        document.SetString("Controls", "showScores", FormatBinding(ShowScoreboard));
        document.SetString("Controls", "console", FormatBinding(ToggleConsole));
        document.SetString("Controls", "bubbleMenuZ", FormatBinding(OpenBubbleMenuZ));
        document.SetString("Controls", "bubbleMenuX", FormatBinding(OpenBubbleMenuX));
        document.SetString("Controls", "bubbleMenuC", FormatBinding(OpenBubbleMenuC));
        document.SetString("Controls", "customBubble", FormatBinding(CustomBubble));

        document.Save(resolvedPath);
    }

    private static InputBindingsSettings LoadFromIni(string path)
    {
        if (OperatingSystem.IsBrowser())
        {
            return new InputBindingsSettings();
        }

        var document = IniConfigurationFile.Load(path);
        return new InputBindingsSettings
        {
            MoveUp = ReadBinding(document, "jump", Keys.W),
            MoveDown = ReadBinding(document, "down", Keys.S),
            MoveLeft = ReadBinding(document, "left", Keys.A),
            MoveRight = ReadBinding(document, "right", Keys.D),
            Taunt = ReadBinding(document, "taunt", Keys.F),
            CallMedic = ReadBinding(document, "medic", Keys.E),
            UseAbility = ReadBinding(document, "secondaryWeapon", Keys.Space),
            InteractWeapon = ReadBinding(document, "interactWeapon", Keys.Q),
            SwapWeaponsBinding = ReadSwapWeaponsBinding(document),
            SwapWeaponsCustomKey = ReadBinding(document, "swapWeaponsCustomKey", Keys.Space),
            ChangeTeam = ReadBinding(document, "changeTeam", Keys.N),
            ChangeClass = ReadBinding(document, "changeClass", Keys.M),
            ShowScoreboard = ReadBinding(document, "showScores", Keys.LeftShift),
            ToggleConsole = ReadBinding(document, "console", Keys.OemTilde),
            OpenBubbleMenuZ = ReadBinding(document, "bubbleMenuZ", Keys.Z),
            OpenBubbleMenuX = ReadBinding(document, "bubbleMenuX", Keys.X),
            OpenBubbleMenuC = ReadBinding(document, "bubbleMenuC", Keys.C),
            CustomBubble = ReadBinding(document, "customBubble", Keys.R),
        };
    }

    private static InputBinding ReadBinding(IniConfigurationFile document, string key, Keys fallback)
    {
        var value = document.GetString("Controls", key, ((int)fallback).ToString(System.Globalization.CultureInfo.InvariantCulture));
        return TryParseBinding(value, out var binding)
            ? binding
            : InputBinding.FromKey(fallback);
    }

    public static string FormatBinding(InputBinding binding)
    {
        return binding.Kind switch
        {
            InputBindingKind.Keyboard => $"Keyboard:{binding.Key}",
            InputBindingKind.Mouse => $"Mouse:{binding.MouseButton}",
            _ => $"Keyboard:{Keys.None}",
        };
    }

    public static bool TryParseBinding(string? value, out InputBinding binding)
    {
        binding = default;
        var text = (value ?? string.Empty).Trim();
        if (text.Length == 0)
        {
            return false;
        }

        if (int.TryParse(text, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var keyValue)
            && Enum.IsDefined((Keys)keyValue))
        {
            binding = InputBinding.FromKey((Keys)keyValue);
            return true;
        }

        var separatorIndex = text.IndexOf(':');
        if (separatorIndex < 0)
        {
            return TryParseKeyboardBinding(text, out binding)
                || TryParseMouseBinding(text, out binding);
        }

        var kindText = text[..separatorIndex].Trim();
        var bindingText = text[(separatorIndex + 1)..].Trim();
        if (kindText.Equals("Keyboard", StringComparison.OrdinalIgnoreCase)
            || kindText.Equals("Key", StringComparison.OrdinalIgnoreCase))
        {
            return TryParseKeyboardBinding(bindingText, out binding);
        }

        if (kindText.Equals("Mouse", StringComparison.OrdinalIgnoreCase))
        {
            return TryParseMouseBinding(bindingText, out binding);
        }

        return false;
    }

    private static bool TryParseKeyboardBinding(string text, out InputBinding binding)
    {
        binding = default;
        if (int.TryParse(text, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var keyValue)
            && Enum.IsDefined((Keys)keyValue))
        {
            binding = InputBinding.FromKey((Keys)keyValue);
            return true;
        }

        if (Enum.TryParse<Keys>(text, ignoreCase: true, out var key)
            && Enum.IsDefined(key))
        {
            binding = InputBinding.FromKey(key);
            return true;
        }

        return false;
    }

    private static bool TryParseMouseBinding(string text, out InputBinding binding)
    {
        binding = default;
        var normalized = text
            .Replace(" ", string.Empty, StringComparison.Ordinal)
            .Replace("_", string.Empty, StringComparison.Ordinal)
            .Replace("-", string.Empty, StringComparison.Ordinal);

        var mouseButton = normalized.ToLowerInvariant() switch
        {
            "left" or "leftbutton" or "mouse1" or "m1" => InputMouseButton.Left,
            "right" or "rightbutton" or "mouse2" or "m2" => InputMouseButton.Right,
            "middle" or "middlebutton" or "mouse3" or "m3" => InputMouseButton.Middle,
            "xbutton1" or "x1" or "side1" or "mouse4" or "m4" => InputMouseButton.XButton1,
            "xbutton2" or "x2" or "side2" or "mouse5" or "m5" => InputMouseButton.XButton2,
            _ => Enum.TryParse<InputMouseButton>(text, ignoreCase: true, out var parsed) ? parsed : (InputMouseButton)(-1),
        };

        if (!Enum.IsDefined(mouseButton))
        {
            return false;
        }

        binding = InputBinding.FromMouse(mouseButton);
        return true;
    }

    private static InputBindingsSettings FromLegacy(LegacyInputBindingsSettings legacy)
    {
        return new InputBindingsSettings
        {
            MoveLeft = InputBinding.FromKey(legacy.MoveLeft),
            MoveRight = InputBinding.FromKey(legacy.MoveRight),
            MoveUp = InputBinding.FromKey(legacy.MoveUp),
            MoveDown = InputBinding.FromKey(legacy.MoveDown),
            Taunt = InputBinding.FromKey(legacy.Taunt),
            CallMedic = InputBinding.FromKey(legacy.CallMedic),
            UseAbility = InputBinding.FromKey(legacy.UseAbility),
            InteractWeapon = InputBinding.FromKey(legacy.InteractWeapon),
            SwapWeaponsBinding = NormalizeSwapWeaponsBinding(legacy.SwapWeaponsBinding),
            SwapWeaponsCustomKey = InputBinding.FromKey(legacy.SwapWeaponsCustomKey),
            ShowScoreboard = InputBinding.FromKey(legacy.ShowScoreboard),
            ChangeTeam = InputBinding.FromKey(legacy.ChangeTeam),
            ChangeClass = InputBinding.FromKey(legacy.ChangeClass),
            ToggleConsole = InputBinding.FromKey(legacy.ToggleConsole),
            OpenBubbleMenuZ = InputBinding.FromKey(legacy.OpenBubbleMenuZ),
            OpenBubbleMenuX = InputBinding.FromKey(legacy.OpenBubbleMenuX),
            OpenBubbleMenuC = InputBinding.FromKey(legacy.OpenBubbleMenuC),
            CustomBubble = InputBinding.FromKey(legacy.CustomBubble),
        };
    }

    private static WeaponSwapBindingMode ReadSwapWeaponsBinding(IniConfigurationFile document)
    {
        var text = document.GetString("Controls", "swapWeapons", WeaponSwapBindingMode.Space.ToString());
        return Enum.TryParse<WeaponSwapBindingMode>(text, ignoreCase: true, out var binding)
            ? NormalizeSwapWeaponsBinding(binding)
            : WeaponSwapBindingMode.Space;
    }

    public static WeaponSwapBindingMode NormalizeSwapWeaponsBinding(WeaponSwapBindingMode binding)
    {
        return binding switch
        {
            WeaponSwapBindingMode.Space => WeaponSwapBindingMode.Space,
            WeaponSwapBindingMode.MouseSecondary => WeaponSwapBindingMode.MouseSecondary,
            WeaponSwapBindingMode.Q => WeaponSwapBindingMode.Q,
            WeaponSwapBindingMode.Custom => WeaponSwapBindingMode.Custom,
            _ => WeaponSwapBindingMode.Space,
        };
    }

    private sealed class LegacyInputBindingsSettings
    {
        public Keys MoveLeft { get; set; } = Keys.A;
        public Keys MoveRight { get; set; } = Keys.D;
        public Keys MoveUp { get; set; } = Keys.W;
        public Keys MoveDown { get; set; } = Keys.S;
        public Keys Taunt { get; set; } = Keys.F;
        public Keys CallMedic { get; set; } = Keys.E;
        public Keys UseAbility { get; set; } = Keys.Space;
        public Keys InteractWeapon { get; set; } = Keys.Q;
        public WeaponSwapBindingMode SwapWeaponsBinding { get; set; } = WeaponSwapBindingMode.Space;
        public Keys SwapWeaponsCustomKey { get; set; } = Keys.Space;
        public Keys ShowScoreboard { get; set; } = Keys.Tab;
        public Keys ChangeTeam { get; set; } = Keys.N;
        public Keys ChangeClass { get; set; } = Keys.M;
        public Keys ToggleConsole { get; set; } = Keys.OemTilde;
        public Keys OpenBubbleMenuZ { get; set; } = Keys.Z;
        public Keys OpenBubbleMenuX { get; set; } = Keys.X;
        public Keys OpenBubbleMenuC { get; set; } = Keys.C;
        public Keys CustomBubble { get; set; } = Keys.R;
    }
}
