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
    public float puzzleTimer;
    private float time_left;
    private bool started;
    [SerializeField] private int hits;
    private int level;
    public GamesManager gm;
    public Spawn2 Spawner;
    [SerializeField] int score_to_reach;
    [SerializeField] ShootScript shoot_script;
    [SerializeField] bool angerVar;
    private bool tutorial;
    //CountDown counScript;
    // Start is called before the first frame update
    void Start()
    {
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
        shoot_script.failureChance = angerVar ? 10 : 0;
        Begin();
        fakegun.SetActive(false);
        realgun.SetActive(true);
        gm.Playing();
        started = true;
        hits = 0;
        level = 1;
        time_left = puzzleTimer;
        ConfigureTargetMovementForCurrentLevel();
        Spawner.setSpawnTime(1.4f);
        Spawner.StartSpawning();
        TMPScore.text = textAdapter.Format(TextId.ScoreProgress, hits, score_to_reach);
        TMPLevel.text = textAdapter.Format(TextId.LevelProgress, level, 3);
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
        Spawner.ClearTargets();
        endTutorialObject.SetActive(false);
        fakegun.SetActive(true);
        realgun.SetActive(false);
        Reset();
        Spawner.StopSpawning();
        TMPScore.text = string.Format("");
        TMPLevel.text = string.Format("");
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
                Spawner.StartSpawning();
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
