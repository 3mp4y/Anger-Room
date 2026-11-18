using System.Collections;
using System.Collections.Generic;
using AngerRoom.Localization;
using UnityEngine;
using TMPro;
[DefaultExecutionOrder(-200)]
public class GamesManager : MonoBehaviour
{
    [Header("Participant controls (choose before Play / Build)")]
    [SerializeField] private bool leftHanded;
    [Tooltip("Interactors subtrees only. Keep the SDK tracking/data providers enabled.")]
    [SerializeField] private GameObject leftInteractors;
    [SerializeField] private GameObject rightInteractors;
    [SerializeField] private GameObject leftControllerVisual;
    [SerializeField] private GameObject rightControllerVisual;
    [SerializeField] private GameObject leftTeleportInteractor;
    [SerializeField] private GameObject rightTeleportInteractor;
    [Tooltip("The controller-mounted labyrinth readout; moved once at session startup.")]
    [SerializeField] private Transform controllerReadout;
    private Vector3 leftReadoutPosition = new Vector3(0.16f, -0.13f, -0.093f);
    [SerializeField] private Transform leftControllerModel;
    [SerializeField] private Transform rightControllerModel;
    private bool handChoiceInitialized;
    private bool sessionLeftHanded;
    private bool shootingControls;

    public bool IsLeftHanded => handChoiceInitialized ? sessionLeftHanded : leftHanded;
    public OVRInput.Controller ActiveController => IsLeftHanded ? OVRInput.Controller.LTouch : OVRInput.Controller.RTouch;
    public Transform ActiveControllerModel => IsLeftHanded ? leftControllerModel : rightControllerModel;
    public string ActiveHandName => IsLeftHanded ? "left" : "right";
    public string ResumeButtons => IsLeftHanded ? "X + Y" : "A + B";

    private void InitializeHandedness()
    {
        if (handChoiceInitialized) return;
        sessionLeftHanded = leftHanded;
        handChoiceInitialized = true;
        if (leftControllerVisual != null) leftControllerVisual.SetActive(IsLeftHanded);
        if (rightControllerVisual != null) rightControllerVisual.SetActive(!IsLeftHanded);
        if (leftTeleportInteractor != null) leftTeleportInteractor.SetActive(IsLeftHanded);
        if (rightTeleportInteractor != null) rightTeleportInteractor.SetActive(!IsLeftHanded);
        RefreshControllerInteractions();
        if (controllerReadout != null && ActiveControllerModel != null)
        {
            // Preserve its local pose; mirror only the sideways offset for the
            // other wrist. The same TMP reference/localization continues to work.
            controllerReadout.SetParent(ActiveControllerModel, false);
            if (IsLeftHanded)
            {
                controllerReadout.localPosition = leftReadoutPosition;
            }
        }
    }

    private void RefreshControllerInteractions()
    {
        bool available = !shootingControls && !AcquisitionBlocked && !SessionEnding;
        if (leftInteractors != null) leftInteractors.SetActive(available && IsLeftHanded);
        if (rightInteractors != null) rightInteractors.SetActive(available && !IsLeftHanded);
    }

    public void UseShootingControls(bool shooting)
    {
        shootingControls = shooting;
        RefreshControllerInteractions();
    }

    [Header("Debug")]
    [Tooltip("Enable the opening-dialogue skipper (hold the active controller's grip for five seconds). Debug buttons can be enabled separately below.")]
    public bool enableDebugOperations;
    [Tooltip("Also enable the fixed debug panel: tutorial skips, win/loss buttons and the one-second timer. Requires Enable Debug Operations. Turn this off to keep only the opening-dialogue skipper.")]
    public bool enableDebugButtons = true;
    [SerializeField] private GameObject debugTutorialControls;
    [SerializeField] private UnityEngine.UI.Button[] debugTutorialButtons = new UnityEngine.UI.Button[3];
    [SerializeField] private UnityEngine.UI.Button[] debugWinButtons = new UnityEngine.UI.Button[3];
    [SerializeField] private UnityEngine.UI.Button[] debugLossButtons = new UnityEngine.UI.Button[3];
    [SerializeField] private UnityEngine.UI.Button debugTimerButton;
    [SerializeField] private GameObject[] openingDialogObjects;
    [SerializeField] private GameObject[] openingObjectsToActivate;
    [SerializeField] private GameObject openingStartObject;
    [SerializeField] private Behaviour openingLightFollow;
    bool openingDialogsComplete;
    readonly DebugGripHold debugGrip = new DebugGripHold();

