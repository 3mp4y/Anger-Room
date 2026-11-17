using System.Collections.Generic;
using Oculus.Interaction;
using UnityEngine;
using UnityEngine.UI;

namespace AngerRoom.Shimmer
{
    [DefaultExecutionOrder(-100)]
    public sealed class AcquisitionGate : MonoBehaviour
    {
        public ShimmerReceiver shimmer;
        public EyeStateLogger eyes;
        public GamesManager games;
        public Light[] startupLights;
        public Text statusText;
        public Canvas statusCanvas;
        [Tooltip("Disable only for an explicitly labelled Quest 2 or Editor test. Production requires eyes.")]
        public bool requireEyeTracking = true;
        [Min(0.1f)] public float sampleTimeoutSeconds = 1f;
        [Min(0.1f)] public float stableStreamSeconds = 1f;
        [Min(1f)] public float retryIntervalSeconds = 5f;
        public bool IsBlocked { get; private set; } = true;
        public bool AwaitingResume { get; private set; }
        public static AcquisitionGate Instance { get; private set; }
        public static bool GameplayBlocked => Instance != null && Instance.IsBlocked;
        readonly Dictionary<Behaviour, bool> interactions = new Dictionary<Behaviour, bool>();
        readonly Dictionary<Canvas, bool> coveredCanvases = new Dictionary<Canvas, bool>();
        Canvas[] interactiveCanvases;
        Behaviour[] eventWrappers;
        Behaviour[] controls;
        bool hasStarted, appPaused, cancelledAttempt;
        readonly ResumeChord resumeChord = new ResumeChord();
        double stableSince = -1, nextRetry;

        void Awake()
        {
            Instance = this;
#if ACQUISITION_RECOVERY_QUEST2_TEST
            requireEyeTracking = false;
#endif
            games.SetAcquisitionBlocked(true);
            foreach (var light in startupLights) light.enabled = false;
            shimmer.recordingDurationSeconds = 0;
            shimmer.showDiagnostics = false;
            if (statusCanvas != null)
            {
                statusCanvas.overrideSorting = true;
                statusCanvas.sortingOrder = 32760;
            }
        }

        void Start()
        {
            var wrappers = new List<Behaviour>();
            wrappers.AddRange(FindObjectsOfType<PointableUnityEventWrapper>(true));
            wrappers.AddRange(FindObjectsOfType<InteractableUnityEventWrapper>(true));
            eventWrappers = wrappers.ToArray();
            var buttons = new List<Behaviour>();
            buttons.AddRange(FindObjectsOfType<PokeInteractable>(true));
            buttons.AddRange(FindObjectsOfType<RayInteractable>(true));
            controls = buttons.ToArray();
            var canvases = new HashSet<Canvas>();
            foreach (var pointable in FindObjectsOfType<PointableCanvas>(true))
                foreach (var canvas in pointable.GetComponentsInChildren<Canvas>(true))
                    if (canvas != statusCanvas) canvases.Add(canvas);
            interactiveCanvases = new List<Canvas>(canvases).ToArray();
        }

        void Update()
        {
            double now = Time.realtimeSinceStartupAsDouble;
            bool fresh = !appPaused && shimmer.HasFreshSamples(sampleTimeoutSeconds);
            if (!fresh) stableSince = -1;
            else if (stableSince < 0) stableSince = now;
            bool streamReady = fresh && now - stableSince >= stableStreamSeconds;
            bool ready = streamReady &&
                (!requireEyeTracking || (eyes != null && eyes.HasStableData));

            if (hasStarted && !IsBlocked && !fresh) Interrupt("Shimmer samples stopped");
            if (IsBlocked && !appPaused && !shimmer.IsConnectingOrRunning && shimmer.AutoRetryAllowed && now >= nextRetry)
            {
                nextRetry = now + retryIntervalSeconds;
                shimmer.StartRecording();
            }
            if (IsBlocked && fresh == false && shimmer.Latest != null) shimmer.StopRecording();
            AwaitingResume = hasStarted && IsBlocked && ready;
            if (!hasStarted && ready) Begin();
            if (resumeChord.Poll(AwaitingResume,
                OVRInput.Get(OVRInput.RawButton.A, OVRInput.Controller.RTouch),
                OVRInput.Get(OVRInput.RawButton.B, OVRInput.Controller.RTouch)))
                ResumeAfterReconnect();
            UpdateStatus(fresh, streamReady);
        }

        void Begin()
        {
            hasStarted = true;
            RecordingSession.Instance?.MarkReady(requireEyeTracking);
            foreach (var light in startupLights) light.enabled = true;
            Unblock();
        }

        public void Interrupt(string reason)
        {
            if (!hasStarted || IsBlocked) return;
            IsBlocked = true;
            games.SetAcquisitionBlocked(true);
            LockInteractions();
            RecordingSession.Instance?.Log("acquisition_interrupted", reason + "; remaining_game_seconds=" + RecordingSession.Number(games.RemainingExperimentSeconds));
            cancelledAttempt = games.CancelAttemptForAcquisitionLoss();
            games.am.PauseForAcquisition(cancelledAttempt);
            stableSince = -1;
        }

