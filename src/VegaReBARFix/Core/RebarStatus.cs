namespace VegaReBARFix.Core;

using System.Management;

/// <summary>State of the three driver registry values that gate ReBAR on legacy ASICs.</summary>
public sealed record RegistryStatus(int? Mode, int? Support, int? Legacy)
{
    /// <summary>True when KMD_RebarControlMode, KMD_RebarControlSupport and
    /// KMD_EnableReBarForLegacyASIC all equal 1 (the Guru3D trio for Vega/Polaris).</summary>
    public bool Patched => Mode == 1 && Support == 1 && Legacy == 1;

    public string Describe()
    {
        if (Patched) return L10n.T("enabled", "включен");
        return $"{L10n.T("wiped:", "слетел:")} Mode={Show(Mode)}, Support={Show(Support)}, Legacy={Show(Legacy)}";
    }

    private static string Show(int? v) => v?.ToString() ?? L10n.T("absent", "нет");
}

/// <summary>Largest BAR seen for the GPU, split by the 4 GiB boundary.</summary>
public sealed record BarStatus(ulong LargestAbove4Gb, ulong LargestBelow4Gb, string? Error)
{
    /// <summary>ReBAR counts as active when a GPU BAR of at least 512 MB sits above 4 GiB.
    /// Above-4G Decoding alone moves the stock 256 MB BAR up, so the size check matters.</summary>
    public bool Active => LargestAbove4Gb >= 512UL * 1024 * 1024;

    public string Describe()
    {
        if (Error is not null) return L10n.T("unknown (", "неизвестно (") + Error + ")";
        if (Active) return $"{L10n.T("active:", "активен:")} {Fmt(LargestAbove4Gb)} {L10n.T("above 4 GB", "выше 4 ГБ")}";
        if (LargestAbove4Gb > 0)
            return $"{L10n.T("not active: BAR above 4 GB is only ", "не активен: BAR выше 4 ГБ всего ")}{Fmt(LargestAbove4Gb)}";
        if (LargestBelow4Gb > 0)
            return $"{L10n.T("not active: BAR ", "не активен: BAR ")}{Fmt(LargestBelow4Gb)} " +
                   L10n.T("below 4 GB — check BIOS or card vBIOS (see README)",
                          "ниже 4 ГБ — BIOS или vBIOS без ReBAR (см. README)");
        return L10n.T("not active: no GPU BAR found", "не активен: BAR GPU не найден");
    }

    public static string Fmt(ulong bytes) =>
        bytes % (1024UL * 1024 * 1024) == 0
            ? $"{bytes / (1024UL * 1024 * 1024)} {L10n.T("GB", "ГБ")}"
            : $"{bytes / (1024UL * 1024)} {L10n.T("MB", "МБ")}";
}

/// <summary>
/// vBIOS verdict. The PCIe Resizable BAR capability itself lives in the card's
/// ROM and is not readable from user mode, so the verdict is honest:
/// a resized BAR (hardware check) proves support; AMD reference stock Vega 10
/// ROMs (IDs "113-D05...") are known to ship without ReBAR flags; anything
/// else stays undetermined until a reboot proves it one way or the other.
/// </summary>
public sealed record VbiosStatus(string BiosId, bool? Supported)
{
    public static VbiosStatus Create(string? biosId, bool barActive)
    {
        if (barActive) return new VbiosStatus(biosId ?? "—", true);
        if (!string.IsNullOrEmpty(biosId) && biosId.StartsWith("113-D05", StringComparison.OrdinalIgnoreCase))
            return new VbiosStatus(biosId, false);
        return new VbiosStatus(biosId ?? "—", null);
    }

