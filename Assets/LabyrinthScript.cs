using System;
using System.Collections;
using AngerRoom.Localization;
using UnityEngine;
using TMPro;

public class LabyrinthScript : MonoBehaviour
{
    [Serializable]
    private sealed class LevelSettings
    {
        public GameObject labyrinthRoot;
        public Transform destination;
        [Tooltip("Total countdown, including the existing three-second blackout.")]
        [Min(3f)] public float duration;
        public GameObject angerObstacles;
    }

    public GameObject blacked;
    public GameObject player;
    public Transform newPos;
    [SerializeField] TextMeshPro TMPTimer;
    [SerializeField] TextMeshPro congrats;
    [SerializeField] private TextAdapter textAdapter;
    [SerializeField] private TMP_Text startButtonText;
    [SerializeField] private TMP_Text tutorialSignText;
    [SerializeField] private TMP_Text[] levelSigns = new TMP_Text[3];
    [Header("Tutorial")]
    [SerializeField] private GameObject tutorialRoot;
    [SerializeField] private Transform tutorialDestination;
    [Header("Levels - first, second, third")]
    [SerializeField] private LevelSettings[] levels = new LevelSettings[3];
    [SerializeField] private Logger logger;
    public AudioSource headAudio;
    public GamesManager gm;
    public AudioManager am;
    public bool angerVar;
    public bool AngerEnabled => angerVar;

    private float time_left;
    private float initialPuzzleTimer;
    private bool started;
    private bool inLabyrinth;
    private bool transitioning;
    private int level;
    public bool IsTutorialActive => inLabyrinth && !started;
    private bool CanInteract => !transitioning && !gm.AcquisitionBlocked && !gm.SessionEnding;

    void Start()
    {
        initialPuzzleTimer = levels[0].duration;
        time_left = initialPuzzleTimer;
        congrats.text = string.Empty;
        // Inactive labyrinths remember their children's activeSelf state.
        // Entering a level always reapplies angerVar, including after debug skips.
        SetAngerObstacles(-1);
        ShowOnlyLabyrinth(null);
        startButtonText.text = textAdapter.Get(TextId.TutorialLabel);
        tutorialSignText.text = textAdapter.Get(TextId.TutorialLabel);
        for (int i = 0; i < levelSigns.Length; i++)
            levelSigns[i].text = textAdapter.Format(TextId.LevelNumber, i + 1);
    }

    public void ShowStartButtonLabel() => startButtonText.text = textAdapter.Get(TextId.StartLabel);
    public void ShowTutorialTimerLabel() => TMPTimer.text = textAdapter.Get(TextId.TimerLabel);
    public void ShowDebugResult(bool won) => congrats.text = textAdapter.Get(won ? TextId.Victory : TextId.GameDisabled);

    public void BeginTutorial()
    {
        if (!CanInteract || inLabyrinth || !gm.BeginAttempt(2, true)) return;
        level = 0;
        inLabyrinth = true;
        started = false;
        congrats.text = string.Empty;
        SetAngerObstacles(-1);
        BeginTransition(tutorialDestination, tutorialRoot);
        gm.Playing();
        ShowStartButtonLabel();
        gm.DoneTutorial(2);
        am.playTutorial(2);
        ShowTutorialTimerLabel();
        logger.Log("Started labyrinth tutorial");
    }

