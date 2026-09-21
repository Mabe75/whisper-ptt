namespace VoicePTT;

/// <summary>Uebersetzt Tastennamen aus config.json in Virtual-Key-Codes.
/// Die Namen entsprechen denen des alten Python-Clients (z. B. "f9").</summary>
public static class HotkeyNames
{
    public static readonly string[] Selectable =
    {
        "f1", "f2", "f3", "f4", "f5", "f6", "f7", "f8", "f9", "f10", "f11", "f12",
        "scroll lock", "pause", "right ctrl", "right alt", "right shift",
    };

    private static readonly Dictionary<string, uint> Map = new(StringComparer.OrdinalIgnoreCase)
    {
        ["scroll lock"] = 0x91, ["scrolllock"] = 0x91,
        ["pause"] = 0x13, ["break"] = 0x13,
        ["right ctrl"] = 0xA3, ["rctrl"] = 0xA3, ["ctrl"] = 0x11,
        ["right alt"] = 0xA5, ["ralt"] = 0xA5, ["alt"] = 0x12,
        ["right shift"] = 0xA1, ["rshift"] = 0xA1, ["shift"] = 0x10,
        ["caps lock"] = 0x14, ["capslock"] = 0x14,
        ["space"] = 0x20, ["insert"] = 0x2D, ["delete"] = 0x2E,
        ["end"] = 0x23, ["home"] = 0x24, ["num lock"] = 0x90, ["numlock"] = 0x90,
        ["print screen"] = 0x2C, ["apps"] = 0x5D, ["menu"] = 0x5D,
    };

    public static uint ToVirtualKey(string name)
    {
        name = (name ?? "").Trim().ToLowerInvariant();
        if (name.Length == 0) name = "f9";
        if (Map.TryGetValue(name, out var vk)) return vk;

        if (name.Length > 1 && name[0] == 'f' && int.TryParse(name.Substring(1), out var fn) && fn >= 1 && fn <= 24)
            return (uint)(0x70 + fn - 1);

        if (name.Length == 1)
        {
            var c = char.ToUpperInvariant(name[0]);
            if ((c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9')) return c;
        }

        Log.Write("Unbekannte Taste '" + name + "', nutze F9.");
        return 0x78;
    }

    public static string Display(string name)
    {
        name = (name ?? "f9").Trim();
        if (name.Length > 1 && (name[0] == 'f' || name[0] == 'F') && int.TryParse(name.Substring(1), out var n))
            return "F" + n;
        if (name.Length == 0) return "F9";
        return name.Length == 1 ? name.ToUpperInvariant() : char.ToUpperInvariant(name[0]) + name.Substring(1);
    }
}