    public string Describe() => Supported switch
    {
        true => $"{BiosId} — {L10n.T("supports ReBAR", "поддерживает ReBAR")}",
        false => $"{BiosId} — {L10n.T(
            "no ReBAR (stock ROM): the patch will not take effect until the card runs a ReBAR-capable BIOS (switch via Dual BIOS) or ReBarUEFI is added to the board",
            "без ReBAR (стоковый ROM): патч не сработает, пока не переведёте карту на BIOS с ReBAR (второй чип Dual BIOS) или не добавите ReBarUEFI")}",
        _ => $"{BiosId} — {L10n.T(
            "not determined: BAR not resized — check the board BIOS; if it is enabled, a vBIOS with ReBAR is required",
            "не определён: BAR не ресайзнут — проверьте BIOS платы; если включён, нужен vBIOS с ReBAR")}"
    };
}

/// <summary>
/// Reads ReBAR state. Hardware side is resolved through WMI
/// (Win32_PnPAllocatedResource → Win32_DeviceMemoryAddress) the same way
/// Device Manager resolves "Large Memory Range" assignments.
/// </summary>
public static class RebarStatus
{
    public static RegistryStatus ReadRegistry()
    {
        var best = AdapterLocator.LocateBest();
        if (best is null) return new RegistryStatus(null, null, null);
        return ReadRegistryFrom(@"HKEY_LOCAL_MACHINE\" + best.RegistryPath);
    }

    public static RegistryStatus ReadRegistryFrom(string fullKeyPath)
    {
        using var k = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(
            fullKeyPath.StartsWith("HKEY_LOCAL_MACHINE\\", StringComparison.OrdinalIgnoreCase)
                ? fullKeyPath["HKEY_LOCAL_MACHINE\\".Length..]
                : fullKeyPath);
        if (k is null) return new RegistryStatus(null, null, null);
        return new RegistryStatus(
            AsInt(k.GetValue("KMD_RebarControlMode")),
            AsInt(k.GetValue("KMD_RebarControlSupport")),
            AsInt(k.GetValue("KMD_EnableReBarForLegacyASIC")));
    }

    /// <summary>Queries WMI for the GPU's mapped memory ranges. Takes ~1 s, cache the result.</summary>
    public static BarStatus ReadBars()
    {
        try
        {
            // All device memory ranges on the system, keyed by starting address.
            var ranges = new Dictionary<string, (ulong Start, ulong End)>();
            using (var q = new ManagementObjectSearcher("SELECT StartingAddress, EndingAddress FROM Win32_DeviceMemoryAddress"))
            foreach (var o in q.Get())
            {
                var start = ToUlong(o["StartingAddress"]);
                if (start is null) continue;
                var end = ToUlong(o["EndingAddress"]) ?? start;
                ranges[start.Value.ToString()] = (start.Value, end.Value);
            }

            ulong above = 0, below = 0;
            using (var q = new ManagementObjectSearcher("SELECT Dependent, Antecedent FROM Win32_PnPAllocatedResource"))
            foreach (var o in q.Get())
            {
                var dep = o["Dependent"]?.ToString() ?? "";
                if (!dep.Contains("VEN_1002", StringComparison.OrdinalIgnoreCase)) continue;
                var ant = o["Antecedent"]?.ToString() ?? "";
                if (!ant.Contains("Win32_DeviceMemoryAddress", StringComparison.Ordinal)) continue;

                var m = System.Text.RegularExpressions.Regex.Match(ant, @"StartingAddress=""(\d+)""");
                if (!m.Success) continue;
                if (!ranges.TryGetValue(m.Groups[1].Value, out var r)) continue;

                var size = r.End - r.Start + 1;
                if (r.Start >= 0x100000000UL) { if (size > above) above = size; }
                else if (size > below) below = size;
            }

            return new BarStatus(above, below, null);
        }
        catch (Exception ex)
        {
            return new BarStatus(0, 0, ex.Message);
        }
    }

    private static int? AsInt(object? v) => v switch
    {
        int i => i,
        _ when int.TryParse(v?.ToString(), out var i) => i,
        _ => null
    };

    private static ulong? ToUlong(object? v) => v switch
    {
        null => null,
        ulong u => u,
        uint ui => ui,
        _ when ulong.TryParse(v.ToString(), out var u) => u,
        _ => null
    };
}
