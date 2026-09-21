using System.Diagnostics;

namespace VoicePTT;

internal static class Program
{
    private const string MutexName = "VoicePTT_SingleInstance_Mutex";

    /// <summary>Signal fuer "bitte sauber beenden" - genutzt von Setup und Deinstallation.</summary>
    public const string QuitEventName = "VoicePTT_Quit_Event";

    private static Mutex _instanceMutex;

    [STAThread]
    private static void Main(string[] args)
    {
        if (args.Any(a => a.Equals("--quit", StringComparison.OrdinalIgnoreCase)))
        {
            // Wird vom Installer/Deinstaller genutzt, um eine laufende Instanz zu beenden.
            QuitRunningInstances();
            return;
        }

        _instanceMutex = new Mutex(true, MutexName, out var isFirst);
        if (!isFirst)
        {
            // Der alte Python-Client benutzt denselben Mutex. Haelt ihn niemand sonst
            // aus dieser Anwendung, laeuft noch die Vorgaengerversion.
            var legacy = Others().Length == 0;
            Log.Write(legacy
                ? "Blockiert: es laeuft noch die alte Python-Version."
                : "Laeuft bereits - zweite Instanz beendet sich.");
            if (legacy)
                MessageBox.Show(
                    "Es laeuft noch die alte Python-Version von Voice Push-to-Talk.\n\n" +
                    "Bitte diese ueber ihr Tray-Symbol beenden (oder abmelden und neu anmelden) " +
                    "und Voice Push-to-Talk anschliessend erneut starten.",
                    "Voice Push-to-Talk", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);

        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
            Log.Write("FATAL:", (e.ExceptionObject as Exception)?.ToString() ?? "unbekannt");
        Application.ThreadException += (_, e) =>
            Log.Write("UI-Fehler:", e.Exception.ToString());

        try
        {
            Log.Write("=== Start, PID", Environment.ProcessId, "Version",
                      typeof(Program).Assembly.GetName().Version, "===");
            var cfg = AppConfig.Load();
            Application.Run(new TrayApp(cfg));
            Log.Write("=== Ende ===");
        }
        catch (Exception ex)
        {
            Log.Write("FATAL:", ex.ToString());
            MessageBox.Show("Voice Push-to-Talk konnte nicht gestartet werden:\n\n" + ex.Message +
                            "\n\nDetails: " + Paths.LogPath,
                "Voice Push-to-Talk", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            try { _instanceMutex?.ReleaseMutex(); } catch { }
            _instanceMutex?.Dispose();
        }
    }

    private static void QuitRunningInstances()
    {
        // Zuerst hoeflich: die laufende Instanz raeumt ihr Tray-Symbol selbst ab.
        try
        {
            if (EventWaitHandle.TryOpenExisting(QuitEventName, out var ev))
            {
                ev.Set();
                ev.Dispose();
            }
        }
        catch (Exception ex) { Log.Write("Quit-Signal fehlgeschlagen:", ex.Message); }

        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (DateTime.UtcNow < deadline)
        {
            var running = Others();
            if (running.Length == 0) return;
            foreach (var p in running) p.Dispose();
            Thread.Sleep(200);
        }

        foreach (var p in Others())
        {
            try { p.Kill(); Log.Write("Instanz", p.Id, "abgebrochen."); }
            catch (Exception ex) { Log.Write("Beenden von PID", p.Id, "fehlgeschlagen:", ex.Message); }
            finally { p.Dispose(); }
        }
    }

    private static Process[] Others()
    {
        var me = Environment.ProcessId;
        var all = Process.GetProcessesByName("VoicePTT");
        var others = all.Where(p => p.Id != me).ToArray();
        foreach (var p in all.Except(others)) p.Dispose();
        return others;
    }
}
