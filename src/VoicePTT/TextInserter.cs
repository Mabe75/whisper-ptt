namespace VoicePTT;

/// <summary>Setzt den erkannten Text in das gerade aktive Fenster.</summary>
public static class TextInserter
{
    /// <summary>Muss auf dem UI-Thread laufen (Zwischenablage benoetigt STA).</summary>
    public static void Insert(string text, AppConfig cfg)
    {
        if (string.IsNullOrEmpty(text)) return;

        if (string.Equals(cfg.InsertMode, "type", StringComparison.OrdinalIgnoreCase))
        {
            Native.SendText(text);
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
            Native.SendText(text);
            return;
        }

        Thread.Sleep(60);
        Native.SendCtrlV();

        if (previous == null) return;
        var restore = previous;
        Task.Delay(500).ContinueWith(_ =>
        {
            try { Clipboard.SetDataObject(restore, true, 10, 60); }
            catch (Exception ex) { Log.Write("Zwischenablage wiederherstellen:", ex.Message); }
        }, CancellationToken.None, TaskContinuationOptions.None, TaskScheduler.FromCurrentSynchronizationContext());
    }
}