    void Update()
    {
        bool allowed = enableDebugOperations && !AcquisitionBlocked && !experimentExpired && !SessionEnding;
        if (debugGrip.Poll(allowed && !openingDialogsComplete,
            OVRInput.Get(OVRInput.Button.PrimaryHandTrigger, ActiveController), Time.realtimeSinceStartupAsDouble))
            DebugSkipOpeningDialogs();
        bool show = allowed && enableDebugButtons && openingDialogsComplete && (openingStartObject == null || !openingStartObject.activeInHierarchy);
        if (debugTutorialControls != null && debugTutorialControls.activeSelf != show) debugTutorialControls.SetActive(show);
        for (int i = 0; i < debugTutorialButtons.Length; i++)
            if (debugTutorialButtons[i] != null) debugTutorialButtons[i].interactable = CanDebugSkipTutorial(i);
        for (int i = 0; i < debugWinButtons.Length; i++)
            if (debugWinButtons[i] != null) debugWinButtons[i].interactable = CanDebugResolveMinigame(i);
        for (int i = 0; i < debugLossButtons.Length; i++)
            if (debugLossButtons[i] != null) debugLossButtons[i].interactable = CanDebugResolveMinigame(i);
        if (debugTimerButton != null) debugTimerButton.interactable = CanDebugShortenTimer;
    }

    public void DebugSkipOpeningDialogs()
    {
        if (!enableDebugOperations || AcquisitionBlocked || experimentExpired || SessionEnding || openingDialogsComplete) return;
        if (openingObjectsToActivate == null || openingDialogObjects == null || openingStartObject == null)
        {
            Debug.LogError("Debug dialog references are missing. Run DebugDialogSetup.Configure before testing.", this);
            return;
        }
        am.SkipOpeningNarration();
        foreach (var obj in openingObjectsToActivate) if (obj != null) obj.SetActive(true);
        if (openingLightFollow != null) openingLightFollow.enabled = true;
        foreach (var obj in openingDialogObjects) if (obj != null) obj.SetActive(false);
        if (openingStartObject != null) openingStartObject.SetActive(false);
        StartOverAllTimer();
        AngerRoom.Shimmer.RecordingSession.Instance?.Log("debug_skip_opening_dialogs", ActiveHandName + " grip held 5 seconds");
    }

    public bool CanDebugSkipTutorial(int game) => DebugSessionAvailable && !HasPendingReward &&
        IsValidGame(game) && !IsGameResolved(game) &&
        ((activeAttempt == game && activeTutorial) || (activeAttempt < 0 && !_reactivationBlacklist.Contains(tutorialButtons[game])));

    public void DebugSkipCardsTutorial() => DebugSkipTutorial(0);
    public void DebugSkipShootingTutorial() => DebugSkipTutorial(1);
    public void DebugSkipLabyrinthTutorial() => DebugSkipTutorial(2);

    // Guard the operations as well as the panel: stale UnityEvents and the gun
    // ray must not activate hidden debug buttons in dialogue-skip-only mode.
    private bool DebugSessionAvailable => enableDebugOperations && enableDebugButtons && openingDialogsComplete &&
        !AcquisitionBlocked && !experimentExpired && !SessionEnding;

    public bool CanDebugResolveMinigame(int game) => DebugSessionAvailable && IsValidGame(game) &&
        !IsGameResolved(game) && !HasPendingReward && (activeAttempt < 0 || activeAttempt == game);

    public bool CanDebugShortenTimer => DebugSessionAvailable && isgoing;

    public void DebugWinMinigame(int game) => DebugResolveMinigame(game, true);
    public void DebugLoseMinigame(int game) => DebugResolveMinigame(game, false);

