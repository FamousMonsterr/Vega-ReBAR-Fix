namespace VegaReBARFix.Core;

/// <summary>
/// Community knowledge base of card vBIOS ROMs and their Resizable BAR support.
/// Seeded with the known AMD reference stock IDs; grows via GitHub Issues —
/// users report working/stock ROM IDs, new entries land here.
/// </summary>
public static class VbiosKnowledgeBase
{
    public enum Verdict { Supported, StockNoRebar, Unknown }

    public sealed record Entry(string IdPrefix, string Card, Verdict Verdict, string Source);

    /// <summary>Longest-prefix match against known ROM IDs.</summary>
    public static (Verdict Verdict, Entry? Known) Lookup(string? biosId)
    {
        if (string.IsNullOrEmpty(biosId)) return (Verdict.Unknown, null);
        foreach (var e in Known.OrderByDescending(k => k.IdPrefix.Length))
            if (biosId.StartsWith(e.IdPrefix, StringComparison.OrdinalIgnoreCase))
                return (e.Verdict, e);
        return (Verdict.Unknown, null);
    }

    /// <summary>
    /// Known table. Stock AMD reference ROMs ship without the PCIe ReBAR
    /// capability (GPU-Z: "GPU hardware support: Unsupported"). Modded ROMs
    /// from the TechPowerUp vBIOS collection keep the reference ID unless
    /// reflashed with a custom string — a resized BAR is the only hard proof.
    /// </summary>
    public static readonly Entry[] Known =
    [
        new("113-D050", "Radeon RX Vega 56 (reference)", Verdict.StockNoRebar,
            "TechPowerUp: stock Vega 10 ROMs ship without ReBAR capability"),
        new("113-D046", "Radeon RX Vega 64 (reference)", Verdict.StockNoRebar,
            "TechPowerUp: stock Vega 10 ROMs ship without ReBAR capability"),
        new("113-D001", "Radeon RX Vega 56/64 (Sapphire early)", Verdict.StockNoRebar,
            "Community reports: Sapphire reference-based stock ROM"),
    ];
}
