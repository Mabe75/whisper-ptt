using System.Diagnostics;
using System.Reflection;

namespace VoicePTT;

/// <summary>Tray-Symbol, Menü und der eigentliche Push-to-Talk-Ablauf.</summary>
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
    private Profile _running;
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
        _hook.Suppress = _cfg.SuppressHotkey;
        _hook.SetProfiles(_cfg.Profiles);
        _hook.Pressed += OnPressed;
        _hook.Released += OnReleased;
        _hook.Install();

        RegisterQuitSignal();

        UpdateTooltip();
        Log.Write("Bereit. Gateway=" + _cfg.GatewayUrl, "Modus=" + _cfg.EndpointType,
                  "LLM=" + (_cfg.LlmConfigured ? _cfg.LlmUrl + " (" + _cfg.LlmModel + ")" : "nicht konfiguriert"));
    }

    /// <summary>Setup und Deinstallation können die laufende Instanz so sauber beenden.</summary>
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
        catch (Exception ex) { Log.Write("Quit-Signal nicht verfügbar:", ex.Message); }
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

    private void OnPressed(Profile profile)
    {
        if (_rec.IsRecording) return;
        try
        {
            _rec.Start(_cfg);
            _running = profile;
            Beep(profile.BeepHz, 55);
            UpdateTooltip();
        }
        catch (Exception ex)
        {
            Log.Write("Start fehlgeschlagen:", ex.Message);
            Beep(300, 200);
            Warn("Aufnahme nicht möglich: " + ex.Message);
        }
    }

    private async void OnReleased(Profile profile)
    {
        if (!_rec.IsRecording) return;
        Beep(520, 55);
        _running = null;
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
        if (seconds < _cfg.MinSeconds)
        {
            Log.Write(profile.ToString(), "- zu kurz (" + seconds.ToString("0.00") + " s), verworfen.");
            Beep(300, 150);
            return;
        }

        _busy = true;
        UpdateTooltip();
        var watch = Stopwatch.StartNew();
        try
        {
            // Korrigiert der Client selbst, holt er beim Gateway bewusst den Rohtext.
            // Ohne konfiguriertes LLM übernimmt das Gateway die Korrektur wie bisher.
            var llm = profile.UsesLlm && _cfg.LlmConfigured;
            var gatewayCorrection = profile.UsesGatewayCorrection || (profile.UsesLlm && !_cfg.LlmConfigured);

            var text = await Transcriber.TranscribeAsync(wav, _cfg, gatewayCorrection);
            var whisperMs = watch.ElapsedMilliseconds;
            if (string.IsNullOrWhiteSpace(text))
            {
                Log.Write(profile.ToString(), "- leeres Ergebnis nach", whisperMs + " ms");
                Beep(300, 150);
                return;
            }

            long llmMs = 0;
            if (llm)
            {
                var t0 = watch.ElapsedMilliseconds;
                try
                {
                    text = await LlmClient.ProcessAsync(text, profile, _cfg);
                }
                catch (Exception ex)
                {
                    var msg = ex.GetBaseException().Message;
                    Log.Write(profile.ToString(), "- Nachbearbeitung fehlgeschlagen:", msg);
                    Warn("Nachbearbeitung fehlgeschlagen, Rohtext eingefügt: " + msg);
                }
                llmMs = watch.ElapsedMilliseconds - t0;
            }

            if (profile.Replacements) text = Vocabulary.ApplyReplacements(text, _cfg);

            if (string.IsNullOrWhiteSpace(text))
            {
                Beep(300, 150);
                return;
            }

            Log.Write(profile.ToString(), "- Audio " + seconds.ToString("0.0") + " s,",
                      "Whisper " + whisperMs + " ms,",
                      llm ? "LLM " + llmMs + " ms," : "ohne LLM,",
                      "eingefügt:", Quote(text));
            TextInserter.Insert(text, _cfg);
            Beep(780, 55);
        }
        catch (Exception ex)
        {
            var msg = ex.GetBaseException().Message;
            Log.Write(profile.ToString(), "- Fehler:", msg);
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

    private void UpdateTooltip()
    {
        string state;
        if (_running != null) state = "Aufnahme: " + _running.Name;
        else if (_busy) state = "Wird verarbeitet...";
        else
        {
            var first = _cfg.Profiles.FirstOrDefault(p => p.Active);
            state = first == null ? "keine Taste aktiv" : HotkeyNames.Display(first.Hotkey) + " halten zum Sprechen";
        }

        var text = "Voice Push-to-Talk - " + state;
        _icon.Text = text.Length > 63 ? text.Substring(0, 60) + "..." : text;
    }

    // ---------------- Menü ----------------

    private void BuildMenu()
    {
        _menu.Items.Clear();

        var any = false;
        foreach (var p in _cfg.Profiles.Where(p => p.Active))
        {
            var hint = p.UsesLlm ? (_cfg.LlmConfigured ? p.Correction : "ohne LLM-Adresse: Gateway") : p.Correction;
            _menu.Items.Add(new ToolStripMenuItem(p + "   (" + hint + ")") { Enabled = false });
            any = true;
        }
        if (!any) _menu.Items.Add(new ToolStripMenuItem("Kein Profil aktiv") { Enabled = false });
        _menu.Items.Add(new ToolStripSeparator());

        var web = new ToolStripMenuItem("Weboberfläche öffnen", null, (_, _) => OpenWebUi());
        web.Font = new Font(web.Font, FontStyle.Bold);
        _menu.Items.Add(web);

        _menu.Items.Add(MicMenu());
        _menu.Items.Add(VolumeMenu());

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
        _menu.Items.Add(new ToolStripMenuItem("Konfigurationsdatei öffnen", null,
            (_, _) => Paths.OpenInShell(Paths.ConfigPath)));
        _menu.Items.Add(new ToolStripMenuItem("Protokoll öffnen", null, (_, _) =>
        {
            if (!File.Exists(Paths.LogPath)) Log.Write("Protokoll geöffnet.");
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
        Log.Write("Mikrofon gewählt:", name ?? "Standard");
    }

    private ToolStripMenuItem VolumeMenu()
    {
        var root = new ToolStripMenuItem("Ton-Lautstärke");
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
        catch (Exception ex) { Log.Write("Weboberfläche öffnen fehlgeschlagen:", ex.Message); }
    }

    private void ShowSettings()
    {
        using var f = new SettingsForm(_cfg);
        if (f.ShowDialog() != DialogResult.OK) return;
        _cfg.Sanitized().Save();
        _hook.Suppress = _cfg.SuppressHotkey;
        _hook.SetProfiles(_cfg.Profiles);
        UpdateTooltip();
        Log.Write("Einstellungen gespeichert.");
    }

    private void ExitApp()
    {
        Log.Write("Beendet über Tray-Menü.");
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