        [ContextMenu("Resume after reconnection")]
        public void ResumeAfterReconnect()
        {
            if (!AwaitingResume || appPaused || !shimmer.HasFreshSamples(sampleTimeoutSeconds) ||
                (requireEyeTracking && !eyes.HasStableData)) return;
            RecordingSession.Instance?.Log("acquisition_resumed", "User confirmed A+B; remaining_game_seconds=" + RecordingSession.Number(games.RemainingExperimentSeconds));
            games.am.ResumeAfterAcquisition();
            Unblock();
            if (cancelledAttempt) games.stoppedPlaying();
            cancelledAttempt = false;
        }

        void Unblock()
        {
            IsBlocked = false;
            AwaitingResume = false;
            games.SetAcquisitionBlocked(false);
            // Restore controls before subscribing their UnityEvents again.
            foreach (var b in controls) Restore(b);
            foreach (var b in eventWrappers) Restore(b);
            interactions.Clear();
            RestoreCoveredCanvases();
        }
        void Restore(Behaviour b) { if (b != null && interactions.TryGetValue(b, out bool enabledBefore)) b.enabled = enabledBefore; }

        void LateUpdate()
        {
            if (IsBlocked)
            {
                // Disable the actual Meta interactables, including dialog and labyrinth controls.
                // Keep tracking and the sensor worker running; do not change Time.timeScale.
                // Unsubscribe callbacks first: disabling a selected control may emit Unselect.
                LockInteractions();
            }
            if (statusCanvas != null && Camera.main != null)
                statusCanvas.transform.SetPositionAndRotation(Camera.main.transform.TransformPoint(0, -0.15f, 1.1f), Camera.main.transform.rotation);
        }
        void LockInteractions()
        {
            if (eventWrappers == null || controls == null) return;
            foreach (var b in eventWrappers) Lock(b);
            foreach (var b in controls) Lock(b);
            HideCoveredCanvases();
        }
        void HideCoveredCanvases()
        {
            if (interactiveCanvases == null) return;
            // Sorting order alone cannot prevent world-space depth occlusion.
            // Hide rendering only: dialog GameObjects, progress and animations stay intact.
            foreach (var canvas in interactiveCanvases)
            {
                if (canvas == null || canvas == statusCanvas) continue;
                if (!coveredCanvases.ContainsKey(canvas)) coveredCanvases.Add(canvas, canvas.enabled);
                canvas.enabled = false;
            }
        }
        void RestoreCoveredCanvases()
        {
            foreach (var pair in coveredCanvases) if (pair.Key != null) pair.Key.enabled = pair.Value;
            coveredCanvases.Clear();
        }
        void Lock(Behaviour b)
        {
            if (b == null) return;
            if (!interactions.ContainsKey(b)) interactions.Add(b, b.enabled);
            b.enabled = false;
        }
        void UpdateStatus(bool fresh, bool streamReady)
        {
            if (statusCanvas == null || statusText == null) return;
            statusCanvas.gameObject.SetActive(IsBlocked);
            if (!IsBlocked) return;
            statusText.color = hasStarted ? new Color(1, 0.6f, 0.3f) : Color.white;
            string title = hasStarted ? "RECORDING INTERRUPTED - GAME PAUSED" : "PREPARING RECORDING";
            bool unsupportedEyes = requireEyeTracking && OVRPlugin.initialized && !OVRPlugin.eyeTrackingSupported;
            string eye = !requireEyeTracking ? "Eye tracking: disabled for test" :
                unsupportedEyes ? "Eye tracking: unavailable on this headset" :
                eyes != null && eyes.HasStableData ? "Eye tracking: ready" : "Eye tracking: waiting for stable gaze";
            string action = AwaitingResume ? "Sensors ready. Release, then press A + B together\non the RIGHT controller to resume." :
                unsupportedEyes ? "This build requires eye tracking.\nFor Quest 2, build the Quest 2 test version in Unity." :
                streamReady ? "Waiting for eye tracking.\nWear the headset and allow eye tracking permission." :
                fresh ? "Confirming a stable Shimmer stream..." :
                "Keep Shimmer powered on and disconnected from Consensys.\nReconnecting automatically; check Nearby devices permission.";
            statusText.text = title + "\nShimmer: " + (fresh ? (streamReady ? "ready" : "confirming samples") : shimmer.Status) +
                "\n" + eye + "\n" + action;
        }
        void OnApplicationPause(bool paused)
        {
            appPaused = paused;
            if (paused) { Interrupt("Headset application paused"); stableSince = -1; }
        }
        void OnDestroy() { RestoreCoveredCanvases(); if (Instance == this) Instance = null; }
    }

    // Require a fresh deliberate chord after readiness, not buttons held during repair.
    public sealed class ResumeChord
    {
        bool armed;
        public bool Poll(bool ready, bool a, bool b)
        {
            if (!ready) { armed = false; return false; }
            if (!a && !b) armed = true;
            if (!armed || !a || !b) return false;
            armed = false;
            return true;
        }
    }
}
