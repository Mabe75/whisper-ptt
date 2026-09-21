using System.Diagnostics;

namespace VoicePTT;

/// <summary>Feste Ablageorte. Das Programmverzeichnis ist schreibgeschuetzt,
/// Konfiguration und Log liegen deshalb im Benutzerprofil.</summary>
public static class Paths
{
    public static string AppDir => AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar);
    public static string ExePath => Path.Combine(AppDir, "VoicePTT.exe");

    /// <summary>%APPDATA%\VoicePTT</summary>
    public static string DataDir { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "VoicePTT");

    /// <summary>%LOCALAPPDATA%\VoicePTT (auch der Ort der alten Python-Installation)</summary>
    public static string LocalDir { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "VoicePTT");

    public static string ConfigPath => Path.Combine(DataDir, "config.json");
    public static string LogPath => Path.Combine(LocalDir, "client.log");
    public static string DefaultConfigPath => Path.Combine(AppDir, "config.default.json");

    public static void EnsureDirs()
    {
        Directory.CreateDirectory(DataDir);
        Directory.CreateDirectory(LocalDir);
    }

    public static void OpenInShell(string path)
    {
        try { Process.Start(new ProcessStartInfo(path) { UseShellExecute = true }); }
        catch (Exception ex) { Log.Write("OpenInShell fehlgeschlagen:", path, ex.Message); }
    }
}
