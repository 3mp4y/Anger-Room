using System.Collections;
using System.Collections.Generic;
using AngerRoom.Localization;
using UnityEngine;
using TMPro;
public class GamesManager : MonoBehaviour
{
    [Header("Debug")]
    [Tooltip("Enable right-grip (5 seconds) dialog skip and the three tutorial skip buttons. Keep disabled for participant sessions.")]
    public bool enableDebugOperations;
    [SerializeField] private GameObject debugTutorialControls;
    [SerializeField] private UnityEngine.UI.Button[] debugTutorialButtons = new UnityEngine.UI.Button[3];
    [SerializeField] private GameObject[] openingDialogObjects;
    [SerializeField] private GameObject[] openingObjectsToActivate;
    [SerializeField] private GameObject openingStartObject;
    [SerializeField] private Behaviour openingLightFollow;
    bool openingDialogsComplete;
    readonly DebugGripHold debugGrip = new DebugGripHold();

    void Update()
    {
        bool allowed = enableDebugOperations && !AcquisitionBlocked && !experimentExpired;
        if (debugGrip.Poll(allowed && !openingDialogsComplete,
            OVRInput.Get(OVRInput.RawButton.RHandTrigger, OVRInput.Controller.RTouch), Time.realtimeSinceStartupAsDouble))
            DebugSkipOpeningDialogs();
        bool show = allowed && openingDialogsComplete;
        if (debugTutorialControls != null && debugTutorialControls.activeSelf != show) debugTutorialControls.SetActive(show);
        for (int i = 0; i < debugTutorialButtons.Length; i++)
            if (debugTutorialButtons[i] != null) debugTutorialButtons[i].interactable = CanDebugSkipTutorial(i);
    }

    public void DebugSkipOpeningDialogs()
    {
        if (!enableDebugOperations || AcquisitionBlocked || experimentExpired || openingDialogsComplete) return;
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
        AngerRoom.Shimmer.RecordingSession.Instance?.Log("debug_skip_opening_dialogs", "right grip held 5 seconds");
    }

    public bool CanDebugSkipTutorial(int game) => enableDebugOperations && !AcquisitionBlocked &&
        openingDialogsComplete && IsValidGame(game) && !IsGameResolved(game) &&
        ((activeAttempt == game && activeTutorial) || (activeAttempt < 0 && !_reactivationBlacklist.Contains(tutorialButtons[game])));

    public void DebugSkipCardsTutorial() => DebugSkipTutorial(0);
    public void DebugSkipShootingTutorial() => DebugSkipTutorial(1);
    public void DebugSkipLabyrinthTutorial() => DebugSkipTutorial(2);
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
    private int gmTriesGun = 0;
    private int gmTriesCards = 0;
    private int gmTriesLab = 0;
    [SerializeField] int[] gameTries = {0,0,0};
    [SerializeField] int[] gamePoints = {100, 100, 100};
    [SerializeField] int[] wins = {0,0,0};
    // GamesManager owns the reward value and collection permission. The physical
    // boxes only display/animate it; they never calculate or award points themselves.
    private readonly bool[] collectedRewards = new bool[3];
    private bool experimentExpired;
    public bool AcquisitionBlocked { get; private set; }
    public float RemainingExperimentSeconds => time_left_exp;
    int activeAttempt = -1;
    bool activeTutorial;
    int attemptSequence;
    List<GameObject> attemptBlacklist;
    List<GameObject> attemptButtons;
    public void SetAcquisitionBlocked(bool blocked) => AcquisitionBlocked = blocked;
    public void MarkAttemptGameplay(int game) { if (activeAttempt == game) activeTutorial = false; }
    public bool BeginAttempt(int game, bool tutorial)
    {
        if (AcquisitionBlocked || IsGameResolved(game)) return false;
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
        if (AcquisitionBlocked || !CanCollectReward(game) || scoreManager == null)
            return false;

        // Claim before calling other components. Repeated reaches, animation
        // callbacks or duplicate requests cannot award this puzzle twice.
        collectedRewards[game] = true;
        scoreManager.AddPlayerPoints(gamePoints[game]);
        logger.Log("Collected reward for game id:" + game + " points:" + gamePoints[game]);
        RewardChanged?.Invoke(game);
        return true;
    }
    public GameObject[] bulbs= new GameObject[3];
    public Material[] lights = new Material[3];
    [SerializeField] private GameObject[] tutorialButtons= new GameObject[3];
    [SerializeField] private GameObject[] realButtons= new GameObject[3];
    private List<GameObject> _reactivationBlacklist = new List<GameObject>();
    public List<GameObject> _activationList;
    private Coroutine audioloop;
    private bool isgoing;
    // Start is called before the first frame update
    void Awake() {
#if ACQUISITION_RECOVERY_QUEST2_TEST
        enableDebugOperations = true;
#endif
        logger = GetComponent<Logger>();
        scoreManager = GetComponent<ScoreboardManager>();
    }