    private void DebugResolveMinigame(int game, bool win)
    {
        // Guard the method too: a stale click or Inspector UnityEvent must not
        // bypass the debug toggle, affect another running game, or award twice.
        if (!CanDebugResolveMinigame(game)) return;
        if (game == 0) foreach (var c in FindObjectsOfType<Card_Minigame>(true)) c.CancelForAcquisitionLoss();
        if (game == 1) foreach (var g in FindObjectsOfType<GunPuzzle>(true)) g.CancelForAcquisitionLoss();
        if (game == 2) foreach (var l in FindObjectsOfType<LabyrinthScript>(true)) l.CancelForAcquisitionLoss();
        if (activeAttempt == game && activeTutorial) am.StopTutorialNarration();

        // The existing cancellation APIs also stop preparation/level-transition
        // coroutines and restore the room. They do not deduct points or log a
        // sensor outage. Mark the tutorial done so its button cannot reappear.
        DoneTutorial(game);
        tutorialButtons[game].SetActive(false);
        string detail = "game=" + game + "; result=" + (win ? "win" : "loss");
        logger.Log("DEBUG forced minigame result: " + detail);
        AngerRoom.Shimmer.RecordingSession.Instance?.Log("debug_minigame_result", detail);
        if (win) GameWon(game);
        else
        {
            // Lose the entire minigame, not just one attempt. Reuse the usual
            // deductions/bulb/reward events, without spawning transient messages.
            while (!IsGameResolved(game)) addTries(game, null);
        }
        stoppedPlaying();
        if (game == 0) foreach (var cards in FindObjectsOfType<Card_Minigame>(true)) cards.ShowDebugResult(win);
        if (game == 1) foreach (var gun in FindObjectsOfType<GunPuzzle>(true)) gun.ShowDebugResult(win);
        if (game == 2) foreach (var labyrinth in FindObjectsOfType<LabyrinthScript>(true)) labyrinth.ShowDebugResult(win);
        // Rewards are deliberately NOT collected here. The real box, reach
        // interaction, score award and ending wait must still be exercised.
    }

    public void DebugSetOverallTimerToOneSecond()
    {
        if (!CanDebugShortenTimer) return;
        time_left_exp = 1f;
        TMPtime_left_exp.text = "00:01";
        logger.Log("DEBUG overall timer set to one second");
        AngerRoom.Shimmer.RecordingSession.Instance?.Log("debug_overall_timer", "remaining_seconds=1");
        // Let OverallTimer expire normally, including reward settlement and
        // narration. Do not restart a timer stopped by all minigames finishing.
    }

    public bool TryPressDebugButtonWithGun(Ray ray, float maxDistance)
    {
        // Normal Meta UI interaction is disabled while holding the gun. In
        // debug mode only, the gun can press this one fixed panel directly.
        // This avoids enabling teleport/grab controls during the shooting game.
        if (!DebugSessionAvailable || !shootingControls || debugTutorialControls == null ||
            !debugTutorialControls.activeInHierarchy) return false;
        Transform panel = debugTutorialControls.transform;
        var plane = new Plane(panel.forward, panel.position);
        if (!plane.Raycast(ray, out float distance) || distance > maxDistance) return false;
        Vector3 point = ray.GetPoint(distance);
        foreach (var button in debugTutorialControls.GetComponentsInChildren<UnityEngine.UI.Button>())
        {
            var rect = (RectTransform)button.transform;
            if (!rect.rect.Contains((Vector2)rect.InverseTransformPoint(point))) continue;
            if (button.IsInteractable()) button.onClick.Invoke();
            return true; // Even a disabled debug button must not fire a real shot.
        }
        return false;
    }

