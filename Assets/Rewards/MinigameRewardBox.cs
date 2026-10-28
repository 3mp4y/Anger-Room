using AngerRoom.Localization;
using Oculus.Interaction;
using TMPro;
using UnityEngine;
using UnityEngine.Serialization;

namespace AngerRoom.Rewards
{
    [DisallowMultipleComponent]
    public sealed class MinigameRewardBox : MonoBehaviour
    {
        [Header("Ownership")]
        [SerializeField] private GamesManager gamesManager;
        [SerializeField, Range(0, 2)] private int gameIndex;
        [SerializeField] private TextAdapter textAdapter;
        [SerializeField] private RewardReachInput reachInput;

        [Header("Scene references")]
        [SerializeField] private Transform lid;
        [SerializeField] private TMP_Text pointsText;
        [SerializeField] private SpriteRenderer buttonIcon;
        [SerializeField] private Sprite lockedIcon;
        [SerializeField] private Sprite unlockedIcon;
        [Tooltip("Optional. Leave empty if the pedestal does not need a minigame-name label.")]
        [FormerlySerializedAs("puzzleLabel")]
        [SerializeField] private TMP_Text minigameLabel;
        [SerializeField] private PokeInteractable openButton;

        [Header("Opening and floating text (local metres / seconds)")]
        [SerializeField] private Vector3 openLidEuler = new Vector3(110f, 0f, 0f);
        [SerializeField, Min(0.1f)] private float openingDuration = 0.65f;
        [SerializeField, Min(0.1f)] private float riseDuration = 0.8f;
        [SerializeField, Min(0f)] private float riseHeight = 0.32f;
        [SerializeField, Min(0f)] private float verticalWobble = 0.018f;
        [SerializeField, Min(0f)] private float horizontalWobble = 0.012f;
        [SerializeField, Min(0f)] private float wobbleFrequency = 0.65f;
        [Tooltip("Maximum angle in either direction from the initial rotation, in degrees.")]
        [SerializeField, Range(0f, 180f)] private float rotationAmplitude = 30f;
        [Tooltip("Full back-and-forth cycles per second. 0.25 takes four seconds.")]
        [SerializeField, Min(0f)] private float rotationFrequency = 0.25f;

        [Header("Collection")]
        [SerializeField, Min(0.01f)] private float reachRadius = 0.16f;
        [SerializeField, Min(0.1f)] private float collectDuration = 0.55f;
        [SerializeField] private float collectSpinSpeed = 900f;

        // A single state prevents repeated button presses from starting overlapping
        // animations, and repeated hand samples from collecting the same reward.
        private enum State { Locked, Ready, Opening, Rising, Available, Collecting, Collected }
        private State state;
        private Quaternion closedLidRotation;
        private Quaternion initialTextRotation;
        private Vector3 initialTextPosition;
        private Vector3 initialTextScale;
        private Vector3 collectionStartPosition;
        private float elapsed;
        private float animationTime;
        private float spin;
        private bool initialized;

        private void Start()
        {
            if (gamesManager == null || textAdapter == null || reachInput == null ||
                lid == null || pointsText == null || buttonIcon == null ||
                lockedIcon == null || unlockedIcon == null || openButton == null)
            {
                Debug.LogError("Assign all MinigameRewardBox references before playing.", this);
                enabled = false;
                return;
            }

            closedLidRotation = lid.localRotation;
            initialTextPosition = pointsText.transform.localPosition;
            initialTextRotation = pointsText.transform.localRotation;
            initialTextScale = pointsText.transform.localScale;
            initialized = true;
            gamesManager.RewardChanged += OnRewardChanged;
            if (minigameLabel != null)
            {
                TextId[] names = { TextId.RewardCards, TextId.RewardGun, TextId.RewardLabyrinth };
                minigameLabel.text = textAdapter.Get(names[gameIndex]);
            }
            RefreshReward();
        }

        private void OnDestroy()
        {
            if (gamesManager != null) gamesManager.RewardChanged -= OnRewardChanged;
        }

        private void OnEnable()
        {
            // If a station was temporarily hidden, reconcile any score change
            // immediately. Update-based animation resumes instead of losing a coroutine.
            if (initialized) RefreshReward();
        }

        private void OnRewardChanged(int changedGame)
        {
            if (initialized && changedGame == gameIndex) RefreshReward();
        }

