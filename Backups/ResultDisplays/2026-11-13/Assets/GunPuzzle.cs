using System.Collections;
using System.Collections.Generic;
using AngerRoom.Localization;
using UnityEngine;
using TMPro;
public class GunPuzzle : MonoBehaviour
{

    public AudioManager am;
    [Tooltip("The shared TextAdapter on the AudioManager object.")]
    [SerializeField] private TextAdapter textAdapter;
    [SerializeField] Logger logger;
    //Variables for gun and controller models
    public GameObject realgun;
    public GameObject fakegun;
    [SerializeField] private GameObject parentObject;
    [Header("Gun grip mounts (local pose, set in Edit Mode)")]
    [SerializeField] private Transform leftGunMount;
    [SerializeField] private Transform rightGunMount;
    [SerializeField] GameObject endTutorialObject;
    [SerializeField] List<GameObject> ObjectsToDeactivateWhilePlaying;
    [SerializeField] GameObject gunLight;
    private List<GameObject> _activeGrandchildren  = new List<GameObject>();
    //Varibales for the timer
    [SerializeField] TextMeshPro TMPTimer;
    [SerializeField] TextMeshPro TMPLevel;
    [SerializeField] TextMeshPro TMPScore;
    [SerializeField] private TMP_Text startButtonText;
    private bool resultVisible;
    [Header("Shooting readout comparison")]
    [UnityEngine.Serialization.FormerlySerializedAs("leftControllerReadout")]
    [SerializeField] private Readout screenReadout = new Readout();
    [SerializeField] private Readout centerEyeReadout = new Readout();
    [SerializeField] private Readout OldTexts = new Readout();
    private bool readoutsActive;

    [System.Serializable]
    private sealed class Readout
    {
        [Tooltip("Show this layout during shooting practice and timed rounds.")]
        [SerializeField] private bool show = true;
        [SerializeField] private GameObject root;
        [SerializeField] private TMP_Text levelLabel, hitsLabel, timerLabel;

        public void Refresh(bool active, TMP_Text level, TMP_Text hits, TMP_Text timer)
        {
            if (root == null) return;
            bool visible = active && show;
            if (root.activeSelf != visible) root.SetActive(visible);
            if (!visible) return;

            // Mirror the existing localized strings, including the countdown
            // between levels. These panels never calculate score or time.
            levelLabel.text = level.text;
            hitsLabel.text = hits.text;
            timerLabel.text = timer.text;
        }
    }
    [Tooltip("The Text (TMP) child of EndTut, including when EndTut is inactive.")]
    [SerializeField] private TMP_Text endTutorialText;
    public float puzzleTimer;
    private float time_left;
    private bool started;
    [Header("Preparation")]
    [Tooltip("Seconds to get ready before targets and the first round timer start.")]
    [SerializeField, Min(0f)] private float preparationSeconds = 3f;
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
    private bool environmentChanged;
    //CountDown counScript;
    // Start is called before the first frame update
    void Start()
    {
        startButtonText.text = textAdapter.Get(TextId.TutorialLabel);
        AttachGunToSelectedHand();
        tutorial = false;
        started = false;
        hits = 0;
        level = 1;
        time_left = puzzleTimer;
        TMPScore.text = string.Format("");
        TMPLevel.text = string.Format("");
        TMPTimer.text = string.Format("");
        endTutorialText.text = textAdapter.Get(TextId.EndTutorial);
        shoot_script.failureChance = angerVar ? 10 : 0; 
    }

