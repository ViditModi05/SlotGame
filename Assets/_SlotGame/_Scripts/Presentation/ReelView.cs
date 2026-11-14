using System;
using System.Collections;
using SlotGame.Configuration;
using UnityEngine.UI;
using UnityEngine;

namespace SlotGame.Presentation
{
    /// <summary>
    /// Displays and animates one masked reel toward a supplied symbol.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RectMask2D))]
    public sealed class ReelView : MonoBehaviour
    {
        private const int CellCount = 5;
        private const int CenterCellIndex = CellCount / 2;

        [Header("References")]
        [Tooltip("Shared configuration containing the symbol artwork.")]
        [SerializeField] private SlotGameConfig config;

        [Tooltip("This reel's existing CenterSymbol Image.")]
        [SerializeField] private Image centerSymbol;

        [Header("Layout")]
        [Tooltip("Maximum symbol width as a fraction of the reel window's width.")]
        [Range(0.1f, 1f)]
        [SerializeField] private float symbolWidthRatio = 0.85f;

        [Header("Spin Animation")]
        [Tooltip("Complete passes through all symbols before reaching the target.")]
        [Range(1, 12)]
        [SerializeField] private int fullRotations = 4;

        [Tooltip("Fraction of the spin duration spent accelerating.")]
        [Range(0.05f, 0.45f)]
        [SerializeField] private float accelerationPortion = 0.2f;

        [Tooltip("Fraction of the spin duration spent slowing down.")]
        [Range(0.05f, 0.45f)]
        [SerializeField] private float decelerationPortion = 0.3f;

        private RectTransform viewport;

        private Outline winOutline;
        private Image[] symbolImages;
        private Coroutine spinRoutine;
        private Action completionCallback;
        private int centerSymbolIndex;
        private float scrollOffset;
        private bool isInitialized;

        public bool IsSpinning { get; private set; }

        private void Awake()
        {
            viewport = (RectTransform)transform;

            if (config == null ||
                centerSymbol == null ||
                centerSymbol.transform.parent != transform)
            {
                Debug.LogError(
                    $"{name}: Assign the configuration and this reel's " +
                    "direct CenterSymbol child.",
                    this);

                enabled = false;
                return;
            }

            if (!config.TryValidate(out string error))
            {
                Debug.LogError($"{name}: {error}", this);
                enabled = false;
                return;
            }

            winOutline = centerSymbol.GetComponent<UnityEngine.UI.Outline>();

            if (winOutline == null)
            {
                Debug.LogError(
                    $"{name}: CenterSymbol needs a UI Outline component.",
                    this);

                enabled = false;
                return;
            }

            // Buffer cells must inherit a disabled outline when cloned.
            winOutline.enabled = false;

            CreateSymbolImages();
            isInitialized = true;

            ShowSymbol(config.GetSymbol(0).Id);
        }

        private void OnEnable()
        {
            if (isInitialized)
            {
                RefreshLayout();
            }
        }

        private void OnDisable()
        {
            if (spinRoutine != null)
            {
                StopCoroutine(spinRoutine);
            }

            spinRoutine = null;
            completionCallback = null;
            IsSpinning = false;
            scrollOffset = 0f;

            if (winOutline != null)
            {
                winOutline.enabled = false;
            }
        }

        private void OnRectTransformDimensionsChange()
        {
            if (isInitialized)
            {
                RefreshLayout();
            }
        }

        /// <summary>
        /// Immediately displays a symbol while the reel is idle.
        /// </summary>
        public void ShowSymbol(SlotSymbolId symbolId)
        {
            EnsureInitialized();

            if (IsSpinning)
            {
                throw new InvalidOperationException(
                    "Cannot replace the displayed symbol during a spin.");
            }

            centerSymbolIndex = FindSymbolIndex(symbolId);
            scrollOffset = 0f;

            SetHighlighted(false);
            RefreshSprites();
            RefreshLayout();
        }


        /// <summary>
        /// Controls winning feedback on the idle center symbol.
        /// </summary>
        public void SetHighlighted(bool highlighted)
        {
            EnsureInitialized();

            if (highlighted && IsSpinning)
            {
                throw new InvalidOperationException(
                    "Cannot highlight a winning symbol while the reel is spinning.");
            }

            winOutline.enabled = highlighted;
        }

        /// <summary>
        /// Animates toward the supplied target and invokes completion once
        /// after landing. Disabling the view cancels without completion.
        /// </summary>
        public void SpinTo(
            SlotSymbolId targetSymbol,
            float duration,
            Action onComplete = null)
        {
            EnsureInitialized();

            if (!isActiveAndEnabled || IsSpinning)
            {
                throw new InvalidOperationException(
                    "The reel must be active and idle before spinning.");
            }

            if (float.IsNaN(duration) ||
                float.IsInfinity(duration) ||
                duration <= 0f)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(duration),
                    "Spin duration must be finite and greater than zero.");
            }

            int targetIndex = FindSymbolIndex(targetSymbol);
            int startingIndex = centerSymbolIndex;

            int stepsToTarget =
                (targetIndex - startingIndex + config.SymbolCount) %
                config.SymbolCount;

