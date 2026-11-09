namespace VoicePTT;

/// <summary>Einstellungen in vier Registerkarten: Verbindung, Profile, Vokabular, Allgemein.</summary>
public sealed class SettingsForm : Form
{
    private sealed class Choice
    {
        public string Key { get; init; } = "";
        public string Label { get; init; } = "";
        public override string ToString() => Label;
    }

    private readonly AppConfig _cfg;
    private readonly List<Profile> _profiles;

    // Verbindung
    private readonly TextBox _url = new() { Width = 300 };
    private readonly ComboBox _endpoint = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 300 };
    private readonly TextBox _model = new() { Width = 300 };
    private readonly TextBox _language = new() { Width = 300 };
    private readonly CheckBox _whisperVocab = new() { Text = "Begriffe als Vorgabe an Whisper senden (nur Endpunkt openai)", AutoSize = true };
    private readonly TextBox _llmUrl = new() { Width = 300 };
    private readonly ComboBox _llmModel = new() { DropDownStyle = ComboBoxStyle.DropDown, Width = 300 };
    private readonly NumericUpDown _llmTemp = new() { DecimalPlaces = 2, Increment = 0.05M, Minimum = 0, Maximum = 2, Width = 80 };
    private readonly NumericUpDown _llmTimeout = new() { Minimum = 5, Maximum = 600, Width = 80 };
    private readonly Label _status = new() { AutoSize = false, Width = 560, Height = 34, ForeColor = SystemColors.GrayText };

    // Profile
    private readonly DataGridView _grid = new();
    private readonly TextBox _instruction = new() { Multiline = true, ScrollBars = ScrollBars.Vertical, Dock = DockStyle.Fill };
    private int _instructionRow = -1;

    // Vokabular
    private readonly DataGridView _vocab = new();

    // Allgemein
    private readonly ComboBox _mic = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 300 };
    private readonly ComboBox _insert = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 300 };
    private readonly ComboBox _rate = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 300 };
    private readonly TrackBar _volume = new() { Minimum = 0, Maximum = 100, TickFrequency = 10, Width = 240 };
    private readonly Label _volumeLabel = new() { AutoSize = true, Text = "8 %" };
    private readonly CheckBox _restore = new() { Text = "Zwischenablage nach dem Einfügen wiederherstellen", AutoSize = true };
    private readonly CheckBox _passthrough = new() { Text = "Taste zusätzlich an das aktive Fenster weitergeben", AutoSize = true };
    private readonly CheckBox _autostart = new() { Text = "Automatisch mit Windows starten", AutoSize = true };

    public SettingsForm(AppConfig cfg)
    {
        _cfg = cfg;
        _profiles = (cfg.Profiles ?? new List<Profile>()).Select(Clone).ToList();

        Text = "Voice Push-to-Talk - Einstellungen";
        StartPosition = FormStartPosition.CenterScreen;
        AutoScaleMode = AutoScaleMode.Dpi;
        MinimizeBox = false;
        MaximizeBox = false;
        Size = new Size(840, 660);
        MinimumSize = new Size(760, 560);
        try
        {
            using var s = typeof(SettingsForm).Assembly.GetManifestResourceStream("VoicePTT.voiceptt.ico");
            if (s != null) Icon = new Icon(s);
        }
        catch
        {
            // Ohne Symbol läuft der Dialog genauso.
        }

        var tabs = new TabControl { Dock = DockStyle.Fill };
        tabs.TabPages.Add(ConnectionTab());
        tabs.TabPages.Add(ProfilesTab());
        tabs.TabPages.Add(VocabularyTab());
        tabs.TabPages.Add(GeneralTab());

        var ok = new Button { Text = "Speichern", DialogResult = DialogResult.OK, AutoSize = true };
        var cancel = new Button { Text = "Abbrechen", DialogResult = DialogResult.Cancel, AutoSize = true };
        ok.Click += OnSave;
        AcceptButton = ok;
        CancelButton = cancel;

        var buttons = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.RightToLeft,
            Dock = DockStyle.Bottom,
            AutoSize = true,
            Padding = new Padding(10, 6, 10, 6),
        };
        buttons.Controls.Add(cancel);
        buttons.Controls.Add(ok);

        Controls.Add(tabs);
        Controls.Add(buttons);

        Fill();
    }

    private static Profile Clone(Profile p) => new()
    {
        Hotkey = p.Hotkey,
        Name = p.Name,
        Correction = p.Correction,
        Instruction = p.Instruction,
        Thinking = p.Thinking,
        Replacements = p.Replacements,
        BeepHz = p.BeepHz,
        Active = p.Active,
    };

    // ---------------- Registerkarten ----------------

    private TabPage ConnectionTab()
    {
        var page = new TabPage("Verbindung") { Padding = new Padding(12), AutoScroll = true };

        var grid = new TableLayoutPanel { ColumnCount = 2, AutoSize = true, Dock = DockStyle.Top };
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

        var testGateway = new Button { Text = "Testen", AutoSize = true };
        testGateway.Click += async (_, _) =>
        {
            testGateway.Enabled = false;
            _status.Text = "Prüfe " + _url.Text.Trim() + " ...";
            _status.Text = await Transcriber.PingAsync(new AppConfig { GatewayUrl = _url.Text.Trim() });
            testGateway.Enabled = true;
        };

        var testLlm = new Button { Text = "Testen", AutoSize = true };
        testLlm.Click += async (_, _) =>
        {
            testLlm.Enabled = false;
            _status.Text = "Prüfe " + _llmUrl.Text.Trim() + " ...";
            _status.Text = await LlmClient.PingAsync(_llmUrl.Text.Trim());
            var models = await LlmClient.ListModelsAsync(_llmUrl.Text.Trim());
            if (models.Count > 0)
            {
                var keep = _llmModel.Text;
                _llmModel.Items.Clear();
                foreach (var m in models) _llmModel.Items.Add(m);
                _llmModel.Text = string.IsNullOrWhiteSpace(keep) ? models[0] : keep;
            }
            testLlm.Enabled = true;
        };

        AddRow(grid, "Gateway-URL", WithButton(_url, testGateway));
        AddRow(grid, "Endpunkt-Typ", _endpoint);
        AddRow(grid, "Whisper-Modell (nur openai)", _model);
        AddRow(grid, "Sprache (leer = automatisch)", _language);
        AddRow(grid, "", _whisperVocab);
        AddRow(grid, " ", new Label { Text = "", AutoSize = true });
        AddRow(grid, "LLM-Adresse (Nachbearbeitung)", WithButton(_llmUrl, testLlm));
        AddRow(grid, "LLM-Modell", _llmModel);
        AddRow(grid, "Temperatur", _llmTemp);
        AddRow(grid, "Zeitlimit (Sekunden)", _llmTimeout);

        var hint = new Label
        {
            AutoSize = false,
            Height = 48,
            Dock = DockStyle.Top,
            ForeColor = SystemColors.GrayText,
            Text = "Ohne LLM-Adresse verhält sich das Programm wie bisher: Profile mit Korrektur \"llm\" lassen " +
                   "dann das Gateway korrigieren. Antwortet das LLM nicht, wird der unveränderte Rohtext eingefügt.",
        };

        var panel = new Panel { Dock = DockStyle.Fill, AutoScroll = true };
        _status.Dock = DockStyle.Top;
        panel.Controls.Add(_status);
        panel.Controls.Add(hint);
        panel.Controls.Add(grid);
        page.Controls.Add(panel);
        return page;
    }

    private TabPage ProfilesTab()
    {
        var page = new TabPage("Profile") { Padding = new Padding(12) };

        _grid.Dock = DockStyle.Fill;
        _grid.AllowUserToAddRows = true;
        _grid.AllowUserToResizeRows = false;
        _grid.RowHeadersVisible = false;
        _grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        _grid.EditMode = DataGridViewEditMode.EditOnEnter;
        _grid.DataError += (_, e) => e.ThrowException = false;

        var keys = new DataGridViewComboBoxColumn
        {
            HeaderText = "Taste",
            Name = "key",
            DataSource = HotkeyNames.Selectable.Select(k => new Choice { Key = k, Label = HotkeyNames.Display(k) }).ToList(),
            DisplayMember = nameof(Choice.Label),
            ValueMember = nameof(Choice.Key),
            FillWeight = 70,
        };
        var corr = new DataGridViewComboBoxColumn
        {
            HeaderText = "Korrektur",
            Name = "corr",
            DataSource = new List<Choice>
            {
                new() { Key = "llm", Label = "LLM im Client" },
                new() { Key = "gateway", Label = "Gateway" },
                new() { Key = "none", Label = "keine" },
            },
            DisplayMember = nameof(Choice.Label),
            ValueMember = nameof(Choice.Key),
            FillWeight = 90,
        };

        _grid.Columns.Add(keys);
        _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Name", Name = "name", FillWeight = 110 });
        _grid.Columns.Add(corr);
        _grid.Columns.Add(new DataGridViewCheckBoxColumn { HeaderText = "Denken", Name = "think", FillWeight = 60 });
        _grid.Columns.Add(new DataGridViewCheckBoxColumn { HeaderText = "Ersetzen", Name = "repl", FillWeight = 60 });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Ton (Hz)", Name = "hz", FillWeight = 60 });
        _grid.Columns.Add(new DataGridViewCheckBoxColumn { HeaderText = "Aktiv", Name = "on", FillWeight = 50 });

        _grid.SelectionChanged += (_, _) => SwitchInstruction();

        var remove = new Button { Text = "Profil entfernen", AutoSize = true };
        remove.Click += (_, _) =>
        {
            if (_grid.CurrentRow == null || _grid.CurrentRow.IsNewRow) return;
            _instructionRow = -1;
            _grid.Rows.Remove(_grid.CurrentRow);
        };
        var reset = new Button { Text = "Standardprofile wiederherstellen", AutoSize = true };
        reset.Click += (_, _) =>
        {
            if (MessageBox.Show(this, "Alle Profile samt Anweisungen durch die Vorgaben ersetzen?",
                    "Zurücksetzen", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
            _profiles.Clear();
            _profiles.AddRange(Profile.Defaults());
            FillProfiles();
        };

        var tools = new FlowLayoutPanel { Dock = DockStyle.Bottom, AutoSize = true };
        tools.Controls.Add(remove);
        tools.Controls.Add(reset);

        var instrBox = new GroupBox
        {
            Text = "Anweisung an das LLM für das gewählte Profil",
            Dock = DockStyle.Bottom,
            Height = 220,
            Padding = new Padding(8),
        };
        instrBox.Controls.Add(_instruction);

        var hint = new Label
        {
            Dock = DockStyle.Bottom,
            AutoSize = false,
            Height = 34,
            ForeColor = SystemColors.GrayText,
            Text = "Neue Zeile in der Tabelle = neues Profil. \"Denken\" macht Qwen-Modelle deutlich langsamer, " +
                   "liefert bei freien Formulierungen aber oft bessere Ergebnisse.",
        };

        page.Controls.Add(_grid);
        page.Controls.Add(hint);
        page.Controls.Add(instrBox);
        page.Controls.Add(tools);
        return page;
    }

    private TabPage VocabularyTab()
    {
        var page = new TabPage("Vokabular") { Padding = new Padding(12) };

        _vocab.Dock = DockStyle.Fill;
        _vocab.AllowUserToAddRows = true;
        _vocab.RowHeadersVisible = false;
        _vocab.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        _vocab.EditMode = DataGridViewEditMode.EditOnEnter;
        _vocab.DataError += (_, e) => e.ThrowException = false;
        _vocab.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Begriff", Name = "term", FillWeight = 70 });
        _vocab.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Varianten (werden ersetzt)", Name = "var", FillWeight = 120 });
        _vocab.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Verwechslungen (nur Hinweis)", Name = "conf", FillWeight = 120 });
        _vocab.Columns.Add(new DataGridViewCheckBoxColumn { HeaderText = "Aktiv", Name = "on", FillWeight = 40 });

        var remove = new Button { Text = "Zeile entfernen", AutoSize = true, Dock = DockStyle.Bottom };
        remove.Click += (_, _) =>
        {
            if (_vocab.CurrentRow != null && !_vocab.CurrentRow.IsNewRow) _vocab.Rows.Remove(_vocab.CurrentRow);
        };

        var hint = new Label
        {
            Dock = DockStyle.Bottom,
            AutoSize = false,
            Height = 94,
            ForeColor = SystemColors.GrayText,
            Text = "Begriff: geht als Vorgabe an Whisper und als Hinweis an das LLM.\r\n" +
                   "Varianten: werden im fertigen Text hart ersetzt, ohne Rücksicht auf den Zusammenhang. Nur " +
                   "eintragen, wenn die Falschschreibung nie etwas anderes bedeuten kann (\"kwen\" ja, \"cloud\" nein).\r\n" +
                   "Verwechslungen: nennen dem LLM ein Wort als wahrscheinliche Fehlerkennung; entschieden wird " +
                   "am Satz. \"Ich frag mal cloud\" wird zu Claude, \"Daten in die cloud\" bleibt die Cloud.",
        };

        page.Controls.Add(_vocab);
        page.Controls.Add(hint);
        page.Controls.Add(remove);
        return page;
    }

    private TabPage GeneralTab()
    {
        var page = new TabPage("Allgemein") { Padding = new Padding(12), AutoScroll = true };

        var grid = new TableLayoutPanel { ColumnCount = 2, AutoSize = true, Dock = DockStyle.Top };
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

        var volRow = new FlowLayoutPanel { AutoSize = true, WrapContents = false, Margin = new Padding(0) };
        volRow.Controls.Add(_volume);
        volRow.Controls.Add(_volumeLabel);

        AddRow(grid, "Mikrofon", _mic);
        AddRow(grid, "Text einfügen per", _insert);
        AddRow(grid, "Abtastrate", _rate);
        AddRow(grid, "Signalton-Lautstärke", volRow);

        var checks = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.TopDown,
            AutoSize = true,
            WrapContents = false,
            Dock = DockStyle.Top,
            Padding = new Padding(0, 10, 0, 0),
        };
        checks.Controls.AddRange(new Control[] { _restore, _passthrough, _autostart });

        var panel = new Panel { Dock = DockStyle.Fill, AutoScroll = true };
        panel.Controls.Add(checks);
        panel.Controls.Add(grid);
        page.Controls.Add(panel);
        return page;
    }

    private static Control WithButton(Control field, Button button)
    {
        var row = new FlowLayoutPanel { AutoSize = true, WrapContents = false, Margin = new Padding(0) };
        row.Controls.Add(field);
        row.Controls.Add(button);
        return row;
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

    // ---------------- Werte laden ----------------

    private void Fill()
    {
        _url.Text = _cfg.GatewayUrl;
        _endpoint.Items.AddRange(new object[]
        {
            "gateway (eigener Dienst)",
            "openai (OpenAI-kompatibler Whisper-Server)",
        });
        _endpoint.SelectedIndex = string.Equals(_cfg.EndpointType, "openai", StringComparison.OrdinalIgnoreCase) ? 1 : 0;
        _model.Text = _cfg.Model;
        _language.Text = _cfg.Language;
        _whisperVocab.Checked = _cfg.SendVocabularyToWhisper;

        _llmUrl.Text = _cfg.LlmUrl;
        if (!string.IsNullOrWhiteSpace(_cfg.LlmModel)) _llmModel.Items.Add(_cfg.LlmModel);
        _llmModel.Text = _cfg.LlmModel;
        _llmTemp.Value = (decimal)Math.Clamp(_cfg.LlmTemperature, 0, 2);
        _llmTimeout.Value = Math.Clamp(_cfg.LlmTimeoutSeconds, 5, 600);

        FillProfiles();
        FillVocabulary();

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

        _insert.Items.AddRange(new object[] { "Einfügen (Strg+V)", "Zeichenweise tippen" });
        _insert.SelectedIndex = string.Equals(_cfg.InsertMode, "type", StringComparison.OrdinalIgnoreCase) ? 1 : 0;

        _rate.Items.AddRange(new object[] { "16000 Hz (empfohlen)", "22050 Hz", "44100 Hz", "48000 Hz" });
        _rate.SelectedIndex = _cfg.SampleRate switch { 22050 => 1, 44100 => 2, 48000 => 3, _ => 0 };

        _volume.Value = (int)Math.Round(Math.Clamp(_cfg.Beep ? _cfg.BeepVolume : 0, 0, 1) * 100);
        _volumeLabel.Text = _volume.Value + " %";
        _volume.ValueChanged += (_, _) => _volumeLabel.Text = _volume.Value + " %";

        _restore.Checked = _cfg.RestoreClipboard;
        _passthrough.Checked = !_cfg.SuppressHotkey;
        _autostart.Checked = Autostart.IsEnabled();
    }

    private void FillProfiles()
    {
        _instructionRow = -1;
        _instruction.Text = "";
        _grid.Rows.Clear();
        foreach (var p in _profiles)
            _grid.Rows.Add(p.Hotkey, p.Name, p.Correction, p.Thinking, p.Replacements, p.BeepHz, p.Active);
        if (_grid.Rows.Count > 0) _grid.CurrentCell = _grid.Rows[0].Cells[1];
        SwitchInstruction();
    }

    private void FillVocabulary()
    {
        _vocab.Rows.Clear();
        foreach (var v in _cfg.Vocabulary ?? new List<VocabularyEntry>())
            _vocab.Rows.Add(v.Term, v.VariantsText, v.ConfusionsText, v.Active);
    }

    /// <summary>Anweisungstext gehört zur Zeile - beim Zeilenwechsel sichern und neu laden.</summary>
    private void SwitchInstruction()
    {
        var row = _grid.CurrentRow == null || _grid.CurrentRow.IsNewRow ? -1 : _grid.CurrentRow.Index;
        if (row == _instructionRow) return;

        if (_instructionRow >= 0 && _instructionRow < _profiles.Count)
            _profiles[_instructionRow].Instruction = _instruction.Text;

        _instructionRow = row;
        _instruction.Enabled = row >= 0;
        _instruction.Text = row >= 0 && row < _profiles.Count ? _profiles[row].Instruction : "";
    }

    // ---------------- Speichern ----------------

    private void OnSave(object sender, EventArgs e)
    {
        var url = _url.Text.Trim();
        if (!IsHttpUrl(url))
        {
            Warn("Bitte eine vollständige Gateway-Adresse angeben, z. B. http://192.168.178.34:8055", _url);
            return;
        }

        var llmUrl = _llmUrl.Text.Trim();
        if (llmUrl.Length > 0 && !IsHttpUrl(llmUrl))
        {
            Warn("Bitte eine vollständige LLM-Adresse angeben, z. B. http://192.168.178.34:8082/v1", _llmUrl);
            return;
        }

        SwitchInstruction();
        var profiles = ReadProfiles();
        if (profiles.Count == 0)
        {
            Warn("Mindestens ein Profil wird gebraucht.", _grid);
            return;
        }

        var doubled = profiles.Where(p => p.Active)
            .GroupBy(p => HotkeyNames.ToVirtualKey(p.Hotkey))
            .FirstOrDefault(g => g.Count() > 1);
        if (doubled != null)
        {
            Warn("Die Taste " + HotkeyNames.Display(doubled.First().Hotkey) +
                 " ist mehrfach belegt. Bitte eine andere wählen oder ein Profil abschalten.", _grid);
            return;
        }

        _cfg.GatewayUrl = url.TrimEnd('/');
        _cfg.EndpointType = _endpoint.SelectedIndex == 1 ? "openai" : "gateway";
        _cfg.Model = _model.Text.Trim();
        _cfg.Language = _language.Text.Trim();
        _cfg.SendVocabularyToWhisper = _whisperVocab.Checked;

        _cfg.LlmUrl = llmUrl.TrimEnd('/');
        _cfg.LlmModel = _llmModel.Text.Trim();
        _cfg.LlmTemperature = (double)_llmTemp.Value;
        _cfg.LlmTimeoutSeconds = (int)_llmTimeout.Value;

        _cfg.Profiles = profiles;
        _cfg.Vocabulary = ReadVocabulary();

        _cfg.InputDevice = _mic.SelectedIndex <= 0 ? null : (string)_mic.SelectedItem;
        _cfg.InsertMode = _insert.SelectedIndex == 1 ? "type" : "paste";
        _cfg.SampleRate = _rate.SelectedIndex switch { 1 => 22050, 2 => 44100, 3 => 48000, _ => 16000 };
        _cfg.BeepVolume = _volume.Value / 100.0;
        _cfg.Beep = _volume.Value > 0;
        _cfg.RestoreClipboard = _restore.Checked;
        _cfg.SuppressHotkey = !_passthrough.Checked;

        if (_autostart.Checked != Autostart.IsEnabled()) Autostart.Set(_autostart.Checked);
    }

    private List<Profile> ReadProfiles()
    {
        var list = new List<Profile>();
        for (var i = 0; i < _grid.Rows.Count; i++)
        {
            var row = _grid.Rows[i];
            if (row.IsNewRow) continue;

            var key = CellText(row, "key");
            if (string.IsNullOrWhiteSpace(key)) continue;

            var p = i < _profiles.Count ? _profiles[i] : new Profile();
            p.Hotkey = key;
            p.Name = CellText(row, "name");
            if (string.IsNullOrWhiteSpace(p.Name)) p.Name = HotkeyNames.Display(key);
            p.Correction = string.IsNullOrWhiteSpace(CellText(row, "corr")) ? "llm" : CellText(row, "corr");
            p.Thinking = Flag(row, "think");
            p.Replacements = Flag(row, "repl");
            p.BeepHz = int.TryParse(CellText(row, "hz"), out var hz) ? hz : 660;
            p.Active = Flag(row, "on");
            list.Add(p);
        }
        return list;
    }

    private List<VocabularyEntry> ReadVocabulary()
    {
        var list = new List<VocabularyEntry>();
        foreach (DataGridViewRow row in _vocab.Rows)
        {
            if (row.IsNewRow) continue;
            var term = CellText(row, "term").Trim();
            if (term.Length == 0) continue;
            list.Add(new VocabularyEntry
            {
                Term = term,
                VariantsText = CellText(row, "var"),
                ConfusionsText = CellText(row, "conf"),
                Active = Flag(row, "on"),
            });
        }
        return list;
    }

    private static string CellText(DataGridViewRow row, string column) => row.Cells[column].Value?.ToString() ?? "";

    private static bool Flag(DataGridViewRow row, string column) =>
        row.Cells[column].Value is bool b && b;

    private static bool IsHttpUrl(string s) =>
        Uri.TryCreate(s, UriKind.Absolute, out var u) && (u.Scheme == "http" || u.Scheme == "https");

    private void Warn(string message, Control focus)
    {
        MessageBox.Show(this, message, "Eingabe prüfen", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        DialogResult = DialogResult.None;
        focus.Focus();
    }
}
