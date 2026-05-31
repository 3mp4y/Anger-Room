using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
public class GunPuzzle : MonoBehaviour
{

    //Variables for gun and controller models
    public GameObject realgun;
    public GameObject fakegun;
    [SerializeField] private GameObject parentObject;
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
    public AudioManager am;

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
        shoot_script.failureChance = 0;
    }

    public void Begin()
    {
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
        Spawner.setSpawnTime(1.3f);
        Spawner.started = true;
        TMPScore.text = string.Format("Hits \n" + hits + "/" + score_to_reach);
        TMPLevel.text = string.Format("Level \n" + level + "/3");
    }


    public void StartTutorial2()
    {
        Begin();
        tutorial = true;
        gm.DoneTutorial(1);
        gm.Playing();
        TMPScore.text = string.Format("Targets to hit");
        TMPLevel.text = string.Format("Level");
        targetMover.SetSpeeds(0.0f, 0.0f, 1.0f);
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
                fakegun.SetActive(true);
                realgun.SetActive(false);
                Reset();
                started = false;
                TMPTimer.text = string.Format("");
                time_left = puzzleTimer;
                Spawner.started = false;
                TMPScore.text = string.Format("");
                TMPLevel.text = string.Format("");
                TMPTimer.text = string.Format("");
                gm.addTries(1);
                am.playBad(1);
            }
      
    }

    private IEnumerator Tutorial()
    {
        yield return new WaitForSeconds(4);
        realgun.SetActive(true);
        fakegun.SetActive(false);
        yield return new WaitForSeconds(20);
        Spawner.started = true;
        Spawner.setSpawnTime(5.0f);
        time_left = 26.0f;
        while (time_left > 0f)
        {
            time_left -= Time.deltaTime;
            int minutes = Mathf.FloorToInt(time_left / 60);
            int seconds = Mathf.FloorToInt(time_left % 60);
            TMPTimer.text = string.Format("{0:00}:{1:00}", minutes, seconds);
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

    public void GotHit()
    {
        if (!tutorial)
        {
        hits++;
        TMPScore.text = string.Format("Hits \n" + hits + "/" + score_to_reach);
        if (hits >= score_to_reach) {
            if (level <= 3) 
            {
            am.playLevelUp(1);
            hits = 0;
            level++;
            TMPLevel.text = string.Format("Level \n" + level + "/3");
            TMPScore.text = string.Format("Hits \n" + hits + "/" + score_to_reach);
            targetMover.SetSpeeds(targetMover.getMin() + 1.3f, targetMover.getMax() + 1.7f, targetMover.getInt() * 0.7f);
            time_left = 21;
            if (angerVar)
                    {
                     if (shoot_script.failureChance < 10)
                        {
                            shoot_script.failureChance = 10;
                        }
                    else
                        {
                            shoot_script.failureChance += 20;
                        }
                    }
            }
            else
            {
            gm.GameWon(1);
            gm.stoppedPlaying();
            started = false;
            time_left = puzzleTimer;
            TMPTimer.text = string.Format("Puzzle solved");
            fakegun.SetActive(true);
            realgun.SetActive(false);
            Reset();
            Spawner.started = false;
            TMPScore.text = string.Format("");
            TMPLevel.text = string.Format("");
            }
        }
        }
        
    }
}