            int totalCells =
                Mathf.Clamp(fullRotations, 1, 12) * config.SymbolCount +
                stepsToTarget;

            float acceleration =
                Mathf.Clamp(accelerationPortion, 0.05f, 0.45f);

            float deceleration =
                Mathf.Clamp(decelerationPortion, 0.05f, 0.45f);

            SetHighlighted(false);

            completionCallback = onComplete;
            IsSpinning = true;

            spinRoutine = StartCoroutine(AnimateSpin(
                startingIndex,
                totalCells,
                duration,
                acceleration,
                deceleration));
        }

        private IEnumerator AnimateSpin(
            int startingIndex,
            int totalCells,
            float duration,
            float acceleration,
            float deceleration)
        {
            float elapsed = 0f;

            while (elapsed < duration)
            {
                yield return null;

                elapsed = Mathf.Min(elapsed + Time.deltaTime, duration);

                float progress = EvaluateProgress(
                    elapsed / duration,
                    acceleration,
                    deceleration);

                ApplyTravel(startingIndex, totalCells * progress);
            }

            ApplyTravel(startingIndex, totalCells);

            spinRoutine = null;
            IsSpinning = false;

            Action callback = completionCallback;
            completionCallback = null;

            callback?.Invoke();
        }

        private static float EvaluateProgress(
            float time,
            float acceleration,
            float deceleration)
        {
            float peakSpeed =
                1f / (1f - (acceleration + deceleration) * 0.5f);

            if (time < acceleration)
            {
                return peakSpeed * time * time / (2f * acceleration);
            }

            if (time <= 1f - deceleration)
            {
                return peakSpeed * (time - acceleration * 0.5f);
            }

            float remainingTime = 1f - time;

            return 1f -
                peakSpeed * remainingTime * remainingTime /
                (2f * deceleration);
        }

        private void ApplyTravel(int startingIndex, float travel)
        {
            int wholeCells = Mathf.FloorToInt(travel);

            int nextCenterIndex =
                (startingIndex + wholeCells) % config.SymbolCount;

            if (nextCenterIndex != centerSymbolIndex)
            {
                centerSymbolIndex = nextCenterIndex;
                RefreshSprites();
            }

            scrollOffset = travel - wholeCells;
            RefreshLayout();
        }

        private void RefreshSprites()
        {
            for (int i = 0; i < symbolImages.Length; i++)
            {
                int offset = i - CenterCellIndex;

                int symbolIndex =
                    (centerSymbolIndex + offset + config.SymbolCount) %
                    config.SymbolCount;

                symbolImages[i].sprite =
                    config.GetSymbol(symbolIndex).Sprite;
            }
        }

        private void CreateSymbolImages()
        {
            symbolImages = new Image[CellCount];

            for (int i = 0; i < symbolImages.Length; i++)
            {
                Image image;

                if (i == CenterCellIndex)
                {
                    image = centerSymbol;
                }
                else
                {
                    image = Instantiate(centerSymbol, viewport);
                    image.name = $"SymbolCell{i}";
                }

                RectTransform rect = image.rectTransform;

                rect.anchorMin = new Vector2(0.5f, 0.5f);
                rect.anchorMax = new Vector2(0.5f, 0.5f);
                rect.pivot = new Vector2(0.5f, 0.5f);
                rect.localScale = Vector3.one;

                image.preserveAspect = true;
                image.raycastTarget = false;

                symbolImages[i] = image;
            }
        }

        private void RefreshLayout()
        {
            float cellHeight = viewport.rect.height * 0.5f;

            float symbolSize = Mathf.Min(
                viewport.rect.width * symbolWidthRatio,
                cellHeight * 0.9f);

            for (int i = 0; i < symbolImages.Length; i++)
            {
                RectTransform rect = symbolImages[i].rectTransform;

                rect.sizeDelta = new Vector2(symbolSize, symbolSize);
                rect.anchoredPosition = new Vector2(
                    0f,
                    (i - CenterCellIndex - scrollOffset) * cellHeight);
            }
        }

        private int FindSymbolIndex(SlotSymbolId symbolId)
        {
            for (int i = 0; i < config.SymbolCount; i++)
            {
                if (config.GetSymbol(i).Id == symbolId)
                {
                    return i;
                }
            }

            throw new ArgumentOutOfRangeException(
                nameof(symbolId),
                symbolId,
                "The symbol is not configured.");
        }

        private void EnsureInitialized()
        {
            if (!isInitialized)
            {
                throw new InvalidOperationException(
                    "Initialize the reel before using it.");
            }
        }

#if UNITY_EDITOR
        [ContextMenu("Preview Spin")]
        private void PreviewSpin()
        {
            if (!Application.isPlaying || !isInitialized || IsSpinning)
            {
                Debug.LogWarning(
                    "Preview requires an initialized, idle reel in Play Mode.",
                    this);

                return;
            }

            int nextIndex =
                (centerSymbolIndex + 1) % config.SymbolCount;

            SpinTo(config.GetSymbol(nextIndex).Id, 2f, () =>
            {
                SetHighlighted(true);

                Debug.Log(
                    $"{name}: Preview finished on " +
                    $"{config.GetSymbol(centerSymbolIndex).DisplayName}.",
                    this);
            });
        }
#endif
    }
}