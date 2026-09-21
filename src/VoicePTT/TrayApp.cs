using System.Diagnostics;
using System.Reflection;

namespace VoicePTT;

/// <summary>Tray-Symbol, Menue und der eigentliche Push-to-Talk-Ablauf.</summary>
public sealed class TrayApp : ApplicationContext
{
    private readonly AppConfig _cfg;
    private readonly Form _sync;
    private readonly NotifyIcon _icon;
    private readonly ContextMenuStrip _menu;
    private readonly HotkeyHook _hook;
    private readonly Recorder _rec = new();
    private EventWaitHandle _quitEvent;
    private RegisteredWaitHandle _quitWait;
    private bool _busy;

    public TrayApp(AppConfig cfg)
    {
        _cfg = cfg;

        // Unsichtbares Fenster: erzeugt Fensterhandle und WinForms-SynchronizationContext.
        _sync = new Form
        {
            ShowInTaskbar = false,
            FormBorderStyle = FormBorderStyle.FixedToolWindow,
            StartPosition = FormStartPosition.Manual,
            Location = new Point(-32000, -32000),
            Size = new Size(1, 1),
        };
        _ = _sync.Handle;

        _menu = new ContextMenuStrip { ShowImageMargin = false };
        _menu.Opening += (_, _) => BuildMenu();

        _icon = new NotifyIcon
        {
            Icon = LoadIcon(),
            Visible = true,
            Text = "Voice Push-to-Talk",
            ContextMenuStrip = _menu,
        };
        _icon.DoubleClick += (_, _) => OpenWebUi();

        _hook = new HotkeyHook(SynchronizationContext.Current ?? new WindowsFormsSynchronizationContext());
        _hook.SetKey(_cfg.Hotkey);
        _hook.Suppress = _cfg.SuppressHotkey;
        _hook.Pressed += OnPressed;
        _hook.Released += OnReleased;
        _hook.Install();

        RegisterQuitSignal();

        UpdateTooltip();
        Log.Write("Bereit. Taste=" + HotkeyNames.Display(_cfg.Hotkey), "Gateway=" + _cfg.GatewayUrl,
                  "Modus=" + _cfg.EndpointType, "Einfuegen=" + _cfg.InsertMode);
    }

    /// <summary>Setup und Deinstallation koennen die laufende Instanz so sauber beenden.</summary>
    private void RegisterQuitSignal()
    {
        try
        {
            _quitEvent = new EventWaitHandle(false, EventResetMode.AutoReset, Program.QuitEventName);
            _quitWait = ThreadPool.RegisterWaitForSingleObject(_quitEvent, (_, _) =>
            {
                try { _sync.BeginInvoke(new Action(() => { Log.Write("Beendet auf Signal."); ExitThread(); })); }
                catch (Exception ex) { Log.Write("Quit-Signal:", ex.Message); }
            }, null, Timeout.Infinite, true);
        }
        catch (Exception ex) { Log.Write("Quit-Signal nicht verfuegbar:", ex.Message); }
    }

    private static Icon LoadIcon()
    {
        try
        {
            using var s = Assembly.GetExecutingAssembly().GetManifestResourceStream("VoicePTT.voiceptt.ico");
            if (s != null) return new Icon(s, SystemInformation.SmallIconSize);
        }
        catch (Exception ex) { Log.Write("Symbol laden fehlgeschlagen:", ex.Message); }
        return SystemIcons.Application;
    }

    // ---------------- Push-to-Talk ----------------

    private void OnPressed()
    {
        if (_rec.IsRecording) return;
        try
        {
            _rec.Start(_cfg);
            Beep(660, 55);
            UpdateTooltip(true);
        }
        catch (Exception ex)
        {
            Log.Write("Start fehlgeschlagen:", ex.Message);
            Beep(300, 200);
            Warn("Aufnahme nicht moeglich: " + ex.Message);
        }
    }

