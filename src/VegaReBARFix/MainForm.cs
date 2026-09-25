namespace VegaReBARFix;

using Microsoft.Win32;
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

    private GroupBox _gbDiag = null!, _gbCards = null!, _gbActions = null!, _gbAuto = null!;
    private Label _capPatch = null!, _capBar = null!, _capVbios = null!, _capDriver = null!, _capKey = null!;
    private Label _lblPatch = null!, _lblBar = null!, _lblVbios = null!, _lblDriver = null!, _lblKey = null!;
    private Label _lblFooter = null!, _lblLang = null!, _lblNoCards = null!;
    private LinkLabel _lnkRebarUefi = null!;
    private ComboBox _langCombo = null!;
    private readonly List<(AdapterInfo Info, CheckBox Cb, Label State)> _cards = new();

    private Button _btnPatch = null!, _btnUndo = null!, _btnForce = null!, _btnRefresh = null!, _btnReboot = null!;
    private CheckBox _chkAutostart = null!, _chkAutoPatch = null!;
    private TextBox _log = null!;
    private ToolTip _tip = null!;
    private Label _infoPatch = null!, _infoBar = null!, _infoVbios = null!;
    private string _fullPatch = "", _fullBar = "", _fullVbios = "";

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
        ClientSize = new Size(R(800), R(660));
        MinimumSize = new Size(R(700), R(600));
        Font = new Font("Segoe UI", 9f);
        Text = L10n.T("Vega-ReBAR-Fix — ReBAR (SAM) for AMD Vega/Polaris",
                      "Vega-ReBAR-Fix — ReBAR (SAM) для AMD Vega/Polaris");

        _gbDiag = new GroupBox();
        _lblPatch  = AddDiagRow(out _capPatch,  baseY: 24, wrap: false);
        _lblBar    = AddDiagRow(out _capBar,    baseY: 50, wrap: false);
        _lblVbios  = AddDiagRow(out _capVbios,  baseY: 76, wrap: false);
        _lblDriver = AddDiagRow(out _capDriver, baseY: 102, wrap: false);
        _lblKey    = AddDiagRow(out _capKey,    baseY: 128, wrap: false);
        _tip = new ToolTip { InitialDelay = 200, AutoPopDelay = 30000 };
        _infoPatch = AddInfoIcon(_lblPatch);
        _infoBar   = AddInfoIcon(_lblBar);
        _infoVbios = AddInfoIcon(_lblVbios);

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

        _gbCards = new GroupBox();
        _lblNoCards = new Label { AutoSize = true, ForeColor = SystemColors.GrayText };
        _gbCards.Controls.Add(_lblNoCards);

        _gbActions = new GroupBox();
        _btnPatch = new Button();
        _btnUndo  = new Button();
        _btnForce = new Button();
        _btnRefresh = new Button();
        _btnReboot = new Button { Enabled = false };
        _btnPatch.Click += (_, _) => RunGuarded(() =>
        {
            var targets = SelectedAdapters();
            var (ok, msg) = Patcher.PatchAll(targets);
            Log(msg);
            if (ok) _btnReboot.Enabled = true;
        });
        // Unconditional: patches EVERY AMD adapter key found, whatever the
        // current state or the checkboxes say — for "just make sure" moments
        // after BIOS changes / re-enumerations.
        _btnForce.Click += (_, _) => RunGuarded(() =>
        {
            var (ok, msg) = Patcher.PatchAll(AdapterLocator.FindAllAmdAdapters());
            Log(L10n.T("Force patch (every AMD key, ignoring state): ", "Принудительный патч (все ключи AMD, безусловно): ") + msg);
            if (ok) _btnReboot.Enabled = true;
        });
        _btnUndo.Click += (_, _) => RunGuarded(() =>
        {
            var targets = SelectedAdapters();
            var (ok, msg) = Patcher.UndoAll(targets);
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
        _gbActions.Controls.AddRange(new Control[] { _btnPatch, _btnUndo, _btnForce, _btnRefresh, _btnReboot });

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

        Controls.AddRange(new Control[] { _gbDiag, _gbCards, _gbActions, _gbAuto, _log, _lblFooter, _lblLang, _langCombo });

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
            BuildCardList();
            RefreshStatus(forceBar: true);
            var adapters = AdapterLocator.FindAllAmdAdapters();
            if (adapters.Count > 1)
                Log(L10n.T(
                    $"Found {adapters.Count} AMD adapter keys (a driver reinstall or a Dual-BIOS switch re-enumerates the same card). By default ALL of them are patched, so the trio is in place no matter which key Windows binds the driver to. Uncheck a key to exclude it.",
                    $"Найдено {adapters.Count} ключей адаптеров AMD (переустановка драйвера или переключение Dual BIOS пересоздаёт ключ той же карты). По умолчанию патчуются ВСЕ, чтобы триплет был на месте независимо от того, какой ключ выберет Windows. Снимите галочку, чтобы исключить ключ."));
            if (fromAutostart)
                Log(L10n.T("Started automatically: wiped patch restored, a reboot is required.",
                           "Запущено автоматически: слетевший патч восстановлен, нужна перезагрузка."));
        };

        ApplyLanguage();
    }

    /// <summary>Builds one checkbox row per AMD adapter key. Defaults to all selected;
    /// previously excluded keys (by MatchingDeviceId) start unchecked.</summary>
    private void BuildCardList()
    {
        foreach (var (_, cb, lbl) in _cards) { _gbCards.Controls.Remove(cb); _gbCards.Controls.Remove(lbl); }
        _cards.Clear();
        _gbCards.Controls.Remove(_lblNoCards);

        var adapters = AdapterLocator.FindAllAmdAdapters();
        var active = AdapterLocator.GetActiveKeyNames();
        var present = AdapterLocator.GetPresentDeviceIds();
        var excluded = ExcludedKeys;

        foreach (var a in adapters)
        {
            bool isActive = AdapterLocator.IsActive(a, active, present);
            bool check = !excluded.Contains(a.MatchingDeviceId, StringComparer.OrdinalIgnoreCase);

            var cb = new CheckBox
            {
                Location = new Point(R(14), R(24) + _cards.Count * R(26)),
                AutoSize = true,
                Checked = check,
                // "&&" so the device-ID ampersands are not eaten as accelerator prefixes
                Text = $"{a.KeyName} — {a.DriverDesc} [{a.MatchingDeviceId.Replace("&", "&&")}] • {a.VramText}" +
                       (isActive ? L10n.T("  • ACTIVE", "  • АКТИВНАЯ") : L10n.T("  • stale key", "  • неактивный ключ"))
            };
            cb.CheckedChanged += (_, _) => SaveExclusions();
            var lblState = new Label
            {
                AutoSize = false,
                Size = new Size(R(96), R(18)),
                Location = new Point(R(470), R(24) + _cards.Count * R(26) + R(2)),
                TextAlign = ContentAlignment.MiddleLeft,
                AutoEllipsis = true
            };
            _gbCards.Controls.Add(cb);
            _gbCards.Controls.Add(lblState);
            _cards.Add((a, cb, lblState));
        }

        if (adapters.Count == 0)
        {
            _lblNoCards.Text = L10n.T("No AMD adapters found.", "Адаптеры AMD не найдены.");
            _lblNoCards.Location = new Point(R(14), R(26));
            _gbCards.Controls.Add(_lblNoCards);
        }

        _gbCards.Text = L10n.T(
            $"Cards to patch ({adapters.Count} key{(adapters.Count == 1 ? "" : "s")})",
            $"Карты для патча (ключей: {adapters.Count})");
        LayoutAll();
        RefreshStatus(forceBar: false);
    }

    private List<AdapterInfo> SelectedAdapters() =>
        _cards.Where(c => c.Cb.Checked).Select(c => c.Info).ToList();

    private static readonly string[] ExcludedDefault = Array.Empty<string>();

    /// <summary>Excluded adapters are remembered by MatchingDeviceId (stable across key renumbering).</summary>
    private string[] ExcludedKeys =>
        Registry.CurrentUser.OpenSubKey(L10n.AppKey)?.GetValue("ExcludedKeys") as string[] ?? ExcludedDefault;

    private void SaveExclusions()
    {
        using var k = Registry.CurrentUser.CreateSubKey(L10n.AppKey);
        k.SetValue("ExcludedKeys",
            _cards.Where(c => !c.Cb.Checked).Select(c => c.Info.MatchingDeviceId).ToArray(),
            RegistryValueKind.MultiString);
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
        _btnForce.Text = L10n.T("Force", "Принудительно");
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
        int cardsH = R(24 + 26 * Math.Max(_cards.Count, 1) + 10);

        _gbDiag.SetBounds(x, R(12), w, R(176));
        _gbCards.SetBounds(x, _gbDiag.Bottom + R(8), w, cardsH);
        _gbActions.SetBounds(x, _gbCards.Bottom + R(8), w, R(64));
        _gbAuto.SetBounds(x, _gbActions.Bottom + R(8), w, R(84));
        _log.SetBounds(x, _gbAuto.Bottom + R(8), w, Math.Max(R(60), H - _gbAuto.Bottom - R(8) - R(42)));

        _lblFooter.SetBounds(R(14), H - R(32), W - R(220), R(18));
        _langCombo.SetBounds(W - R(82), H - R(35), R(66), R(25));
        _lblLang.SetBounds(W - R(160), H - R(32), R(74), R(18));

        _btnPatch.SetBounds(R(14), R(24), R(118), R(30));
        _btnUndo.SetBounds(R(140), R(24), R(98), R(30));
        _btnForce.SetBounds(R(246), R(24), R(112), R(30));
        _btnRefresh.SetBounds(R(366), R(24), R(98), R(30));
        _btnReboot.Size = new Size(R(126), R(30));
        _btnReboot.Location = new Point(_gbActions.ClientSize.Width - R(12) - _btnReboot.Width, R(24));

        _chkAutostart.Location = new Point(R(14), R(24));
        _chkAutoPatch.Location = new Point(R(14), R(50));

        // Value column starts after the widest auto-sized caption; each ⓘ icon
        // sits right after ITS caption so the attribution is unambiguous.
        int valueX = R(140);
        foreach (var row in _rows)
        {
            row.Cap.Location = new Point(R(12), R(row.BaseY));
            valueX = Math.Max(valueX, row.Cap.Right + R(10));
        }
        foreach (var row in _rows)
            row.Val.SetBounds(valueX, R(row.BaseY),
                Math.Max(R(120), _gbDiag.ClientSize.Width - valueX - R(12)), R(row.BaseH));

        PlaceInfoIcon(_infoPatch, _capPatch);
        PlaceInfoIcon(_infoBar, _capBar);
        PlaceInfoIcon(_infoVbios, _capVbios);
        _lnkRebarUefi.SetBounds(valueX, R(152), 0, 0);

        // Card rows: checkbox auto-sizes, the patch-state label follows its right edge.
        for (int i = 0; i < _cards.Count; i++)
        {
            var (_, cb, state) = _cards[i];
            cb.Location = new Point(R(14), R(24) + i * R(26));
            state.Location = new Point(cb.Right + R(12), R(24) + i * R(26) + R(2));
        }
    }

    /// <summary>The ⓘ icon goes immediately after its caption text.</summary>
    private void PlaceInfoIcon(Label icon, Label caption)
    {
        icon.SetBounds(caption.Right + R(4), caption.Top - R(1), R(20), R(18));
    }

    /// <summary>Single-line status row; the full explanation lives behind the ⓘ icon.</summary>
    private Label AddDiagRow(out Label caption, int baseY, bool wrap)
    {
        caption = new Label
        {
            AutoSize = true,
            Location = new Point(R(12), R(baseY)),
            TextAlign = ContentAlignment.MiddleLeft
        };
        var val = new Label
        {
            AutoSize = false,
            TextAlign = ContentAlignment.MiddleLeft,
            AutoEllipsis = true,
            Text = "…"
        };
        _gbDiag.Controls.Add(caption);
        _gbDiag.Controls.Add(val);
        _rows = (_rows ?? Array.Empty<(Label, Label, int, int)>())
            .Append((caption, val, baseY, 18)).ToArray();
        return val;
    }

    private Label AddInfoIcon(Label valueLabel)
    {
        var icon = new Label
        {
            Text = "ⓘ",
            AutoSize = false,
            Size = new Size(R(20), R(18)),
            Cursor = Cursors.Hand,
            ForeColor = SystemColors.GrayText,
            TextAlign = ContentAlignment.MiddleCenter
        };
        icon.Click += (_, _) =>
        {
            var text = icon == _infoPatch ? _fullPatch : icon == _infoBar ? _fullBar : _fullVbios;
            MessageBox.Show(this, text, L10n.T("Details", "Подробнее"),
                MessageBoxButtons.OK, MessageBoxIcon.Information);
        };
        _gbDiag.Controls.Add(icon);
        return icon;
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

            // Per-card trio state + aggregated patch row across the selected keys.
            var perCard = new List<(AdapterInfo Info, bool Patched)>();
            foreach (var (info, cb, state) in _cards)
            {
                var st = RebarStatus.ReadRegistryFrom(@"HKEY_LOCAL_MACHINE\" + info.RegistryPath);
                state.Text = L10n.T(st.Patched ? "patch: yes" : "patch: NO", st.Patched ? "патч: есть" : "патч: НЕТ");
                state.ForeColor = st.Patched ? Good : Bad;
                perCard.Add((info, st.Patched));
            }
            var selected = _cards.Where(c => c.Cb.Checked).Count();
            bool allPatched = selected > 0 && perCard.All(p =>
                !_cards.Any(c => c.Info.KeyName == p.Info.KeyName && c.Cb.Checked) || p.Patched);

            _lblPatch.Text = _cards.Count == 0
                ? L10n.T("AMD adapter not found", "AMD адаптер не найден")
                : allPatched
                    ? L10n.T($"enabled ({selected} key{(selected == 1 ? "" : "s")})", $"включен ({selected} ключ(ей))")
                    : L10n.T("wiped on some keys — see 'Cards to patch'", "слетел на части ключей — см. «Карты для патча»");
            _lblPatch.ForeColor = allPatched ? Good : Bad;
            _fullPatch = L10n.T(
                $"Registry trio KMD_RebarControlMode / KMD_RebarControlSupport / KMD_EnableReBarForLegacyASIC (=1) on every selected adapter key. Without all three the driver ignores ReBAR on Vega/Polaris. Windows Update and driver reinstalls recreate the key without them — the autostart guard re-applies at sign-in.",
                "Триплет KMD_RebarControlMode / KMD_RebarControlSupport / KMD_EnableReBarForLegacyASIC (=1) на каждом выбранном ключе адаптера. Без всех трёх драйвер игнорирует ReBAR на Vega/Polaris. Обновления Windows и переустановки драйвера пересоздают ключ без них — страж автозапуска восстанавливает при входе.");
            _tip.SetToolTip(_infoPatch, _fullPatch);

            if (forceBar) { _bar = RebarStatus.ReadBars(); _barAgeSeconds = 0; }
            _lblBar.Text = _bar.DescribeShort();
            _lblBar.ForeColor = _bar.Error is not null ? Unknown : _bar.Active ? Good : Bad;
            _fullBar = _bar.Describe() + L10n.T(
                "\n\nReBAR counts as active only when a GPU BAR of at least 512 MB is actually mapped above the 4 GB line. A 256 MB BAR above 4 GB is Above-4G placement (already fine); the resize itself happens at driver start, so apply the patch and reboot.",
                "\n\nReBAR считается активным, только если BAR GPU не меньше 512 МБ реально отображён выше границы 4 ГБ. BAR 256 МБ выше 4 ГБ — это размещение Above-4G (уже хорошо); сам ресайз происходит при старте драйвера, поэтому после патча нужна перезагрузка.");
            _tip.SetToolTip(_infoBar, _fullBar);

            var vbios = VbiosStatus.Create(best?.BiosId, _bar.Active);
            _lblVbios.Text = vbios.DescribeShort();
            _lblVbios.ForeColor = vbios.Supported switch
            {
                true => Good,
                false => Bad,
                _ => Caution
            };
            _lnkRebarUefi.Visible = vbios.Supported != true;
            _fullVbios = vbios.Describe() + L10n.T(
                "\n\nGPU-Z's 'GPU hardware support: Unsupported' is ignorable per the Guru3D unlock — the trio works on stock vBIOS. If the board BIOS is right and the BAR still does not resize, try the other Dual-BIOS ROM (full power-off!), then ReBarUEFI or a ReBAR-capable vBIOS mod.",
                "\n\nСтроку «GPU hardware support: Unsupported» в GPU-Z можно игнорировать (по данным Guru3D-unlock) — триплет работает и на стоковом vBIOS. Если BIOS платы верный, а BAR не ресайзнется — попробуйте второй ROM Dual BIOS (с полным выключением!), затем ReBarUEFI или vBIOS-мод с ReBAR.");
            _tip.SetToolTip(_infoVbios, _fullVbios);

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

            _btnPatch.Enabled = _cards.Any(c => c.Cb.Checked && !perCard.Any(p => p.Info.KeyName == c.Info.KeyName && p.Patched));
            _btnUndo.Enabled = _cards.Any(c => c.Cb.Checked);
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
