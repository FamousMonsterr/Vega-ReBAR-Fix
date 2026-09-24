namespace VegaReBARFix.Core;

using Microsoft.Win32;

/// <summary>Located AMD display adapter class key and its identity fields.</summary>
public sealed record AdapterInfo(
    string KeyName,
    string RegistryPath,
    string MatchingDeviceId,
    string DriverDesc,
    string DriverVersion,
    string DriverDate);

/// <summary>
/// Finds the AMD GPU class key under HKLM\...\Class\{4d36e968-...}.
/// The key number (0000, 0001, ...) shifts when Windows Update reinstalls the driver,
/// so it must be discovered, never hard-coded.
/// </summary>
public static class AdapterLocator
{
    public const string DisplayClassPath =
        @"SYSTEM\CurrentControlSet\Control\Class\{4d36e968-e325-11ce-bfc1-08002be10318}";

    public static string? Locate()
    {
        try
        {
            using var cls = Registry.LocalMachine.OpenSubKey(DisplayClassPath);
            if (cls is null) return null;

            foreach (var sub in cls.GetSubKeyNames())
            {
                if (sub.Length != 4 || !sub.All(char.IsAsciiDigit)) continue;
                using var k = cls.OpenSubKey(sub);
                if (k is null) continue;

                var mid = k.GetValue("MatchingDeviceId") as string;
                if (mid is null || !mid.StartsWith("PCI\\VEN_1002", StringComparison.OrdinalIgnoreCase))
                    continue;

                // Skip Microsoft/indirect display virtual adapters.
                var desc = k.GetValue("DriverDesc") as string ?? "";
                if (desc.Contains("Remote Display", StringComparison.OrdinalIgnoreCase))
                    continue;

                return sub;
            }
        }
        catch
        {
            return null;
        }
        return null;
    }

    public static AdapterInfo? GetInfo(string keyName)
    {
        try
        {
            var path = $@"{DisplayClassPath}\{keyName}";
            using var k = Registry.LocalMachine.OpenSubKey(path);
            if (k is null) return null;
            return new AdapterInfo(
                keyName,
                path,
                k.GetValue("MatchingDeviceId") as string ?? "",
                k.GetValue("DriverDesc") as string ?? "",
                k.GetValue("DriverVersion") as string ?? "",
                k.GetValue("DriverDate") as string ?? "");
        }
        catch
        {
            return null;
        }
    }
}
