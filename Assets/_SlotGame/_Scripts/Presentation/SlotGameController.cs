using System;
using SlotGame.Configuration;
using SlotGame.Gameplay;
using UnityEngine;

namespace SlotGame.Presentation
{
    /// <summary>
    /// Connects session rules, player input and slot-game presentation.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SlotGameController : MonoBehaviour
    {
        private const int ReelCount = 3;

        [Header("Configuration")]
        [Tooltip("Shared configuration also used by the reel prefab.")]
        [SerializeField] private SlotGameConfig config;

        [Header("Presentation")]
        [Tooltip("Scene view containing the labels, buttons and popup.")]
        [SerializeField] private SlotGameView view;

        [Tooltip("Scene instance occupying the left reel opening.")]
        [SerializeField] private ReelView leftReel;

        [Tooltip("Scene instance occupying the middle reel opening.")]
        [SerializeField] private ReelView middleReel;

        [Tooltip("Scene instance occupying the right reel opening.")]
        [SerializeField] private ReelView rightReel;

        [Tooltip("Scene lever presentation.")]
        [SerializeField] private LeverView lever;

        [Header("Spin Timing")]
        [Tooltip("Duration of the left reel's spin, in seconds.")]
        [Min(0.1f)]
        [SerializeField] private float spinDuration = 2f;

        [Tooltip("Additional duration between consecutive reel stops.")]
        [Min(0f)]
        [SerializeField] private float stopStagger = 0.3f;

        private readonly bool[] finishedReels = new bool[ReelCount];

        private SlotSession session;
        private ReelView[] reels;
        private bool isInitialized;

        private void Awake()
        {
            if (config == null ||
                view == null ||
                leftReel == null ||
                middleReel == null ||
                rightReel == null ||
                lever == null ||
                leftReel == middleReel ||
                leftReel == rightReel ||
                middleReel == rightReel)
            {
                Debug.LogError(
                    $"{name}: Assign the configuration, view, lever " +
                    "and three distinct reel instances.",
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

            if (!HasValidSpinTiming())
            {
                Debug.LogError(
                    $"{name}: Spin duration must be finite and positive, " +
                    "and stop stagger must be finite and nonnegative.",
                    this);

                enabled = false;
                return;
            }

            reels = new[] { leftReel, middleReel, rightReel };
            session = new SlotSession(config, new System.Random());

            isInitialized = true;
        }

        private void OnEnable()
        {
            if (!isInitialized)
            {
                return;
            }

            view.SpinRequested += HandleSpinRequested;
            view.BetChangeRequested += HandleBetChangeRequested;
            view.NewSessionRequested += HandleNewSessionRequested;
            view.ResetConfirmed += HandleResetConfirmed;
            view.ResetCancelled += HandleResetCancelled;
        }

        private void Start()
        {
            if (!isInitialized)
            {
                return;
            }

            if (!IsPresentationReady())
            {
                Debug.LogError(
                    $"{name}: Keep the view, reels and lever active, " +
                    "and resolve any presentation setup errors.",
                    this);

                enabled = false;
                return;
            }

            view.SetPayouts(config);
            view.SetConfirmationVisible(false);

            ResetPresentation();
            Render();
            view.FocusMainControl();
        }

        private void OnDisable()
        {
            if (!isInitialized || view == null)
            {
                return;
            }

            view.SpinRequested -= HandleSpinRequested;
            view.BetChangeRequested -= HandleBetChangeRequested;
            view.NewSessionRequested -= HandleNewSessionRequested;
            view.ResetConfirmed -= HandleResetConfirmed;
            view.ResetCancelled -= HandleResetCancelled;
        }

        private void HandleSpinRequested()
        {
            if (session.IsSpinning || view.IsConfirmationVisible)
            {
                return;
            }

            if (!IsPresentationReady() || !HasValidSpinTiming())
            {
                Render("Unable to start the spin. Please try again.");
                view.FocusMainControl();
                return;
            }

            if (!session.TryBeginSpin(out SpinResult result, out string error))
            {
                Render(error);
                view.FocusMainControl();
                return;
            }

            Array.Clear(finishedReels, 0, finishedReels.Length);

            Render();

            lever.ResetPose();
            lever.PlayPull();

            leftReel.SpinTo(
                result.LeftSymbol,
                spinDuration,
                () => HandleReelCompleted(result, 0));

            middleReel.SpinTo(
                result.MiddleSymbol,
                spinDuration + stopStagger,
                () => HandleReelCompleted(result, 1));

            rightReel.SpinTo(
                result.RightSymbol,
                spinDuration + stopStagger * 2f,
                () => HandleReelCompleted(result, 2));
        }

        private void HandleReelCompleted(SpinResult expectedResult, int reelIndex)
        {
            // Ignore older spin callbacks and duplicate reel completions.
            if (this == null ||
                !isInitialized ||
                !ReferenceEquals(session.PendingResult, expectedResult) ||
                finishedReels[reelIndex])
            {
                return;
            }

            finishedReels[reelIndex] = true;

            for (int i = 0; i < finishedReels.Length; i++)
            {
                if (!finishedReels[i])
                {
                    return;
                }
            }

            if (!session.TryCompleteSpin())
            {
                return;
            }

            bool isWin = session.LatestResult.IsWin;

            for (int i = 0; i < reels.Length; i++)
            {
                reels[i].SetHighlighted(isWin);
            }

            if (!session.CanAffordAnyBet)
            {
                view.SetConfirmationVisible(true);
            }

            Render();
            view.FocusMainControl();
        }

        private void HandleBetChangeRequested(int direction)
        {
            if (session.IsSpinning || view.IsConfirmationVisible)
            {
                return;
            }

            int nextIndex = FindAdjacentBetIndex(direction);

            if (nextIndex < 0 || !session.TrySelectBet(nextIndex))
            {
                return;
            }

            Render();
            view.FocusMainControl();
        }

        private void HandleNewSessionRequested()
        {
            if (session.IsSpinning || view.IsConfirmationVisible)
            {
                return;
            }

            if (!IsPresentationReady())
            {
                Render("Please wait for the current animation.");
                view.FocusMainControl();
                return;
            }

            view.SetConfirmationVisible(true);
            Render();
        }

        private void HandleResetConfirmed()
        {
            if (session.IsSpinning || !view.IsConfirmationVisible)
            {
                return;
            }

            if (!IsPresentationReady())
            {
                Render("Please wait for the current animation.");
                view.FocusMainControl();
                return;
            }

            if (!session.TryReset())
            {
                return;
            }

            Array.Clear(finishedReels, 0, finishedReels.Length);

            view.SetConfirmationVisible(false);

            ResetPresentation();
            Render();
            view.FocusMainControl();
        }

        private void HandleResetCancelled()
        {
            if (session.IsSpinning || !view.IsConfirmationVisible)
            {
                return;
            }

            view.SetConfirmationVisible(false);
            Render();
            view.FocusMainControl();
        }

        private int FindAdjacentBetIndex(int direction)
        {
            if (direction == 0)
            {
                return -1;
            }

            int bestIndex = -1;
            int smallestDistance = int.MaxValue;

            for (int i = 0; i < session.BetPresetCount; i++)
            {
                int bet = session.GetBetPreset(i);

                int distance = direction < 0
                    ? session.SelectedBet - bet
                    : bet - session.SelectedBet;

                if (distance > 0 && distance < smallestDistance)
                {
                    smallestDistance = distance;
                    bestIndex = i;
                }
            }

            return bestIndex;
        }

        private void ResetPresentation()
        {
            SlotSymbolId initialSymbol = config.GetSymbol(0).Id;

            for (int i = 0; i < reels.Length; i++)
            {
                reels[i].ShowSymbol(initialSymbol);
            }

            lever.ResetPose();
        }

        private void Render(string message = null)
        {
            bool isIdle = !session.IsSpinning;

            view.Render(
                credits: session.Credits,
                selectedBet: session.SelectedBet,
                canSpin: session.CanSpin,
                canDecreaseBet: isIdle && FindAdjacentBetIndex(-1) >= 0,
                canIncreaseBet: isIdle && FindAdjacentBetIndex(1) >= 0,
                canStartNewSession: isIdle,
                message: message ?? BuildStatusMessage());
        }

        private string BuildStatusMessage()
        {
            if (session.IsSpinning)
            {
                return "Spinning...";
            }

            string message = "Ready to spin";

            if (session.LatestResult != null)
            {
                message = session.LatestResult.IsWin
                    ? $"Win: {session.LatestResult.Payout} credits!"
                    : "No match.";
            }

            if (!session.CanAffordAnyBet)
            {
                message += "\nStart a new session.";
            }
            else if (!session.CanAffordSelectedBet)
            {
                message += "\nSelect a smaller bet.";
            }
            else if (!session.CanSpin)
            {
                message += "\nChoose a smaller bet or reset.";
            }

            return message;
        }

        private bool IsPresentationReady()
        {
            if (view == null ||
                !view.isActiveAndEnabled ||
                lever == null ||
                !lever.isActiveAndEnabled)
            {
                return false;
            }

            for (int i = 0; i < reels.Length; i++)
            {
                if (reels[i] == null ||
                    !reels[i].isActiveAndEnabled ||
                    reels[i].IsSpinning)
                {
                    return false;
                }
            }

            return true;
        }

        private bool HasValidSpinTiming()
        {
            float lastDuration = spinDuration + stopStagger * 2f;

            return spinDuration > 0f &&
                stopStagger >= 0f &&
                !float.IsNaN(lastDuration) &&
                !float.IsInfinity(lastDuration);
        }
    }
}