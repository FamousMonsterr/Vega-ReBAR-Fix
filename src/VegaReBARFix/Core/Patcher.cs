namespace VegaReBARFix.Core;

using Microsoft.Win32;

/// <summary>
/// Applies / reverts the ReBAR registry flags on the AMD adapter class key.
/// The original values are stored under HKCU\Software\VegaReBARFix\Backup
/// and a full .reg export is written next to them before any change.
/// </summary>
public static class Patcher
{
    private const string BackupKeyPath = @"Software\VegaReBARFix\Backup";
    private const string BackupDir = "VegaReBARFix"; // under %ProgramData%

    public sealed record BackupInfo(string KeyName, int Mode, int Support, string RegFile);

    public static (bool Ok, string Message) Patch()
    {
        var key = AdapterLocator.Locate();
        if (key is null) return (false, "Ключ адаптера AMD не найден — драйвер установлен?");

        SaveBackup(key);
        var path = $@"SYSTEM\CurrentControlSet\Control\Class\{{4d36e968-e325-11ce-bfc1-08002be10318}}\{key}";
        using var k = Registry.LocalMachine.OpenSubKey(path, writable: true);
        if (k is null) return (false, "Не удалось открыть ключ на запись (нет прав администратора?)");

        k.SetValue("KMD_RebarControlMode", 1, RegistryValueKind.DWord);
        k.SetValue("KMD_RebarControlSupport", 1, RegistryValueKind.DWord);

        var st = RebarStatus.ReadRegistryFrom(@"HKEY_LOCAL_MACHINE\" + path);
        return st.Patched
            ? (true, $"Патч применён: KMD_RebarControlMode=1, KMD_RebarControlSupport=1 (ключ {key}). Требуется перезагрузка.")
            : (false, "Запись не подтвердилась перечитыванием: " + st.Describe());
    }

    public static (bool Ok, string Message) Undo()
    {
        var key = AdapterLocator.Locate();
        if (key is null) return (false, "Ключ адаптера AMD не найден.");

        var path = $@"SYSTEM\CurrentControlSet\Control\Class\{{4d36e968-e325-11ce-bfc1-08002be10318}}\{key}";
        using var k = Registry.LocalMachine.OpenSubKey(path, writable: true);
        if (k is null) return (false, "Не удалось открыть ключ на запись (нет прав администратора?)");

        var (hadMode, mode) = ReadBackup("Mode");
        var (hadSupport, support) = ReadBackup("Support");

        if (hadMode) k.SetValue("KMD_RebarControlMode", mode, RegistryValueKind.DWord);
        else k.DeleteValue("KMD_RebarControlMode", throwOnMissingValue: false);

        if (hadSupport) k.SetValue("KMD_RebarControlSupport", support, RegistryValueKind.DWord);
        else k.DeleteValue("KMD_RebarControlSupport", throwOnMissingValue: false);

        return (true, $"Откат выполнен (Mode={mode}, Support={(hadSupport ? support.ToString() : "удалён")}). Требуется перезагрузка.");
    }

    private static void SaveBackup(string keyName)
    {
        var path = $@"SYSTEM\CurrentControlSet\Control\Class\{{4d36e968-e325-11ce-bfc1-08002be10318}}\{keyName}";
        using (var k = Registry.LocalMachine.OpenSubKey(path))
        {
            using var b = Registry.CurrentUser.CreateSubKey(BackupKeyPath);
            b.SetValue("KeyName", keyName);
            var mode = k?.GetValue("KMD_RebarControlMode");
            var support = k?.GetValue("KMD_RebarControlSupport");
            b.SetValue("Mode", mode is int i ? i : -1);       // -1 = value was absent
            b.SetValue("Support", support is int i2 ? i2 : -1);
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

    private static (bool Existed, int Value) ReadBackup(string name)
    {
        using var b = Registry.CurrentUser.OpenSubKey(BackupKeyPath);
        if (b?.GetValue(name) is not int v) return (false, 0);
        return v < 0 ? (false, 0) : (true, v);
    }
}
