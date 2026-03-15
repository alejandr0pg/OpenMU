namespace MUnique.OpenMU.GameLogic.PlugIns;

/// <summary>
/// Static helper for VIP status checks and benefit multipliers.
/// </summary>
public static class VipHelper
{
    /// <summary>
    /// Checks whether the player has an active VIP subscription.
    /// </summary>
    public static bool IsVip(Player player)
        => player.Account?.VipExpiresAt > DateTime.UtcNow;

    /// <summary>Experience multiplier (1.5x for VIP).</summary>
    public static float ExpMultiplier(Player player)
        => IsVip(player) ? 1.5f : 1.0f;

    /// <summary>Item drop rate multiplier (1.3x for VIP).</summary>
    public static float DropMultiplier(Player player)
        => IsVip(player) ? 1.3f : 1.0f;

    /// <summary>Zen drop multiplier (1.2x for VIP).</summary>
    public static float ZenMultiplier(Player player)
        => IsVip(player) ? 1.2f : 1.0f;

    /// <summary>Max slot machine bet (5M for VIP, 1M normal).</summary>
    public static long MaxSlotBet(Player player)
        => IsVip(player) ? 5_000_000L : 1_000_000L;

    /// <summary>Max roulette bet (25M for VIP, 5M normal).</summary>
    public static long MaxRouletteBet(Player player)
        => IsVip(player) ? 25_000_000L : 5_000_000L;
}
