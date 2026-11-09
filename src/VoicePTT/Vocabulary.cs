using System.Text;
using System.Text.RegularExpressions;

namespace VoicePTT;

/// <summary>Die Vokabeltabelle wirkt an drei Stellen: als Vorgabe für Whisper,
/// als Hinweis im LLM-Prompt und als harte Ersetzung im fertigen Text.</summary>
public static class Vocabulary
{
    private static IEnumerable<VocabularyEntry> Active(AppConfig cfg) =>
        (cfg.Vocabulary ?? new List<VocabularyEntry>())
        .Where(v => v != null && v.Active && !string.IsNullOrWhiteSpace(v.Term));

    /// <summary>Begriffsliste für das prompt-Feld von Whisper. Bewusst knapp:
    /// eine überladene Vorgabe verschlechtert die Erkennung eher.</summary>
    public static string WhisperPrompt(AppConfig cfg)
    {
        var terms = Active(cfg).Select(v => v.Term.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        if (terms.Count == 0) return "";

        // 224 Token sind die Obergrenze von Whisper; grob 4 Zeichen pro Token.
        var sb = new StringBuilder();
        foreach (var t in terms)
        {
            if (sb.Length + t.Length + 2 > 700) break;
            if (sb.Length > 0) sb.Append(", ");
            sb.Append(t);
        }
        return sb.ToString();
    }

    /// <summary>Hinweisblock, der an die Profil-Anweisung angehängt wird.</summary>
    /// <param name="text">Der Rohtext. Genannt werden nur Verwechslungen, die darin wirklich
    /// vorkommen - jedes zusätzliche Paar kostet das Modell messbar Trefferquote.</param>
    public static string PromptBlock(AppConfig cfg, string text)
    {
        var entries = Active(cfg).ToList();
        var terms = entries.Select(v => v.Term.Trim())
                           .Distinct(StringComparer.OrdinalIgnoreCase)
                           .Take(40)
                           .ToList();
        if (terms.Count == 0) return "";

        var sb = new StringBuilder();
        sb.Append("\n\nDiese Eigennamen kommen regelmäßig vor und werden oft falsch erkannt: ");
        sb.Append(string.Join(", ", terms)).Append(".\n");

        var pairs = entries
            .SelectMany(e => (e.Confusions ?? new List<string>())
                .Where(c => !string.IsNullOrWhiteSpace(c) && Contains(text, c))
                .Select(c => "\"" + c.Trim() + "\" ist fast immer \"" + e.Term.Trim() + "\" gemeint"))
            .Distinct()
            .ToList();

        if (pairs.Count > 0)
        {
            sb.Append("Häufige Verwechslungen: ").Append(string.Join("; ", pairs)).Append(".\n");
            sb.Append("Regel dazu: Prüfe jedes Vorkommen einzeln. Steht das Wort für ein Werkzeug oder Gegenüber, ");
            sb.Append("mit dem man arbeitet, redet oder das man fragt, ist der Eigenname gemeint. Bezeichnet es im ");
            sb.Append("Satz eine Sache oder Technik, bleibt das normale Wort stehen. Im Zweifel der Eigenname.");
        }
        else
        {
            sb.Append("Regel dazu: Setze einen Eigennamen nur ein, wenn aus dem Satz hervorgeht, dass er gemeint ist. ");
            sb.Append("Ein normales deutsches oder englisches Wort, das im Satz Sinn ergibt, bleibt unverändert.");
        }

        return sb.ToString();
    }

    /// <summary>Wortweises Vorkommen, damit "cloud" nicht in "Cloudflare" anschlägt.</summary>
    private static bool Contains(string text, string word)
    {
        if (string.IsNullOrEmpty(text)) return false;
        try
        {
            return Regex.IsMatch(text, @"(?<![\p{L}\p{N}])" + Regex.Escape(word.Trim()) + @"(?![\p{L}\p{N}])",
                                 RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));
        }
        catch (Exception ex)
        {
            Log.Write("Verwechslungsprüfung übersprungen:", word, ex.Message);
            return false;
        }
    }

    /// <summary>Harte Ersetzung der eingetragenen Falschschreibungen, wortweise und
    /// ohne Rücksicht auf Groß-/Kleinschreibung der Fundstelle.</summary>
    public static string ApplyReplacements(string text, AppConfig cfg)
    {
        if (string.IsNullOrEmpty(text)) return text;

        foreach (var entry in Active(cfg))
        {
            var term = entry.Term.Trim();
            foreach (var variant in entry.Variants ?? new List<string>())
            {
                var v = (variant ?? "").Trim();
                if (v.Length == 0 || string.Equals(v, term, StringComparison.OrdinalIgnoreCase)) continue;
                try
                {
                    var pattern = @"(?<![\p{L}\p{N}])" + Regex.Escape(v) + @"(?![\p{L}\p{N}])";
                    text = Regex.Replace(text, pattern, _ => term,
                                         RegexOptions.IgnoreCase | RegexOptions.CultureInvariant,
                                         TimeSpan.FromSeconds(1));
                }
                catch (Exception ex)
                {
                    Log.Write("Ersetzung übersprungen:", v, ex.Message);
                }
            }
        }
        return text;
    }
}
