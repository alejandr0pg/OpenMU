// <copyright file="RouletteGameLogic.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.PlayerActions.Casino;

/// <summary>
/// Bet types for European roulette.
/// </summary>
public enum RouletteBetType : byte
{
    /// <summary>Straight-up bet on a single number (0-36).</summary>
    Number = 0,

    /// <summary>Red color bet.</summary>
    Red = 1,

    /// <summary>Black color bet.</summary>
    Black = 2,

    /// <summary>Even numbers bet.</summary>
    Even = 3,

    /// <summary>Odd numbers bet.</summary>
    Odd = 4,

    /// <summary>Low numbers 1-18.</summary>
    Low1to18 = 5,

    /// <summary>High numbers 19-36.</summary>
    High19to36 = 6,

    /// <summary>First dozen (1-12).</summary>
    Dozen1 = 7,

    /// <summary>Second dozen (13-24).</summary>
    Dozen2 = 8,

    /// <summary>Third dozen (25-36).</summary>
    Dozen3 = 9,

    /// <summary>First column (1,4,7,10,13,16,19,22,25,28,31,34).</summary>
    Column1 = 10,

    /// <summary>Second column (2,5,8,11,14,17,20,23,26,29,32,35).</summary>
    Column2 = 11,

    /// <summary>Third column (3,6,9,12,15,18,21,24,27,30,33,36).</summary>
    Column3 = 12,
}

/// <summary>
/// Result of a single roulette spin.
/// </summary>
public readonly record struct RouletteResult(
    byte WinningNumber,
    bool IsRed,
    bool IsBlack,
    bool IsGreen);

/// <summary>
/// Pure domain logic for European roulette (single zero, 0-36).
/// No I/O, no player references.
/// </summary>
public static class RouletteGameLogic
{
    /// <summary>
    /// The 18 red numbers on a European roulette wheel.
    /// </summary>
    private static readonly HashSet<byte> RedNumbers =
        [1, 3, 5, 7, 9, 12, 14, 16, 18, 19, 21, 23, 25, 27, 30, 32, 34, 36];

    /// <summary>
    /// Executes one spin of the roulette wheel.
    /// </summary>
    /// <param name="rng">A <see cref="Random"/> instance owned by the caller.</param>
    /// <returns>A <see cref="RouletteResult"/> with the winning number and color.</returns>
    public static RouletteResult Spin(Random rng)
    {
        ArgumentNullException.ThrowIfNull(rng);

        var number = (byte)rng.Next(0, 37); // 0-36 inclusive
        var isRed = RedNumbers.Contains(number);
        var isGreen = number == 0;
        var isBlack = !isRed && !isGreen;

        return new RouletteResult(number, isRed, isBlack, isGreen);
    }

    /// <summary>
    /// Calculates the payout for a given bet against the winning number.
    /// </summary>
    /// <param name="betType">The type of bet placed.</param>
    /// <param name="betNumber">The specific number for <see cref="RouletteBetType.Number"/> bets (0-36).</param>
    /// <param name="winningNumber">The number the wheel landed on.</param>
    /// <param name="betAmount">The wager in Zen.</param>
    /// <returns>Total payout including the original bet, or 0 if the bet lost.</returns>
    public static long CalculatePayout(
        RouletteBetType betType,
        byte betNumber,
        byte winningNumber,
        long betAmount)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(betAmount);

        var isWin = betType switch
        {
            RouletteBetType.Number => betNumber == winningNumber,
            RouletteBetType.Red => winningNumber > 0 && RedNumbers.Contains(winningNumber),
            RouletteBetType.Black => winningNumber > 0 && !RedNumbers.Contains(winningNumber),
            RouletteBetType.Even => winningNumber > 0 && winningNumber % 2 == 0,
            RouletteBetType.Odd => winningNumber > 0 && winningNumber % 2 == 1,
            RouletteBetType.Low1to18 => winningNumber >= 1 && winningNumber <= 18,
            RouletteBetType.High19to36 => winningNumber >= 19 && winningNumber <= 36,
            RouletteBetType.Dozen1 => winningNumber >= 1 && winningNumber <= 12,
            RouletteBetType.Dozen2 => winningNumber >= 13 && winningNumber <= 24,
            RouletteBetType.Dozen3 => winningNumber >= 25 && winningNumber <= 36,
            RouletteBetType.Column1 => winningNumber > 0 && winningNumber % 3 == 1,
            RouletteBetType.Column2 => winningNumber > 0 && winningNumber % 3 == 2,
            RouletteBetType.Column3 => winningNumber > 0 && winningNumber % 3 == 0,
            _ => false,
        };

        if (!isWin)
        {
            return 0;
        }

        return betType switch
        {
            RouletteBetType.Number => (betAmount * 35) + betAmount,
            RouletteBetType.Dozen1 or RouletteBetType.Dozen2 or RouletteBetType.Dozen3
                => (betAmount * 2) + betAmount,
            RouletteBetType.Column1 or RouletteBetType.Column2 or RouletteBetType.Column3
                => (betAmount * 2) + betAmount,
            _ => betAmount + betAmount, // Even-money bets: 1x + bet back
        };
    }

    /// <summary>
    /// Validates that the bet number is valid for the given bet type.
    /// </summary>
    public static bool IsValidBet(RouletteBetType betType, byte betNumber)
    {
        if (betType == RouletteBetType.Number)
        {
            return betNumber <= 36;
        }

        return betType is >= RouletteBetType.Red and <= RouletteBetType.Column3;
    }
}
