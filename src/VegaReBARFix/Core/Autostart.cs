namespace VegaReBARFix.Core;

using System.Diagnostics;
using Microsoft.Win32;

/// <summary>
/// Autostart guard, implemented as a Task Scheduler job running with highest
/// privileges at logon — the app then patches silently, without a UAC prompt
/// on every boot (an HKCU Run entry with a requireAdministrator payload would
/// show UAC each logon).
/// </summary>
public static class Autostart
{
    public const string TaskName = "VegaReBARFix_Autostart";
    private const string AppKey = @"Software\VegaReBARFix";

    public static bool AutoPatchEnabled
    {
        get => (Registry.CurrentUser.OpenSubKey(AppKey)?.GetValue("AutoPatch") as int? ?? 1) == 1;
        set
        {
            using var k = Registry.CurrentUser.CreateSubKey(AppKey);
            k.SetValue("AutoPatch", value ? 1 : 0, RegistryValueKind.DWord);
        }
    }

    public static bool TaskExists()
    {
        return Run($"/Query /TN {TaskName}") == 0;
    }

    /// <summary>Creates (or recreates) the logon task pointing at <paramref name="exePath"/>.</summary>
    public static (bool Ok, string Message) Enable(string exePath)
    {
        var tr = $"\\\"{exePath}\\\" -autostart";
        var rc = Run($"/Create /F /TN {TaskName} /TR \"{tr}\" /SC ONLOGON /RL HIGHEST");
        return rc == 0
            ? (true, L10n.T($"Autostart enabled: Task Scheduler job {TaskName}.",
                            $"Автозапуск включён: задача {TaskName} в Планировщике заданий."))
            : (false, L10n.T($"Failed to create the Task Scheduler job (schtasks, code {rc}). Run the tool as administrator.",
                             $"Не удалось создать задачу Планировщика (schtasks, код {rc}). Запустите утилиту от администратора."));
    }

    public static (bool Ok, string Message) Disable()
    {
        if (!TaskExists())
            return (true, L10n.T("Autostart is already off.", "Автозапуск уже выключен."));
        var rc = Run($"/Delete /F /TN {TaskName}");
        return rc == 0
            ? (true, L10n.T($"Autostart disabled: Task Scheduler job {TaskName} deleted.",
                            $"Автозапуск выключен: задача {TaskName} удалена."))
            : (false, L10n.T($"Failed to delete the Task Scheduler job (code {rc}).",
                             $"Не удалось удалить задачу Планировщика (код {rc})."));
    }

    /// <summary>schtasks.exe, hidden, returns process exit code.</summary>
    private static int Run(string arguments)
    {
        try
        {
            using var p = Process.Start(new ProcessStartInfo("schtasks.exe", arguments)
            {
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            });
            if (p is null) return -1;
            var so = p.StandardOutput.ReadToEnd();
            p.WaitForExit(15_000);
            if (p.ExitCode != 0) Trace.WriteLine("schtasks " + arguments + " -> " + p.ExitCode + " " + so);
            return p.ExitCode;
        }
        catch
        {
            return -1;
        }
    }
}
