// STUDY EXAMPLE ONLY: kept outside Assets, so Unity does not compile or attach it.
// This is an alternative version of the shooting controller, not an extra timer
// component to run alongside the live controller. See README.md for integration limits.
using System.Collections;
using System.Collections.Generic;
using AngerRoom.Localization;
using UnityEngine;
using TMPro;
using AngerRoom.Examples.Timers;
public class ShootingMinigameTimerExample : MonoBehaviour
{

    public AudioManager am;
    [Tooltip("The shared TextAdapter on the AudioManager object.")]
    [SerializeField] private TextAdapter textAdapter;
    [SerializeField] Logger logger;
    //Variables for gun and controller models
    public GameObject realgun;
    public GameObject fakegun;
    [SerializeField] private GameObject parentObject;
    [SerializeField] GameObject endTutorialObject;
    [SerializeField] List<GameObject> ObjectsToDeactivateWhilePlaying;
    [SerializeField] GameObject gunLight;
    private List<GameObject> _activeGrandchildren  = new List<GameObject>();
    //Varibales for the timer
    [SerializeField] TextMeshPro TMPTimer;
    [SerializeField] TextMeshPro TMPLevel;
    [SerializeField] TextMeshPro TMPScore;
    [Tooltip("The Text (TMP) child of EndTut, including when EndTut is inactive.")]
    [SerializeField] private TMP_Text endTutorialText;
    [Tooltip("Copy the current first-level duration from the shooting controller before testing this example.")]
    [Min(0.01f)] public float roundDurationSeconds;
    // Each controller instance owns a DIFFERENT timer object. This is not static.
    private readonly CountdownTimer roundTimer = new CountdownTimer();
    [SerializeField] private int hits;
    private int level;
    public GamesManager gm;
    public Spawn2 Spawner;
    [SerializeField] int score_to_reach;
    [SerializeField] ShootScript shoot_script;
    [SerializeField] bool angerVar;
    // Read-only access for GameConditions; the existing Inspector flag stays here.
    public bool AngerEnabled => angerVar;
    private bool tutorial;
    // Start is called before the first frame update
    void Start()
    {
        tutorial = false;
        roundTimer.Stop();
        hits = 0;
        level = 1;

        TMPScore.text = string.Format("");
        TMPLevel.text = string.Format("");
        TMPTimer.text = string.Format("");
        endTutorialText.text = textAdapter.Get(TextId.EndTutorial);
        shoot_script.failureChance = angerVar ? 10 : 0;
    }

    public void Begin()
    {
        foreach (GameObject obj in ObjectsToDeactivateWhilePlaying)
        {
            obj.SetActive(false);
        }
        gunLight.SetActive(true);
        _activeGrandchildren.Clear();

        foreach (Transform child in parentObject.transform)
        {
            foreach (Transform grandchild in child)
            {
                if (grandchild.gameObject.activeSelf)
                {
                    //Debug.Log(grandchild);
                    _activeGrandchildren.Add(grandchild.gameObject);
                    grandchild.gameObject.SetActive(false);
                }
            }
        }
    }
    public void Reset()
    {
        foreach (GameObject obj in ObjectsToDeactivateWhilePlaying)
        {
            obj.SetActive(true);
        }
        gunLight.SetActive(false);
        foreach (GameObject grandchild in _activeGrandchildren)
        {
            grandchild.SetActive(true);
        }
    }
    public void StartGun()
    {
        // Start also resets a previous attempt. Time advances only when Update ticks it.
        roundTimer.Start(roundDurationSeconds);
        shoot_script.failureChance = angerVar ? 10 : 0;
        Begin();
        fakegun.SetActive(false);
        realgun.SetActive(true);
        gm.Playing();

        hits = 0;
        level = 1;

        ConfigureTargetMovementForCurrentLevel();
        Spawner.setSpawnTime(1.4f);
        Spawner.StartSpawning();
        TMPScore.text = textAdapter.Format(TextId.ScoreProgress, hits, score_to_reach);
        TMPLevel.text = textAdapter.Format(TextId.LevelProgress, level, 3);
        UpdateTimerDisplay();
    }


    private void ConfigureTargetMovementForCurrentLevel()
    {
        // Movement ownership:
        // 1. ShootingMinigameTimerExample chooses settings for the current level.
        // 2. Spawn2 remembers them for targets it will create next.
        // 3. Spawn2 applies them to each NEW target's TargetMover component.
        // We never modify the TargetMover on the prefab asset itself.
        //
        // Calculate from the level number, not from a previous target's values.
        // This makes a retry start at level-one speeds even after a tutorial
        // or a previous attempt reached level three. The original progression is:
        // Level 1: 0.2-0.6 units/sec, new speed every 2 seconds.
        // Level 2: 0.6-1.5 units/sec, new speed every 0.8 seconds.
        // Level 3: 1.0-2.4 units/sec, new speed every 0.32 seconds.
        int completedLevels = level - 1;
        float minimumSpeed = 0.2f + completedLevels * 0.4f;
        float maximumSpeed = 0.6f + completedLevels * 0.9f;
        float speedChangeInterval = 2f * Mathf.Pow(0.4f, completedLevels);

        // Configure BEFORE StartSpawning. Existing targets keep their own values;
        // the level transition already clears them before starting the next level.
        Spawner.ConfigureMovement(minimumSpeed, maximumSpeed, speedChangeInterval);
    }

