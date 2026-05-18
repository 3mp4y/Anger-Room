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
    [SerializeField] TextMeshPro Levels;
    [SerializeField] TextMeshPro hit_score;
    public float puzzleTimer;
    private float time_left;
    private bool started = false;
    public TargetMover targetMover;
    private bool won = false;
    private int hits = 0;
    private int level = 1;
    public GamesManager gm;

    public GameObject Spawner;
    //CountDown counScript;
    // Start is called before the first frame update
    void Start()
    {
        time_left = puzzleTimer;
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
                    Debug.Log(grandchild);
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
        started = true;
        time_left = puzzleTimer;
        Spawner.SetActive(true);
        hit_score.text = string.Format("Hits \n" + hits + "/15");
        Levels.text = string.Format("Level \n" + level + "/3");
        
    }

    // Update is called once per frame
    void Update()
    {
        if (started && !won) 
        {
        time_left -= Time.deltaTime;
        int minutes = Mathf.FloorToInt(time_left / 60);
        int seconds = Mathf.FloorToInt(time_left % 60);
        TMPTimer.text = string.Format("{0:00}:{1:00}", minutes, seconds);
        }
        if (time_left <= 0)
            {
                fakegun.SetActive(true);
                realgun.SetActive(false);
                Reset();
                started = false;
                TMPTimer.text = string.Format("");
                time_left = puzzleTimer;
                Spawner.SetActive(false);
                hit_score.text = string.Format("");
                Levels.text = string.Format("");
                TMPTimer.text = string.Format("");
                gm.addTries(1);
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
            time_left = 0;
            TMPTimer.text = string.Format("Puzzle solved");
            }
        }
        
        
    }
}
