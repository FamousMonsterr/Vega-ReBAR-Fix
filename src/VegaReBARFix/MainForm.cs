namespace VegaReBARFix;

using VegaReBARFix.Core;

/// <summary>
/// RDP_CnC-style dialog: a live diagnostics block (green/red status labels,
/// refreshed on a timer), action buttons and autostart checkboxes.
/// Reboot only ever happens on an explicit button press.
/// The window is resizable and DPI-scaled; captions auto-size so nothing clips.
/// UI language: English by default, Russian on Russian systems; switchable live.
/// </summary>
public sealed class MainForm : Form
{
    private readonly System.Windows.Forms.Timer _timer = new() { Interval = 2000 };

    private GroupBox _gbDiag = null!, _gbActions = null!, _gbAuto = null!;
    private Label _capPatch = null!, _capBar = null!, _capVbios = null!, _capDriver = null!, _capKey = null!;
    private Label _lblPatch = null!, _lblBar = null!, _lblVbios = null!, _lblDriver = null!, _lblKey = null!;
    private Label _lblFooter = null!, _lblLang = null!;
    private ComboBox _langCombo = null!;

    private Button _btnPatch = null!, _btnUndo = null!, _btnRefresh = null!, _btnReboot = null!;
    private CheckBox _chkAutostart = null!, _chkAutoPatch = null!;
    private TextBox _log = null!;

    private BarStatus _bar = new(0, 0, null);
    private int _barAgeSeconds = int.MaxValue;   // hardware check runs on demand + every 30 s
    private bool _busy;
    private bool _suppressAutoEvents;

