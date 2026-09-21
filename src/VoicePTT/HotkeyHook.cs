using System.Runtime.InteropServices;

namespace VoicePTT;

/// <summary>Globaler Tastatur-Hook fuer eine einzelne Push-to-Talk-Taste.
/// Muss auf einem Thread mit Nachrichtenschleife (UI-Thread) installiert werden.</summary>
public sealed class HotkeyHook : IDisposable
{
    private readonly Native.HookProc _proc;   // Referenz halten, sonst raeumt der GC sie ab.
    private readonly SynchronizationContext _ui;
    private IntPtr _handle = IntPtr.Zero;
    private bool _isDown;

    public event Action Pressed;
    public event Action Released;

    public uint VirtualKey { get; private set; } = 0x78;
    public bool Suppress { get; set; } = true;

    public HotkeyHook(SynchronizationContext ui)
    {
        _ui = ui;
        _proc = Callback;
    }

    public void SetKey(string name)
    {
        VirtualKey = HotkeyNames.ToVirtualKey(name);
        _isDown = false;
    }

    public void Install()
    {
        if (_handle != IntPtr.Zero) return;
        _handle = Native.SetWindowsHookEx(Native.WH_KEYBOARD_LL, _proc, Native.GetModuleHandle(null), 0);
        if (_handle == IntPtr.Zero)
            throw new InvalidOperationException("Tastatur-Hook konnte nicht gesetzt werden (Fehler " +
                                                Marshal.GetLastWin32Error() + ").");
        Log.Write("Tastatur-Hook aktiv, VK=0x" + VirtualKey.ToString("X2"), "suppress=" + Suppress);
    }

    private IntPtr Callback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0 && VirtualKey != 0)
        {
            var data = Marshal.PtrToStructure<Native.KBDLLHOOKSTRUCT>(lParam);
            if (data.vkCode == VirtualKey && data.dwExtraInfo != Native.SelfMarker)
            {
                var msg = (int)wParam;
                if (msg is Native.WM_KEYDOWN or Native.WM_SYSKEYDOWN)
                {
                    if (!_isDown)
                    {
                        _isDown = true;
                        _ui.Post(_ => Pressed?.Invoke(), null);
                    }
                    if (Suppress) return 1;
                }
                else if (msg is Native.WM_KEYUP or Native.WM_SYSKEYUP)
                {
                    if (_isDown)
                    {
                        _isDown = false;
                        _ui.Post(_ => Released?.Invoke(), null);
                    }
                    if (Suppress) return 1;
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
