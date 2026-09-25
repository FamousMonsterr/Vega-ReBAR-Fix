namespace VegaReBARFix;

using VegaReBARFix.Core;

/// <summary>
/// RDP_CnC-style dialog: a live diagnostics block (green/red status labels,
/// refreshed on a timer), action buttons and autostart checkboxes.
/// Reboot only ever happens on an explicit button press.
///
/// DPI best practice for a hand-laid-out form: the process runs PerMonitorV2
/// (csproj ApplicationHighDpiMode) and this form does its own scaling — every
/// coordinate and size from the 96-DPI design grid is multiplied by
/// DeviceDpi/96 in LayoutAll(). Text itself scales with DPI automatically
/// (GDI renders point sizes at the monitor DPI), so only geometry is scaled
/// here. Long status rows are two lines tall and wrap.
/// </summary>
public sealed class MainForm : Form
{
    private readonly System.Windows.Forms.Timer _timer = new() { Interval = 2000 };

    private GroupBox _gbDiag = null!, _gbActions = null!, _gbAuto = null!;
    private Label _capPatch = null!, _capBar = null!, _capVbios = null!, _capDriver = null!, _capKey = null!;
    private Label _lblPatch = null!, _lblBar = null!, _lblVbios = null!, _lblDriver = null!, _lblKey = null!;
    private Label _lblFooter = null!, _lblLang = null!;
    private LinkLabel _lnkRebarUefi = null!;
    private ComboBox _langCombo = null!;

    private Button _btnPatch = null!, _btnUndo = null!, _btnRefresh = null!, _btnReboot = null!;
    private CheckBox _chkAutostart = null!, _chkAutoPatch = null!;
    private TextBox _log = null!;

    /// <summary>Caption/value pairs with their 96-DPI design geometry (base Y, base height).</summary>
    private (Label Cap, Label Val, int BaseY, int BaseH)[] _rows = null!;

    private BarStatus _bar = new(0, 0, null);
    private string? _lastVbiosId;
    private bool _fastStartupHintShown;
    private int _barAgeSeconds = int.MaxValue;   // hardware check runs on demand + every 30 s
    private bool _busy;
    private bool _suppressAutoEvents;

    /// <summary>DPI scale factor relative to the 96-DPI design grid (1.5 at 150 %).</summary>
    private float S => DeviceDpi / 96f;
    private int R(float v) => (int)MathF.Round(v * S);

