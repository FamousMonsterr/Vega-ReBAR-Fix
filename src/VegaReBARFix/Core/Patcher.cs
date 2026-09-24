namespace VegaReBARFix.Core;

using Microsoft.Win32;

/// <summary>
/// Applies / reverts the ReBAR registry flags on the AMD adapter class key.
/// The Guru3D trio for legacy ASICs (Vega/Polaris) is:
///   KMD_RebarControlMode = 1, KMD_RebarControlSupport = 1, KMD_EnableReBarForLegacyASIC = 1.
/// Original values are stored under HKCU\Software\VegaReBARFix\Backup and a full
/// .reg export is written to %ProgramData%\VegaReBARFix\ before any change.
/// </summary>
public static class Patcher
{
    private const string BackupKeyPath = @"Software\VegaReBARFix\Backup";
    private const string BackupDir = "VegaReBARFix"; // under %ProgramData%

    public static (bool Ok, string Message) Patch()
    {
        var best = AdapterLocator.LocateBest();
        if (best is null) return (false, "Ключ адаптера AMD не найден — драйвер установлен?");

        SaveBackup(best.KeyName);
        using var k = Registry.LocalMachine.OpenSubKey(best.RegistryPath, writable: true);
        if (k is null) return (false, "Не удалось открыть ключ на запись (нет прав администратора?)");

        k.SetValue("KMD_RebarControlMode", 1, RegistryValueKind.DWord);
        k.SetValue("KMD_RebarControlSupport", 1, RegistryValueKind.DWord);
        k.SetValue("KMD_EnableReBarForLegacyASIC", 1, RegistryValueKind.DWord);

        var st = RebarStatus.ReadRegistryFrom(@"HKEY_LOCAL_MACHINE\" + best.RegistryPath);
        return st.Patched
            ? (true, $"Патч применён (ключ {best.KeyName}): Mode=1, Support=1, LegacyASIC=1. Требуется перезагрузка.")
            : (false, "Запись не подтвердилась перечитыванием: " + st.Describe());
    }

    public static (bool Ok, string Message) Undo()
    {
        // Prefer the key recorded at patch time: a driver reinstall may have
        // re-enumerated the adapter under a different number since then.
        using (var b = Registry.CurrentUser.OpenSubKey(BackupKeyPath))
            if (b?.GetValue("KeyName") is string saved && AdapterLocator.GetInfo(saved) is not null)
                return UndoKey(saved, b);

        var best = AdapterLocator.LocateBest();
        if (best is null) return (false, "Ключ адаптера AMD не найден.");
        return UndoKey(best.KeyName, null);
    }

    private static (bool Ok, string Message) UndoKey(string keyName, RegistryKey? backup)
    {
        var path = $@"SYSTEM\CurrentControlSet\Control\Class\{{4d36e968-e325-11ce-bfc1-08002be10318}}\{keyName}";
        using var k = Registry.LocalMachine.OpenSubKey(path, writable: true);
        if (k is null) return (false, "Не удалось открыть ключ на запись (нет прав администратора?)");

        // -1 means the value did not exist before the patch -> delete it now.
        var mode = backup?.GetValue("Mode") as int? ?? 0;
        var support = backup?.GetValue("Support") as int? ?? -1;
        var legacy = backup?.GetValue("Legacy") as int? ?? -1;

        if (mode >= 0) k.SetValue("KMD_RebarControlMode", mode, RegistryValueKind.DWord);
        else k.DeleteValue("KMD_RebarControlMode", throwOnMissingValue: false);

        RestoreOrDelete(k, "KMD_RebarControlSupport", support);
        RestoreOrDelete(k, "KMD_EnableReBarForLegacyASIC", legacy);

        return (true, $"Откат выполнен (ключ {keyName}). Требуется перезагрузка.");
    }

    private static void RestoreOrDelete(RegistryKey k, string name, int original)
    {
        if (original >= 0) k.SetValue(name, original, RegistryValueKind.DWord);
        else k.DeleteValue(name, throwOnMissingValue: false);
    }

    private static void SaveBackup(string keyName)
    {
        var path = $@"SYSTEM\CurrentControlSet\Control\Class\{{4d36e968-e325-11ce-bfc1-08002be10318}}\{keyName}";
        using (var k = Registry.LocalMachine.OpenSubKey(path))
        {
            using var b = Registry.CurrentUser.CreateSubKey(BackupKeyPath);
            b.SetValue("KeyName", keyName);
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
            using var b = Registry.CurrentUser.CreateSubKey(BackupKeyPath);
            b.SetValue("RegFile", File.Exists(file) ? file : "");
        }
        catch { /* best effort: HKCU backup above is authoritative */ }
    }

    private static int AsBackupInt(object? v) => v is int i ? i : -1; // -1 = was absent
}
