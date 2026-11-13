using System;
using System.Collections.Generic;
using UnityEngine;

namespace SlotGame.Configuration
{
    /// <summary>
    /// Shared designer configuration for the game.
    /// Validate before using its entries to initialize gameplay.
    /// </summary>
    [CreateAssetMenu(
        fileName = "SO_SlotGameConfig",
        menuName = "Slot Game/Game Config")]
    public sealed class SlotGameConfig : ScriptableObject
    {
        #region Inspector Configuration

        [Header("Session")]
        [Tooltip("Credits granted when a new session begins.")]
        [Min(1)]
        [SerializeField] private int startingCredits = 1000;

        [Header("Bet Presets")]
        [Tooltip("Distinct positive bet amounts available to the player.")]
        [SerializeField] private int[] betPresets = Array.Empty<int>();

        [Header("Symbols")]
        [Tooltip("Exactly one definition for each symbol ID. Weights are relative.")]
        [SerializeField]
        private SlotSymbolDefinition[] symbols =
            new SlotSymbolDefinition[4];

        #endregion

        #region Public Access

        public int StartingCredits => startingCredits;
        public int BetPresetCount => betPresets?.Length ?? 0;
        public int SymbolCount => symbols?.Length ?? 0;

        public int GetBetPreset(int index)
        {
            return betPresets[index];
        }

        public SlotSymbolDefinition GetSymbol(int index)
        {
            return symbols[index];
        }

        #endregion

        #region Validation

        /// <summary>
        /// Checks configuration without modifying it.
        /// Returns the first failure through error, or an empty string on success.
        /// </summary>
        public bool TryValidate(out string error)
        {
            if (startingCredits <= 0)
            {
                error = "Starting Credits must be greater than zero.";
                return false;
            }

            if (betPresets == null || betPresets.Length == 0)
            {
                error = "Add at least one bet preset.";
                return false;
            }

            HashSet<int> uniqueBets = new HashSet<int>();
            int minimumBet = int.MaxValue;
            int maximumBet = 0;

            for (int i = 0; i < betPresets.Length; i++)
            {
                int bet = betPresets[i];

                if (bet <= 0)
                {
                    error = $"Bet preset {i + 1} must be greater than zero.";
                    return false;
                }

                if (!uniqueBets.Add(bet))
                {
                    error = $"Bet preset {i + 1} repeats the amount {bet}.";
                    return false;
                }

                minimumBet = Math.Min(minimumBet, bet);
                maximumBet = Math.Max(maximumBet, bet);
            }

            if (startingCredits < minimumBet)
            {
                error = "Starting Credits must cover at least the lowest bet.";
                return false;
            }

            int requiredSymbolCount = Enum.GetValues(typeof(SlotSymbolId)).Length;

            if (symbols == null || symbols.Length != requiredSymbolCount)
            {
                error =
                    $"Configure exactly {requiredSymbolCount} symbol definitions, " +
                    "one for each symbol ID.";
                return false;
            }

            HashSet<SlotSymbolId> uniqueIds = new HashSet<SlotSymbolId>();
            double totalWeight = 0d;

            for (int i = 0; i < symbols.Length; i++)
            {
                SlotSymbolDefinition symbol = symbols[i];

                if (symbol == null)
                {
                    error = $"Symbol entry {i + 1} is missing.";
                    return false;
                }

                if (!Enum.IsDefined(typeof(SlotSymbolId), symbol.Id))
                {
                    error = $"Symbol entry {i + 1} has an invalid ID.";
                    return false;
                }

                if (!uniqueIds.Add(symbol.Id))
                {
                    error = $"Symbol ID {symbol.Id} is configured more than once.";
                    return false;
                }

                if (string.IsNullOrWhiteSpace(symbol.DisplayName))
                {
                    error = $"Symbol {symbol.Id} needs a display name.";
                    return false;
                }

                if (symbol.Sprite == null)
                {
                    error = $"Symbol {symbol.Id} needs a sprite.";
                    return false;
                }

                float weight = symbol.Weight;

                if (float.IsNaN(weight) ||
                    float.IsInfinity(weight) ||
                    weight < 0f)
                {
                    error =
                        $"Symbol {symbol.Id} needs a finite, nonnegative weight.";
                    return false;
                }

                totalWeight += weight;

                if (symbol.PayoutMultiplier <= 0)
                {
                    error =
                        $"Symbol {symbol.Id} needs a payout multiplier " +
                        "greater than zero.";
                    return false;
                }

                // Widen before multiplying so the validation cannot overflow.
                long maximumPayout =
                    (long)maximumBet * symbol.PayoutMultiplier;

                if (maximumPayout > int.MaxValue)
                {
                    error =
                        $"Symbol {symbol.Id}'s payout exceeds the supported " +
                        "integer range at the highest bet. Reduce its " +
                        "multiplier or the highest bet.";
                    return false;
                }
            }

            if (totalWeight <= 0d)
            {
                error = "At least one symbol must have a weight greater than zero.";
                return false;
            }

            error = string.Empty;
            return true;
        }

        [ContextMenu("Validate Configuration")]
        private void ValidateConfiguration()
        {
            if (TryValidate(out string error))
            {
                Debug.Log($"{name}: Configuration is valid.", this);
            }
            else
            {
                Debug.LogError($"{name}: {error}", this);
            }
        }

        #endregion
    }
}