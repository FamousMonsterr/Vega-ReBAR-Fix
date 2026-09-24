namespace VegaReBARFix.Core;

using System.Management;

/// <summary>State of the two driver registry values that gate ReBAR.</summary>
public sealed record RegistryStatus(int? Mode, int? Support)
{
    /// <summary>True when both KMD_RebarControlMode and KMD_RebarControlSupport equal 1.</summary>
    public bool Patched => Mode == 1 && Support == 1;

    public string Describe() => Patched
        ? "включен"
        : $"слетел (Mode={Mode?.ToString() ?? "нет"}, Support={Support?.ToString() ?? "нет"})";
}

/// <summary>Largest BAR seen for the GPU, split by the 4 GiB boundary.</summary>
public sealed record BarStatus(ulong LargestAbove4Gb, ulong LargestBelow4Gb, string? Error)
{
    /// <summary>ReBAR counts as active when a GPU BAR of at least 512 MB sits above 4 GiB.
    /// Above-4G Decoding alone moves the stock 256 MB BAR up, so the size check matters.</summary>
    public bool Active => LargestAbove4Gb >= 512UL * 1024 * 1024;

    public string Describe()
    {
        if (Error is not null) return "неизвестно (" + Error + ")";
        if (Active) return $"активен: {Fmt(LargestAbove4Gb)} выше 4 ГБ";
        if (LargestAbove4Gb > 0)
            return $"не активен: BAR выше 4 ГБ всего {Fmt(LargestAbove4Gb)}";
        if (LargestBelow4Gb > 0)
            return $"не активен: крупнейший BAR {Fmt(LargestBelow4Gb)} ниже 4 ГБ — проверьте BIOS (Above 4G Decoding / Re-Size BAR)";
        return "не активен: BAR GPU не найден";
    }

    public static string Fmt(ulong bytes) =>
        bytes % (1024UL * 1024 * 1024) == 0
            ? $"{bytes / (1024UL * 1024 * 1024)} ГБ"
            : $"{bytes / (1024UL * 1024)} МБ";
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
        var key = AdapterLocator.Locate();
        if (key is null) return new RegistryStatus(null, null);
        var path = $@"HKEY_LOCAL_MACHINE\{AdapterLocator.DisplayClassPath}\{key}";
        return ReadRegistryFrom(path);
    }

    public static RegistryStatus ReadRegistryFrom(string fullKeyPath)
    {
        using var k = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(
            fullKeyPath.StartsWith("HKEY_LOCAL_MACHINE\\", StringComparison.OrdinalIgnoreCase)
                ? fullKeyPath["HKEY_LOCAL_MACHINE\\".Length..]
                : fullKeyPath);
        if (k is null) return new RegistryStatus(null, null);
        return new RegistryStatus(AsInt(k.GetValue("KMD_RebarControlMode")), AsInt(k.GetValue("KMD_RebarControlSupport")));
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
