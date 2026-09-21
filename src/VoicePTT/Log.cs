using System.Text;

namespace VoicePTT;

public static class Log
{
    private static readonly object Gate = new();
    private const long MaxBytes = 1_000_000;

    public static void Write(params object[] parts)
    {
        try
        {
            var line = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + " " +
                       string.Join(" ", parts.Select(p => p?.ToString() ?? "null"));
            lock (Gate)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(Paths.LogPath)!);
                var fi = new FileInfo(Paths.LogPath);
                if (fi.Exists && fi.Length > MaxBytes)
                {
                    var old = Paths.LogPath + ".1";
                    File.Delete(old);
                    File.Move(Paths.LogPath, old);
                }
                File.AppendAllText(Paths.LogPath, line + Environment.NewLine, new UTF8Encoding(false));
            }
        }
        catch
        {
            // Logging darf nie die Anwendung stoeren.
        }
    }
}
