namespace VoicePTT;

/// <summary>Ein Tastenkuerzel wie "ctrl+v", "shift+insert" oder "ctrl+shift+v".
/// Gebraucht, weil nicht jedes Programm Strg+V als Einfuegen versteht: Konsolen
/// erwarten haeufig Umschalt+Einfg.</summary>
public sealed class KeyChord
{
    public const ushort VkCtrl = 0x11;
    public const ushort VkShift = 0x10;
    public const ushort VkAlt = 0x12;
    public const ushort VkWin = 0x5B;

    /// <summary>Die in der Praxis ueblichen Kuerzel - Vorschlagsliste fuer den Dialog.</summary>
    public static readonly string[] Selectable = { "ctrl+v", "shift+insert", "ctrl+shift+v" };

    public static readonly KeyChord CtrlV = new(new[] { VkCtrl }, 0x56, "ctrl+v");

    /// <summary>Feste Reihenfolge, damit die Tastenfolge reproduzierbar ist.</summary>
    private static readonly ushort[] Order = { VkCtrl, VkShift, VkAlt, VkWin };

    private static readonly Dictionary<string, ushort> ModMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["ctrl"] = VkCtrl, ["strg"] = VkCtrl, ["control"] = VkCtrl,
        ["shift"] = VkShift, ["umschalt"] = VkShift,
        ["alt"] = VkAlt,
        ["win"] = VkWin, ["windows"] = VkWin, ["meta"] = VkWin, ["cmd"] = VkWin,
    };

    private static readonly Dictionary<ushort, string> ModLabel = new()
    {
        [VkCtrl] = "Strg", [VkShift] = "Umschalt", [VkAlt] = "Alt", [VkWin] = "Windows",
    };

    /// <summary>Schreibweise, die beim Normalisieren herauskommt - unabhaengig davon,
    /// welches Synonym der Nutzer eingetippt hat.</summary>
    private static readonly Dictionary<ushort, string> ModCanon = new()
    {
        [VkCtrl] = "ctrl", [VkShift] = "shift", [VkAlt] = "alt", [VkWin] = "win",
    };

    /// <summary>Damit in der Konfiguration immer dieselbe Schreibweise landet, egal ob
    /// jemand "insert", "einfg" oder "Einfuegen" eingetippt hat.</summary>
    private static readonly Dictionary<ushort, string> KeyCanon = new()
    {
        [0x2D] = "insert", [0x2E] = "delete", [0x20] = "space",
        [0x24] = "home", [0x23] = "end", [0x0D] = "enter",
    };

    private static readonly Dictionary<string, string> KeyLabel = new(StringComparer.OrdinalIgnoreCase)
    {
        ["insert"] = "Einfg", ["delete"] = "Entf", ["space"] = "Leertaste",
        ["home"] = "Pos1", ["end"] = "Ende", ["return"] = "Enter", ["enter"] = "Enter",
    };

    public ushort[] Modifiers { get; }
    public ushort Key { get; }

    /// <summary>Normalisierte Schreibweise, z. B. "ctrl+shift+v".</summary>
    public string Text { get; }

    private KeyChord(ushort[] modifiers, ushort key, string text)
    {
        Modifiers = modifiers;
        Key = key;
        Text = text;
    }

    /// <summary>Zerlegt ein Kuerzel. Gibt null zurueck und setzt <paramref name="problem"/>,
    /// wenn die Angabe nicht zu gebrauchen ist.</summary>
    public static KeyChord Parse(string text, out string problem)
    {
        problem = null;
        var raw = (text ?? "").Trim();
        if (raw.Length == 0)
        {
            problem = "keine Angabe";
            return null;
        }

        var parts = raw.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length == 0)
        {
            problem = "keine Angabe";
            return null;
        }

        var mods = new List<ushort>();
        for (var i = 0; i < parts.Length - 1; i++)
        {
            if (!ModMap.TryGetValue(parts[i], out var m))
            {
                problem = "\"" + parts[i] + "\" ist keine Zusatztaste (erlaubt: Strg, Umschalt, Alt, Win)";
                return null;
            }
            if (!mods.Contains(m)) mods.Add(m);
        }

        var main = parts[^1];
        if (ModMap.ContainsKey(main))
        {
            problem = "nach \"" + main + "\" fehlt die eigentliche Taste";
            return null;
        }
        if (!HotkeyNames.TryToVirtualKey(main, out var vk))
        {
            problem = "\"" + main + "\" ist keine bekannte Taste";
            return null;
        }

        mods.Sort((a, b) => Array.IndexOf(Order, a).CompareTo(Array.IndexOf(Order, b)));

        var teile = mods.Select(m => ModCanon[m]).ToList();
        teile.Add(KeyCanon.TryGetValue((ushort)vk, out var kanon) ? kanon : main.ToLowerInvariant());
        return new KeyChord(mods.ToArray(), (ushort)vk, string.Join("+", teile));
    }

    /// <summary>Wie <see cref="Parse"/>, nimmt bei Unsinn aber den Ersatz und schreibt ins Protokoll.</summary>
    public static KeyChord ParseOrDefault(string text, KeyChord fallback)
    {
        var chord = Parse(text, out var problem);
        if (chord != null) return chord;
        Log.Write("Einfuegekuerzel \"" + (text ?? "") + "\" unbrauchbar (" + problem + "), nutze " + fallback.Display() + ".");
        return fallback;
    }

    public static bool IsValid(string text) => Parse(text, out _) != null;

    /// <summary>Lesbare Form fuer Dialog und Protokoll, z. B. "Umschalt+Einfg".</summary>
    public string Display()
    {
        var parts = Text.Split('+');
        var namen = new List<string>();
        for (var i = 0; i < parts.Length - 1; i++)
            namen.Add(ModMap.TryGetValue(parts[i], out var m) && ModLabel.TryGetValue(m, out var l) ? l : parts[i]);

        var main = parts[^1];
        namen.Add(KeyLabel.TryGetValue(main, out var kl) ? kl : HotkeyNames.Display(main));
        return string.Join("+", namen);
    }

    public override string ToString() => Display();
}

/// <summary>Was im Tippmodus aus einem Zeilenumbruch wird.</summary>
public enum NewlineMode
{
    /// <summary>Enter senden - richtig in Textfeldern, schickt in Eingabemasken aber ab.</summary>
    Enter,

    /// <summary>Durch ein Leerzeichen ersetzen - der Text bleibt immer eine Eingabe.</summary>
    Space,

    /// <summary>Umschalt+Enter - in vielen Eingabemasken ein Umbruch ohne Abschicken.</summary>
    ShiftEnter,
}

public static class NewlineModes
{
    public static readonly string[] Selectable = { "enter", "space", "shift+enter" };

    public static bool TryParse(string text, out NewlineMode mode)
    {
        switch ((text ?? "").Trim().ToLowerInvariant())
        {
            case "enter":
            case "return":
                mode = NewlineMode.Enter;
                return true;
            case "space":
            case "leerzeichen":
                mode = NewlineMode.Space;
                return true;
            case "shift+enter":
            case "shiftenter":
            case "umschalt+enter":
                mode = NewlineMode.ShiftEnter;
                return true;
            default:
                mode = NewlineMode.Enter;
                return false;
        }
    }

    public static NewlineMode Parse(string text) => TryParse(text, out var m) ? m : NewlineMode.Enter;

    public static string Label(string text) => (text ?? "").Trim().ToLowerInvariant() switch
    {
        "space" or "leerzeichen" => "Leerzeichen",
        "shift+enter" or "shiftenter" or "umschalt+enter" => "Umschalt+Enter",
        "" => "",
        _ => "Enter",
    };
}
