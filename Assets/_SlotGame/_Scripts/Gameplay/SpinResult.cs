using System;
using SlotGame.Configuration;

namespace SlotGame.Gameplay
{
    /// <summary>
    /// Read-only outcome of one spin, retained through animation and settlement.
    /// </summary>
    public sealed class SpinResult
    {
        public SlotSymbolId LeftSymbol { get; }
        public SlotSymbolId MiddleSymbol { get; }
        public SlotSymbolId RightSymbol { get; }

        public int Bet { get; }
        public int Payout { get; }

        public bool IsWin => Payout > 0;

        /// <summary>
        /// Captures outcome data supplied by the game's outcome generator.
        /// </summary>
        internal SpinResult(
            SlotSymbolId leftSymbol,
            SlotSymbolId middleSymbol,
            SlotSymbolId rightSymbol,
            int bet,
            int payout)
        {
            if (bet <= 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(bet),
                    "The committed bet must be greater than zero.");
            }

            if (payout < 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(payout),
                    "The payout cannot be negative.");
            }

            LeftSymbol = leftSymbol;
            MiddleSymbol = middleSymbol;
            RightSymbol = rightSymbol;
            Bet = bet;
            Payout = payout;
        }
    }
}