    void Start()
    {
        isgoing = false;
        foreach (GameObject butt in tutorialButtons)
        {
            _activationList.Add(butt);
        }
        //Playing(); //Deactive buttons while the host speaks
        time_left_exp = timer_experiment;
        if (debugTutorialControls != null) debugTutorialControls.SetActive(false);
        if (enableDebugOperations) AngerRoom.Shimmer.RecordingSession.Instance?.Log("debug_mode_enabled");
    }

    public void StartOverAllTimer()
    {
        if (AcquisitionBlocked || isgoing || experimentExpired) return;
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
            am.playLoss();
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
    
    private IEnumerator validInsults() {
        yield return new WaitForSeconds(23f);
        Debug.Log("Inizio Insulti");
        while (true)
        {
            if (Random.Range(0, 1) == 0)
            {
            am.play_gen_insult();
            yield return new WaitForSeconds(33f);
            }
            Debug.Log("Shucks");
            yield return new WaitForSeconds(5f);
        }
    }
    public void StartInsult()
    {
        if (AcquisitionBlocked) return;
        audioloop = StartCoroutine(validInsults());
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
        if (AcquisitionBlocked || !IsValidGame(i) || IsGameResolved(i)) return;
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
        if (!loss)
        {
            int remainingTries = 3 - tries;
            message.text = textAdapter != null
                ? textAdapter.Format(TextId.AttemptFailed, remainingTries)
                : $"Has fallado :(\nIntentos restantes: {remainingTries}";
            yield return new WaitForSeconds(4);
            message.text = string.Empty;
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
        if (gameTries[0] == 3 && gameTries[1] == 3 && gameTries[2] == 3)
        {
            am.playLoss();
        }
    }

    public void Playing()
    {
        if (AcquisitionBlocked) return;
        StartCoroutine(DeactivatingButtons());
    }

    public IEnumerator DeactivatingButtons()
    {
        yield return new WaitForSeconds(0.1f);
        foreach (GameObject button in _activationList)
        {
            button.SetActive(false);
        }
    }

    public void DoneTutorial(int i)
    {
        if (AcquisitionBlocked) return;
        if (!_reactivationBlacklist.Contains(tutorialButtons[i])) _reactivationBlacklist.Add(tutorialButtons[i]);
        if (!_activationList.Contains(realButtons[i])) _activationList.Add(realButtons[i]);
    }

    public void stoppedPlaying()
    {
        if (AcquisitionBlocked) return;
        activeAttempt = -1;
        am.clock.Stop();
        foreach (GameObject butt in _activationList)
        {
            if (!_reactivationBlacklist.Contains(butt))
                butt.SetActive(true);
        }
        StopLoop();
    }
    private void PermanentlyDeactivate(GameObject obj)
    {
        _reactivationBlacklist.Add(obj);
        obj.SetActive(false);
        Debug.Log(obj.name + " permanently deactivated.");
    }

    public void GameWon(int game)
    {
        if (AcquisitionBlocked || !IsValidGame(game) || IsGameResolved(game)) return;
        AngerRoom.Shimmer.RecordingSession.Instance?.Log("attempt_won", "id=" + attemptSequence + "; game=" + game);
        logger.Log("Game  id:" + game + " was won with points" + gamePoints[game]);
        wins[game] = 1;
        _reactivationBlacklist.Add(realButtons[game]);
        RewardChanged?.Invoke(game);
        if (wins[0] == 1 && wins[1] == 1 && wins[2] == 1)
        {
          am.playEnd();
        }
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
