namespace VegaReBARFix.Core;

using Microsoft.Win32;

/// <summary>Located AMD display adapter class key and its identity fields.</summary>
public sealed record AdapterInfo(
    string KeyName,
    string RegistryPath,
    string MatchingDeviceId,
    string DriverDesc,
    string DriverVersion,
    string DriverDate,
    ulong? VramBytes,
    string? BiosId)
{
    public string ShortId
    {
        get
        {
            // PCI\VEN_1002&DEV_687F&SUBSYS_...&REV_C3 -> VEN_1002&DEV_687F
            var parts = MatchingDeviceId.Split('&');
            var dev = string.Join('&', parts.Take(2));
            return dev.Length > 0 ? dev : MatchingDeviceId;
        }
    }

    public string VramText => VramBytes is null ? "—"
        : VramBytes % (1024UL * 1024 * 1024) == 0
            ? $"{VramBytes / (1024UL * 1024 * 1024)} {L10n.T("GB", "ГБ")}"
            : $"{VramBytes / (1024UL * 1024)} {L10n.T("MB", "МБ")}";
}

public partial class AdapterLocator
{
    /// <summary>Device IDs actually present in Win32_VideoController right now.</summary>
    public static HashSet<string> GetPresentDeviceIds()
    {
        var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        try
        {
            using var q = new System.Management.ManagementObjectSearcher("SELECT PNPDeviceID FROM Win32_VideoController");
            foreach (var o in q.Get())
            {
                var id = o["PNPDeviceID"]?.ToString();
                if (!string.IsNullOrEmpty(id)) ids.Add(id);
            }
        }
        catch { /* WMI failed: IsPresent() then assumes every key may be active */ }
        return ids;
    }

    /// <summary>
    /// Class key numbers Windows actually bound the running display devices to.
    /// The Enum branch of each present device carries a "Driver" value like
    /// "{4d36e968-...}\0004" — the authoritative live adapter key, which may
    /// differ from any key's MatchingDeviceId string (INF matching is looser).
    /// </summary>
    public static HashSet<string> GetActiveKeyNames()
    {
        var keys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var pnpId in GetPresentDeviceIds())
        {
            try
            {
                var driver = Registry.GetValue(
                    $@"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Enum\{pnpId}", "Driver", null) as string;
                if (string.IsNullOrEmpty(driver)) continue;
                var idx = driver.LastIndexOf('\\');
                if (idx >= 0 && idx + 1 < driver.Length) keys.Add(driver[(idx + 1)..]);
            }
            catch { /* enum branch not readable: fall back to IsPresent() */ }
        }
        return keys;
    }

    /// <summary>True when the key is the live one per the Enum→Driver mapping,
    /// or (fallback) when its MatchingDeviceId prefixes a present device ID.</summary>
    public static bool IsActive(AdapterInfo a, HashSet<string>? activeKeys = null, HashSet<string>? presentIds = null)
    {
        activeKeys ??= GetActiveKeyNames();
        if (activeKeys.Count > 0) return activeKeys.Contains(a.KeyName);
        return IsPresent(a.MatchingDeviceId, presentIds);
    }

    /// <summary>True when <paramref name="matchingDeviceId"/> is a prefix of a device
    /// actually present in Win32_VideoController (present IDs carry an extra
    /// instance suffix like "\6&amp;2D57B13C&amp;0&amp;00000019").</summary>
    public static bool IsPresent(string matchingDeviceId, HashSet<string>? presentIds = null)
    {
        presentIds ??= GetPresentDeviceIds();
        if (presentIds.Count == 0) return true; // WMI unavailable: assume it may be active
        return presentIds.Any(id => id.StartsWith(matchingDeviceId, StringComparison.OrdinalIgnoreCase));
    }
}

/// <summary>
/// Finds the AMD GPU class key under HKLM\...\Class\{4d36e968-...}.
/// The key number (0000, 0001, ...) shifts when Windows Update reinstalls the driver,
/// so it must be discovered, never hard-coded.
///
/// Systems may hold several AMD adapters (e.g. a Vega dGPU plus a Vega iGPU in an APU).
/// The patch must land on the discrete card, so the adapter with the largest
/// dedicated VRAM (HardwareInformation.qwMemorySize) wins.
/// </summary>
public static partial class AdapterLocator
{
    public const string DisplayClassPath =
        @"SYSTEM\CurrentControlSet\Control\Class\{4d36e968-e325-11ce-bfc1-08002be10318}";

    public static List<AdapterInfo> FindAllAmdAdapters()
    {
        var result = new List<AdapterInfo>();
        try
        {
            using var cls = Registry.LocalMachine.OpenSubKey(DisplayClassPath);
            if (cls is null) return result;

            foreach (var sub in cls.GetSubKeyNames())
            {
                if (sub.Length != 4 || !sub.All(char.IsAsciiDigit)) continue;
                using var k = cls.OpenSubKey(sub);
                if (k is null) continue;

                var mid = k.GetValue("MatchingDeviceId") as string;
                if (mid is null || !mid.StartsWith("PCI\\VEN_1002", StringComparison.OrdinalIgnoreCase))
                    continue;

                var desc = k.GetValue("DriverDesc") as string ?? "";
                if (desc.Contains("Remote Display", StringComparison.OrdinalIgnoreCase))
                    continue; // Microsoft/indirect virtual adapter, not real hardware

                ulong? vram = null;
                if (k.GetValue("HardwareInformation.qwMemorySize") is long q && q > 0) vram = (ulong)q;

                string? bios = null;
                if (k.GetValue("HardwareInformation.BiosString") is byte[] raw)
                {
                    bios = System.Text.Encoding.Unicode.GetString(raw).TrimEnd('\0').Trim();
                    if (bios.Length == 0) bios = null;
                }

                result.Add(new AdapterInfo(
                    sub,
                    $@"{DisplayClassPath}\{sub}",
                    mid,
                    desc,
                    k.GetValue("DriverVersion") as string ?? "",
                    k.GetValue("DriverDate") as string ?? "",
                    vram,
                    bios));
            }
        }
        catch
        {
            // fall through with whatever was collected
        }
        return result;
    }

    /// <summary>The adapter to report diagnostics for: the live key per the
    /// Enum→Driver mapping; fall back to the largest dedicated VRAM.</summary>
    public static AdapterInfo? LocateBest()
    {
        var all = FindAllAmdAdapters();
        if (all.Count == 0) return null;
        var active = GetActiveKeyNames();
        if (active.Count > 0)
        {
            var live = all.FirstOrDefault(a => active.Contains(a.KeyName));
            if (live is not null) return live;
        }
        var sorted = new List<AdapterInfo>(all);
        sorted.Sort((a, b) => (b.VramBytes ?? 0).CompareTo(a.VramBytes ?? 0));
        return sorted[0];
    }

    /// <summary>Key name (e.g. "0001") of the best adapter, or null.</summary>
    public static string? Locate() => LocateBest()?.KeyName;

    public static AdapterInfo? GetInfo(string keyName) =>
        FindAllAmdAdapters().FirstOrDefault(a => a.KeyName == keyName);
}