    public MainForm(bool fromAutostart)
    {
        AutoScaleMode = AutoScaleMode.Dpi;
        AutoScaleDimensions = new SizeF(96F, 96F);
        FormBorderStyle = FormBorderStyle.Sizable;
        MaximizeBox = true;
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(780, 570);
        MinimumSize = new Size(700, 520);
        Font = new Font("Segoe UI", 9f);
        Text = L10n.T("Vega-ReBAR-Fix — ReBAR (SAM) for AMD Vega/Polaris",
                      "Vega-ReBAR-Fix — ReBAR (SAM) для AMD Vega/Polaris");

        _gbDiag = new GroupBox { Location = new Point(12, 12), Size = new Size(756, 160) };
        _lblPatch  = AddDiagRow(_gbDiag, 26, out _capPatch);
        _lblBar    = AddDiagRow(_gbDiag, 52, out _capBar);
        _lblVbios  = AddDiagRow(_gbDiag, 78, out _capVbios);
        _lblDriver = AddDiagRow(_gbDiag, 104, out _capDriver);
        _lblKey    = AddDiagRow(_gbDiag, 130, out _capKey);

        _gbActions = new GroupBox { Location = new Point(12, 180), Size = new Size(756, 64) };
        _btnPatch = new Button { Location = new Point(14, 24), Size = new Size(118, 30) };
        _btnUndo  = new Button { Location = new Point(140, 24), Size = new Size(98, 30) };
        _btnRefresh = new Button { Location = new Point(246, 24), Size = new Size(98, 30) };
        _btnReboot = new Button { Size = new Size(126, 30), Enabled = false };
        _btnPatch.Click += (_, _) => RunGuarded(() =>
        {
            var (ok, msg) = Patcher.Patch();
            Log(msg);
            if (ok) _btnReboot.Enabled = true;
        });
        _btnUndo.Click += (_, _) => RunGuarded(() =>
        {
            var (ok, msg) = Patcher.Undo();
            Log(msg);
            if (ok) _btnReboot.Enabled = true;
        });
        _btnRefresh.Click += (_, _) => { _barAgeSeconds = int.MaxValue; RefreshStatus(forceBar: true); Log(L10n.T("Status refreshed.", "Статус обновлён.")); };
        _btnReboot.Click += (_, _) =>
        {
            if (MessageBox.Show(this,
                    L10n.T("Reboot the computer now?\n\nThe registry is patched before the reboot; ReBAR takes effect after it.",
                           "Перезагрузить компьютер сейчас?\n\nРеестр патчится до перезагрузки; ReBAR заработает после неё."),
                    L10n.T("Reboot", "Перезагрузка"),
                    MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
            try
            {
                System.Diagnostics.Process.Start("shutdown.exe", "/r /t 5 /c \"Vega-ReBAR-Fix\"");
            }
            catch (Exception ex) { Log(L10n.T("Failed to start reboot: ", "Не удалось запустить перезагрузку: ") + ex.Message); }
        };
        _gbActions.Controls.AddRange(new Control[] { _btnPatch, _btnUndo, _btnRefresh, _btnReboot });

        _gbAuto = new GroupBox { Location = new Point(12, 252), Size = new Size(756, 84) };
        _chkAutostart = new CheckBox { Location = new Point(14, 24), AutoSize = true };
        _chkAutoPatch = new CheckBox
        {
            Location = new Point(14, 50),
            AutoSize = true,
            Checked = Autostart.AutoPatchEnabled
        };
        _chkAutostart.CheckedChanged += (_, _) =>
        {
            if (_suppressAutoEvents) return;
            RunGuarded(() =>
            {
                var (ok, msg) = _chkAutostart.Checked
                    ? Autostart.Enable(Environment.ProcessPath ?? Application.ExecutablePath)
                    : Autostart.Disable();
                Log(msg);
                if (!ok)
                {
                    _suppressAutoEvents = true;
                    _chkAutostart.Checked = false;   // revert on failure, without re-entering the handler
                    _suppressAutoEvents = false;
                }
            });
        };
        _chkAutoPatch.CheckedChanged += (_, _) => Autostart.AutoPatchEnabled = _chkAutoPatch.Checked;
        _gbAuto.Controls.AddRange(new Control[] { _chkAutostart, _chkAutoPatch });

        _log = new TextBox
        {
            Location = new Point(12, 344),
            Size = new Size(756, 190),
            Multiline = true,
            ReadOnly = true,
            ScrollBars = ScrollBars.Vertical,
            BackColor = SystemColors.Window
        };

        _lblFooter = new Label
        {
            AutoSize = false,
            Size = new Size(560, 18),
            AutoEllipsis = true,
            ForeColor = SystemColors.GrayText,
            TextAlign = ContentAlignment.MiddleLeft
        };
        _lblLang = new Label
        {
            AutoSize = true,
            ForeColor = SystemColors.GrayText
        };
        _langCombo = new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            Size = new Size(64, 23)
        };
        _langCombo.Items.AddRange(new object[] { "Eng", "Рус" });
        _langCombo.SelectedIndex = L10n.Lang == "ru" ? 1 : 0;
        _langCombo.SelectedIndexChanged += (_, _) =>
        {
            var lang = _langCombo.SelectedIndex == 1 ? "ru" : "en";
            if (lang == L10n.Lang) return;
            L10n.Set(lang);
            ApplyLanguage();
            Log(L10n.T("Language switched to English.", "Язык переключён на русский."));
        };

        Controls.AddRange(new Control[] { _gbDiag, _gbActions, _gbAuto, _log, _lblFooter, _lblLang, _langCombo });

        AcceptButton = _btnPatch;
        Resize += (_, _) => LayoutAll();

        _timer.Tick += (_, _) =>
        {
            _barAgeSeconds += 2;
            RefreshStatus(forceBar: _barAgeSeconds >= 30);
        };
        _timer.Start();

        Shown += (_, _) =>
        {
            _suppressAutoEvents = true;
            _chkAutostart.Checked = Autostart.TaskExists();  // reflect reality, do not recreate the job
            _suppressAutoEvents = false;
            RefreshStatus(forceBar: true);
            var adapters = AdapterLocator.FindAllAmdAdapters();
            if (adapters.Count > 1)
                Log(L10n.T(
                    $"Found {adapters.Count} AMD adapters; patching '{AdapterLocator.LocateBest()?.DriverDesc}' (the one with the largest VRAM).",
                    $"Найдено {adapters.Count} адаптеров AMD; патч применяется к «{AdapterLocator.LocateBest()?.DriverDesc}» (наибольший объём VRAM)."));
            if (fromAutostart)
                Log(L10n.T("Started automatically: wiped patch restored, a reboot is required.",
                           "Запущено автоматически: слетевший патч восстановлен, нужна перезагрузка."));
        };

        ApplyLanguage();
    }

    private void ApplyLanguage()
    {
        Text = L10n.T("Vega-ReBAR-Fix — ReBAR (SAM) for AMD Vega/Polaris",
                      "Vega-ReBAR-Fix — ReBAR (SAM) для AMD Vega/Polaris");
        _gbDiag.Text = L10n.T("Diagnostics", "Диагностика");
        _capPatch.Text = L10n.T("Registry patch:", "Патч реестра:");
        _capBar.Text = L10n.T("BAR above 4 GB:", "BAR выше 4 ГБ:");
        _capVbios.Text = L10n.T("Card vBIOS:", "vBIOS карты:");
        _capDriver.Text = L10n.T("Driver:", "Драйвер:");
        _capKey.Text = L10n.T("Adapter key:", "Ключ адаптера:");
        _gbActions.Text = L10n.T("Actions", "Действия");
        _btnPatch.Text = L10n.T("Patch", "Пропатчить");
        _btnUndo.Text = L10n.T("Undo", "Откатить");
        _btnRefresh.Text = L10n.T("Refresh", "Обновить");
        _btnReboot.Text = L10n.T("Reboot PC", "Перезагрузить ПК");
        _gbAuto.Text = L10n.T("Autostart", "Автозапуск");
        _chkAutostart.Text = L10n.T("Check at Windows sign-in (Task Scheduler, no UAC)",
                                    "Проверять при входе в Windows (Планировщик, без UAC)");
        _chkAutoPatch.Text = L10n.T("Patch automatically if wiped after an update",
                                    "Автоматически патчить, если слетело после обновления");
        _lblLang.Text = L10n.T("Language:", "Язык:");
        _lblFooter.Text = "Vega-ReBAR-Fix v" + CleanVersion() + "  •  " + AdapterTitle();
        RefreshStatus(forceBar: false);
        LayoutAll();
    }

    /// <summary>ProductVersion carries a "+commit" suffix from SourceLink — show only the numeric part.</summary>
    private static string CleanVersion()
    {
        var v = Application.ProductVersion ?? "";
        var plus = v.IndexOf('+');
        return plus > 0 ? v[..plus] : v;
    }

    /// <summary>Manual layout so every block tracks the resizable window.</summary>
    private void LayoutAll()
    {
        int W = ClientSize.Width, H = ClientSize.Height;
        int x = 12, w = W - 24;

        _gbDiag.SetBounds(x, 12, w, 160);
        _gbActions.SetBounds(x, _gbDiag.Bottom + 8, w, 64);
        _gbAuto.SetBounds(x, _gbActions.Bottom + 8, w, 84);
        _log.SetBounds(x, _gbAuto.Bottom + 8, w, Math.Max(60, H - _gbAuto.Bottom - 8 - 40));

        _lblFooter.SetBounds(14, H - 30, W - 210, 18);
        _langCombo.SetBounds(W - 78, H - 33, 64, 23);
        _lblLang.SetBounds(W - 152, H - 30, 72, 18);

        _btnReboot.Location = new Point(_gbActions.ClientSize.Width - 12 - _btnReboot.Width, 24);

        // Value labels start after the widest auto-sized caption, so rows align and never clip captions.
        int valueX = 140;
        foreach (var cap in new[] { _capPatch, _capBar, _capVbios, _capDriver, _capKey })
            valueX = Math.Max(valueX, cap.Right + 10);
        foreach (var val in new[] { _lblPatch, _lblBar, _lblVbios, _lblDriver, _lblKey })
            val.SetBounds(valueX, val.Top, Math.Max(120, _gbDiag.ClientSize.Width - valueX - 12), 18);
    }

    /// <summary>Caption auto-sizes to its text; the value column starts at a shared X.</summary>
    private Label AddDiagRow(GroupBox gb, int y, out Label caption)
    {
        caption = new Label
        {
            Location = new Point(12, y),
            AutoSize = true,
            TextAlign = ContentAlignment.MiddleLeft
        };
        var val = new Label
        {
            Location = new Point(140, y),
            AutoSize = false,
            Size = new Size(gb.Width - 152, 18),
            TextAlign = ContentAlignment.MiddleLeft,
            AutoEllipsis = true,
            Text = "…"
        };
        gb.Controls.Add(caption);
        gb.Controls.Add(val);
        return val;
    }

    private static string AdapterTitle()
    {
        var best = AdapterLocator.LocateBest();
        return best is null
            ? L10n.T("AMD adapter not found", "AMD адаптер не найден")
            : $"{best.DriverDesc} [{best.ShortId}] ({L10n.T("key", "ключ")} {best.KeyName})";
    }

    private void RefreshStatus(bool forceBar)
    {
        if (_busy) return;
        _busy = true;
        try
        {
            var best = AdapterLocator.LocateBest();
            var reg = best is null
                ? new RegistryStatus(null, null, null)
                : RebarStatus.ReadRegistryFrom(@"HKEY_LOCAL_MACHINE\" + best.RegistryPath);

            _lblPatch.Text = best is null ? L10n.T("AMD adapter not found", "AMD адаптер не найден") : reg.Describe();
            _lblPatch.ForeColor = reg.Patched ? Good : Bad;

            if (forceBar) { _bar = RebarStatus.ReadBars(); _barAgeSeconds = 0; }
            _lblBar.Text = _bar.Describe();
            _lblBar.ForeColor = _bar.Error is not null ? Unknown : _bar.Active ? Good : Bad;

            var vbios = VbiosStatus.Create(best?.BiosId, _bar.Active);
            _lblVbios.Text = vbios.Describe();
            _lblVbios.ForeColor = vbios.Supported switch
            {
                true => Good,
                false => Bad,
                _ => Caution
            };

            _lblDriver.Text = best is null ? "—" : $"{best.DriverVersion}  ({best.DriverDate})";
            _lblDriver.ForeColor = SystemColors.ControlText;
            _lblKey.Text = best is null ? "—" : $"{best.KeyName}  [{best.ShortId}]";
            _lblKey.ForeColor = SystemColors.ControlText;

            _btnPatch.Enabled = best is not null && !reg.Patched;
            _btnUndo.Enabled = best is not null;
        }
        finally { _busy = false; }
    }

    private void RunGuarded(Action action)
    {
        try { action(); }
        catch (Exception ex) { Log(L10n.T("Error: ", "Ошибка: ") + ex.Message); }
        RefreshStatus(forceBar: false);
    }

    private void Log(string message)
    {
        _log.AppendText($"[{DateTime.Now:HH:mm:ss}] {message}{Environment.NewLine}");
    }

    private static readonly Color Good = Color.FromArgb(0, 150, 0);
    private static readonly Color Bad = Color.FromArgb(190, 0, 0);
    private static readonly Color Unknown = SystemColors.GrayText;
    private static readonly Color Caution = Color.FromArgb(200, 120, 0);
}
