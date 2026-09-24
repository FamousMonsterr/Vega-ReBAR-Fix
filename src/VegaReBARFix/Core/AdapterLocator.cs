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
public static class AdapterLocator
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

    /// <summary>The adapter the patch applies to: the AMD card with the most dedicated VRAM.</summary>
    public static AdapterInfo? LocateBest()
    {
        var all = FindAllAmdAdapters();
        if (all.Count == 0) return null;
        if (all.Count > 1)
            all.Sort((a, b) => (b.VramBytes ?? 0).CompareTo(a.VramBytes ?? 0));
        return all[0];
    }

    /// <summary>Key name (e.g. "0001") of the best adapter, or null.</summary>
    public static string? Locate() => LocateBest()?.KeyName;

    public static AdapterInfo? GetInfo(string keyName) =>
        FindAllAmdAdapters().FirstOrDefault(a => a.KeyName == keyName);
}
