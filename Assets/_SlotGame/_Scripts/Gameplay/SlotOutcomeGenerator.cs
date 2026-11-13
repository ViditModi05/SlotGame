using System;
using SlotGame.Configuration;

namespace SlotGame.Gameplay
{
    /// <summary>
    /// Generates committed three-reel outcomes from a validated configuration snapshot.
    /// </summary>
    public sealed class SlotOutcomeGenerator
    {
        #region Runtime Data

        private readonly Random random;
        private readonly SlotSymbolId[] symbolIds;
        private readonly int[] payoutMultipliers;
        private readonly double[] cumulativeWeights;
        private readonly int[] betPresets;

        private readonly double totalWeight;
        private readonly int lastSelectableIndex;

        #endregion

        #region Construction

        /// <summary>
        /// Copies configuration for this generator's lifetime.
        /// Supply a long-lived RNG reserved for gameplay outcomes.
        /// </summary>
        public SlotOutcomeGenerator(SlotGameConfig config, Random random)
        {
            if (config == null)
            {
                throw new ArgumentNullException(nameof(config));
            }

            if (random == null)
            {
                throw new ArgumentNullException(nameof(random));
            }

            if (!config.TryValidate(out string error))
            {
                throw new ArgumentException(error, nameof(config));
            }

            this.random = random;

            symbolIds = new SlotSymbolId[config.SymbolCount];
            payoutMultipliers = new int[config.SymbolCount];
            cumulativeWeights = new double[config.SymbolCount];
            betPresets = new int[config.BetPresetCount];

            double cumulativeWeight = 0d;
            int lastPositiveIndex = 0;

            for (int i = 0; i < config.SymbolCount; i++)
            {
                SlotSymbolDefinition symbol = config.GetSymbol(i);

                symbolIds[i] = symbol.Id;
                payoutMultipliers[i] = symbol.PayoutMultiplier;

                cumulativeWeight += symbol.Weight;
                cumulativeWeights[i] = cumulativeWeight;

                if (symbol.Weight > 0f)
                {
                    lastPositiveIndex = i;
                }
            }

            totalWeight = cumulativeWeight;
            lastSelectableIndex = lastPositiveIndex;

            for (int i = 0; i < config.BetPresetCount; i++)
            {
                betPresets[i] = config.GetBetPreset(i);
            }
        }

        #endregion

        #region Outcome Generation

        /// <summary>
        /// Makes three weighted selections and captures their bet and payout.
        /// The bet must match one of the copied configuration presets.
        /// </summary>
        public SpinResult Generate(int bet)
        {
            if (!IsBetPreset(bet))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(bet),
                    bet,
                    "The bet must match a configured preset.");
            }

            int leftIndex = SelectSymbolIndex();
            int middleIndex = SelectSymbolIndex();
            int rightIndex = SelectSymbolIndex();

            bool isMatch =
                symbolIds[leftIndex] == symbolIds[middleIndex] &&
                symbolIds[middleIndex] == symbolIds[rightIndex];

            int payout = isMatch
                ? checked(bet * payoutMultipliers[leftIndex])
                : 0;

            return new SpinResult(
                symbolIds[leftIndex],
                symbolIds[middleIndex],
                symbolIds[rightIndex],
                bet,
                payout);
        }

        private int SelectSymbolIndex()
        {
            double draw = random.NextDouble() * totalWeight;

            for (int i = 0; i < cumulativeWeights.Length; i++)
            {
                if (draw < cumulativeWeights[i])
                {
                    return i;
                }
            }

            // Multiplication can round onto the upper endpoint (i.e, 1).
            // Preserve zero-weight exclusion when using the final interval.
            return lastSelectableIndex;
        }

        private bool IsBetPreset(int bet)
        {
            for (int i = 0; i < betPresets.Length; i++)
            {
                if (betPresets[i] == bet)
                {
                    return true;
                }
            }

            return false;
        }

        #endregion
    }
}