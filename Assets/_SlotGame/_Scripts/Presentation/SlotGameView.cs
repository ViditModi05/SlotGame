using System;
using System.Text;
using SlotGame.Configuration;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace SlotGame.Presentation
{
    /// <summary>
    /// Displays game state and emits player input requests.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SlotGameView : MonoBehaviour
    {
        [Header("Text")]
        [Tooltip("Displays the current credit balance.")]
        [SerializeField] private TMP_Text creditsText;

        [Tooltip("Displays the selected bet.")]
        [SerializeField] private TMP_Text betText;

        [Tooltip("Displays spin results and status messages.")]
        [SerializeField] private TMP_Text resultText;

        [Tooltip("Displays the configured symbol payout multipliers.")]
        [SerializeField] private TMP_Text payoutText;

        [Header("Main Controls")]
        [Tooltip("Requests a spin.")]
        [SerializeField] private Button spinButton;

        [Tooltip("Requests a smaller bet.")]
        [SerializeField] private Button decreaseBetButton;

        [Tooltip("Requests a larger bet.")]
        [SerializeField] private Button increaseBetButton;

        [Tooltip("Requests confirmation before starting a new session.")]
        [SerializeField] private Button newSessionButton;

        [Header("Confirmation")]
        [Tooltip("Outer popup root containing the backdrop, panel and buttons.")]
        [SerializeField] private GameObject confirmationPopup;

        [Tooltip("Confirms the new-session request.")]
        [SerializeField] private Button confirmButton;

        [Tooltip("Cancels the new-session request.")]
        [SerializeField] private Button cancelButton;

        [Header("Navigation")]
        [Tooltip("Scene EventSystem used to select available UI controls.")]
        [SerializeField] private EventSystem eventSystem;

        private bool isInitialized;

        public event Action SpinRequested;
        public event Action<int> BetChangeRequested;
        public event Action NewSessionRequested;
        public event Action ResetConfirmed;
        public event Action ResetCancelled;

        public bool IsConfirmationVisible => confirmationPopup.activeSelf;

        private void Awake()
        {
            if (creditsText == null ||
                betText == null ||
                resultText == null ||
                payoutText == null ||
                spinButton == null ||
                decreaseBetButton == null ||
                increaseBetButton == null ||
                newSessionButton == null ||
                confirmationPopup == null ||
                confirmButton == null ||
                cancelButton == null ||
                eventSystem == null)
            {
                Debug.LogError(
                    $"{name}: Assign all SlotGameView UI references.",
                    this);

                enabled = false;
                return;
            }

            confirmationPopup.SetActive(false);
            isInitialized = true;
        }

        private void OnEnable()
        {
            if (!isInitialized)
            {
                return;
            }

            spinButton.onClick.AddListener(HandleSpinClicked);
            decreaseBetButton.onClick.AddListener(HandleDecreaseBetClicked);
            increaseBetButton.onClick.AddListener(HandleIncreaseBetClicked);
            newSessionButton.onClick.AddListener(HandleNewSessionClicked);
            confirmButton.onClick.AddListener(HandleConfirmClicked);
            cancelButton.onClick.AddListener(HandleCancelClicked);
        }

        private void OnDisable()
        {
            if (!isInitialized)
            {
                return;
            }

            spinButton.onClick.RemoveListener(HandleSpinClicked);
            decreaseBetButton.onClick.RemoveListener(HandleDecreaseBetClicked);
            increaseBetButton.onClick.RemoveListener(HandleIncreaseBetClicked);
            newSessionButton.onClick.RemoveListener(HandleNewSessionClicked);
            confirmButton.onClick.RemoveListener(HandleConfirmClicked);
            cancelButton.onClick.RemoveListener(HandleCancelClicked);

            if (confirmationPopup != null)
            {
                confirmationPopup.SetActive(false);
            }

            if (eventSystem != null)
            {
                eventSystem.SetSelectedGameObject(null);
            }
        }

        /// <summary>
        /// Displays authoritative values and permitted player actions.
        /// </summary>
        public void Render(
            int credits,
            int selectedBet,
            bool canSpin,
            bool canDecreaseBet,
            bool canIncreaseBet,
            bool canStartNewSession,
            string message)
        {
            EnsureInitialized();

            creditsText.text = $"Credits: {credits}";
            betText.text = $"Bet: {selectedBet}";
            resultText.text = message ?? string.Empty;

            bool mainControlsAvailable = !IsConfirmationVisible;

            spinButton.interactable =
                mainControlsAvailable && canSpin;

            decreaseBetButton.interactable =
                mainControlsAvailable && canDecreaseBet;

            increaseBetButton.interactable =
                mainControlsAvailable && canIncreaseBet;

            newSessionButton.interactable =
                mainControlsAvailable && canStartNewSession;
        }

        /// <summary>
        /// Builds the payout guide from the validated shared configuration.
        /// </summary>
        public void SetPayouts(SlotGameConfig config)
        {
            EnsureInitialized();

            if (config == null)
            {
                throw new ArgumentNullException(nameof(config));
            }

            StringBuilder text = new StringBuilder();

            text.AppendLine("<b>PAYOUTS</b>");
            text.AppendLine("Three matching symbols:");

            for (int i = 0; i < config.SymbolCount; i++)
            {
                SlotSymbolDefinition symbol = config.GetSymbol(i);

                text.Append(symbol.DisplayName)
                    .Append(" x")
                    .Append(symbol.PayoutMultiplier)
                    .AppendLine();
            }

            text.AppendLine();
            text.Append("Win = bet x multiplier");

            payoutText.text = text.ToString();
        }

        /// <summary>
        /// Shows or hides confirmation. Render fresh control availability
        /// after closing, then restore focus with FocusMainControl.
        /// </summary>
        public void SetConfirmationVisible(bool visible)
        {
            EnsureInitialized();

            eventSystem.SetSelectedGameObject(null);
            confirmationPopup.SetActive(visible);

            if (visible)
            {
                spinButton.interactable = false;
                decreaseBetButton.interactable = false;
                increaseBetButton.interactable = false;
                newSessionButton.interactable = false;

                eventSystem.SetSelectedGameObject(cancelButton.gameObject);
            }
        }

        /// <summary>
        /// Selects an available control for keyboard or controller input.
        /// </summary>
        public void FocusMainControl()
        {
            EnsureInitialized();

            eventSystem.SetSelectedGameObject(null);

            if (IsConfirmationVisible)
            {
                eventSystem.SetSelectedGameObject(cancelButton.gameObject);
                return;
            }

            GameObject selection = null;

            if (spinButton.interactable)
            {
                selection = spinButton.gameObject;
            }
            else if (decreaseBetButton.interactable)
            {
                selection = decreaseBetButton.gameObject;
            }
            else if (increaseBetButton.interactable)
            {
                selection = increaseBetButton.gameObject;
            }
            else if (newSessionButton.interactable)
            {
                selection = newSessionButton.gameObject;
            }

            eventSystem.SetSelectedGameObject(selection);
        }

        private void HandleSpinClicked()
        {
            SpinRequested?.Invoke();
        }

        private void HandleDecreaseBetClicked()
        {
            BetChangeRequested?.Invoke(-1);
        }

        private void HandleIncreaseBetClicked()
        {
            BetChangeRequested?.Invoke(1);
        }

        private void HandleNewSessionClicked()
        {
            NewSessionRequested?.Invoke();
        }

        private void HandleConfirmClicked()
        {
            ResetConfirmed?.Invoke();
        }

        private void HandleCancelClicked()
        {
            ResetCancelled?.Invoke();
        }

        private void EnsureInitialized()
        {
            if (!isInitialized)
            {
                throw new InvalidOperationException(
                    "Initialize the game view before using it.");
            }
        }
    }
}