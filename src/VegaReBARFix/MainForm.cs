namespace VegaReBARFix;

using VegaReBARFix.Core;

/// <summary>
/// RDP_CnC-style dialog: a live diagnostics block (green/red status labels,
/// refreshed on a timer), action buttons and autostart checkboxes.
/// Reboot only ever happens on an explicit button press.
/// </summary>
public sealed class MainForm : Form
{
    private readonly System.Windows.Forms.Timer _timer = new() { Interval = 2000 };

    private Label _lblPatch = null!;
    private Label _lblBar = null!;
    private Label _lblDriver = null!;
    private Label _lblKey = null!;

    private Button _btnPatch = null!;
    private Button _btnUndo = null!;
    private Button _btnReboot = null!;
    private CheckBox _chkAutostart = null!;
    private CheckBox _chkAutoPatch = null!;
    private TextBox _log = null!;

    private BarStatus _bar = new(0, 0, "не проверялось");
    private int _barAgeSeconds = int.MaxValue;   // hardware check runs on demand + every 30 s
    private bool _busy;

    public MainForm(bool fromAutostart)
    {
        Text = "Vega-ReBAR-Fix — ReBAR (SAM) для AMD Vega/Polaris";
        FormBorderStyle = FormBorderStyle.FixedSingle;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(566, 470);
        Font = new Font("Segoe UI", 9f);

        var gbDiag = new GroupBox
        {
            Text = "Диагностика",
            Location = new Point(12, 12),
            Size = new Size(542, 132)
        };
        _lblPatch = AddDiagRow(gbDiag, "Патч реестра:", 28);
        _lblBar   = AddDiagRow(gbDiag, "BAR выше 4 ГБ:", 54);
        _lblDriver= AddDiagRow(gbDiag, "Драйвер:", 80);
        _lblKey   = AddDiagRow(gbDiag, "Ключ адаптера:", 104);

        var gbActions = new GroupBox
        {
            Text = "Действия",
            Location = new Point(12, 150),
            Size = new Size(542, 64)
        };
        _btnPatch = new Button { Text = "Пропатчить", Location = new Point(14, 24), Size = new Size(118, 30) };
        _btnUndo  = new Button { Text = "Откатить", Location = new Point(140, 24), Size = new Size(98, 30) };
        var btnRefresh = new Button { Text = "Обновить", Location = new Point(246, 24), Size = new Size(98, 30) };
        _btnReboot = new Button
        {
            Text = "Перезагрузить ПК",
            Location = new Point(404, 24),
            Size = new Size(126, 30),
            Enabled = false
        };
        _btnPatch.Click += (_, _) => RunPatched(() =>
        {
            var (ok, msg) = Patcher.Patch();
            Log(msg);
            if (ok) _btnReboot.Enabled = true;
        });
        _btnUndo.Click += (_, _) => RunPatched(() =>
        {
            var (ok, msg) = Patcher.Undo();
            Log(msg);
            if (ok) _btnReboot.Enabled = true;
        });
        btnRefresh.Click += (_, _) => { _barAgeSeconds = int.MaxValue; RefreshStatus(forceBar: true); Log("Статус обновлён."); };
        _btnReboot.Click += (_, _) =>
        {
            if (MessageBox.Show(this,
                    "Перезагрузить компьютер сейчас?\n\nРеестр патчится до перезагрузки; ReBAR заработает после неё.",
                    "Перезагрузка", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
            try
            {
                System.Diagnostics.Process.Start("shutdown.exe", "/r /t 5 /c \"Vega-ReBAR-Fix: применение ReBAR\"");
            }
            catch (Exception ex) { Log("Не удалось запустить перезагрузку: " + ex.Message); }
        };
        gbActions.Controls.AddRange(new Control[] { _btnPatch, _btnUndo, btnRefresh, _btnReboot });

        var gbAuto = new GroupBox
        {
            Text = "Автозапуск",
            Location = new Point(12, 220),
            Size = new Size(542, 84)
        };
        _chkAutostart = new CheckBox
        {
            Text = "Проверять при входе в Windows (задача в Планировщике, без UAC)",
            Location = new Point(14, 24),
            AutoSize = true
        };
        _chkAutoPatch = new CheckBox
        {
            Text = "Автоматически патчить, если слетело после обновления",
            Location = new Point(14, 50),
            AutoSize = true,
            Checked = Autostart.AutoPatchEnabled
        };
        _chkAutostart.CheckedChanged += (_, _) =>
        {
            if (_suppressAutoEvents) return;
            RunPatched(() =>
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
        gbAuto.Controls.AddRange(new Control[] { _chkAutostart, _chkAutoPatch });

        _log = new TextBox
        {
            Location = new Point(12, 310),
            Size = new Size(542, 116),
            Multiline = true,
            ReadOnly = true,
            ScrollBars = ScrollBars.Vertical,
            BackColor = SystemColors.Window
        };

        var lblFooter = new Label
        {
            Text = "Vega-ReBAR-Fix v" + Application.ProductVersion + "  •  GPU: " + AdapterKeyTitle(),
            Location = new Point(14, 434),
            AutoSize = true,
            ForeColor = SystemColors.GrayText
        };

        Controls.AddRange(new Control[] { gbDiag, gbActions, gbAuto, _log, lblFooter });

        AcceptButton = _btnPatch;

        _timer.Tick += (_, _) =>
        {
            _barAgeSeconds += 2;
            RefreshStatus(forceBar: _barAgeSeconds >= 30);
        };
        _timer.Start();

        Shown += (_, _) =>
        {
            _chkAutostart.Checked = Autostart.TaskExists();  // fires handler only on change
            RefreshStatus(forceBar: true);
            if (fromAutostart) Log("Запущено автоматически: слетевший патч восстановлен, нужна перезагрузка.");
        };
    }

    private Label AddDiagRow(GroupBox gb, string caption, int y)
    {
        var cap = new Label { Text = caption, Location = new Point(12, y), AutoSize = true };
        var val = new Label { Location = new Point(140, y), AutoSize = false, Size = new Size(388, 18), Text = "…" };
        gb.Controls.Add(cap);
        gb.Controls.Add(val);
        return val;
    }

    private static string AdapterKeyTitle()
    {
        var key = AdapterLocator.Locate();
        if (key is null) return "AMD адаптер не найден";
        var info = AdapterLocator.GetInfo(key);
        return info is null ? key : $"{info.DriverDesc} (Class\\...\\{key})";
    }

    private void RefreshStatus(bool forceBar)
    {
        if (_busy) return;
        _busy = true;
        try
        {
            var key = AdapterLocator.Locate();
            var reg = key is null
                ? new RegistryStatus(null, null)
                : RebarStatus.ReadRegistryFrom($@"HKEY_LOCAL_MACHINE\{AdapterLocator.DisplayClassPath}\{key}");

            _lblPatch.Text = key is null ? "AMD адаптер не найден" : reg.Describe();
            _lblPatch.ForeColor = reg.Patched ? Good : Bad;

            if (forceBar) { _bar = RebarStatus.ReadBars(); _barAgeSeconds = 0; }
            _lblBar.Text = _bar.Describe();
            _lblBar.ForeColor = _bar.Error is not null ? Unknown : _bar.Active ? Good : Bad;

            var info = key is null ? null : AdapterLocator.GetInfo(key);
            _lblDriver.Text = info is null ? "—" : $"{info.DriverVersion}  ({info.DriverDate})";
            _lblDriver.ForeColor = SystemColors.ControlText;
            _lblKey.Text = key is null ? "—" : $"Class\\{{4d36e968-...}}\\{key}  [{info?.MatchingDeviceId}]";
            _lblKey.ForeColor = SystemColors.ControlText;

            _btnPatch.Enabled = key is not null && !reg.Patched;
            _btnUndo.Enabled = key is not null;
        }
        finally { _busy = false; }
    }

    private bool _suppressAutoEvents;

    private void RunPatched(Action action)
    {
        try { action(); }
        catch (Exception ex) { Log("Ошибка: " + ex.Message); }
        RefreshStatus(forceBar: false);
    }

    private void Log(string message)
    {
        _log.AppendText($"[{DateTime.Now:HH:mm:ss}] {message}{Environment.NewLine}");
    }

    private static readonly Color Good = Color.FromArgb(0, 150, 0);
    private static readonly Color Bad = Color.FromArgb(190, 0, 0);
    private static readonly Color Unknown = SystemColors.GrayText;
}
