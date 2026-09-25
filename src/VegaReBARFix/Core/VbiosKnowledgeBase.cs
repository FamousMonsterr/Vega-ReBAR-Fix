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
    /// Known table. Verdicts are conservative: the Guru3D unlock author ran the
    /// trio on 100% stock motherboard BIOS + stock vBIOS with success, and says
    /// GPU-Z's "GPU hardware support" row is ignorable — so a stock ROM is NOT
    /// proven to block ReBAR. A resized BAR is the only hard evidence either way.
    /// </summary>
    public static readonly Entry[] Known =
    [
        new("113-D050", "Radeon RX Vega 56 (reference/Sapphire stock)", Verdict.Unknown,
            "stock ROM; Guru3D reports the trio working on stock vBIOS — check the board BIOS first"),
        new("113-D046", "Radeon RX Vega 64 (reference stock)", Verdict.Unknown,
            "stock ROM; Guru3D reports the trio working on stock vBIOS — check the board BIOS first"),
    ];
}