    private async void OnReleased()
    {
        if (!_rec.IsRecording) return;
        Beep(520, 55);
        UpdateTooltip();

        byte[] wav;
        try
        {
            wav = await _rec.StopAsync();
        }
        catch (Exception ex)
        {
            Log.Write("Stop fehlgeschlagen:", ex.Message);
            Beep(300, 200);
            return;
        }

        var seconds = Wav.Seconds(wav, _cfg.SampleRate);
        Log.Write("Aufnahme beendet:", seconds.ToString("0.00") + " s");
        if (seconds < _cfg.MinSeconds)
        {
            Log.Write("Zu kurz, verworfen.");
            Beep(300, 150);
            return;
        }

        if (_busy)
        {
            Log.Write("Vorherige Anfrage laeuft noch - trotzdem gesendet.");
        }

        _busy = true;
        UpdateTooltip();
        try
        {
            var text = await Transcriber.TranscribeAsync(wav, _cfg);
            if (string.IsNullOrWhiteSpace(text))
            {
                Log.Write("Leeres Ergebnis.");
                Beep(300, 150);
                return;
            }
            Log.Write("Eingefuegt:", Quote(text));
            TextInserter.Insert(text, _cfg);
            Beep(780, 55);
        }
        catch (Exception ex)
        {
            var msg = ex.GetBaseException().Message;
            Log.Write("Fehler:", msg);
            Beep(300, 250);
            Warn("Transkription fehlgeschlagen: " + msg);
        }
        finally
        {
            _busy = false;
            UpdateTooltip();
        }
    }

    private static string Quote(string t) => "\"" + (t.Length <= 80 ? t : t.Substring(0, 80) + "...") + "\"";

    private void Beep(double f, int ms) => Tone.Play(f, ms, _cfg.Beep ? _cfg.BeepVolume : 0);

    private void Warn(string msg)
    {
        try { _icon.ShowBalloonTip(5000, "Voice Push-to-Talk", msg, ToolTipIcon.Warning); } catch { }
    }

    private void UpdateTooltip(bool recording = false)
    {
        var state = recording ? "Aufnahme laeuft..." : _busy ? "Wird uebertragen..." : HotkeyNames.Display(_cfg.Hotkey) + " halten zum Sprechen";
        var text = "Voice Push-to-Talk - " + state;
        _icon.Text = text.Length > 63 ? text.Substring(0, 60) + "..." : text;
    }

    // ---------------- Menue ----------------

    private void BuildMenu()
    {
        _menu.Items.Clear();

        var head = new ToolStripMenuItem(HotkeyNames.Display(_cfg.Hotkey) + " halten zum Sprechen") { Enabled = false };
        _menu.Items.Add(head);
        _menu.Items.Add(new ToolStripSeparator());

        var web = new ToolStripMenuItem("Weboberflaeche oeffnen", null, (_, _) => OpenWebUi());
        web.Font = new Font(web.Font, FontStyle.Bold);
        _menu.Items.Add(web);

        _menu.Items.Add(MicMenu());
        _menu.Items.Add(HotkeyMenu());
        _menu.Items.Add(VolumeMenu());

        var correct = new ToolStripMenuItem("LLM-Korrektur", null, (_, _) =>
        {
            _cfg.Correct = !_cfg.Correct;
            _cfg.Save();
        })
        {
            Checked = _cfg.Correct,
            CheckOnClick = false,
            Enabled = !string.Equals(_cfg.EndpointType, "openai", StringComparison.OrdinalIgnoreCase),
            ToolTipText = "Nur beim Endpunkt-Typ 'gateway' verfuegbar.",
        };
        _menu.Items.Add(correct);

        var passthrough = new ToolStripMenuItem("Taste an aktives Fenster weitergeben", null, (_, _) =>
        {
            _cfg.SuppressHotkey = !_cfg.SuppressHotkey;
            _hook.Suppress = _cfg.SuppressHotkey;
            _cfg.Save();
        })
        { Checked = !_cfg.SuppressHotkey };
        _menu.Items.Add(passthrough);

        var auto = new ToolStripMenuItem("Automatisch mit Windows starten", null, (_, _) =>
            Autostart.Set(!Autostart.IsEnabled()))
        { Checked = Autostart.IsEnabled() };
        _menu.Items.Add(auto);

        _menu.Items.Add(new ToolStripSeparator());
        _menu.Items.Add(new ToolStripMenuItem("Einstellungen...", null, (_, _) => ShowSettings()));
        _menu.Items.Add(new ToolStripMenuItem("Konfigurationsdatei oeffnen", null,
            (_, _) => Paths.OpenInShell(Paths.ConfigPath)));
        _menu.Items.Add(new ToolStripMenuItem("Protokoll oeffnen", null, (_, _) =>
        {
            if (!File.Exists(Paths.LogPath)) Log.Write("Protokoll geoeffnet.");
            Paths.OpenInShell(Paths.LogPath);
        }));
        _menu.Items.Add(new ToolStripSeparator());
        _menu.Items.Add(new ToolStripMenuItem("Beenden", null, (_, _) => ExitApp()));
    }

