using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;


public class CubesPuzzle : MonoBehaviour
{
    private bool[] solved = new bool[5];
    public GameObject[] cards = new GameObject[5];
    [SerializeField] int round = 1;
    [SerializeField] int level = 1;
    //public CountDown counScript;
    private float time_shortening;
    public bool anger_var;
    public AudioSource audioData;
    public AudioClip shuffle;
    public AudioClip lvlup;
    [SerializeField] TextMeshPro timer_txt;
    [SerializeField] TextMeshPro round_txt;
    [SerializeField] TextMeshPro level_txt;
    bool isPlaying = false;
    public GamesManager gm;
    public AudioManager am;
    [SerializeField] float time_left;
    public float puzzTimer;
    
    [Tooltip("The game starts in tutorial mode.")]
    [SerializeField] bool tutorial;

    // definisco le 3 possibili configurazioni iniziali risolvibili
    private bool[][] ConfigTrue = {
    new bool[] {false, false, true, false, false},
    new bool[] {false, true, true, false, true},
    new bool[] {false, true, false, true, false}
};

    // definisco le 3 possibili configurazioni iniziali non risolvibili
    private readonly bool[][] ConfigFalse = {
    new bool[] {false, false, false, false, false},
    new bool[] {true, true, true, false, false},
    new bool[] {true, false, true, false, true}
};

    void Update()
    {
        int minutes = Mathf.FloorToInt(time_left / 60);
        int seconds = Mathf.FloorToInt(time_left % 60);
        if (isPlaying)
        {
            if (time_left > 0)
            {
                time_left -= Time.deltaTime;
                timer_txt.text = string.Format("{0:00}:{1:00}", minutes, seconds);
            }
            else
            {
                isPlaying = false;
                gm.addTries(0);
                am.playBad(0);
                CleanCards();
            }
        }
    }
    void Start()
    {
     level_txt.text = string.Format("");
     round_txt.text = string.Format("");
     timer_txt.text = string.Format("");
     time_shortening = 0.0f;
     //tutorial = true;
    }

    private void AdaptAll()
    {
        audioData.PlayOneShot(shuffle);
        for (int i = 0; i < 5; i++)
        {
            cards[i].SetActive(true);
            if (solved[i])
            {
             cards[i].transform.eulerAngles = new Vector3(90.0f, 270.0f, 90.0f);
            }
            else
            {
             cards[i].transform.eulerAngles = new Vector3(-90.0f, -180.0f, 0.0f);
            }
        }
    }
        
    public void CleanCards()
    {
        solved = new bool[5];
        level = 1;
        round = 1;
        level_txt.text = string.Format("");
        round_txt.text = string.Format("");
        timer_txt.text = string.Format("");
        foreach (GameObject cube in cards)
        {
            cube.SetActive(false);
        }
    }
    
    public void StartPuzzle()
    {
        tutorial = false;
        CleanCards();
        level_txt.text = string.Format("Level \n" + level + "/3" );
        round_txt.text = string.Format("Round \n" + round + "/3");
        StartCards(true);
        isPlaying = true;
        Debug.Log( "Cards are" + solved[0] + solved[1] + solved[2] + solved[3] + solved[4]);
    }

    public void StartTutorial()
    {
        am.playTutorial(0);
        gm.DoneTutorial(0);
        gm.Playing();
        solved = new bool[] {true, true, true, true, true};
        AdaptAll();
        level_txt.text = string.Format("Level");
        round_txt.text = string.Format("Round");
        timer_txt.text = string.Format("Timer");
        StartCoroutine(wait_for_tutorial());
    }

    public IEnumerator wait_for_tutorial()
    {
        yield return new WaitForSeconds(39.1f);
        gm.stoppedPlaying();
    }

    void StartCards(bool conf) {
        // sorteggio una delle 3 configurazioni non risolvibili
        time_left = puzzTimer;
        if (conf)
        {
            ConfigTrue[Random.Range(0, 3)].CopyTo(solved, 0); // conf sarà adesso la configurazione sorteggiata
            Debug.Log("True");
        }
        else 
        {
            ConfigFalse[Random.Range(0, 3)].CopyTo(solved, 0); // conf sarà adesso la configurazione sorteggiata
            //solved = ConfigFalse[Random.Range(0, 3)]; // conf sarà adesso la configurazione sorteggiata
            Debug.Log("Fake");
        }
        Debug.Log( "Cards start as" + solved[0] + solved[1] + solved[2] + solved[3] + solved[4]);
        
        AdaptAll();
        
    }

    public void ChangeCubeL() { //cambio i primi 3 cubi
        if(isPlaying) {
        Change(0,1,2);
        StartCoroutine(CheckResutls());
        }
        if(tutorial) {
        Change(0,1,2);
        }
    }

    public void ChangeCubeR() { //cambio gli ultimi 3 cubi
        if(isPlaying) {
        Change(2,3,4);
        StartCoroutine(CheckResutls());
        }
        if(tutorial) {
        Change(2,3,4);
        }
    }

    public void ChangeCubeW() { //cambio i 3 cubi centrali
        if(isPlaying) {
        Change(1,2,3);
        StartCoroutine(CheckResutls());
        }
        if(tutorial) {
        Change(1,2,3);
        }
    }

    public void Change(int x, int y, int z) {
        solved[x] = !solved[x];
        solved[y] = !solved[y];
        solved[z] = !solved[z];
        AdaptAll();
    }
    
    private void lvlUp()
    {
        audioData.PlayOneShot(lvlup);
        level_txt.text = string.Format("Level \n" + level + "/3" );
        round_txt.text = string.Format("Round \n" + round + "/3");
    }

    private IEnumerator CheckResutls()
    {
        if (solved[0] && solved[1] && solved[2] && solved[3] && solved[4]) {

            switch (level)
            {
                case 1:
                case 2: if (round < 3)
                        {
                            round++;
                            lvlUp();
                            yield return new WaitForSeconds(0.7f);
                            Debug.Log("Solved" + round + "times");
                            StartCards(true);
                        }
                        else
                        {
                        round = 1;
                        level++;
                        lvlUp();
                        yield return new WaitForSeconds(1f);
                        time_shortening += 1.5f;
                        //timer.text = string.Format("Beggining Round " + rounds);
                        StartCards(true);
                        }
                        time_left = puzzTimer-time_shortening;
                        break;

                case 3: if (round < 3)
                        {
                            round++;
                            lvlUp();
                            yield return new WaitForSeconds(0.7f);
                            Debug.Log("Solved" + round + "times");
                            if (Random.Range(round, round+2)+1 > 3 && anger_var)
                            {
                            StartCards(false);
                            } 
                            else
                            {
                            StartCards(true);
                            }
                        time_left = puzzTimer - time_shortening*2;
                        }
                        else
                        {
                        Debug.Log("Solved!");
                        isPlaying = false;
                        timer_txt.text = string.Format("Puzzle solved");
                        level_txt.text = string.Format("");
                        round_txt.text = string.Format("");
                        gm.GameWon(0);
                        gm.stoppedPlaying();
                        }
                        break;
                default:
                        level_txt.text = string.Format("    what" );
                        round_txt.text = string.Format("uh???");
                        Debug.Log("WHAT");
                        break;
            }
        }   
    }
}



