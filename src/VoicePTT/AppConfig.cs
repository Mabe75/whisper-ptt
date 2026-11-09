using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace VoicePTT;

/// <summary>Inhalt von config.json. Die Feldnamen der Grundeinstellungen sind absichtlich
/// identisch mit denen des alten Python-Clients, damit alte Konfigurationen weiter passen.</summary>
public class AppConfig
{
    [JsonPropertyName("gateway_url")] public string GatewayUrl { get; set; } = "http://127.0.0.1:8055";
    [JsonPropertyName("endpoint_type")] public string EndpointType { get; set; } = "gateway";
    [JsonPropertyName("model")] public string Model { get; set; } = "whisper-1";
    [JsonPropertyName("language")] public string Language { get; set; } = "";

    /// <summary>Taste des alten Clients. Wird beim ersten Start in das erste Profil übernommen
    /// und danach nur noch mitgeschrieben, damit eine parallel laufende Altversion sie findet.</summary>
    [JsonPropertyName("hotkey")] public string Hotkey { get; set; } = "f9";

    [JsonPropertyName("input_device")]
    [JsonConverter(typeof(LooseStringConverter))]
    public string InputDevice { get; set; }

    [JsonPropertyName("correct")] public bool Correct { get; set; } = true;
    [JsonPropertyName("insert_mode")] public string InsertMode { get; set; } = "paste";
    [JsonPropertyName("restore_clipboard")] public bool RestoreClipboard { get; set; } = true;
    [JsonPropertyName("samplerate")] public int SampleRate { get; set; } = 16000;
    [JsonPropertyName("beep")] public bool Beep { get; set; } = true;
    [JsonPropertyName("beep_volume")] public double BeepVolume { get; set; } = 0.08;
    [JsonPropertyName("suppress_hotkey")] public bool SuppressHotkey { get; set; } = true;
    [JsonPropertyName("min_seconds")] public double MinSeconds { get; set; } = 0.3;

    // ---- ab Version 2.1: Nachbearbeitung im Client ----

    /// <summary>OpenAI-kompatibler Chat-Endpunkt, z. B. http://192.168.178.34:8082/v1</summary>
    [JsonPropertyName("llm_url")] public string LlmUrl { get; set; } = "";

    [JsonPropertyName("llm_model")] public string LlmModel { get; set; } = "";
    [JsonPropertyName("llm_temperature")] public double LlmTemperature { get; set; }
    [JsonPropertyName("llm_timeout_seconds")] public int LlmTimeoutSeconds { get; set; } = 120;

    /// <summary>Wird unverändert in den Request gemischt - für serverspezifische Schalter.
    /// Ein Feld mit dem Wert null entfernt den vom Client gesetzten Schlüssel.</summary>
    [JsonPropertyName("llm_extra")] public JsonObject LlmExtra { get; set; } = new();

    /// <summary>Begriffe an Whisper durchreichen (nur bei endpoint_type "openai";
    /// das Gateway kennt das Feld nicht und bekommt es deshalb auch nicht).</summary>
    [JsonPropertyName("send_vocabulary_to_whisper")] public bool SendVocabularyToWhisper { get; set; } = true;

    /// <summary>Schreibt den fertigen Systemprompt ins Protokoll - hilft beim Feilen an den Anweisungen.</summary>
    [JsonPropertyName("log_prompts")] public bool LogPrompts { get; set; }

    [JsonPropertyName("vocabulary")] public List<VocabularyEntry> Vocabulary { get; set; }

    [JsonPropertyName("profiles")] public List<Profile> Profiles { get; set; }

    [JsonIgnore] public bool LlmConfigured =>
        !string.IsNullOrWhiteSpace(LlmUrl) && !string.IsNullOrWhiteSpace(LlmModel);