    private ToolStripMenuItem MicMenu()
    {
        var root = new ToolStripMenuItem("Mikrofon");
        root.DropDownItems.Add(new ToolStripMenuItem("Standard (automatisch)", null, (_, _) => SetMic(null))
        { Checked = string.IsNullOrWhiteSpace(_cfg.InputDevice) });

        var devices = Recorder.Devices();
        if (devices.Count > 0) root.DropDownItems.Add(new ToolStripSeparator());
        foreach (var (_, name) in devices)
        {
            var n = name;
            root.DropDownItems.Add(new ToolStripMenuItem(n, null, (_, _) => SetMic(n))
            { Checked = string.Equals(_cfg.InputDevice, n, StringComparison.OrdinalIgnoreCase) });
        }
        if (devices.Count == 0)
            root.DropDownItems.Add(new ToolStripMenuItem("Kein Mikrofon gefunden") { Enabled = false });
        return root;
    }

    private void SetMic(string name)
    {
        _cfg.InputDevice = name;
        _cfg.Save();
        Log.Write("Mikrofon gewaehlt:", name ?? "Standard");
    }

    private ToolStripMenuItem HotkeyMenu()
    {
        var root = new ToolStripMenuItem("Push-to-Talk-Taste");
        foreach (var key in HotkeyNames.Selectable)
        {
            var k = key;
            root.DropDownItems.Add(new ToolStripMenuItem(HotkeyNames.Display(k), null, (_, _) => SetHotkey(k))
            { Checked = string.Equals(_cfg.Hotkey, k, StringComparison.OrdinalIgnoreCase) });
        }
        return root;
    }

    private void SetHotkey(string key)
    {
        _cfg.Hotkey = key;
        _hook.SetKey(key);
        _cfg.Save();
        UpdateTooltip();
        Log.Write("Taste gewaehlt:", key);
    }

    private ToolStripMenuItem VolumeMenu()
    {
        var root = new ToolStripMenuItem("Ton-Lautstaerke");
        (string Label, double Value)[] levels =
        {
            ("Aus", 0.0), ("Sehr leise", 0.03), ("Leise", 0.08),
            ("Mittel", 0.2), ("Lauter", 0.5), ("Voll", 1.0),
        };
        foreach (var (label, value) in levels)
        {
            var v = value;
            root.DropDownItems.Add(new ToolStripMenuItem(label, null, (_, _) =>
            {
                _cfg.BeepVolume = v;
                _cfg.Beep = v > 0;
                _cfg.Save();
                Tone.Play(700, 60, v);
            })
            { Checked = Math.Abs((_cfg.Beep ? _cfg.BeepVolume : 0) - v) < 0.001 });
        }
        return root;
    }

    private void OpenWebUi()
    {
        var url = (_cfg.GatewayUrl ?? "").Trim();
        if (url.Length == 0) return;
        try { Process.Start(new ProcessStartInfo(url) { UseShellExecute = true }); }
        catch (Exception ex) { Log.Write("Weboberflaeche oeffnen fehlgeschlagen:", ex.Message); }
    }

    private void ShowSettings()
    {
        using var f = new SettingsForm(_cfg);
        if (f.ShowDialog() != DialogResult.OK) return;
        _cfg.Sanitized().Save();
        _hook.SetKey(_cfg.Hotkey);
        _hook.Suppress = _cfg.SuppressHotkey;
        UpdateTooltip();
        Log.Write("Einstellungen gespeichert.");
    }

    private void ExitApp()
    {
        Log.Write("Beendet ueber Tray-Menue.");
        ExitThread();
    }

    protected override void ExitThreadCore()
    {
        try { _icon.Visible = false; } catch { }
        _quitWait?.Unregister(null);
        _quitEvent?.Dispose();
        _hook.Dispose();
        _rec.Dispose();
        _icon.Dispose();
        _menu.Dispose();
        _sync.Dispose();
        base.ExitThreadCore();
    }
}
