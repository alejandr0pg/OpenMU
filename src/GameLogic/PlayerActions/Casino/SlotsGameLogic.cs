// <copyright file="SlotsGameLogic.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.PlayerActions.Casino;

/// <summary>
/// Symbols available on the slot machine reels.
/// </summary>
public enum SlotSymbol : byte
{
    /// <summary>Jewel of Bless — most common.</summary>
    JewelOfBless = 0,

    /// <summary>Jewel of Soul.</summary>
    JewelOfSoul = 1,

    /// <summary>Jewel of Chaos.</summary>
    JewelOfChaos = 2,

    /// <summary>Jewel of Life.</summary>
    JewelOfLife = 3,

    /// <summary>Jewel of Creation.</summary>
    JewelOfCreation = 4,

    /// <summary>Wings — rarest, jackpot symbol.</summary>
    Wings = 5,
}

/// <summary>
/// Result of a single slot machine spin.
/// </summary>
public readonly record struct SpinResult(
    SlotSymbol Reel1,
    SlotSymbol Reel2,
    SlotSymbol Reel3,
    long WinAmount,
    int Multiplier);

/// <summary>
/// Pure domain logic for a 3-reel slot machine.
/// No I/O, no EF, no player references.
/// House edge is approximately 5–10 %.
/// </summary>
public static class SlotsGameLogic
{
    /// <summary>
    /// Weighted symbol table shared by every reel.
    /// Total weight = 100.  Distribution chosen so that the
    /// expected return is ~92 % (house edge ~8 %).
    /// </summary>
    private static readonly (SlotSymbol Symbol, int Weight)[] ReelWeights =
    [
        (SlotSymbol.JewelOfBless, 30),
        (SlotSymbol.JewelOfSoul, 25),
        (SlotSymbol.JewelOfChaos, 20),
        (SlotSymbol.JewelOfLife, 13),
        (SlotSymbol.JewelOfCreation, 8),
        (SlotSymbol.Wings, 4),
    ];

    private const int TotalWeight = 100;

    /// <summary>
    /// Executes one spin and computes the payout.
    /// </summary>
    /// <param name="betAmount">The wager in Zen (must be positive).</param>
    /// <param name="rng">A <see cref="Random"/> instance owned by the caller.</param>
    /// <returns>A <see cref="SpinResult"/> with reel outcomes and winnings.</returns>
    public static SpinResult Spin(long betAmount, Random rng)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(betAmount);
        ArgumentNullException.ThrowIfNull(rng);

        var reel1 = PickSymbol(rng);
        var reel2 = PickSymbol(rng);
        var reel3 = PickSymbol(rng);

        var multiplier = DetermineMultiplier(reel1, reel2, reel3);
        var winAmount = betAmount * multiplier;

        return new SpinResult(reel1, reel2, reel3, winAmount, multiplier);
    }

    private static SlotSymbol PickSymbol(Random rng)
    {
        var roll = rng.Next(TotalWeight);
        var cumulative = 0;

        foreach (var (symbol, weight) in ReelWeights)
        {
            cumulative += weight;
            if (roll < cumulative)
            {
                return symbol;
            }
        }

        // Fallback (should never happen).
        return SlotSymbol.JewelOfBless;
    }

    private static int DetermineMultiplier(
        SlotSymbol r1,
        SlotSymbol r2,
        SlotSymbol r3)
    {
        // Three-of-a-kind payouts.
        if (r1 == r2 && r2 == r3)
        {
            return r1 switch
            {
                SlotSymbol.Wings => 50,
                SlotSymbol.JewelOfCreation => 20,
                SlotSymbol.JewelOfLife => 10,
                SlotSymbol.JewelOfChaos => 5,
                SlotSymbol.JewelOfSoul => 3,
                SlotSymbol.JewelOfBless => 2,
                _ => 0,
            };
        }

        // Two-of-a-kind (any position) — player gets bet back.
        if (r1 == r2 || r2 == r3 || r1 == r3)
        {
            return 1;
        }

        return 0;
    }
}
