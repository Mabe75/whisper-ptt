using System.Text.RegularExpressions;

namespace VoicePTT;

/// <summary>Setzt den erkannten Text in das gerade aktive Fenster.
/// Methode, Einfuegekuerzel und Umbruchbehandlung kommen aus dem Profil, sonst
/// aus den allgemeinen Einstellungen.</summary>
public static class TextInserter
{
    /// <summary>Muss auf dem UI-Thread laufen (Zwischenablage benoetigt STA).</summary>
    public static void Insert(string text, AppConfig cfg, Profile profile = null)
    {
        if (string.IsNullOrEmpty(text)) return;

        var mode = Pick(profile?.InsertMode, cfg.InsertMode, "paste");
        var newline = NewlineModes.Parse(Pick(profile?.Newline, cfg.Newline, "enter"));

        // "space" heisst: im Text sollen gar keine Umbrueche stehen. Das gilt auch beim
        // Einfuegen ueber die Zwischenablage, nicht nur beim Tippen - sonst schickt eine
        // Eingabemaske den Text beim ersten Umbruch ab.
        if (newline == NewlineMode.Space) text = WithoutNewlines(text);
        if (text.Length == 0) return;

        if (string.Equals(mode, "type", StringComparison.OrdinalIgnoreCase))
        {
            Native.SendText(text, newline);
            return;
        }

        string previous = null;
        try
        {
            if (cfg.RestoreClipboard && Clipboard.ContainsText()) previous = Clipboard.GetText();
        }
        catch (Exception ex) { Log.Write("Zwischenablage lesen:", ex.Message); }

        try
        {
            Clipboard.SetDataObject(text, true, 10, 60);
        }
        catch (Exception ex)
        {
            Log.Write("Zwischenablage schreiben fehlgeschlagen, tippe stattdessen:", ex.Message);
            Native.SendText(text, newline);
            return;
        }

        Thread.Sleep(60);
        Native.SendChord(Chord(cfg, profile));

        if (previous == null) return;
        var restore = previous;
        Task.Delay(500).ContinueWith(_ =>
        {
            try { Clipboard.SetDataObject(restore, true, 10, 60); }
            catch (Exception ex) { Log.Write("Zwischenablage wiederherstellen:", ex.Message); }
        }, CancellationToken.None, TaskContinuationOptions.None, TaskScheduler.FromCurrentSynchronizationContext());
    }

    /// <summary>Beschreibt fuer das Protokoll, wie dieses Profil einfuegt.</summary>
    public static string Describe(AppConfig cfg, Profile profile)
    {
        var mode = Pick(profile?.InsertMode, cfg.InsertMode, "paste");
        var newline = Pick(profile?.Newline, cfg.Newline, "enter");
        if (string.Equals(mode, "type", StringComparison.OrdinalIgnoreCase))
            return "tippt, Umbruch " + NewlineModes.Label(newline);
        return "fuegt ein mit " + Chord(cfg, profile).Display()
               + (NewlineModes.Parse(newline) == NewlineMode.Space ? ", ohne Umbrueche" : "");
    }

    private static KeyChord Chord(AppConfig cfg, Profile profile) =>
        KeyChord.ParseOrDefault(Pick(profile?.PasteKeys, cfg.PasteKeys, "ctrl+v"), KeyChord.CtrlV);

    /// <summary>Profil gewinnt, dann die allgemeine Einstellung, dann die Vorgabe.</summary>
    private static string Pick(string fromProfile, string fromConfig, string fallback)
    {
        if (!string.IsNullOrWhiteSpace(fromProfile)) return fromProfile.Trim();
        if (!string.IsNullOrWhiteSpace(fromConfig)) return fromConfig.Trim();
        return fallback;
    }

    private static string WithoutNewlines(string text) =>
        Regex.Replace(text, @"[ \t]*\r?\n[ \t]*", " ").Trim();
}