    public void StartTutorial2()
    {
        roundTimer.Stop(); // Tutorials do not consume a timed attempt.
        tutorial = true;
        gm.DoneTutorial(1);
        gm.Playing();
        TMPScore.text = textAdapter.Get(TextId.TargetScoreLabel);
        TMPLevel.text = textAdapter.Get(TextId.LevelLabel);
        TMPTimer.text = textAdapter.Get(TextId.TutorialTimerLabel);
        // Both conditions use stationary practice targets. These settings apply
        // to new tutorial targets only; StartGun replaces them for level one.
        Spawner.ConfigureMovement(0.0f, 0.0f, 1.0f);
        am.playTutorial(1);
        StartCoroutine(Tutorial());
    }
    // Unity supplies elapsed time; CountdownTimer owns the arithmetic/state.
    private void Update()
    {
        bool expiredThisFrame = roundTimer.Tick(Time.deltaTime);
        if (expiredThisFrame)
        {
            HandleTimeExpired();
            return; // Do not overwrite the failure message with a timer display.
        }

        if (roundTimer.IsRunning)
            UpdateTimerDisplay();
    }

    private void UpdateTimerDisplay()
    {
        // Display policy belongs here, not inside the reusable timer.
        // Ceiling displays 01:00 while 59.9 seconds remain; zero is reserved for expiry.
        int displayedSeconds = Mathf.CeilToInt(roundTimer.RemainingSeconds);
        TMPTimer.text = $"{displayedSeconds / 60:00}:{displayedSeconds % 60:00}";
    }

    private void HandleTimeExpired()
    {
        // These are shooting-minigame decisions. CountdownTimer knows none of them.
        Spawner.StopSpawning();
        Spawner.ClearTargets();
        fakegun.SetActive(true);
        realgun.SetActive(false);
        Reset();
        TMPTimer.text = string.Empty;
        TMPScore.text = string.Empty;
        TMPLevel.text = string.Empty;
        gm.addTries(1, TMPTimer, textAdapter);
        am.playBad(1);
    }

    private IEnumerator Tutorial()
    {
        while (am.isPresentSpeaking())
            {
                yield return null;
            }
        Begin();
        endTutorialObject.SetActive(true);
        realgun.SetActive(true);
        fakegun.SetActive(false);
        Spawner.StartSpawning();
        Spawner.setSpawnTime(5.0f);
    }

    public void Endtutorial()
    {
        roundTimer.Stop();
        Spawner.ClearTargets();
        endTutorialObject.SetActive(false);
        fakegun.SetActive(true);
        realgun.SetActive(false);
        Reset();
        Spawner.StopSpawning();
        TMPScore.text = string.Format("");
        TMPLevel.text = string.Empty;
        TMPTimer.text = string.Format("");
        tutorial = false;
        gm.stoppedPlaying();
    }

    public void GotHit()
    {
        StartCoroutine(CoroutineHit());
    }
    private IEnumerator CoroutineHit()
    {
    // Ignore extra target reports during expiry, victory or a level transition.
    if (!tutorial && roundTimer.IsRunning)
        {
        hits++;
        TMPScore.text = textAdapter.Format(TextId.ScoreProgress, hits, score_to_reach);
        if (hits >= score_to_reach) {
            roundTimer.Stop();
            Spawner.StopSpawning();
            Spawner.ClearTargets();
                if (level < 3)
                {
                am.playLevelUp(1);
                hits = 0;
                level++;
                TMPLevel.text = textAdapter.Format(TextId.LevelProgress, level, 3);
                TMPScore.text = textAdapter.Format(TextId.ScoreProgress, hits, score_to_reach);
                float timeLevelUp = 3.9f;
                while (timeLevelUp > 0.2f)
                    {
                    timeLevelUp -= Time.deltaTime;
                    int seconds = Mathf.FloorToInt(timeLevelUp % 60);
                    TMPTimer.text = textAdapter.Format(TextId.NextLevelCountdown, seconds);
                    yield return null;
                    }
                ConfigureTargetMovementForCurrentLevel();
                // Preserve the current shooting rules: later levels get 21 seconds.
                roundTimer.Start(21f);
                UpdateTimerDisplay();
                if (angerVar) {shoot_script.failureChance += 20;}
                TMPScore.text = textAdapter.Format(TextId.ScoreProgress, hits, score_to_reach);
                Spawner.StartSpawning();

                }
                else
                {
                gm.GameWon(1);
                gm.stoppedPlaying();
                roundTimer.Stop();

                TMPTimer.text = textAdapter.Get(TextId.Victory);
                fakegun.SetActive(true);
                realgun.SetActive(false);
                Reset();
                TMPScore.text = string.Format("");
                TMPLevel.text = string.Format("");
                }
            }
        }
    }

    public bool GetGunTutorial()
    {
        return tutorial;
    }
}
