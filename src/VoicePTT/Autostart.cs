using Microsoft.Win32;

namespace VoicePTT;

/// <summary>Autostart ueber HKCU\...\Run - kein Adminrecht noetig.</summary>
public static class Autostart
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "VoicePTT";

    public static bool IsEnabled()
    {
        try
        {
            using var k = Registry.CurrentUser.OpenSubKey(RunKey);
            var v = k?.GetValue(ValueName) as string;
            return !string.IsNullOrEmpty(v);
        }
        catch { return false; }
    }

    public static void Set(bool on)
    {
        try
        {
            using var k = Registry.CurrentUser.CreateSubKey(RunKey, true);
            if (k == null) return;
            if (on) k.SetValue(ValueName, "\"" + Environment.ProcessPath + "\"");
            else k.DeleteValue(ValueName, false);
            Log.Write("Autostart", on ? "aktiviert" : "deaktiviert");
        }
        catch (Exception ex) { Log.Write("Autostart setzen fehlgeschlagen:", ex.Message); }
    }
}
