using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;

namespace VoicePTT;

/// <summary>Schickt die Aufnahme an das Gateway bzw. an einen OpenAI-kompatiblen Whisper-Server.</summary>
public static class Transcriber
{
    private static readonly HttpClient Http = new(new HttpClientHandler { UseProxy = false })
    {
        Timeout = TimeSpan.FromSeconds(120)
    };

    public static async Task<string> TranscribeAsync(byte[] wav, AppConfig cfg, CancellationToken ct = default)
    {
        var url = BuildUrl(cfg, out var isOpenAi);

        using var form = new MultipartFormDataContent();
        var file = new ByteArrayContent(wav);
        file.Headers.ContentType = new MediaTypeHeaderValue("audio/wav");
        // Bewusst von Hand gesetzt: die Kurzform ohne filename*-Zusatz, genau wie beim alten Client.
        file.Headers.ContentDisposition = new ContentDispositionHeaderValue("form-data")
        {
            Name = "\"file\"",
            FileName = "\"ptt.wav\"",
        };
        form.Add(file);

        if (isOpenAi)
        {
            form.Add(new StringContent(string.IsNullOrWhiteSpace(cfg.Model) ? "whisper-1" : cfg.Model), "model");
            form.Add(new StringContent("json"), "response_format");
            if (!string.IsNullOrWhiteSpace(cfg.Language))
                form.Add(new StringContent(cfg.Language), "language");
        }
        else
        {
            form.Add(new StringContent(cfg.Correct ? "true" : "false"), "correct");
        }

        using var resp = await Http.PostAsync(url, form, ct).ConfigureAwait(false);
        var body = await resp.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
        if (!resp.IsSuccessStatusCode)
            throw new HttpRequestException("HTTP " + (int)resp.StatusCode + " " + resp.ReasonPhrase + " - " +
                                           Shorten(body));

        return ExtractText(body);
    }

    public static string BuildUrl(AppConfig cfg, out bool isOpenAi)
    {
        var b = (cfg.GatewayUrl ?? "").Trim().TrimEnd('/');
        isOpenAi = string.Equals(cfg.EndpointType, "openai", StringComparison.OrdinalIgnoreCase);
        if (isOpenAi)
        {
            if (b.EndsWith("/v1", StringComparison.OrdinalIgnoreCase)) b = b.Substring(0, b.Length - 3);
            return b + "/v1/audio/transcriptions";
        }
        return b + "/api/transcribe";
    }

    private static string ExtractText(string body)
    {
        try
        {
            using var doc = JsonDocument.Parse(body);
            if (doc.RootElement.ValueKind == JsonValueKind.Object &&
                doc.RootElement.TryGetProperty("text", out var t) && t.ValueKind == JsonValueKind.String)
                return (t.GetString() ?? "").Trim();
        }
        catch (JsonException)
        {
            // Manche Server antworten mit reinem Text.
            return body.Trim();
        }
        return "";
    }

    private static string Shorten(string s) =>
        string.IsNullOrEmpty(s) ? "" : (s.Length <= 200 ? s : s.Substring(0, 200) + "...");

    /// <summary>Erreichbarkeitspruefung fuer den Einstellungsdialog.</summary>
    public static async Task<string> PingAsync(AppConfig cfg)
    {
        var b = (cfg.GatewayUrl ?? "").Trim().TrimEnd('/');
        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(6));
            using var resp = await Http.GetAsync(b, HttpCompletionOption.ResponseHeadersRead, cts.Token);
            return "Erreichbar (HTTP " + (int)resp.StatusCode + ")";
        }
        catch (Exception ex)
        {
            return "Nicht erreichbar: " + ex.GetBaseException().Message;
        }
    }
}