    private static readonly JsonSerializerOptions Opts = new()
    {
        WriteIndented = true,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    public static AppConfig Load()
    {
        Paths.EnsureDirs();
        MigrateIfNeeded();

        if (File.Exists(Paths.ConfigPath))
        {
            try
            {
                var cfg = JsonSerializer.Deserialize<AppConfig>(File.ReadAllText(Paths.ConfigPath), Opts);
                if (cfg != null) return cfg.Sanitized();
            }
            catch (Exception ex)
            {
                Log.Write("Config-Fehler, nutze Standardwerte:", ex.Message);
                try { File.Copy(Paths.ConfigPath, Paths.ConfigPath + ".bad", true); } catch { }
            }
        }

        var fresh = new AppConfig().Sanitized();
        fresh.Save();
        return fresh;
    }

    /// <summary>Konfiguration der Vorgängerversion bzw. die mitgelieferte Vorlage übernehmen.</summary>
    private static void MigrateIfNeeded()
    {
        if (File.Exists(Paths.ConfigPath)) return;
        foreach (var src in new[] { Path.Combine(Paths.LocalDir, "config.json"), Paths.DefaultConfigPath })
        {
            if (!File.Exists(src)) continue;
            try
            {
                File.Copy(src, Paths.ConfigPath);
                Log.Write("Konfiguration übernommen aus", src);
                return;
            }
            catch (Exception ex) { Log.Write("Übernahme fehlgeschlagen:", src, ex.Message); }
        }
    }

    public AppConfig Sanitized()
    {
        if (SampleRate < 8000 || SampleRate > 48000) SampleRate = 16000;
        if (BeepVolume < 0) BeepVolume = 0;
        if (BeepVolume > 1) BeepVolume = 1;
        if (MinSeconds < 0) MinSeconds = 0;
        if (LlmTimeoutSeconds < 5) LlmTimeoutSeconds = 5;
        if (LlmTimeoutSeconds > 600) LlmTimeoutSeconds = 600;
        if (LlmTemperature < 0) LlmTemperature = 0;
        if (LlmTemperature > 2) LlmTemperature = 2;
        if (string.IsNullOrWhiteSpace(Hotkey)) Hotkey = "f9";
        if (string.IsNullOrWhiteSpace(GatewayUrl)) GatewayUrl = "http://127.0.0.1:8055";
        InsertMode = string.Equals(InsertMode, "type", StringComparison.OrdinalIgnoreCase) ? "type" : "paste";
        EndpointType = string.Equals(EndpointType, "openai", StringComparison.OrdinalIgnoreCase) ? "openai" : "gateway";
        LlmExtra ??= new JsonObject();
        Vocabulary ??= VocabularyEntry.Defaults();
        Vocabulary.RemoveAll(v => v == null || string.IsNullOrWhiteSpace(v.Term));

        BuildProfilesIfMissing();
        foreach (var p in Profiles)
        {
            if (string.IsNullOrWhiteSpace(p.Hotkey)) p.Hotkey = "f9";
            if (string.IsNullOrWhiteSpace(p.Name)) p.Name = HotkeyNames.Display(p.Hotkey);
            var c = (p.Correction ?? "").ToLowerInvariant();
            p.Correction = c is "llm" or "gateway" or "none" ? c : "llm";
            if (p.BeepHz < 100 || p.BeepHz > 4000) p.BeepHz = 660;
        }
        DeactivateDuplicateHotkeys();

        // Die Taste des ersten aktiven Profils bleibt im alten Feld stehen, damit eine
        // parallel installierte Altversion dieselbe Taste benutzt.
        var first = Profiles.FirstOrDefault(p => p.Active);
        if (first != null) Hotkey = first.Hotkey;

        return this;
    }

    private void BuildProfilesIfMissing()
    {
        if (Profiles != null && Profiles.Count > 0)
        {
            Profiles.RemoveAll(p => p == null);
            if (Profiles.Count > 0) return;
        }

        Profiles = Profile.Defaults();

        // Was der Nutzer bisher eingestellt hatte, gilt weiter für das Diktat-Profil.
        var diktat = Profiles[0];
        diktat.Hotkey = Hotkey;
        if (!LlmConfigured) diktat.Correction = Correct ? "gateway" : "none";
        Log.Write("Profile angelegt, Diktat auf Taste", HotkeyNames.Display(diktat.Hotkey),
                  "Korrektur:", diktat.Correction);
    }

    /// <summary>Zwei Profile auf derselben Taste ergäben doppelte Aufnahmen - das zweite ruht.</summary>
    private void DeactivateDuplicateHotkeys()
    {
        var seen = new HashSet<uint>();
        foreach (var p in Profiles)
        {
            if (!p.Active) continue;
            var vk = HotkeyNames.ToVirtualKey(p.Hotkey);
            if (seen.Add(vk)) continue;
            p.Active = false;
            Log.Write("Profil", p.Name, "deaktiviert: Taste", HotkeyNames.Display(p.Hotkey), "ist doppelt belegt.");
        }
    }

    public void Save()
    {
        try
        {
            Paths.EnsureDirs();
            var tmp = Paths.ConfigPath + ".tmp";
            File.WriteAllText(tmp, JsonSerializer.Serialize(this, Opts), new UTF8Encoding(false));
            File.Move(tmp, Paths.ConfigPath, true);
        }
        catch (Exception ex) { Log.Write("Config speichern fehlgeschlagen:", ex.Message); }
    }
}

/// <summary>input_device darf null, Zahl oder Text sein - wie im alten Client.</summary>
public sealed class LooseStringConverter : JsonConverter<string>
{
    public override string Read(ref Utf8JsonReader r, Type t, JsonSerializerOptions o) => r.TokenType switch
    {
        JsonTokenType.Null => null,
        JsonTokenType.String => r.GetString(),
        JsonTokenType.Number => r.TryGetInt64(out var l) ? l.ToString() : r.GetDouble().ToString("0"),
        JsonTokenType.True or JsonTokenType.False => null,
        _ => null,
    };

    public override void Write(Utf8JsonWriter w, string v, JsonSerializerOptions o)
    {
        if (string.IsNullOrEmpty(v)) w.WriteNullValue(); else w.WriteStringValue(v);
    }
}