    public void Begin()
    {
        if (gm.AcquisitionBlocked) return;
        resultVisible = false;
        environmentChanged = true;
        gm.UseShootingControls(true);
        foreach (GameObject obj in ObjectsToDeactivateWhilePlaying)
        {
            obj.SetActive(false);
        }
        gunLight.SetActive(true);
        _activeGrandchildren.Clear();

        foreach (Transform child in parentObject.transform)
        {
            // The mount also lives under the tracked controller root. Hide only
            // controller geometry, never the gun attached beneath its mount.
            if (child == leftGunMount || child == rightGunMount) continue;
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
        if (!environmentChanged) return;
        environmentChanged = false;
        gm.UseShootingControls(false);
        readoutsActive = false;
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

    private void AttachGunToSelectedHand()
    {
        Transform mount = gm.IsLeftHanded ? leftGunMount : rightGunMount;
        if (mount == null || gm.ActiveControllerModel == null)
        {
            Debug.LogError("Assign both gun mounts and controller models before playing.", this);
            enabled = false;
            return;
        }
        // Mounts hold the hand-specific POSITION and ROTATION. Keep the existing
        // gun scale and all of its muzzle/effect references on this single gun.
        realgun.transform.SetParent(mount, false);
        realgun.transform.localPosition = Vector3.zero;
        realgun.transform.localRotation = Quaternion.identity;
        parentObject = gm.ActiveControllerModel.gameObject;
    }
    public void StartGun()
    {   
        // BeginAttempt also accepts an already-active attempt, so guard here
        // against a second button event restarting its preparation coroutine.
        if (readoutsActive || environmentChanged || !gm.BeginAttempt(1, false)) return;
        tutorial = false;
        readoutsActive = true;
        shoot_script.failureChance = angerVar ? 10 : 0;
        Begin();
        fakegun.SetActive(false);
        realgun.SetActive(true);
        gm.Playing();
        started = false;
        hits = 0;
        level = 1;
        time_left = puzzleTimer;
        ConfigureTargetMovementForCurrentLevel();
        Spawner.setSpawnTime(1.4f);
        Spawner.StopSpawning();
        Spawner.ClearTargets();
        TMPScore.text = textAdapter.Format(TextId.ScoreProgress, hits, score_to_reach);
        TMPLevel.text = textAdapter.Format(TextId.LevelProgress, level, 3);
        StartCoroutine(PrepareFirstRound());
    }

    private IEnumerator PrepareFirstRound()
    {
        float remaining = preparationSeconds;
        while (remaining > 0f)
        {
            if (gm.SessionEnding) yield break;
            // Sensor loss normally cancels this coroutine through the existing
            // recovery API. This guard also prevents time advancing while blocked.
            if (gm.AcquisitionBlocked) { yield return null; continue; }
            TMPTimer.text = textAdapter.Format(TextId.PreparationCountdown, Mathf.CeilToInt(remaining));
            yield return null;
            remaining -= Time.deltaTime;
        }
        if (gm.AcquisitionBlocked || gm.SessionEnding) yield break;
        time_left = puzzleTimer;
        TMPTimer.text = string.Format("{0:00}:{1:00}",
            Mathf.FloorToInt(time_left / 60), Mathf.FloorToInt(time_left % 60));
        Spawner.StartSpawning();
        started = true;
    }

    private void ConfigureTargetMovementForCurrentLevel()
    {
        // Movement ownership:
        // 1. GunPuzzle chooses settings for the current level.
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
        if (!gm.BeginAttempt(1, true)) return;
        readoutsActive = true;
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
    // Update is called once per frame
    void Update()
    {
        if (gm.AcquisitionBlocked) return;
        if (started) 
        {
        time_left -= Time.deltaTime;
        int minutes = Mathf.FloorToInt(time_left / 60);
        int seconds = Mathf.FloorToInt(time_left % 60);
        TMPTimer.text = string.Format("{0:00}:{1:00}", minutes, seconds);
        }
        if (time_left <= 0 && !tutorial)
            {
                Spawner.ClearTargets();
                fakegun.SetActive(true);
                realgun.SetActive(false);
                Reset();
                started = false;
                TMPTimer.text = string.Format("");
                time_left = puzzleTimer;
                Spawner.StopSpawning();
                TMPScore.text = string.Format("");
                TMPLevel.text = string.Format("");
                gm.addTries(1, TMPTimer, textAdapter);
                resultVisible = true; // GamesManager clears a retry message after four seconds.
                am.playBad(1);
            }
      
    }

/*
    private IEnumerator Tutorial2()
    {
        yield return new WaitForSeconds(30);
        Begin();
        realgun.SetActive(true);
        fakegun.SetActive(false);
        Spawner.started = true;
        Spawner.setSpawnTime(5.0f);
        time_left = 26.0f;
        while (time_left > 0f)
        {
            time_left -= Time.deltaTime;
            int minutes = Mathf.FloorToInt(time_left / 60);
            int seconds = Mathf.FloorToInt(time_left % 60);
            TMPTimer.text = string.Format("Tutorial \n" + "{0:00}:{1:00}", minutes, seconds);
            yield return null;
        }
                fakegun.SetActive(true);
                realgun.SetActive(false);
                Reset();
                time_left = puzzleTimer;
                Spawner.started = false;
                TMPScore.text = string.Format("");
                TMPLevel.text = string.Format("");
                TMPTimer.text = string.Format("");
                tutorial = false;
                gm.stoppedPlaying();
    }
*/
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
        if (gm.AcquisitionBlocked) return;
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
        if (gm.AcquisitionBlocked || (!started && !tutorial)) return;
        StartCoroutine(CoroutineHit());
    }
    private IEnumerator CoroutineHit()
    {
    if (!tutorial)
        {
        hits++;
        TMPScore.text = textAdapter.Format(TextId.ScoreProgress, hits, score_to_reach);
        if (hits >= score_to_reach) {
            started = false;
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
                time_left = 21;
                if (angerVar) {shoot_script.failureChance += 20;}                  
                TMPScore.text = textAdapter.Format(TextId.ScoreProgress, hits, score_to_reach);
                Spawner.StartSpawning();
                started = true;
                }
                else
                {
                gm.GameWon(1);
                gm.stoppedPlaying();
                started = false;
                time_left = puzzleTimer;
                TMPTimer.text = textAdapter.Get(TextId.Victory);
                fakegun.SetActive(true);
                realgun.SetActive(false);
                Reset();
                TMPScore.text = string.Format("");
                TMPLevel.text = string.Format("");
                resultVisible = true;
                }
            }
        }
    }

    public bool GetGunTutorial()
    {
        return tutorial;
    }

    public void CancelForAcquisitionLoss()
    {
        if (!started && !tutorial && !readoutsActive && !environmentChanged && !resultVisible) return;
        resultVisible = false;
        StopAllCoroutines();
        started = false;
        tutorial = false;
        Spawner.StopSpawning();
        Spawner.ClearTargets();
        endTutorialObject.SetActive(false);
        realgun.SetActive(false);
        fakegun.SetActive(true);
        Reset();
        readoutsActive = false;
        hits = 0;
        level = 1;
        time_left = puzzleTimer;
        TMPScore.text = TMPLevel.text = TMPTimer.text = string.Empty;
    }

    private void LateUpdate()
    {
        // Update and the level-transition coroutine have both set the table text by now.
        // Result visibility is separate from gameplay: the gun and spawner stay
        // stopped. Retry messages clear after four seconds through GamesManager;
        // victory/final failure remain visible, just like the other minigames.
        bool visible = readoutsActive || (resultVisible && !string.IsNullOrEmpty(TMPTimer.text));
        screenReadout.Refresh(visible, TMPLevel, TMPScore, TMPTimer);
        centerEyeReadout.Refresh(visible, TMPLevel, TMPScore, TMPTimer);
        OldTexts.Refresh(visible, TMPLevel, TMPScore, TMPTimer);
    }

    private void OnDisable()
    {
        // The panels live elsewhere in the scene, so disabling this component alone
        // wouldn't hide them. LateUpdate will restore them if the session resumes.
        screenReadout.Refresh(false, TMPLevel, TMPScore, TMPTimer);
        centerEyeReadout.Refresh(false, TMPLevel, TMPScore, TMPTimer);
        OldTexts.Refresh(false, TMPLevel, TMPScore, TMPTimer);
        
    }

    public void ShowStartButtonLabel() => startButtonText.text = textAdapter.Get(TextId.StartLabel);

    public void ShowDebugResult(bool won)
    {
        TMPLevel.text = TMPScore.text = string.Empty;
        TMPTimer.text = textAdapter.Get(won ? TextId.Victory : TextId.GameDisabled);
        resultVisible = true;
    }
}