    public void DebugSkipTutorial(int game)
    {
        if (!CanDebugSkipTutorial(game)) return;
        bool interruptedTutorial = activeAttempt == game && activeTutorial;
        if (interruptedTutorial)
        {
            if (game == 0) foreach (var c in FindObjectsOfType<Card_Minigame>(true)) c.CancelForAcquisitionLoss();
            if (game == 1) foreach (var g in FindObjectsOfType<GunPuzzle>(true)) g.CancelForAcquisitionLoss();
            if (game == 2) foreach (var l in FindObjectsOfType<LabyrinthScript>(true)) l.CancelForAcquisitionLoss();
            am.StopTutorialNarration();
        }
        DoneTutorial(game);
        tutorialButtons[game].SetActive(false);
        if (game == 2) foreach (var l in FindObjectsOfType<LabyrinthScript>(true)) l.ShowStartButtonLabel();
        stoppedPlaying();
        AngerRoom.Shimmer.RecordingSession.Instance?.Log("debug_skip_minigame_tutorial", "game=" + game + "; interrupted=" + interruptedTutorial);
    }

    public AudioManager am;
    private Logger logger;
    private ScoreboardManager scoreManager;
    [SerializeField] float timer_experiment;
    [SerializeField] float time_left_exp;
    [SerializeField] TextMeshPro TMPtime_left_exp;
    [SerializeField] TextMeshPro[] scores = new TextMeshPro[3];
    //[SerializeField] TextMeshPro[] scores2 = new TextMeshPro[3];
    //private int gmTriesGun = 0;
    //private int gmTriesCards = 0;
    //private int gmTriesLab = 0;
    [SerializeField] int[] gameTries = {0,0,0};
    [SerializeField] int[] gamePoints = {100, 100, 100};
    [SerializeField] int[] wins = {0,0,0};
    // GamesManager owns the reward value and collection permission. The physical
    // boxes only display/animate it; they never calculate or award points themselves.
    private readonly bool[] collectedRewards = new bool[3];
    private bool experimentExpired;
    private bool minigamesFinished;
    public bool SessionEnding { get; private set; }
    // Derive this from the reward ledger instead of maintaining another flag
    // that could get out of sync when points are collected or a session expires.
    public bool HasPendingReward
    {
        get
        {
            for (int game = 0; game < gamePoints.Length; game++)
                if (CanCollectReward(game)) return true;
            return false;
        }
    }
    public bool AcquisitionBlocked { get; private set; }
    public float RemainingExperimentSeconds => time_left_exp;
    int activeAttempt = -1;
    bool activeTutorial;
    int attemptSequence;
    List<GameObject> attemptBlacklist;
    List<GameObject> attemptButtons;
    public void SetAcquisitionBlocked(bool blocked)
    {
        AcquisitionBlocked = blocked;
        RefreshControllerInteractions();
    }
    public void MarkAttemptGameplay(int game) { if (activeAttempt == game) activeTutorial = false; }
    public bool BeginAttempt(int game, bool tutorial)
    {
        // Introduction access is independent of sensor readiness. Guard the
        // method too, in case a UnityEvent calls it while its button is hidden.
        if (!openingDialogsComplete || AcquisitionBlocked || experimentExpired || SessionEnding || HasPendingReward || !IsValidGame(game) || IsGameResolved(game)) return false;
        if (activeAttempt == game) return true;
        if (activeAttempt >= 0) return false;
        activeAttempt = game;
        activeTutorial = tutorial;
        attemptSequence++;
        attemptBlacklist = new List<GameObject>(_reactivationBlacklist);
        attemptButtons = new List<GameObject>(_activationList);
        AngerRoom.Shimmer.RecordingSession.Instance?.Log("attempt_started", "id=" + attemptSequence + "; game=" + game + "; tutorial=" + tutorial);
        return true;
    }
    public bool CancelAttemptForAcquisitionLoss()
    {
        bool active = activeAttempt >= 0;
        if (active)
            AngerRoom.Shimmer.RecordingSession.Instance?.Log("attempt_canceled", "id=" + attemptSequence + "; game=" + activeAttempt + "; reason=acquisition_loss; remaining_attempts=" + getRemainingTries(activeAttempt) + "; points=" + GetRewardPoints(activeAttempt));
        // These APIs stop pending transitions as well as active countdowns.
        foreach (var card in FindObjectsOfType<Card_Minigame>(true)) card.CancelForAcquisitionLoss();
        foreach (var gun in FindObjectsOfType<GunPuzzle>(true)) gun.CancelForAcquisitionLoss();
        foreach (var labyrinth in FindObjectsOfType<LabyrinthScript>(true)) labyrinth.CancelForAcquisitionLoss();
        if (active && activeTutorial)
        {
            _reactivationBlacklist = new List<GameObject>(attemptBlacklist);
            _activationList = new List<GameObject>(attemptButtons);
            foreach (var button in realButtons) button.SetActive(false);
        }
        activeAttempt = -1;
        StopLoop();
        am.clock.Stop();
        return active;
    }
    public event System.Action<int> RewardChanged;