    public MainForm(bool fromAutostart)
    {
        AutoScaleMode = AutoScaleMode.None;      // scaling is done manually in LayoutAll()
        FormBorderStyle = FormBorderStyle.Sizable;
        MaximizeBox = true;
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(R(800), R(600));
        MinimumSize = new Size(R(700), R(540));
        Font = new Font("Segoe UI", 9f);
        Text = L10n.T("Vega-ReBAR-Fix — ReBAR (SAM) for AMD Vega/Polaris",
                      "Vega-ReBAR-Fix — ReBAR (SAM) для AMD Vega/Polaris");

        _gbDiag = new GroupBox();
        _lblPatch  = AddDiagRow(out _capPatch,  baseY: 24, baseH: 18, wrap: false);
        _lblBar    = AddDiagRow(out _capBar,    baseY: 56, baseH: 34, wrap: true);
        _lblVbios  = AddDiagRow(out _capVbios,  baseY: 92, baseH: 34, wrap: true);
        _lblDriver = AddDiagRow(out _capDriver, baseY: 128, baseH: 18, wrap: false);
        _lblKey    = AddDiagRow(out _capKey,    baseY: 150, baseH: 18, wrap: false);

        _lnkRebarUefi = new LinkLabel
        {
            AutoSize = true,
            LinkColor = Color.FromArgb(0, 100, 190),
            Text = L10n.T("ReBarUEFI — board-firmware mod (opens GitHub)", "ReBarUEFI — мод прошивки платы (откроется GitHub)")
        };
        _lnkRebarUefi.Click += (_, _) =>
        {
            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "https://github.com/xCuri0/ReBarUEFI",
                    UseShellExecute = true
                });
            }
            catch (Exception ex) { Log(L10n.T("Failed to open the link: ", "Не удалось открыть ссылку: ") + ex.Message); }
        };
        _gbDiag.Controls.Add(_lnkRebarUefi);

        _gbActions = new GroupBox();
        _btnPatch = new Button();
        _btnUndo  = new Button();
        _btnRefresh = new Button();
        _btnReboot = new Button { Enabled = false };
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

        _gbAuto = new GroupBox();
        _chkAutostart = new CheckBox { Location = new Point(R(14), R(24)), AutoSize = true };
        _chkAutoPatch = new CheckBox
        {
            Location = new Point(R(14), R(50)),
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
            Multiline = true,
            ReadOnly = true,
            ScrollBars = ScrollBars.Vertical,
            BackColor = SystemColors.Window
        };

        _lblFooter = new Label
        {
            AutoSize = false,
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
            DropDownStyle = ComboBoxStyle.DropDownList
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
        DpiChanged += (_, _) => LayoutAll();

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
        _lnkRebarUefi.Text = L10n.T("ReBarUEFI — board-firmware mod (opens GitHub)", "ReBarUEFI — мод прошивки платы (откроется GitHub)");
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

    /// <summary>
    /// Manual DPI-aware layout: every geometry value below is expressed on the
    /// 96-DPI design grid and multiplied by the current DPI scale factor.
    /// </summary>
    private void LayoutAll()
    {
        int W = ClientSize.Width, H = ClientSize.Height;
        int x = R(12), w = W - 2 * R(12);

        _gbDiag.SetBounds(x, R(12), w, R(198));
        _gbActions.SetBounds(x, _gbDiag.Bottom + R(8), w, R(64));
        _gbAuto.SetBounds(x, _gbActions.Bottom + R(8), w, R(84));
        _log.SetBounds(x, _gbAuto.Bottom + R(8), w, Math.Max(R(60), H - _gbAuto.Bottom - R(8) - R(42)));

        _lblFooter.SetBounds(R(14), H - R(32), W - R(220), R(18));
        _langCombo.SetBounds(W - R(82), H - R(35), R(66), R(25));
        _lblLang.SetBounds(W - R(160), H - R(32), R(74), R(18));

        _btnPatch.SetBounds(R(14), R(24), R(118), R(30));
        _btnUndo.SetBounds(R(140), R(24), R(98), R(30));
        _btnRefresh.SetBounds(R(246), R(24), R(98), R(30));
        _btnReboot.Size = new Size(R(126), R(30));
        _btnReboot.Location = new Point(_gbActions.ClientSize.Width - R(12) - _btnReboot.Width, R(24));

        _chkAutostart.Location = new Point(R(14), R(24));
        _chkAutoPatch.Location = new Point(R(14), R(50));

        // Value column starts after the widest auto-sized caption, so rows align
        // and captions can never clip, at any DPI.
        int valueX = R(140);
        foreach (var row in _rows)
            valueX = Math.Max(valueX, row.Cap.Right + R(10));
        foreach (var row in _rows)
            row.Val.SetBounds(valueX, R(row.BaseY),
                Math.Max(R(120), _gbDiag.ClientSize.Width - valueX - R(12)), R(row.BaseH));

        _lnkRebarUefi.SetBounds(valueX, R(172), 0, 0);
    }

    /// <summary>Caption auto-sizes to its text; the value column starts at a shared X.
    /// Long rows (wrap: true) are two lines tall and wrap instead of clipping.</summary>
    private Label AddDiagRow(out Label caption, int baseY, int baseH, bool wrap)
    {
        caption = new Label
        {
            AutoSize = true,
            TextAlign = ContentAlignment.MiddleLeft
        };
        var val = new Label
        {
            AutoSize = false,
            TextAlign = ContentAlignment.MiddleLeft,
            AutoEllipsis = !wrap,           // wrapping rows show their full text on two lines
            Text = "…"
        };
        gbAdd(caption);
        gbAdd(val);
        _rows = (_rows ?? Array.Empty<(Label, Label, int, int)>())
            .Append((caption, val, baseY, baseH)).ToArray();
        return val;

        void gbAdd(Control c) => _gbDiag.Controls.Add(c);
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
            _lnkRebarUefi.Visible = vbios.Supported != true;

            // The driver re-reads the ROM from the card at every real power-on,
            // so a changed ID means the machine cold-booted into another ROM.
            if (_lastVbiosId is not null && best?.BiosId is not null && best.BiosId != _lastVbiosId)
                Log(L10n.T(
                    $"vBIOS changed: {_lastVbiosId} -> {best.BiosId} (the card cold-booted into another ROM).",
                    $"vBIOS изменился: {_lastVbiosId} -> {best.BiosId} (карта загрузилась с другой прошивки)."));
            _lastVbiosId = best?.BiosId;

            if (vbios.Supported != true && PowerConfig.FastStartupEnabled && !_fastStartupHintShown)
            {
                _fastStartupHintShown = true;
                Log(L10n.T(
                    "Fast Startup is ON: \"Shut down\" hibernates and the card keeps its old ROM. After flipping the Dual BIOS switch do a FULL power-off: shutdown /s /full /t 0, then power on.",
                    "Включён Fast Startup: «Завершение работы» уходит в гибернацию, и карта продолжает со старым ROM. После переключения тумблера Dual BIOS сделайте ПОЛНОЕ выключение: shutdown /s /full /t 0, затем включите ПК."));
            }

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
