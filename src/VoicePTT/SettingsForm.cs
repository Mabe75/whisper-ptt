namespace VoicePTT;

/// <summary>Kleiner Einstellungsdialog - deckt alles ab, wofuer man frueher config.json bearbeiten musste.</summary>
public sealed class SettingsForm : Form
{
    private readonly AppConfig _cfg;

    private readonly TextBox _url = new() { Width = 320 };
    private readonly ComboBox _endpoint = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 320 };
    private readonly TextBox _model = new() { Width = 320 };
    private readonly TextBox _language = new() { Width = 320 };
    private readonly ComboBox _hotkey = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 320 };
    private readonly ComboBox _mic = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 320 };
    private readonly ComboBox _insert = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 320 };
    private readonly ComboBox _rate = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 320 };
    private readonly TrackBar _volume = new() { Minimum = 0, Maximum = 100, TickFrequency = 10, Width = 260 };
    private readonly Label _volumeLabel = new() { AutoSize = true, Text = "8 %" };
    private readonly CheckBox _correct = new() { Text = "LLM-Korrektur verwenden (nur Endpunkt gateway)", AutoSize = true };
    private readonly CheckBox _restore = new() { Text = "Zwischenablage nach dem Einfuegen wiederherstellen", AutoSize = true };
    private readonly CheckBox _passthrough = new() { Text = "Taste zusaetzlich an das aktive Fenster weitergeben", AutoSize = true };
    private readonly CheckBox _autostart = new() { Text = "Automatisch mit Windows starten", AutoSize = true };
    private readonly Label _status = new() { AutoSize = true, ForeColor = SystemColors.GrayText, Text = " " };

    public SettingsForm(AppConfig cfg)
    {
        _cfg = cfg;
        Text = "Voice Push-to-Talk - Einstellungen";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        AutoScaleMode = AutoScaleMode.Dpi;
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;
        Padding = new Padding(12);
        try
        {
            using var s = typeof(SettingsForm).Assembly.GetManifestResourceStream("VoicePTT.voiceptt.ico");
            if (s != null) Icon = new Icon(s);
        }
        catch
        {
            // Ohne Symbol laeuft der Dialog genauso.
        }

        var grid = new TableLayoutPanel
        {
            ColumnCount = 2,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Dock = DockStyle.Top,
        };
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

        var test = new Button { Text = "Verbindung testen", AutoSize = true };
        test.Click += async (_, _) =>
        {
            test.Enabled = false;
            _status.Text = "Pruefe " + _url.Text.Trim() + " ...";
            _status.Text = await Transcriber.PingAsync(new AppConfig { GatewayUrl = _url.Text.Trim() });
            test.Enabled = true;
        };

        var urlRow = new FlowLayoutPanel { AutoSize = true, WrapContents = false, Margin = new Padding(0) };
        urlRow.Controls.Add(_url);
        urlRow.Controls.Add(test);

        AddRow(grid, "Gateway-URL", urlRow);
        AddRow(grid, "Endpunkt-Typ", _endpoint);
        AddRow(grid, "Modell (nur openai)", _model);
        AddRow(grid, "Sprache (leer = automatisch)", _language);
        AddRow(grid, "Push-to-Talk-Taste", _hotkey);
        AddRow(grid, "Mikrofon", _mic);
        AddRow(grid, "Text einfuegen per", _insert);
        AddRow(grid, "Abtastrate", _rate);

        var volRow = new FlowLayoutPanel { AutoSize = true, WrapContents = false, Margin = new Padding(0) };
        volRow.Controls.Add(_volume);
        volRow.Controls.Add(_volumeLabel);
        AddRow(grid, "Signalton-Lautstaerke", volRow);

        var checks = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.TopDown,
            AutoSize = true,
            WrapContents = false,
            Margin = new Padding(0, 8, 0, 0),
        };
        checks.Controls.AddRange(new Control[] { _correct, _restore, _passthrough, _autostart });

        var ok = new Button { Text = "Speichern", DialogResult = DialogResult.OK, AutoSize = true };
        var cancel = new Button { Text = "Abbrechen", DialogResult = DialogResult.Cancel, AutoSize = true };
        var buttons = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.RightToLeft,
            Dock = DockStyle.Top,
            AutoSize = true,
            Margin = new Padding(0, 10, 0, 0),
        };
        buttons.Controls.Add(cancel);
        buttons.Controls.Add(ok);
        AcceptButton = ok;
        CancelButton = cancel;
        ok.Click += OnSave;

        var root = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.TopDown,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            WrapContents = false,
            Dock = DockStyle.Fill,
        };
        root.Controls.Add(grid);
        root.Controls.Add(checks);
        root.Controls.Add(_status);
        root.Controls.Add(buttons);
        Controls.Add(root);

        _volume.ValueChanged += (_, _) => _volumeLabel.Text = _volume.Value + " %";
        _endpoint.SelectedIndexChanged += (_, _) => UpdateEnabledState();

        Fill();
    }

    private static void AddRow(TableLayoutPanel grid, string caption, Control field)
    {
        grid.Controls.Add(new Label
        {
            Text = caption,
            AutoSize = true,
            Anchor = AnchorStyles.Left,
            Margin = new Padding(0, 6, 10, 6),
        });
        grid.Controls.Add(field);
    }

    private void Fill()
    {
        _url.Text = _cfg.GatewayUrl;

        _endpoint.Items.AddRange(new object[]
        {
            "gateway (Spark-Dienst mit LLM-Korrektur)",
            "openai (lokaler Whisper-Server)",
        });
        _endpoint.SelectedIndex = string.Equals(_cfg.EndpointType, "openai", StringComparison.OrdinalIgnoreCase) ? 1 : 0;

        _model.Text = _cfg.Model;
        _language.Text = _cfg.Language;

        foreach (var k in HotkeyNames.Selectable) _hotkey.Items.Add(HotkeyNames.Display(k));
        var cur = HotkeyNames.Display(_cfg.Hotkey);
        if (!_hotkey.Items.Contains(cur)) _hotkey.Items.Add(cur);
        _hotkey.SelectedItem = cur;

        _mic.Items.Add("Standard (automatisch)");
        foreach (var d in Recorder.Devices()) _mic.Items.Add(d.Name);
        _mic.SelectedIndex = 0;
        if (!string.IsNullOrWhiteSpace(_cfg.InputDevice))
        {
            var i = _mic.Items.IndexOf(_cfg.InputDevice);
            if (i < 0)
            {
                _mic.Items.Add(_cfg.InputDevice);
                i = _mic.Items.Count - 1;
            }
            _mic.SelectedIndex = i;
        }

        _insert.Items.AddRange(new object[] { "Einfuegen (Strg+V)", "Zeichenweise tippen" });
        _insert.SelectedIndex = string.Equals(_cfg.InsertMode, "type", StringComparison.OrdinalIgnoreCase) ? 1 : 0;

        _rate.Items.AddRange(new object[] { "16000 Hz (empfohlen)", "22050 Hz", "44100 Hz", "48000 Hz" });
        _rate.SelectedIndex = _cfg.SampleRate switch { 22050 => 1, 44100 => 2, 48000 => 3, _ => 0 };

        _volume.Value = (int)Math.Round(Math.Clamp(_cfg.Beep ? _cfg.BeepVolume : 0, 0, 1) * 100);
        _volumeLabel.Text = _volume.Value + " %";

        _correct.Checked = _cfg.Correct;
        _restore.Checked = _cfg.RestoreClipboard;
        _passthrough.Checked = !_cfg.SuppressHotkey;
        _autostart.Checked = Autostart.IsEnabled();

        UpdateEnabledState();
    }

    private void UpdateEnabledState()
    {
        var openAi = _endpoint.SelectedIndex == 1;
        _model.Enabled = openAi;
        _correct.Enabled = !openAi;
    }

    private void OnSave(object sender, EventArgs e)
    {
        var url = _url.Text.Trim();
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || (uri.Scheme != "http" && uri.Scheme != "https"))
        {
            MessageBox.Show(this,
                "Bitte eine vollstaendige Adresse angeben, z. B. http://127.0.0.1:8055",
                "Ungueltige Gateway-URL", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            DialogResult = DialogResult.None;
            return;
        }

        _cfg.GatewayUrl = url.TrimEnd('/');
        _cfg.EndpointType = _endpoint.SelectedIndex == 1 ? "openai" : "gateway";
        _cfg.Model = _model.Text.Trim();
        _cfg.Language = _language.Text.Trim();
        _cfg.Hotkey = HotkeyNames.Selectable.FirstOrDefault(
            k => HotkeyNames.Display(k) == (string)_hotkey.SelectedItem) ?? _cfg.Hotkey;
        _cfg.InputDevice = _mic.SelectedIndex <= 0 ? null : (string)_mic.SelectedItem;
        _cfg.InsertMode = _insert.SelectedIndex == 1 ? "type" : "paste";
        _cfg.SampleRate = _rate.SelectedIndex switch { 1 => 22050, 2 => 44100, 3 => 48000, _ => 16000 };
        _cfg.BeepVolume = _volume.Value / 100.0;
        _cfg.Beep = _volume.Value > 0;
        _cfg.Correct = _correct.Checked;
        _cfg.RestoreClipboard = _restore.Checked;
        _cfg.SuppressHotkey = !_passthrough.Checked;

        if (_autostart.Checked != Autostart.IsEnabled()) Autostart.Set(_autostart.Checked);
    }
}