    public int GetRewardPoints(int game) => IsValidGame(game) ? gamePoints[game] : 0;
    public bool IsRewardCollected(int game) => IsValidGame(game) && collectedRewards[game];
    public bool IsGameResolved(int game) => IsValidGame(game) && (experimentExpired || wins[game] == 1 || gameTries[game] >= 3);
    public bool CanCollectReward(int game) => IsGameResolved(game) && !collectedRewards[game];
    private bool IsValidGame(int game) => game >= 0 && game < gamePoints.Length;

    public bool TryCollectReward(int game)
    {
        if (AcquisitionBlocked || SessionEnding || !CanCollectReward(game) || scoreManager == null)
            return false;

        // Claim before calling other components. Repeated reaches, animation
        // callbacks or duplicate requests cannot award this puzzle twice.
        collectedRewards[game] = true;
        scoreManager.AddPlayerPoints(gamePoints[game]);
        logger.Log("Collected reward for game id:" + game + " points:" + gamePoints[game]);
        RewardChanged?.Invoke(game);
        CheckCollectedEnding();
        RefreshStartControls();
        return true;
    }
    public GameObject[] bulbs= new GameObject[3];
    public Material[] lights = new Material[3];
    [SerializeField] private GameObject[] tutorialButtons= new GameObject[3];
    [SerializeField] private GameObject[] realButtons= new GameObject[3];
    private List<GameObject> _reactivationBlacklist = new List<GameObject>();
    public List<GameObject> _activationList = new List<GameObject>();
    private Coroutine audioloop;
    private bool isgoing;
    // Start is called before the first frame update
    void Awake() {
        InitializeHandedness();
#if ACQUISITION_RECOVERY_QUEST2_TEST
        enableDebugOperations = true;
#endif
        logger = GetComponent<Logger>();
        scoreManager = GetComponent<ScoreboardManager>();
        // Disable only the six minigame start controls, even when their parent
        // objects are inactive. Dialog canvases and their interactors stay alone.
        RefreshStartControls();
    }

    void Start()
    {
        AngerRoom.Shimmer.RecordingSession.Instance?.SetControlHand(IsLeftHanded);
        isgoing = false;
        foreach (GameObject butt in tutorialButtons)
        {
            if (!_activationList.Contains(butt)) _activationList.Add(butt);
        }
        RefreshStartControls();
        time_left_exp = timer_experiment;
        if (debugTutorialControls != null) debugTutorialControls.SetActive(false);
        if (enableDebugOperations) AngerRoom.Shimmer.RecordingSession.Instance?.Log("debug_mode_enabled");
    }

    public void StartOverAllTimer()
    {
        if (AcquisitionBlocked || isgoing || experimentExpired || minigamesFinished || SessionEnding) return;
        // The final introduction/Start button and the debug dialogue skipper
        // share this path: starting the experiment also releases the intro lock.
        openingDialogsComplete = true;
        AngerRoom.Shimmer.RecordingSession.Instance?.Log("gameplay_timer_started");
        stoppedPlaying();
        isgoing = true;
        StartCoroutine(OverallTimer());
    }
    private IEnumerator OverallTimer()
    {
        while (isgoing)
        {
        if (AcquisitionBlocked) { yield return null; continue; }
        int minutes = Mathf.FloorToInt(time_left_exp / 60);
        int seconds = Mathf.FloorToInt(time_left_exp % 60);
            if (time_left_exp > 0)
            {
            time_left_exp -= Time.deltaTime;
            TMPtime_left_exp.text = string.Format("{0:00}:{1:00}", minutes, seconds);
            }
            else
            {
            ExpireExperiment();
            am.PlayTimeout();
            }
            yield return null;
        }
    }

