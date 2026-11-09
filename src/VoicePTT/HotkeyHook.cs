using System.Runtime.InteropServices;

namespace VoicePTT;

/// <summary>Globaler Tastatur-Hook für die Push-to-Talk-Tasten aller Profile.
/// Muss auf einem Thread mit Nachrichtenschleife (UI-Thread) installiert werden.</summary>
public sealed class HotkeyHook : IDisposable
{
    private readonly Native.HookProc _proc;   // Referenz halten, sonst räumt der GC sie ab.
    private readonly SynchronizationContext _ui;
    private IntPtr _handle = IntPtr.Zero;

    /// <summary>Tastencode -> Profil. Wird beim Ändern komplett ersetzt, nie bearbeitet.</summary>
    private Dictionary<uint, Profile> _keys = new();

    /// <summary>Die gerade gehaltene Taste. Eine zweite wird ignoriert, solange sie liegt.</summary>
    private uint _downKey;

    public event Action<Profile> Pressed;
    public event Action<Profile> Released;

    public bool Suppress { get; set; } = true;

    public HotkeyHook(SynchronizationContext ui)
    {
        _ui = ui;
        _proc = Callback;
    }

    public void SetProfiles(IEnumerable<Profile> profiles)
    {
        var map = new Dictionary<uint, Profile>();
        foreach (var p in profiles ?? Enumerable.Empty<Profile>())
        {
            if (p == null || !p.Active) continue;
            var vk = HotkeyNames.ToVirtualKey(p.Hotkey);
            if (!map.ContainsKey(vk)) map[vk] = p;
        }
        _keys = map;
        _downKey = 0;
        Log.Write("Tasten aktiv:", string.Join(", ", map.Values.Select(p => p.ToString())));
    }

    public void Install()
    {
        if (_handle != IntPtr.Zero) return;
        _handle = Native.SetWindowsHookEx(Native.WH_KEYBOARD_LL, _proc, Native.GetModuleHandle(null), 0);
        if (_handle == IntPtr.Zero)
            throw new InvalidOperationException("Tastatur-Hook konnte nicht gesetzt werden (Fehler " +
                                                Marshal.GetLastWin32Error() + ").");
        Log.Write("Tastatur-Hook aktiv, suppress=" + Suppress);
    }

    private IntPtr Callback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0)
        {
            var data = Marshal.PtrToStructure<Native.KBDLLHOOKSTRUCT>(lParam);
            if (data.dwExtraInfo != Native.SelfMarker)
            {
                var map = _keys;
                if (map.TryGetValue(data.vkCode, out var profile))
                {
                    var msg = (int)wParam;
                    if (msg is Native.WM_KEYDOWN or Native.WM_SYSKEYDOWN)
                    {
                        if (_downKey == 0)
                        {
                            _downKey = data.vkCode;
                            _ui.Post(_ => Pressed?.Invoke(profile), null);
                        }
                        if (Suppress) return 1;
                    }
                    else if (msg is Native.WM_KEYUP or Native.WM_SYSKEYUP)
                    {
                        if (_downKey == data.vkCode)
                        {
                            _downKey = 0;
                            _ui.Post(_ => Released?.Invoke(profile), null);
                        }
                        if (Suppress) return 1;
                    }
                }
            }
        }
        return Native.CallNextHookEx(_handle, nCode, wParam, lParam);
    }

    public void Dispose()
    {
        if (_handle == IntPtr.Zero) return;
        Native.UnhookWindowsHookEx(_handle);
        _handle = IntPtr.Zero;
    }
}
