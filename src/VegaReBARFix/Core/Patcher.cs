namespace VegaReBARFix.Core;

using Microsoft.Win32;

/// <summary>
/// Applies / reverts the ReBAR registry flag trio on AMD adapter class keys.
/// Same physical card can own several class keys (a driver reinstall or a
/// Dual-BIOS switch re-enumerates it), so the patch targets EVERY selected
/// adapter — the trio is inert on stale keys and must be present on whichever
/// key Windows decides to bind the driver to.
/// Per-key originals are stored under HKCU\Software\VegaReBARFix\Backup\&lt;key&gt;
/// and a full .reg export is written to %ProgramData%\VegaReBARFix\.
/// </summary>
public static class Patcher
{
    private const string BackupRoot = @"Software\VegaReBARFix\Backup";
    private const string BackupDir = "VegaReBARFix"; // under %ProgramData%

    public static string KeyPath(string keyName) =>
        $@"SYSTEM\CurrentControlSet\Control\Class\{{4d36e968-e325-11ce-bfc1-08002be10318}}\{keyName}";

    /// <summary>Patches every given adapter; returns aggregated result.</summary>
    public static (bool Ok, string Message) PatchAll(IReadOnlyList<AdapterInfo> adapters)
    {
        if (adapters.Count == 0)
            return (false, L10n.T("AMD adapter key not found — is the driver installed?",
                                  "Ключ адаптера AMD не найден — драйвер установлен?"));

        var ok = true;
        var details = new List<string>();
        foreach (var a in adapters)
        {
            var (k, msg) = PatchOne(a);
            ok &= k;
            details.Add($"{a.KeyName}: {msg}");
        }
        return (ok, (ok
                ? L10n.T("Patch applied: ", "Патч применён: ")
                : L10n.T("Not everything was patched: ", "Патч применён не везде: "))
            + string.Join("; ", details)
            + L10n.T(". A reboot is required.", ". Требуется перезагрузка."));
    }

    private static (bool Ok, string Msg) PatchOne(AdapterInfo a)
    {
        SaveBackup(a.KeyName);
        using var k = Registry.LocalMachine.OpenSubKey(a.RegistryPath, writable: true);
        if (k is null)
            return (false, L10n.T("cannot open for writing (no admin?)", "нет доступа на запись (нет прав?)"));

        k.SetValue("KMD_RebarControlMode", 1, RegistryValueKind.DWord);
        k.SetValue("KMD_RebarControlSupport", 1, RegistryValueKind.DWord);
        k.SetValue("KMD_EnableReBarForLegacyASIC", 1, RegistryValueKind.DWord);

        var st = RebarStatus.ReadRegistryFrom(@"HKEY_LOCAL_MACHINE\" + a.RegistryPath);
        return st.Patched
            ? (true, L10n.T("Mode=1, Support=1, LegacyASIC=1", "Mode=1, Support=1, LegacyASIC=1"))
            : (false, L10n.T("write not confirmed", "запись не подтвердилась") + " — " + st.Describe());
    }

    /// <summary>Reverts every given adapter from its per-key backup.</summary>
    public static (bool Ok, string Message) UndoAll(IReadOnlyList<AdapterInfo> adapters)
    {
        if (adapters.Count == 0)
            return (false, L10n.T("AMD adapter key not found.", "Ключ адаптера AMD не найден."));

        var ok = true;
        var details = new List<string>();
        foreach (var a in adapters)
        {
            var (k, msg) = UndoOne(a.KeyName);
            ok &= k;
            details.Add($"{a.KeyName}: {msg}");
        }
        return (ok, (ok
                ? L10n.T("Undo completed: ", "Откат выполнен: ")
                : L10n.T("Not everything was reverted: ", "Откат выполнен не везде: "))
            + string.Join("; ", details)
            + L10n.T(". A reboot is required.", ". Требуется перезагрузка."));
    }

    private static (bool Ok, string Msg) UndoOne(string keyName)
    {
        using var backup = Registry.CurrentUser.OpenSubKey($@"{BackupRoot}\{keyName}");
        using var k = Registry.LocalMachine.OpenSubKey(KeyPath(keyName), writable: true);
        if (k is null)
            return (false, L10n.T("cannot open for writing (no admin?)", "нет доступа на запись (нет прав?)"));

        RestoreOrDelete(k, "KMD_RebarControlMode", backup, "Mode");
        RestoreOrDelete(k, "KMD_RebarControlSupport", backup, "Support");
        RestoreOrDelete(k, "KMD_EnableReBarForLegacyASIC", backup, "Legacy");
        return (true, L10n.T("reverted from backup", "восстановлено из бэкапа"));
    }

    private static void RestoreOrDelete(RegistryKey target, string valueName, RegistryKey? backup, string backupName)
    {
        if (backup?.GetValue(backupName) is int original && original >= 0)
            target.SetValue(valueName, original, RegistryValueKind.DWord);
        else
            target.DeleteValue(valueName, throwOnMissingValue: false);
    }

    private static void SaveBackup(string keyName)
    {
        var path = KeyPath(keyName);
        using (var k = Registry.LocalMachine.OpenSubKey(path))
        {
            using var b = Registry.CurrentUser.CreateSubKey($@"{BackupRoot}\{keyName}");
            b.SetValue("Mode", AsBackupInt(k?.GetValue("KMD_RebarControlMode")));
            b.SetValue("Support", AsBackupInt(k?.GetValue("KMD_RebarControlSupport")));
            b.SetValue("Legacy", AsBackupInt(k?.GetValue("KMD_EnableReBarForLegacyASIC")));
        }

        try
        {
            var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), BackupDir);
            Directory.CreateDirectory(dir);
            var file = Path.Combine(dir, $"classkey_{keyName}_{DateTime.Now:yyyyMMdd_HHmmss}.reg");
            var psi = new System.Diagnostics.ProcessStartInfo("reg.exe", $"export \"HKLM\\{path}\" \"{file}\" /y")
            {
                CreateNoWindow = true, UseShellExecute = false,
                RedirectStandardOutput = true, RedirectStandardError = true
            };
            using var p = System.Diagnostics.Process.Start(psi);
            p?.WaitForExit(10_000);
            using var b = Registry.CurrentUser.CreateSubKey($@"{BackupRoot}\{keyName}");
            b.SetValue("RegFile", File.Exists(file) ? file : "");
        }
        catch { /* best effort: HKCU backup above is authoritative */ }
    }

    private static int AsBackupInt(object? v) => v is int i ? i : -1; // -1 = was absent
}