    private void ExpireExperiment()
    {
        // Close the session before publishing reward events. A minigame still
        // finishing its own animation cannot report a late win or extra failure.
        if (experimentExpired) return;
        experimentExpired = true;
        isgoing = false;
        time_left_exp = 0f;
        TMPtime_left_exp.text = "00:00";
        StopLoop();
        am.clock.Stop();

        // DeactivatingButtons is an IEnumerator: calling it without starting a
        // coroutine does nothing. At timeout, disable these controls immediately
        // and blacklist them so a late stoppedPlaying callback cannot restore them.
        foreach (GameObject button in _activationList) PermanentlyDeactivate(button);
        foreach (GameObject button in realButtons)
            if (!_reactivationBlacklist.Contains(button)) PermanentlyDeactivate(button);

        for (int game = 0; game < gamePoints.Length; game++)
        {
            // Keep completed results (including uncollected rewards) unchanged.
            // Only unfinished minigames receive the automatic ten-point fallback.
            if (wins[game] == 1 || gameTries[game] >= 3 || collectedRewards[game]) continue;
            gamePoints[game] = 10;
            // Reuse the normal exactly-once award path. RewardChanged tells the
            // box to hide its points object; no reach animation or sound is needed.
            TryCollectReward(game);
        }
    }
    
    private IEnumerator validInsults(float timer) {
        yield return new WaitForSeconds(23f);
        Debug.Log("Inizio Insulti");
        while (true)
        {
            if (Random.value < 0.8f)
            {
            am.play_gen_insult();
            yield return new WaitForSeconds(timer);
            }
            Debug.Log("Shucks");
            yield return new WaitForSeconds(1.7f);
        }
    }
    public void StartInsult(float timer_per_game)
    {
        if (AcquisitionBlocked || minigamesFinished || SessionEnding) return;
        // Keep the existing schedule if another event calls this again.
        // StopLoop clears the reference so the next minigame can start its own.
        if (audioloop != null) return;
        audioloop = StartCoroutine(validInsults(timer_per_game));
    }

    public void StopLoop()
    {
        if (audioloop != null)
        {
            StopCoroutine(audioloop);
            audioloop = null;
        }
    }
    // Update is called once per frame
/*
    public void addTriesX(int i, TextMeshPro message)
    {
        gameTries[i] += 1;
        logger.Log("Participant lost game id:" + i + " for the " + gameTries[i] + " time");
        switch(gameTries[i])
        {
            case 1: scores[i].color = new Color(217, 209, 0, 255);
                    scores[i].text = string.Format("80pts");
                    StartCoroutine(lossMessage(gameTries[i], false, message));
                    break;
            case 2: scores[i].color = new Color(240, 10, 10, 255);
                    scores[i].text = string.Format("50pts");
                    StartCoroutine(lossMessage(gameTries[i], false, message));
                    break;
            case 3: scores[i].color = new Color(132, 132, 132, 255);
                    scores[i].text = string.Format("10pts");
                    StartCoroutine(lossMessage(gameTries[i], true, message));
                    _reactivationBlacklist.Add(realButtons[i]);
                    break;
            default: Debug.Log("Ah");
                     break;
        }
        bulbs[i].GetComponent<Renderer>().material = lights[gameTries[i]-1];
        stoppedPlaying();
        CheckLost();
    }
    */

    public void addTries(int i, TextMeshPro message)
    {
        // Existing minigames keep their current messages until they are migrated.
        addTries(i, message, null);
    }

