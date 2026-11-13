using System;
using SlotGame.Configuration;

namespace SlotGame.Gameplay
{
    /// <summary>
    /// Owns session credits, bet selection and the accepted spin lifecycle.
    /// </summary>
    public sealed class SlotSession
    {
        #region Configuration Snapshot

        private readonly SlotOutcomeGenerator outcomeGenerator;
        private readonly int startingCredits;
        private readonly int[] betPresets;
        private readonly int minimumBetIndex;
        private readonly int maximumPayoutMultiplier;

        #endregion

        #region Session State

        public int Credits { get; private set; }
        public int SelectedBetIndex { get; private set; }

        public SpinResult PendingResult { get; private set; }
        public SpinResult LatestResult { get; private set; }

        public int SelectedBet => betPresets[SelectedBetIndex];
        public int MinimumBet => betPresets[minimumBetIndex];
        public int BetPresetCount => betPresets.Length;

        public bool IsSpinning => PendingResult != null;
        public bool CanAffordSelectedBet => Credits >= SelectedBet;
        public bool CanAffordAnyBet => Credits >= MinimumBet;

        public bool CanSpin =>
            !IsSpinning &&
            CanAffordSelectedBet &&
            HasPayoutCapacity;

        private bool HasPayoutCapacity =>
            (long)Credits - SelectedBet +
            (long)SelectedBet * maximumPayoutMultiplier <= int.MaxValue;

        #endregion

        #region Construction

        public SlotSession(SlotGameConfig config, Random random)
        {
            // The generator validates configuration before we copy session settings.
            outcomeGenerator = new SlotOutcomeGenerator(config, random);

            startingCredits = config.StartingCredits;
            betPresets = new int[config.BetPresetCount];

            int lowestIndex = 0;

            for (int i = 0; i < betPresets.Length; i++)
            {
                betPresets[i] = config.GetBetPreset(i);

                if (betPresets[i] < betPresets[lowestIndex])
                {
                    lowestIndex = i;
                }
            }

            minimumBetIndex = lowestIndex;

            int maximumMultiplier = 0;

            for (int i = 0; i < config.SymbolCount; i++)
            {
                SlotSymbolDefinition symbol = config.GetSymbol(i);

                if (symbol.Weight > 0f)
                {
                    maximumMultiplier = Math.Max(
                        maximumMultiplier,
                        symbol.PayoutMultiplier);
                }
            }

            maximumPayoutMultiplier = maximumMultiplier;

            ResetState();
        }

        #endregion

        #region Bet Selection

        public int GetBetPreset(int index)
        {
            return betPresets[index];
        }

        public bool TrySelectBet(int index)
        {
            if (IsSpinning || index < 0 || index >= betPresets.Length)
            {
                return false;
            }

            SelectedBetIndex = index;
            return true;
        }

        #endregion

        #region Spin Lifecycle

        /// <summary>
        /// Accepts one spin, deducts its bet and stores its committed result.
        /// Rejected requests leave session state and RNG untouched.
        /// </summary>
        public bool TryBeginSpin(out SpinResult result, out string error)
        {
            result = null;
            error = string.Empty;

            if (IsSpinning)
            {
                error = "Wait for the current spin to finish.";
                return false;
            }

            if (!CanAffordSelectedBet)
            {
                error = "Not enough credits for the selected bet.";
                return false;
            }

            // Reserve room for every possible payout before drawing an outcome.
            if (!HasPayoutCapacity)
            {
                error =
                    "The credit balance is too high for this bet's maximum payout. " +
                    "Select a smaller bet or start a new session.";
                return false;
            }

            SpinResult generatedResult = outcomeGenerator.Generate(SelectedBet);

            Credits -= generatedResult.Bet;
            PendingResult = generatedResult;
            LatestResult = null;

            result = generatedResult;
            return true;
        }

        /// <summary>
        /// Settles only the stored pending result, at most once.
        /// </summary>
        public bool TryCompleteSpin()
        {
            if (!IsSpinning)
            {
                return false;
            }

            Credits = checked(Credits + PendingResult.Payout);
            LatestResult = PendingResult;
            PendingResult = null;

            return true;
        }

        #endregion

        #region Session Reset

        /// <summary>
        /// Starts a fresh session when idle, without reseeding the gameplay RNG.
        /// </summary>
        public bool TryReset()
        {
            if (IsSpinning)
            {
                return false;
            }

            ResetState();
            return true;
        }

        private void ResetState()
        {
            Credits = startingCredits;
            SelectedBetIndex = minimumBetIndex;
            PendingResult = null;
            LatestResult = null;
        }

        #endregion
    }
}