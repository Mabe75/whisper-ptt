using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace VoicePTT;

/// <summary>Inhalt von config.json. Die Feldnamen sind absichtlich identisch
/// mit denen des alten Python-Clients, damit alte Konfigurationen weiter passen.</summary>
public class AppConfig
{
    [JsonPropertyName("gateway_url")] public string GatewayUrl { get; set; } = "http://127.0.0.1:8055";
    [JsonPropertyName("endpoint_type")] public string EndpointType { get; set; } = "gateway";
    [JsonPropertyName("model")] public string Model { get; set; } = "whisper-1";
    [JsonPropertyName("language")] public string Language { get; set; } = "";
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

    /// <summary>Hotkey nicht an das aktive Fenster weiterreichen (neu ab 2.0).</summary>
    [JsonPropertyName("suppress_hotkey")] public bool SuppressHotkey { get; set; } = true;

    /// <summary>Kuerzere Aufnahmen werden verworfen.</summary>
    [JsonPropertyName("min_seconds")] public double MinSeconds { get; set; } = 0.3;

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

        var fresh = new AppConfig();
        fresh.Save();
        return fresh;
    }

    /// <summary>Konfiguration der Vorgaengerversion bzw. die mitgelieferte Vorlage uebernehmen.</summary>
    private static void MigrateIfNeeded()
    {
        if (File.Exists(Paths.ConfigPath)) return;
        foreach (var src in new[] { Path.Combine(Paths.LocalDir, "config.json"), Paths.DefaultConfigPath })
        {
            if (!File.Exists(src)) continue;
            try
            {
                File.Copy(src, Paths.ConfigPath);
                Log.Write("Konfiguration uebernommen aus", src);
                return;
            }
            catch (Exception ex) { Log.Write("Uebernahme fehlgeschlagen:", src, ex.Message); }
        }
    }

    public AppConfig Sanitized()
    {
        if (SampleRate < 8000 || SampleRate > 48000) SampleRate = 16000;
        if (BeepVolume < 0) BeepVolume = 0;
        if (BeepVolume > 1) BeepVolume = 1;
        if (MinSeconds < 0) MinSeconds = 0;
        if (string.IsNullOrWhiteSpace(Hotkey)) Hotkey = "f9";
        if (string.IsNullOrWhiteSpace(GatewayUrl)) GatewayUrl = "http://127.0.0.1:8055";
        InsertMode = InsertMode?.ToLowerInvariant() == "type" ? "type" : "paste";
        EndpointType = EndpointType?.ToLowerInvariant() == "openai" ? "openai" : "gateway";
        return this;
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
