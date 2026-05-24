#nullable enable

using System.IO;
using OpenGarrison.Core;
using Microsoft.Xna.Framework.Input;

namespace OpenGarrison.Client;

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

    public Keys ToggleClassMenu
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
            var migrated = JsonConfigurationFile.LoadOrCreate<InputBindingsSettings>(legacyPath);
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

        document.SetInt("Controls", "jump", (int)MoveUp);
        document.SetInt("Controls", "down", (int)MoveDown);
        document.SetInt("Controls", "left", (int)MoveLeft);
        document.SetInt("Controls", "right", (int)MoveRight);
        document.SetInt("Controls", "taunt", (int)Taunt);
        document.SetInt("Controls", "medic", (int)CallMedic);
        document.SetInt("Controls", "secondaryWeapon", (int)UseAbility);
        document.SetInt("Controls", "interactWeapon", (int)InteractWeapon);
        document.SetString("Controls", "swapWeapons", NormalizeSwapWeaponsBinding(SwapWeaponsBinding).ToString());
        document.SetInt("Controls", "swapWeaponsCustomKey", (int)SwapWeaponsCustomKey);
        document.SetInt("Controls", "changeTeam", (int)ChangeTeam);
        document.SetInt("Controls", "changeClass", (int)ChangeClass);
        document.SetInt("Controls", "showScores", (int)ShowScoreboard);
        document.SetInt("Controls", "console", (int)ToggleConsole);
        document.SetInt("Controls", "bubbleMenuZ", (int)OpenBubbleMenuZ);
        document.SetInt("Controls", "bubbleMenuX", (int)OpenBubbleMenuX);
        document.SetInt("Controls", "bubbleMenuC", (int)OpenBubbleMenuC);
        document.SetInt("Controls", "customBubble", (int)CustomBubble);

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
            MoveUp = ReadKey(document, "jump", Keys.W),
            MoveDown = ReadKey(document, "down", Keys.S),
            MoveLeft = ReadKey(document, "left", Keys.A),
            MoveRight = ReadKey(document, "right", Keys.D),
            Taunt = ReadKey(document, "taunt", Keys.F),
            CallMedic = ReadKey(document, "medic", Keys.E),
            UseAbility = ReadKey(document, "secondaryWeapon", Keys.Space),
            InteractWeapon = ReadKey(document, "interactWeapon", Keys.Q),
            SwapWeaponsBinding = ReadSwapWeaponsBinding(document),
            SwapWeaponsCustomKey = ReadKey(document, "swapWeaponsCustomKey", Keys.Space),
            ChangeTeam = ReadKey(document, "changeTeam", Keys.N),
            ChangeClass = ReadKey(document, "changeClass", Keys.M),
            ShowScoreboard = ReadKey(document, "showScores", Keys.LeftShift),
            ToggleConsole = ReadKey(document, "console", Keys.OemTilde),
            OpenBubbleMenuZ = ReadKey(document, "bubbleMenuZ", Keys.Z),
            OpenBubbleMenuX = ReadKey(document, "bubbleMenuX", Keys.X),
            OpenBubbleMenuC = ReadKey(document, "bubbleMenuC", Keys.C),
            CustomBubble = ReadKey(document, "customBubble", Keys.R),
        };
    }

    private static Keys ReadKey(IniConfigurationFile document, string key, Keys fallback)
    {
        var value = document.GetInt("Controls", key, (int)fallback);
        return Enum.IsDefined(typeof(Keys), value)
            ? (Keys)value
            : fallback;
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
}
