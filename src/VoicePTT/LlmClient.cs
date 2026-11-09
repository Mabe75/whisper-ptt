using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace VoicePTT;

/// <summary>Nachbearbeitung des Rohtextes über einen OpenAI-kompatiblen Chat-Endpunkt.</summary>
public static class LlmClient
{
    private static readonly HttpClient Http = new(new HttpClientHandler { UseProxy = false })
    {
        Timeout = Timeout.InfiniteTimeSpan,   // die Frist setzt der Aufrufer pro Anfrage
    };

    public static async Task<string> ProcessAsync(string text, Profile profile, AppConfig cfg,
                                                  CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(text)) return text;
        if (!cfg.LlmConfigured) throw new InvalidOperationException("Keine LLM-Adresse konfiguriert.");

        var system = (profile.Instruction ?? "").Trim() + Vocabulary.PromptBlock(cfg, text);
        if (cfg.LogPrompts) Log.Write("Systemprompt:", system.Replace("\n", " | "));

        var body = new JsonObject
        {
            ["model"] = cfg.LlmModel,
            ["temperature"] = cfg.LlmTemperature,
            ["stream"] = false,
            ["messages"] = new JsonArray(
                new JsonObject { ["role"] = "system", ["content"] = system },
                new JsonObject { ["role"] = "user", ["content"] = text }),
            // Qwen3 und Verwandte denken sonst vor jeder Antwort laut nach - das dauert
            // ein Vielfaches und bringt bei Korrekturen nichts.
            ["chat_template_kwargs"] = new JsonObject { ["enable_thinking"] = profile.Thinking },
        };
        MergeExtra(body, cfg.LlmExtra);

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(TimeSpan.FromSeconds(cfg.LlmTimeoutSeconds));

        using var content = new StringContent(body.ToJsonString(), Encoding.UTF8, "application/json");
        using var resp = await Http.PostAsync(ChatUrl(cfg.LlmUrl), content, cts.Token).ConfigureAwait(false);
        var raw = await resp.Content.ReadAsStringAsync(cts.Token).ConfigureAwait(false);

        if (!resp.IsSuccessStatusCode)
            throw new HttpRequestException("LLM antwortete mit HTTP " + (int)resp.StatusCode + " - " + Shorten(raw));

        var answer = ExtractContent(raw);
        if (string.IsNullOrWhiteSpace(answer))
            throw new InvalidOperationException("LLM lieferte keinen Text.");

        return Tidy(answer);
    }

    /// <summary>Nutzerfelder gewinnen. Ein Feld mit dem Wert null entfernt den Schlüssel.</summary>
    private static void MergeExtra(JsonObject body, JsonObject extra)
    {
        if (extra == null) return;
        foreach (var kv in extra.ToList())
        {
            if (kv.Value == null) body.Remove(kv.Key);
            else body[kv.Key] = kv.Value.DeepClone();
        }
    }

    public static string ChatUrl(string baseUrl)
    {
        var b = (baseUrl ?? "").Trim().TrimEnd('/');
        if (b.EndsWith("/chat/completions", StringComparison.OrdinalIgnoreCase)) return b;
        if (b.EndsWith("/v1", StringComparison.OrdinalIgnoreCase)) return b + "/chat/completions";
        return b + "/v1/chat/completions";
    }

    private static string ModelsUrl(string baseUrl)
    {
        var b = (baseUrl ?? "").Trim().TrimEnd('/');
        if (b.EndsWith("/chat/completions", StringComparison.OrdinalIgnoreCase))
            b = b.Substring(0, b.Length - "/chat/completions".Length);
        if (b.EndsWith("/v1", StringComparison.OrdinalIgnoreCase)) return b + "/models";
        return b + "/v1/models";
    }

    private static string ExtractContent(string raw)
    {
        using var doc = JsonDocument.Parse(raw);
        if (!doc.RootElement.TryGetProperty("choices", out var choices) ||
            choices.ValueKind != JsonValueKind.Array || choices.GetArrayLength() == 0)
            return "";

        var first = choices[0];
        if (first.TryGetProperty("message", out var msg) &&
            msg.TryGetProperty("content", out var c) && c.ValueKind == JsonValueKind.String)
            return c.GetString() ?? "";

        // Manche Server liefern den Text im Completion-Format.
        if (first.TryGetProperty("text", out var t) && t.ValueKind == JsonValueKind.String)
            return t.GetString() ?? "";

        return "";
    }

    /// <summary>Denkblöcke und Verpackung entfernen, die manche Modelle trotz Vorgabe mitschicken.</summary>
    private static string Tidy(string s)
    {
        s = Regex.Replace(s, @"<think>.*?</think>", "", RegexOptions.Singleline | RegexOptions.IgnoreCase);
        s = s.Trim();

        // Nur eine Verpackung des ganzen Textes entfernen, nie ein Zitat mitten im Satz.
        if (s.Length > 1)
        {
            if (s[0] == '"' && s[^1] == '"' && s.IndexOf('"', 1) == s.Length - 1)
                s = s.Substring(1, s.Length - 2).Trim();
            else if (s[0] == '„' && s[^1] == '“' && s.IndexOf('„', 1) < 0)
                s = s.Substring(1, s.Length - 2).Trim();
        }

        return s;
    }

    private static string Shorten(string s) =>
        string.IsNullOrEmpty(s) ? "" : (s.Length <= 200 ? s : s.Substring(0, 200) + "...");

    /// <summary>Erreichbarkeitsprüfung für den Einstellungsdialog.</summary>
    public static async Task<string> PingAsync(string baseUrl)
    {
        if (string.IsNullOrWhiteSpace(baseUrl)) return "Keine Adresse angegeben.";
        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(8));
            using var resp = await Http.GetAsync(ModelsUrl(baseUrl), cts.Token);
            var raw = await resp.Content.ReadAsStringAsync(cts.Token);
            if (!resp.IsSuccessStatusCode) return "HTTP " + (int)resp.StatusCode + " - " + Shorten(raw);

            using var doc = JsonDocument.Parse(raw);
            if (doc.RootElement.TryGetProperty("data", out var data) && data.ValueKind == JsonValueKind.Array)
            {
                var ids = data.EnumerateArray()
                    .Select(e => e.TryGetProperty("id", out var id) ? id.GetString() : null)
                    .Where(s => !string.IsNullOrEmpty(s))
                    .ToList();
                return ids.Count == 0 ? "Erreichbar, meldet keine Modelle."
                                      : "Erreichbar. Modelle: " + string.Join(", ", ids);
            }
            return "Erreichbar (HTTP " + (int)resp.StatusCode + ")";
        }
        catch (Exception ex)
        {
            return "Nicht erreichbar: " + ex.GetBaseException().Message;
        }
    }

    /// <summary>Holt die Modellnamen für die Auswahlliste; leer, wenn nichts zu holen ist.</summary>
    public static async Task<List<string>> ListModelsAsync(string baseUrl)
    {
        var result = new List<string>();
        if (string.IsNullOrWhiteSpace(baseUrl)) return result;
        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(8));
            using var resp = await Http.GetAsync(ModelsUrl(baseUrl), cts.Token);
            if (!resp.IsSuccessStatusCode) return result;
            using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync(cts.Token));
            if (doc.RootElement.TryGetProperty("data", out var data) && data.ValueKind == JsonValueKind.Array)
                foreach (var e in data.EnumerateArray())
                    if (e.TryGetProperty("id", out var id) && id.ValueKind == JsonValueKind.String)
                        result.Add(id.GetString());
        }
        catch (Exception ex) { Log.Write("Modellliste:", ex.GetBaseException().Message); }
        return result;
    }
}
