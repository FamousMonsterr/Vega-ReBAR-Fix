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

    public string DescribeShort() => Patched ? L10n.T("enabled", "включен") : L10n.T("wiped", "слетел");

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

    public string DescribeShort()
    {
        if (Error is not null) return L10n.T("unknown", "неизвестно");
        if (Active) return $"{L10n.T("active", "активен")}: {Fmt(LargestAbove4Gb)}";
        if (LargestAbove4Gb > 0) return $"{L10n.T("BAR not resized", "BAR не ресайзнут")} ({Fmt(LargestAbove4Gb)})";
        return L10n.T("not active", "не активен");
    }

    public static string Fmt(ulong bytes) =>
        bytes % (1024UL * 1024 * 1024) == 0
            ? $"{bytes / (1024UL * 1024 * 1024)} {L10n.T("GB", "ГБ")}"
            : $"{bytes / (1024UL * 1024)} {L10n.T("MB", "МБ")}";
}

/// <summary>
/// vBIOS verdict. The ROM ID comes from the driver key (what the driver read
/// from the card at this boot); the verdict combines the hardware evidence
/// (a resized BAR is hard proof of support) with the community ROM knowledge
/// base. Verdicts re-compute live, so a cold-boot into a different Dual-BIOS
/// ROM flips the row without restarting the tool.
/// </summary>
public sealed record VbiosStatus(string BiosId, bool? Supported, string? Hint = null)
{
    public static VbiosStatus Create(string? biosId, bool barActive)
    {
        var (verdict, known) = VbiosKnowledgeBase.Lookup(biosId);

        // Hard evidence wins: the BAR actually resized -> this ROM supports ReBAR.
        if (barActive)
            return new VbiosStatus(biosId ?? "—", true, L10n.T(
                "proven: the BAR is resized on this ROM",
                "доказано: BAR ресайзнут на этой прошивке"));

        if (biosId is null)
            return new VbiosStatus("—", null, null);

        switch (verdict)
        {
            case VbiosKnowledgeBase.Verdict.StockNoRebar:
                return new VbiosStatus(biosId, false, L10n.T(
                    "stock ROM without ReBAR: the patch will not take effect until the card runs a ReBAR-capable BIOS (switch via Dual BIOS, then FULL power-off) or ReBarUEFI is added to the board",
                    "стоковый ROM без ReBAR: патч не сработает, пока не переведёте карту на BIOS с ReBAR (тумблер Dual BIOS + полное выключение) или не добавите ReBarUEFI"));
            default:
                return new VbiosStatus(biosId, null, L10n.T(
                    "not determined: BAR not resized — check the board BIOS (Above 4G Decoding / Re-Size BAR / CSM off). Per Guru3D the trio works even on stock vBIOS, so a ReBAR-capable ROM or ReBarUEFI is only the fallback",
                    "не определён: BAR не ресайзнут — проверьте BIOS платы (Above 4G Decoding / Re-Size BAR / CSM off). По данным Guru3D триплет работает и на стоковом vBIOS — ROM с ReBAR или ReBarUEFI лишь запасной вариант"));
        }
    }

    public string Describe() => Hint is null ? BiosId : $"{BiosId} — {Hint}";

    public string DescribeShort() => $"{BiosId} — " + Supported switch
    {
        true => L10n.T("supports ReBAR", "поддерживает ReBAR"),
        false => L10n.T("no ReBAR", "без ReBAR"),
        _ => L10n.T("not determined", "не определён")
    };
}

public static class PowerConfig
{
    /// <summary>Fast Startup (hiberboot) makes "Shut down" hibernate instead of a
    /// real power-off, so the card never re-reads its Dual-BIOS ROM. Enabled = 1.</summary>
    public static bool FastStartupEnabled =>
        Microsoft.Win32.Registry.GetValue(
            @"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\Session Manager\Power",
            "HiberbootEnabled", 0) is int i && i == 1;
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

    /// <summary>Queries WMI for the GPU's mapped memory ranges, merged with the
    /// authoritative system resource map (HKLM\HARDWARE\RESOURCEMAP — the same
    /// data Device Manager shows). WMI's CIM layer occasionally drops rows;
    /// the resource map is read from the registry and always live.</summary>
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

            // Second, independent source: the live system resource map (no CIM).
            var resMap = LargestAbove4GbFromResourceMap();
            if (resMap is ulong rm && rm > above) above = rm;

            return new BarStatus(above, below, null);
        }
        catch (Exception ex)
        {
            return new BarStatus(0, 0, ex.Message);
        }
    }

    /// <summary>
    /// Parses HKLM\HARDWARE\RESOURCEMAP\System Resources\Reserved\.Translated —
    /// a CM_RESOURCE_LIST with every memory range the OS has assigned. Returns the
    /// largest range starting at or above the 4 GiB line (system-wide; the GPU BAR
    /// is in practice the only such large range), or null if unreadable.
    /// </summary>
    public static ulong? LargestAbove4GbFromResourceMap()
    {
        try
        {
            if (Microsoft.Win32.Registry.GetValue(
                    @"HKEY_LOCAL_MACHINE\HARDWARE\RESOURCEMAP\System Resources\Reserved",
                    ".Translated", null) is not byte[] raw || raw.Length < 16)
                return null;

            int pos = 0;
            uint listCount = BitConverter.ToUInt32(raw, pos); pos += 4;
            ulong maxAbove = 0;

            for (int list = 0; list < listCount && pos + 8 <= raw.Length; list++)
            {
                pos += 8; // INTERFACE_TYPE (4) + BusNumber (4)
                if (pos + 4 > raw.Length) break;
                uint partialCount = BitConverter.ToUInt32(raw, pos + 4); // Version(2)+Revision(2) first
                pos += 8;

                for (uint i = 0; i < partialCount && pos + 20 <= raw.Length; i++)
                {
                    byte type = raw[pos];
                    ulong start = BitConverter.ToUInt64(raw, pos + 8);
                    uint lenRaw = BitConverter.ToUInt32(raw, pos + 16);

                    ulong size = type switch
                    {
                        3 => lenRaw, // CmResourceTypeMemory: length in bytes
                        7 => (lenRaw >> 24) switch // CmResourceTypeMemoryLarge: high byte = granularity
                        {
                            0 => 4UL * 1024 * 1024,
                            1 => 1024UL * 1024 * 1024,
                            2 => 2UL * 1024 * 1024 * 1024,
                            _ => 0UL
                        } * (lenRaw & 0xFFFFFF),
                        _ => 0UL
                    };

                    if (size > 0 && start >= 0x100000000UL && size > maxAbove)
                        maxAbove = size;
                    pos += 20; // sizeof(CM_PARTIAL_RESOURCE_DESCRIPTOR)
                }
            }
            return maxAbove;
        }
        catch
        {
            return null;
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
