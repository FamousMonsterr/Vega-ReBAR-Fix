namespace VegaReBARFix;

using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Principal;
using VegaReBARFix.Core;

internal static class Program
{
    private const string MutexName = "Local\\VegaReBARFix_SingleInstance";

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool AttachConsole(int dwProcessId);

    [STAThread]
    private static int Main(string[] args)
    {
        L10n.Initialize();
        ApplicationConfiguration.Initialize();

        string mode = args.Select(Norm).FirstOrDefault(m => m is "-status" or "-patch" or "-undo" or "-autostart") ?? "";

        using var mutex = new Mutex(true, MutexName, out var first);
        if (!first && mode != "-status")
            return 0; // another instance already runs; autostart duplicates exit quietly
        GC.KeepAlive(mutex); // hold the mutex for the whole lifetime, incl. the GUI message loop

        return mode switch
        {
            "-status" => CliStatus(),
            "-patch" => ElevatedAction(args, doWork: () => Patcher.Patch(), title: () => L10n.T("Patch", "Патч")),
            "-undo" => ElevatedAction(args, doWork: () => Patcher.Undo(), title: () => L10n.T("Undo", "Откат")),
            "-autostart" => AutostartRun(),
            _ => RunGui(args)
        };

        static string Norm(string a) => a.ToLowerInvariant();
    }

    // ---- CLI ---------------------------------------------------------------

    private static int CliStatus()
    {
        AttachConsole(-1);
        try
        {
            var best = AdapterLocator.LocateBest();
            if (best is null)
            {
                Console.WriteLine(L10n.T("AMD adapter not found.", "AMD адаптер не найден."));
                return 4;
            }
            var reg = RebarStatus.ReadRegistryFrom(@"HKEY_LOCAL_MACHINE\" + best.RegistryPath);
            var bar = RebarStatus.ReadBars();

            var all = AdapterLocator.FindAllAmdAdapters();
            var multi = all.Count > 1
                ? L10n.T($"  (AMD adapters found: {all.Count}, the one with the max VRAM selected)",
                         $"  (всего AMD-адаптеров: {all.Count}, выбрана карта с максимальной VRAM)")
                : "";
            Console.WriteLine($"{L10n.T("Key:", "Ключ:")}      {best.KeyName}  [{best.ShortId}]{multi}");
            var regLine = reg.Patched
                ? L10n.T("patch PRESENT — enabled", "патч ЕСТЬ — включен")
                : L10n.T("patch MISSING — ", "патч НЕТ — ") + reg.Describe();
            Console.WriteLine($"{L10n.T("Registry:", "Реестр:")}  {regLine}");
            Console.WriteLine($"{L10n.T("BAR:", "BAR:")}     {bar.Describe()}");
            var vbios = VbiosStatus.Create(best.BiosId, bar.Active);
            Console.WriteLine($"{L10n.T("vBIOS:", "vBIOS:")}   {vbios.Describe()}");
            Console.WriteLine($"{L10n.T("Driver:", "Драйвер:")} {best.DriverVersion} ({best.DriverDate})");
            Console.WriteLine(bar.Active && reg.Patched
                ? L10n.T("RESULT: ReBAR is active.", "ИТОГ: ReBAR активен.")
                : L10n.T("RESULT: ReBAR is not fully active.", "ИТОГ: ReBAR не активен полностью."));
            return reg.Patched ? 0 : 1;
        }
        finally
        {
            Console.Out.Flush();
            FreeConsoleQuiet();
        }
    }

    private static int ElevatedAction(string[] args, Func<(bool Ok, string Message)> doWork, Func<string> title)
    {
        if (!IsElevated())
        {
            // Re-run self elevated; the child shows the outcome and exits with the real code.
            try
            {
                using var p = Process.Start(new ProcessStartInfo(
                    Environment.ProcessPath ?? Application.ExecutablePath,
                    JoinArgs(args) + " -child")
                { UseShellExecute = true, Verb = "runas" });
                if (p is null) return 1;
                p.WaitForExit();
                return p.ExitCode;
            }
            catch (System.ComponentModel.Win32Exception)
            {
                return 1; // UAC declined
            }
        }

        var (ok, message) = doWork();
        if (args.Contains("-child", StringComparer.OrdinalIgnoreCase))
            MessageBox.Show(message, "Vega-ReBAR-Fix: " + title(), MessageBoxButtons.OK,
                ok ? MessageBoxIcon.Information : MessageBoxIcon.Error);
        else
        {
            AttachConsole(-1);
            Console.WriteLine(message);
            Console.Out.Flush();
        }
        return ok ? 0 : 1;
    }

    private static int AutostartRun()
    {
        if (!IsElevated())
        {
            // Task is created with /RL HIGHEST; if it ever runs unelevated, elevate silently.
            try
            {
                Process.Start(new ProcessStartInfo(
                    Environment.ProcessPath ?? Application.ExecutablePath,
                    JoinArgs(new[] { "-autostart" }) + " -child")
                { UseShellExecute = true, Verb = "runas" });
                return 0;
            }
            catch { return 1; }
        }

        var best = AdapterLocator.LocateBest();
        var reg = best is null
            ? new RegistryStatus(null, null, null)
            : RebarStatus.ReadRegistryFrom(@"HKEY_LOCAL_MACHINE\" + best.RegistryPath);

        if (reg.Patched)
            return 0; // everything is fine — silent exit, no window

        if (Autostart.AutoPatchEnabled)
        {
            var (ok, msg) = Patcher.Patch();
            if (ok) return ShowGuardWindow();   // patched: user decides when to reboot
            MessageBox.Show(msg, "Vega-ReBAR-Fix", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return 1;
        }

        return ShowGuardWindow(); // auto-patch disabled: show status, no changes

        int ShowGuardWindow()
        {
            Application.Run(new MainForm(fromAutostart: true));
            return 0;
        }
    }

    private static int RunGui(string[] args)
    {
        if (!IsElevated() && !args.Contains("-noelevate", StringComparer.OrdinalIgnoreCase))
        {
            try
            {
                Process.Start(new ProcessStartInfo(
                    Environment.ProcessPath ?? Application.ExecutablePath, "")
                { UseShellExecute = true, Verb = "runas" });
                return 0; // elevated instance takes over
            }
            catch (System.ComponentModel.Win32Exception)
            {
                // UAC declined: continue unelevated — status works, writes will fail with a hint
            }
        }

        Application.Run(new MainForm(fromAutostart: false));
        return 0;
    }

    // ---- helpers -----------------------------------------------------------

    private static bool IsElevated()
    {
        using var id = WindowsIdentity.GetCurrent();
        return new WindowsPrincipal(id).IsInRole(WindowsBuiltInRole.Administrator);
    }

    private static string JoinArgs(string[] args) =>
        string.Join(' ', args.Select(Quote));

    private static string Quote(string a) => a.Contains(' ') ? '"' + a + '"' : a;

    [DllImport("kernel32.dll")]
    private static extern bool FreeConsole();
    private static void FreeConsoleQuiet() { try { FreeConsole(); } catch { } }
}