        private void RefreshReward()
        {
            pointsText.text = textAdapter.Format(TextId.Points, gamesManager.GetRewardPoints(gameIndex));
            if (gamesManager.IsRewardCollected(gameIndex))
            {
                state = State.Collected;
                pointsText.gameObject.SetActive(false);
                SetButton(true);
            }
            else if (state == State.Locked || state == State.Ready)
            {
                bool ready = gamesManager.CanCollectReward(gameIndex);
                state = ready ? State.Ready : State.Locked;
                SetButton(ready);
            }
        }

        private void SetButton(bool unlocked)
        {
            buttonIcon.sprite = unlocked ? unlockedIcon : lockedIcon;
            // InteractableColorVisual observes this PokeInteractable: Disabled is
            // translucent red, Normal/Hover/Select are green (configured in the scene).
            // Keep the unlocked indication after opening. Further physical presses
            // are harmless: OpenBox accepts only Ready, so it cannot reopen/award twice.
            openButton.enabled = unlocked;
        }

        // Bound to the physical Meta poke button's Select event in the scene.
        public void OpenBox()
        {
            if (!initialized || state != State.Ready || !gamesManager.CanCollectReward(gameIndex)) return;
            state = State.Opening;
            elapsed = 0f;
            SetButton(true);
        }

        private void Update()
        {
            if (!initialized || state == State.Collected) return;
            float dt = Time.deltaTime;
            animationTime += dt;
            elapsed += dt;

            float rise = 0f;
            if (state == State.Opening)
            {
                float t = Mathf.Clamp01(elapsed / openingDuration);
                lid.localRotation = Quaternion.Slerp(closedLidRotation,
                    closedLidRotation * Quaternion.Euler(openLidEuler), Mathf.SmoothStep(0f, 1f, t));
                if (t >= 1f) { state = State.Rising; elapsed = 0f; }
            }
            else if (state == State.Rising)
            {
                float t = Mathf.Clamp01(elapsed / riseDuration);
                rise = riseHeight * Mathf.SmoothStep(0f, 1f, t);
                if (t >= 1f) { state = State.Available; elapsed = 0f; }
            }
            else if (state == State.Available)
            {
                rise = riseHeight;
            }

            if (state == State.Collecting)
            {
                float t = Mathf.Clamp01(elapsed / collectDuration);
                spin = Mathf.Repeat(spin + collectSpinSpeed * t * dt, 360f);
                pointsText.transform.localRotation = initialTextRotation * Quaternion.Euler(0f, spin, 0f);
                pointsText.transform.localPosition = collectionStartPosition + Vector3.up * (0.08f * t);
                pointsText.transform.localScale = initialTextScale * (1f - Mathf.SmoothStep(0f, 1f, t));
                if (t >= 1f)
                {
                    // This is the only award request, at the end of the disappearance.
                    // GamesManager independently guards ownership and duplicate awards.
                    if (!gamesManager.TryCollectReward(gameIndex))
                    {
                        RefreshReward();
                        if (state != State.Collected)
                        {
                            state = State.Available;
                            elapsed = 0f;
                            pointsText.transform.localScale = initialTextScale;
                        }
                    }
                }
                return;
            }

            float phase = animationTime * wobbleFrequency * Mathf.PI * 2f;
            pointsText.transform.localPosition = initialTextPosition + new Vector3(
                Mathf.Sin(phase * 0.7f) * horizontalWobble,
                rise + Mathf.Sin(phase) * verticalWobble, 0f);
            // Sine reverses smoothly at both limits. Calculate from the initial
            // orientation each frame so the idle rotation never accumulates.
            float angle = Mathf.Sin(animationTime * rotationFrequency * Mathf.PI * 2f) * rotationAmplitude;
            pointsText.transform.localRotation = initialTextRotation * Quaternion.Euler(0f, angle, 0f);

            // Closed/opening/rising boxes cannot be collected. This tests tracked
            // hand/controller positions, not the head or an arbitrary Player collider.
            if (state == State.Available && reachInput.IsReaching(pointsText.transform.position, reachRadius))
            {
                // Start the fast collection spin at the visible swing angle,
                // avoiding a rotation jump when the hand reaches the reward.
                spin = angle;
                state = State.Collecting;
                elapsed = 0f;
                collectionStartPosition = pointsText.transform.localPosition;
            }
        }
    }
}
