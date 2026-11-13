using System;
using UnityEngine;

namespace SlotGame.Configuration
{
    /// <summary>
    /// Designer configuration for one symbol, stored within SlotGameConfig.
    /// </summary>
    [Serializable]
    public sealed class SlotSymbolDefinition
    {
        [Header("Identity")]
        [Tooltip("Stable identity used to compare symbols and evaluate wins.")]
        [SerializeField] private SlotSymbolId id;

        [Header("Presentation")]
        [Tooltip("Player-facing symbol name used in payout information.")]
        [SerializeField] private string displayName;

        [Tooltip("Artwork displayed for this symbol. Bar uses the full triple-BAR image.")]
        [SerializeField] private Sprite sprite;

        [Header("Outcome Settings")]
        [Tooltip("Relative selection weight for each reel. Zero excludes this symbol.")]
        [Min(0f)]
        [SerializeField] private float weight = 1f;

        [Tooltip("A matching three-symbol win awards the committed bet multiplied by this value.")]
        [Min(1)]
        [SerializeField] private int payoutMultiplier = 1;

        public SlotSymbolId Id => id;
        public string DisplayName => displayName;
        public Sprite Sprite => sprite;
        public float Weight => weight;
        public int PayoutMultiplier => payoutMultiplier;
    }
}