    public void addTries(int i, TextMeshPro message, TextAdapter textAdapter)
    {
        if (AcquisitionBlocked || SessionEnding || !IsValidGame(i) || IsGameResolved(i)) return;
        AngerRoom.Shimmer.RecordingSession.Instance?.Log("attempt_failed", "id=" + attemptSequence + "; game=" + i);
        gameTries[i] += 1;
        gamePoints[i] -= 30;
        Debug.Log(gamePoints[i]);
        //scores[i].text = string.Format(gamePoints[i]+"pts");
        scores[i].text = textAdapter != null
            ? textAdapter.Format(TextId.Points, gamePoints[i])
            : $"{gamePoints[i]}pts";
        logger.Log("Participant lost game id:" + i + " for the " + gameTries[i] + " time");
        switch(gameTries[i])
        {
            case 1:
                    //scores[i].text = string.Format("70pts");
                    //scores2[i].color = new Color(217, 209, 0, 255);
                    //scores2[i].text = string.Format("70pts");
                    StartCoroutine(lossMessage(gameTries[i], false, message, textAdapter));
                    break;
            case 2:
                    //scores[i].text = string.Format("40pts");
                    //scores2[i].color = new Color32(240, 10, 10, 255);
                    //scores2[i].text = string.Format("50pts");
                    StartCoroutine(lossMessage(gameTries[i], false, message, textAdapter));
                    break;
            case 3:
                    //scores[i].text = string.Format("10pts");
                    //scores2[i].color = new Color32(132, 132, 132, 200);
                    //scores2[i].text = string.Format("10pts");
                    // Ten points remain after the third failure. Unlock the box;
                    // the scoreboard receives them only when its reward is collected.
                    StartCoroutine(lossMessage(gameTries[i], true, message, textAdapter));
                    _reactivationBlacklist.Add(realButtons[i]);
                    break;
            default: Debug.Log("Ah");
                     break;
        }
        bulbs[i].GetComponent<Renderer>().material = lights[gameTries[i]-1];
        RewardChanged?.Invoke(i);
        stoppedPlaying();
        CheckLost();
    }

    private IEnumerator lossMessage(int tries, bool loss, TextMeshPro message, TextAdapter textAdapter)
    {
        if (message == null) yield break; // Debug resolution needs no temporary attempt message.
        if (!loss)
        {
            int remainingTries = 3 - tries;
            string displayedMessage = textAdapter != null
                ? textAdapter.Format(TextId.AttemptFailed, remainingTries)
                : $"Has fallado :(\nIntentos restantes: {remainingTries}";
            message.text = displayedMessage;
            yield return new WaitForSeconds(4);
            // A debug result (or a later attempt) can replace this message before
            // its delay ends. Never let the old timer erase that newer text.
            if (message != null && message.text == displayedMessage) message.text = string.Empty;
        }
        else
        {
            message.text = textAdapter != null
                ? textAdapter.Get(TextId.GameDisabled)
                : "Has fallado!\nJuego desactivado.";
        }
    }

    public int getRemainingTries(int game)
    {
        if (experimentExpired && wins[game] != 1) return 0;
        return 3-gameTries[game];
    }
    public void CheckLost()
    {
        // Resolved means won OR all attempts used. It does not mean collected:
        // stop the countdown now, but leave the reward boxes usable.
        if (experimentExpired || minigamesFinished || SessionEnding) return;
        for (int game = 0; game < gamePoints.Length; game++)
            if (!IsGameResolved(game)) return;
        minigamesFinished = true;
        isgoing = false;
        StopLoop();
        am.clock.Stop();
        AngerRoom.Shimmer.RecordingSession.Instance?.Log("all_minigames_resolved");
        CheckCollectedEnding();
    }

    private void CheckCollectedEnding()
    {
        // Timeout settles unfinished rewards separately and uses the loss ending.
        if (experimentExpired || SessionEnding || !minigamesFinished) return;
        foreach (bool collected in collectedRewards) if (!collected) return;
        int wonCount = 0;
        foreach (int win in wins) wonCount += win;
        if (wonCount == 0) am.playLoss();
        else am.playEnd(); // One completion message for both mixed results and all wins.
    }

