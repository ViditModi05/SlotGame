using System;
using System.Collections;
using UnityEngine.UI;
using UnityEngine;

namespace SlotGame.Presentation
{
    /// <summary>
    /// Plays the supplied raised and lowered lever poses.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Image))]
    public sealed class LeverView : MonoBehaviour
    {
        [Header("Artwork")]
        [Tooltip("Lever artwork shown while idle.")]
        [SerializeField] private Sprite raisedSprite;

        [Tooltip("Lever artwork shown during a pull.")]
        [SerializeField] private Sprite loweredSprite;

        [Header("Timing")]
        [Tooltip("Seconds to hold the lowered pose before returning to idle.")]
        [Min(0.01f)]
        [SerializeField] private float holdDuration = 0.15f;

        private Image leverImage;
        private Coroutine pullRoutine;
        private bool isInitialized;

        public bool IsPlaying => pullRoutine != null;

        private void Awake()
        {
            leverImage = GetComponent<Image>();

            if (raisedSprite == null ||
                loweredSprite == null ||
                float.IsNaN(holdDuration) ||
                float.IsInfinity(holdDuration) ||
                holdDuration <= 0f)
            {
                Debug.LogError(
                    $"{name}: Assign both lever sprites and a finite, " +
                    "positive hold duration.",
                    this);

                enabled = false;
                return;
            }

            leverImage.sprite = raisedSprite;
            isInitialized = true;
        }

        /// <summary>
        /// Plays one pull, ignoring repeat requests while already playing.
        /// </summary>
        public void PlayPull()
        {
            if (!isInitialized || !isActiveAndEnabled)
            {
                throw new InvalidOperationException(
                    "The lever must be initialized and active before playing.");
            }

            if (IsPlaying)
            {
                return;
            }

            pullRoutine = StartCoroutine(AnimatePull());
        }

        private IEnumerator AnimatePull()
        {
            leverImage.sprite = loweredSprite;

            yield return new WaitForSeconds(holdDuration);

            leverImage.sprite = raisedSprite;
            pullRoutine = null;
        }

        private void OnDisable()
        {
            if (pullRoutine != null)
            {
                StopCoroutine(pullRoutine);
            }

            pullRoutine = null;

            if (leverImage != null && raisedSprite != null)
            {
                leverImage.sprite = raisedSprite;
            }
        }

#if UNITY_EDITOR
        [ContextMenu("Preview Pull")]
        private void PreviewPull()
        {
            if (!Application.isPlaying ||
                !isInitialized ||
                !isActiveAndEnabled)
            {
                Debug.LogWarning(
                    "Preview requires an initialized, active lever in Play Mode.",
                    this);

                return;
            }

            PlayPull();
        }
#endif
    }
}