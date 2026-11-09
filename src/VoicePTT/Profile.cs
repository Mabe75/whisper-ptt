using System.Text.Json.Serialization;

namespace VoicePTT;

/// <summary>Ein Push-to-Talk-Profil: eine Taste, ein Verarbeitungsweg.</summary>
public class Profile
{
    /// <summary>Taste, die dieses Profil auslöst (z. B. "f9").</summary>
    [JsonPropertyName("hotkey")] public string Hotkey { get; set; } = "f9";

    [JsonPropertyName("name")] public string Name { get; set; } = "Diktat";

    /// <summary>"llm" = Nachbearbeitung im Client, "gateway" = Korrektur im Dienst,
    /// "none" = Rohtext unverändert übernehmen.</summary>
    [JsonPropertyName("correction")] public string Correction { get; set; } = "llm";

    /// <summary>Systemanweisung für das LLM. Die Vokabelliste wird automatisch angehängt.</summary>
    [JsonPropertyName("instruction")] public string Instruction { get; set; } = "";

    /// <summary>Darf das Modell vor der Antwort nachdenken? Deutlich langsamer.</summary>
    [JsonPropertyName("thinking")] public bool Thinking { get; set; }

    /// <summary>Harte Ersetzungen aus der Vokabeltabelle anwenden.</summary>
    [JsonPropertyName("replacements")] public bool Replacements { get; set; } = true;

    /// <summary>Tonhöhe des Startsignals - macht das Profil hörbar unterscheidbar.</summary>
    [JsonPropertyName("beep_hz")] public int BeepHz { get; set; } = 660;

    [JsonPropertyName("active")] public bool Active { get; set; } = true;

    public bool UsesLlm => string.Equals(Correction, "llm", StringComparison.OrdinalIgnoreCase);
    public bool UsesGatewayCorrection => string.Equals(Correction, "gateway", StringComparison.OrdinalIgnoreCase);

    public override string ToString() => HotkeyNames.Display(Hotkey) + " · " + Name;

    /// <summary>Die vier Profile, mit denen eine frische Installation startet.</summary>
    public static List<Profile> Defaults() => new()
    {
        new Profile
        {
            Hotkey = "f9",
            Name = "Diktat",
            Correction = "llm",
            BeepHz = 660,
            Instruction = string.Join("\n", new[]
            {
                "Du bist ein Korrekturwerkzeug für diktierten Text.",
                "",
                "Aufgabe:",
                "- Satzzeichen, Absätze und Groß-/Kleinschreibung ergänzen.",
                "- Füllwörter und Versprecher entfernen: äh, ehm, öhm, also, halt, ne, sozusagen, quasi, Wortwiederholungen.",
                "- Wortwahl und Satzbau ansonsten unverändert lassen. Nichts hinzufügen, nichts weglassen.",
                "- Antworte ausschließlich mit dem korrigierten Text. Keine Vorrede, keine Anführungszeichen, keine Erklärung.",
            }),
        },
        new Profile
        {
            Hotkey = "f8",
            Name = "Formulieren",
            Correction = "llm",
            BeepHz = 780,
            Instruction = string.Join("\n", new[]
            {
                "Du machst aus diktierten Notizen sauber formulierten Text.",
                "",
                "Aufgabe:",
                "- Den Inhalt vollständig erhalten, die Formulierung darfst du frei verbessern.",
                "- Ganze Sätze, klarer Aufbau, sachlicher und höflicher Ton, sinnvolle Absätze.",
                "- Umgangssprache, Füllwörter und Wiederholungen verschwinden.",
                "- Keine neuen Aussagen erfinden, keine Grußformel ergänzen, die nicht diktiert wurde.",
                "- Antworte ausschließlich mit dem fertigen Text. Keine Vorrede, keine Erklärung.",
            }),
        },
        new Profile
        {
            Hotkey = "f7",
            Name = "Roh",
            Correction = "none",
            Replacements = false,
            BeepHz = 540,
            Instruction = "",
        },
        new Profile
        {
            Hotkey = "f6",
            Name = "Assistent",
            Correction = "llm",
            BeepHz = 920,
            Instruction = string.Join("\n", new[]
            {
                "Der folgende Text ist ein gesprochener Auftrag an dich, kein zu korrigierender Text.",
                "",
                "Aufgabe:",
                "- Führe den Auftrag aus und liefere das fertige Ergebnis.",
                "- Antworte auf Deutsch, außer der Auftrag verlangt ausdrücklich etwas anderes.",
                "- Antworte ausschließlich mit dem Ergebnis: keine Vorrede wie \"Hier ist...\", keine Rückfragen,",
                "  keine Erklärungen, keine Code-Zäune, keine Anführungszeichen um das Ganze.",
                "- Ist der Auftrag unklar, liefere die plausibelste Umsetzung statt einer Nachfrage.",
            }),
        },
    };
}

/// <summary>Ein Eintrag der Vokabeltabelle.</summary>
public class VocabularyEntry
{
    /// <summary>Die korrekte Schreibweise, z. B. "Qwen".</summary>
    [JsonPropertyName("term")] public string Term { get; set; } = "";

    /// <summary>Falschschreibungen, die hart ersetzt werden. Leer lassen, wenn der Begriff
    /// auch als normales Wort vorkommen kann (z. B. "Cloud" für "Claude").</summary>
    [JsonPropertyName("variants")] public List<string> Variants { get; set; } = new();

    /// <summary>Wörter, die das LLM als wahrscheinliche Verwechslung behandeln soll.
    /// Sie werden nie hart ersetzt - über den Einzelfall entscheidet der Satzzusammenhang.</summary>
    [JsonPropertyName("confusions")] public List<string> Confusions { get; set; } = new();

    [JsonPropertyName("active")] public bool Active { get; set; } = true;

    [JsonIgnore]
    public string VariantsText
    {
        get => string.Join(", ", Variants ?? new List<string>());
        set => Variants = Split(value);
    }

    [JsonIgnore]
    public string ConfusionsText
    {
        get => string.Join(", ", Confusions ?? new List<string>());
        set => Confusions = Split(value);
    }

    private static List<string> Split(string value) => (value ?? "")
        .Split(new[] { ',', ';', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries)
        .Select(v => v.Trim())
        .Where(v => v.Length > 0)
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .ToList();

    public static List<VocabularyEntry> Defaults() => new()
    {
        new VocabularyEntry { Term = "Claude", Confusions = new List<string> { "cloud", "clouds" } },
        new VocabularyEntry { Term = "Qwen", Variants = new List<string> { "kwen", "quen", "qwenn", "kwenn" } },
        new VocabularyEntry { Term = "Anthropic", Confusions = new List<string> { "anthropik" } },
        new VocabularyEntry { Term = "Home Assistant", Variants = new List<string> { "home assistent", "homeassistent" } },
    };
}
