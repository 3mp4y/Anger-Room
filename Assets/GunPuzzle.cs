using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
public class GunPuzzle : MonoBehaviour
{

    //Variables for gun and controller models
    public GameObject realgun;
    public GameObject fakegun;
    public GameObject controllervisual1;
    public GameObject controllervisual2;
    //Varibales for the timer
    [SerializeField] TextMeshPro TMPTimer;
    [SerializeField] TextMeshPro Levels;
    [SerializeField] TextMeshPro hit_score;
    public float puzzleTimer;
    private float time_left;
    //Others
    private bool started = false;
    public TargetMover targetMover;
    private bool won = false;
    private int tries = 0;
    private int hits = 0;
    private int level = 1;

    public GameObject Spawner;
    //CountDown counScript;
    // Start is called before the first frame update
    void Start()
    {
        
    }

    public void StartGun()
    {   
        if (tries < 3)
        {
        fakegun.SetActive(false);
        realgun.SetActive(true);
        controllervisual1.SetActive(false);
        controllervisual2.SetActive(false);
        started = true;
        time_left = puzzleTimer;
        Spawner.SetActive(true);
        hit_score.text = string.Format("Hits \n" + hits + "/15");
        Levels.text = string.Format("Level \n" + level + "/3");
        }
        
    }

    public void StartTutorial()
    {   
        fakegun.SetActive(false);
        realgun.SetActive(true);
        controllervisual1.SetActive(false);
        controllervisual2.SetActive(false);
        Spawner.SetActive(true);
        
    }

    // Update is called once per frame
    void Update()
    {
        if (started && !won)
        time_left -= Time.deltaTime;
        int minutes = Mathf.FloorToInt(time_left / 60);
        int seconds = Mathf.FloorToInt(time_left % 60);
        TMPTimer.text = string.Format("{0:00}:{1:00}", minutes, seconds);

        if (time_left <= 0)
            {
                fakegun.SetActive(true);
                realgun.SetActive(false);
                controllervisual1.SetActive(true);
                controllervisual2.SetActive(true);
                started = false;
                TMPTimer.text = string.Format("");
                time_left = puzzleTimer;
                Spawner.SetActive(false);
                tries++;
                hit_score.text = string.Format("");
                Levels.text = string.Format("");
                TMPTimer.text = string.Format("");
            }
    }

    public void GotHit()
    {
        hits++;
        hit_score.text = string.Format("Hits \n" + hits + "/15");
        if (hits > 4)
        {
            if (level < 3) 
            {
            hits = 0;
            level++;
            Levels.text = string.Format("Level \n" + level + "/3");
            targetMover.ChangeSpeeds(2.0f, 2.0f, 0.3f);
            }
            else
            {
            won = true;
            TMPTimer.text = string.Format("Puzzle solved");
            }
        }
        
        
    }
}