    public void BeginSessionEnding()
    {
        if (SessionEnding) return;
        SessionEnding = true;
        RefreshControllerInteractions();
        isgoing = false;
        activeAttempt = -1;
        StopLoop();
        am.clock.Stop();
        // Stop active countdowns and pending transitions at timeout. Reuse the
        // existing cleanup APIs; this does not consume another attempt.
        foreach (var card in FindObjectsOfType<Card_Minigame>(true)) card.CancelForAcquisitionLoss();
        foreach (var gun in FindObjectsOfType<GunPuzzle>(true)) gun.CancelForAcquisitionLoss();
        foreach (var labyrinth in FindObjectsOfType<LabyrinthScript>(true)) labyrinth.CancelForAcquisitionLoss();
        foreach (var button in _activationList) button.SetActive(false);
        foreach (var button in realButtons) button.SetActive(false);
    }

    public IEnumerator QuitAfterEnding()
    {
        // Do not let the recovery gate interpret an intentional sensor shutdown
        // as an outage and reconnect while the final CSV is being saved.
        var gate = AngerRoom.Shimmer.AcquisitionGate.Instance;
        if (gate != null) gate.enabled = false;
        AngerRoom.Shimmer.RecordingSession.Instance?.Log("ending_audio_finished");
        foreach (var receiver in FindObjectsOfType<AngerRoom.Shimmer.ShimmerReceiver>())
            yield return receiver.FinishSessionRecording();
        AngerRoom.Shimmer.RecordingSession.Instance?.Log("application_quit_requested");
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    public void Playing()
    {
        if (AcquisitionBlocked || SessionEnding) return;
        StartCoroutine(DeactivatingButtons());
    }

    public IEnumerator DeactivatingButtons()
    {
        yield return new WaitForSeconds(0.1f);
        RefreshStartControls();
    }

    public void DoneTutorial(int i)
    {
        if (AcquisitionBlocked) return;
        if (i == 0) foreach (var cards in FindObjectsOfType<Card_Minigame>(true)) cards.ShowStartButtonLabel();
        if (i == 1) foreach (var gun in FindObjectsOfType<GunPuzzle>(true)) gun.ShowStartButtonLabel();
        if (!_reactivationBlacklist.Contains(tutorialButtons[i])) _reactivationBlacklist.Add(tutorialButtons[i]);
        if (!_activationList.Contains(realButtons[i])) _activationList.Add(realButtons[i]);
    }

    public void stoppedPlaying()
    {
        if (AcquisitionBlocked || SessionEnding) return;
        activeAttempt = -1;
        am.clock.Stop();
        RefreshStartControls();
        StopLoop();
    }

    private void RefreshStartControls()
    {
        // Revealing GameComponents during narration must not unlock a minigame.
        // These restrictions do not disable the Dialog buttons or controller UI.
        bool canStart = openingDialogsComplete && activeAttempt < 0 && !HasPendingReward &&
            !AcquisitionBlocked && !experimentExpired && !SessionEnding;
        foreach (GameObject button in tutorialButtons) RefreshStartControl(button, canStart);
        foreach (GameObject button in realButtons) RefreshStartControl(button, canStart);
    }

    private void RefreshStartControl(GameObject button, bool canStart)
    {
        // Real start buttons become eligible only after their tutorial is done.
        if (button != null)
            button.SetActive(canStart && _activationList.Contains(button) && !_reactivationBlacklist.Contains(button));
    }
    private void PermanentlyDeactivate(GameObject obj)
    {
        _reactivationBlacklist.Add(obj);
        obj.SetActive(false);
        Debug.Log(obj.name + " permanently deactivated.");
    }

    public void GameWon(int game)
    {
        if (AcquisitionBlocked || SessionEnding || !IsValidGame(game) || IsGameResolved(game)) return;
        AngerRoom.Shimmer.RecordingSession.Instance?.Log("attempt_won", "id=" + attemptSequence + "; game=" + game);
        logger.Log("Game  id:" + game + " was won with points" + gamePoints[game]);
        wins[game] = 1;
        _reactivationBlacklist.Add(realButtons[game]);
        RewardChanged?.Invoke(game);
        RefreshStartControls();
        CheckLost();
    }

}

public sealed class DebugGripHold
{
    double began = -1;
    bool fired;
    public bool Poll(bool enabled, bool held, double now)
    {
        if (!enabled || !held) { began = -1; fired = false; return false; }
        if (began < 0) began = now;
        if (fired || now - began < 5) return false;
        fired = true;
        return true;
    }
}