    // Buttons use level numbers 1, 2, 3; only this method converts to indices.
    // Both level-1 buttons call here, so retries and tutorial skips use the
    // same duration, destination and anger setup as a completed tutorial.
    public void BeginLevel(int levelNumber)
    {
        if (!CanInteract || levelNumber < 1 || levelNumber > levels.Length) return;
        if (levelNumber == 1)
        {
            if (started || (inLabyrinth && level != 0) || !gm.BeginAttempt(2, false)) return;
            gm.MarkAttemptGameplay(2);
        }
        else if (!inLabyrinth || !started || levelNumber != level + 1) return;

        LevelSettings settings = levels[levelNumber - 1];
        level = levelNumber;
        inLabyrinth = true;
        started = true;
        time_left = settings.duration;
        congrats.text = string.Empty;
        // These obstacles already exist in the scene: nothing is instantiated.
        // Configure them BEFORE activating the destination labyrinth root.
        SetAngerObstacles(levelNumber - 1);
        BeginTransition(settings.destination, settings.labyrinthRoot);
        UpdateTimerText();
        if (levelNumber == 1)
        {
            gm.Playing();
            am.clock.Play();
            gm.StartInsult(19f);
        }
        logger.Log("Started labyrinth level " + levelNumber);
    }

    public void ReturnFromTutorial()
    {
        if (!CanInteract || !inLabyrinth || started || level != 0) return;
        ReturnToTheatre();
        gm.stoppedPlaying();
    }

    public void FinishMinigame()
    {
        if (!CanInteract || !inLabyrinth || !started || level != levels.Length) return;
        congrats.text = textAdapter.Get(TextId.Victory);
        ReturnToTheatre();
        gm.GameWon(2);
        gm.stoppedPlaying();
    }

    private void ReturnToTheatre()
    {
        started = false;
        inLabyrinth = false;
        level = 0;
        TMPTimer.text = string.Empty;
        SetAngerObstacles(-1);
        BeginTransition(newPos, null);
    }

    private void BeginTransition(Transform destination, GameObject destinationRoot)
    {
        // Set the guard before a second button event can arrive.
        transitioning = true;
        blacked.SetActive(true);
        if (destinationRoot != null) destinationRoot.SetActive(true);
        am.playElevator();
        StartCoroutine(TeleportAfterBlackout(destination, destinationRoot));
    }

    private IEnumerator TeleportAfterBlackout(Transform destination, GameObject destinationRoot)
    {
        // Preserve existing timing: the countdown includes these three seconds.
        // The configured durations are not adjusted by this method.
        yield return new WaitForSeconds(3f);
        PlatformTeleport.TryTeleport(player, destination);
        // Keep the previous floor present until the player has left it.
        ShowOnlyLabyrinth(destinationRoot);
        blacked.SetActive(false);
        transitioning = false;
    }

    private void SetAngerObstacles(int activeIndex)
    {
        for (int i = 0; i < levels.Length; i++)
            levels[i].angerObstacles.SetActive(angerVar && i == activeIndex);
    }

    private void ShowOnlyLabyrinth(GameObject activeRoot)
    {
        tutorialRoot.SetActive(tutorialRoot == activeRoot);
        foreach (LevelSettings settings in levels)
            settings.labyrinthRoot.SetActive(settings.labyrinthRoot == activeRoot);
    }

    private void UpdateTimerText()
    {
        float remaining = Mathf.Max(0f, time_left);
        TMPTimer.text = textAdapter.Format(TextId.LabyrinthStatus, level,
            Mathf.FloorToInt(remaining / 60), Mathf.FloorToInt(remaining % 60));
    }

    void Update()
    {
        if (gm.AcquisitionBlocked || gm.SessionEnding || !started) return;
        time_left -= Time.deltaTime;
        UpdateTimerText();
        if (time_left > 0) return;
        StopAllCoroutines();
        ReturnToTheatre();
        am.playBad(2);
        gm.addTries(2, congrats, textAdapter);
    }

    public void CancelForAcquisitionLoss()
    {
        if (!inLabyrinth && !started && !transitioning && !blacked.activeSelf) return;
        StopAllCoroutines();
        started = false;
        inLabyrinth = false;
        transitioning = false;
        PlatformTeleport.TryTeleport(player, newPos);
        blacked.SetActive(false);
        SetAngerObstacles(-1);
        ShowOnlyLabyrinth(null);
        level = 0;
        time_left = initialPuzzleTimer;
        TMPTimer.text = congrats.text = string.Empty;
    }
}
