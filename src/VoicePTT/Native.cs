using System.Runtime.InteropServices;

namespace VoicePTT;

internal static class Native
{
    public const int WH_KEYBOARD_LL = 13;
    public const int WM_KEYDOWN = 0x0100;
    public const int WM_KEYUP = 0x0101;
    public const int WM_SYSKEYDOWN = 0x0104;
    public const int WM_SYSKEYUP = 0x0105;

    public const uint INPUT_KEYBOARD = 1;
    public const uint KEYEVENTF_KEYUP = 0x0002;
    public const uint KEYEVENTF_UNICODE = 0x0004;

    public const ushort VK_CONTROL = 0x11;
    public const ushort VK_V = 0x56;
    public const ushort VK_SHIFT_KEY = 0x10;
    public const ushort VK_RETURN = 0x0D;

    public delegate IntPtr HookProc(int nCode, IntPtr wParam, IntPtr lParam);

    [StructLayout(LayoutKind.Sequential)]
    public struct KBDLLHOOKSTRUCT
    {
        public uint vkCode;
        public uint scanCode;
        public uint flags;
        public uint time;
        public IntPtr dwExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct MOUSEINPUT
    {
        public int dx, dy;
        public uint mouseData, dwFlags, time;
        public IntPtr dwExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct KEYBDINPUT
    {
        public ushort wVk, wScan;
        public uint dwFlags, time;
        public IntPtr dwExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct HARDWAREINPUT
    {
        public uint uMsg;
        public ushort wParamL, wParamH;
    }

    [StructLayout(LayoutKind.Explicit)]
    public struct InputUnion
    {
        [FieldOffset(0)] public MOUSEINPUT mi;
        [FieldOffset(0)] public KEYBDINPUT ki;
        [FieldOffset(0)] public HARDWAREINPUT hi;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct INPUT
    {
        public uint type;
        public InputUnion U;
    }

    [DllImport("user32.dll", SetLastError = true)]
    public static extern IntPtr SetWindowsHookEx(int idHook, HookProc lpfn, IntPtr hMod, uint dwThreadId);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool UnhookWindowsHookEx(IntPtr hhk);

    [DllImport("user32.dll")]
    public static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    public static extern IntPtr GetModuleHandle(string lpModuleName);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

    [DllImport("user32.dll")]
    public static extern short GetAsyncKeyState(int vKey);

    public static readonly int InputSize = Marshal.SizeOf<INPUT>();

    // Kennzeichnet selbst erzeugte Tastatureingaben, damit der eigene Hook sie ignoriert.
    public static readonly IntPtr SelfMarker = new(0x56505454);

    private static INPUT Key(ushort vk, bool up) => new()
    {
        type = INPUT_KEYBOARD,
        U = new InputUnion
        {
            ki = new KEYBDINPUT { wVk = vk, dwFlags = up ? KEYEVENTF_KEYUP : 0, dwExtraInfo = SelfMarker }
        }
    };

    private static INPUT Unicode(char c, bool up) => new()
    {
        type = INPUT_KEYBOARD,
        U = new InputUnion
        {
            ki = new KEYBDINPUT
            {
                wScan = c,
                dwFlags = KEYEVENTF_UNICODE | (up ? KEYEVENTF_KEYUP : 0),
                dwExtraInfo = SelfMarker
            }
        }
    };

    /// <summary>Sendet ein Tastenkuerzel, z. B. Strg+V oder Umschalt+Einfg.
    /// Zusatztasten werden in umgekehrter Reihenfolge wieder losgelassen.</summary>
    public static void SendChord(KeyChord chord)
    {
        WaitForModifiersReleased();
        var seq = new List<INPUT>(chord.Modifiers.Length * 2 + 2);
        foreach (var m in chord.Modifiers) seq.Add(Key(m, false));
        seq.Add(Key(chord.Key, false));
        seq.Add(Key(chord.Key, true));
        for (var i = chord.Modifiers.Length - 1; i >= 0; i--) seq.Add(Key(chord.Modifiers[i], true));

        var arr = seq.ToArray();
        SendInput((uint)arr.Length, arr, InputSize);
    }

    public static void SendText(string text, NewlineMode newline = NewlineMode.Enter)
    {
        WaitForModifiersReleased();
        const int chunk = 200;
        var list = new List<INPUT>(chunk * 2);
        foreach (var c in text)
        {
            if (c == '\r') continue;
            if (c == '\n')
            {
                switch (newline)
                {
                    case NewlineMode.Space:
                        list.Add(Unicode(' ', false));
                        list.Add(Unicode(' ', true));
                        break;

                    // In Eingabemasken ein Umbruch, der die Eingabe nicht abschickt.
                    case NewlineMode.ShiftEnter:
                        list.Add(Key(VK_SHIFT_KEY, false));
                        list.Add(Key(VK_RETURN, false));
                        list.Add(Key(VK_RETURN, true));
                        list.Add(Key(VK_SHIFT_KEY, true));
                        break;

                    default:
                        list.Add(Key(VK_RETURN, false));
                        list.Add(Key(VK_RETURN, true));
                        break;
                }
            }
            else
            {
                list.Add(Unicode(c, false));
                list.Add(Unicode(c, true));
            }

            if (list.Count >= chunk * 2)
            {
                var b = list.ToArray();
                SendInput((uint)b.Length, b, InputSize);
                list.Clear();
                Thread.Sleep(1);
            }
        }
        if (list.Count > 0)
        {
            var b = list.ToArray();
            SendInput((uint)b.Length, b, InputSize);
        }
    }

    // Haengende Modifier des Nutzers wuerden Strg+V verfaelschen - kurz abwarten.
    private static void WaitForModifiersReleased()
    {
        const int VK_SHIFT = 0x10, VK_MENU = 0x12, VK_LWIN = 0x5B, VK_RWIN = 0x5C;
        for (var i = 0; i < 40; i++)
        {
            if (!(Down(VK_SHIFT) || Down(VK_CONTROL) || Down(VK_MENU) || Down(VK_LWIN) || Down(VK_RWIN)))
                return;
            Thread.Sleep(10);
        }
    }

    private static bool Down(int vk) => (GetAsyncKeyState(vk) & 0x8000) != 0;
}
