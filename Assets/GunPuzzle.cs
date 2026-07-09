using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
public class GunPuzzle : MonoBehaviour
{

    public AudioManager am;
    [SerializeField] Logger logger;
    //Variables for gun and controller models
    public GameObject realgun;
    public GameObject fakegun;
    [SerializeField] private GameObject parentObject;
    [SerializeField] GameObject endTutorialObject;
    [SerializeField] GameObject Scores;
    private List<GameObject> _activeGrandchildren  = new List<GameObject>();
    //Varibales for the timer
    [SerializeField] TextMeshPro TMPTimer;
    [SerializeField] TextMeshPro TMPLevel;
    [SerializeField] TextMeshPro TMPScore;
    public float puzzleTimer;
    private float time_left;
    private bool started;
    public TargetMover targetMover;
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
        shoot_script.failureChance = angerVar ? 10 : 0; 
    }

    public void Begin()
    {
        Scores.SetActive(false);
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
        Scores.SetActive(true);
        foreach (GameObject grandchild in _activeGrandchildren)
        {
            grandchild.SetActive(true);
        }
    }
    public void StartGun()
    {   
        Begin();
        fakegun.SetActive(false);
        realgun.SetActive(true);
        gm.Playing();
        started = true;
        hits = 0;
        level = 1;
        time_left = puzzleTimer;
        targetMover.SetSpeeds(0.2f, 0.6f, 2);
        Spawner.setSpawnTime(1.8f);
        Spawner.StartSpawning();
        TMPScore.text = string.Format("Puntos \n" + hits + "/" + score_to_reach);
        TMPLevel.text = string.Format("Nivel \n" + level + "/3");
    }


    public void StartTutorial2()
    {
        tutorial = true;
        gm.DoneTutorial(1);
        gm.Playing();
        TMPScore.text = string.Format("Puntuación a alcanzar");
        TMPLevel.text = string.Format("Nivel");
        TMPTimer.text = string.Format("Tutorial Reloj");
        targetMover.SetSpeeds(0.0f, 0.0f, 1.0f);
        endTutorialObject.SetActive(true);
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
                gm.addTries(1, TMPTimer);
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
        TMPScore.text = string.Format("Puntos \n" + hits + "/" + score_to_reach);
        if (hits >= score_to_reach) {
            started = false;
            Spawner.StopSpawning();
            Spawner.ClearTargets();
                if (level < 3) 
                {
                am.playLevelUp(1);
                hits = 0;
                level++;
                TMPLevel.text = string.Format("Nivel \n" + level + "/3");
                TMPScore.text = string.Format("Puntos \n" + hits + "/" + score_to_reach);
                float timeLevelUp = 3.9f;
                while (timeLevelUp > 0.2f)
                    {
                    timeLevelUp -= Time.deltaTime;
                    int seconds = Mathf.FloorToInt(timeLevelUp % 60);
                    TMPTimer.text = string.Format("Next level in... \n" + "{0:00}", seconds);
                    yield return null;
                    }
                targetMover.SetSpeeds(targetMover.getMin() + 0.4f, targetMover.getMax() + 0.9f, targetMover.getInt() * 0.4f);
                time_left = 21;
                if (angerVar) {shoot_script.failureChance += 20;}                  
                TMPScore.text = string.Format("Puntos \n" + hits + "/" + score_to_reach);
                Spawner.StartSpawning();
                started = true;
                }
                else
                {
                gm.GameWon(1);
                gm.stoppedPlaying();
                started = false;
                time_left = puzzleTimer;
                TMPTimer.text = string.Format("¡Has ganado!");
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
