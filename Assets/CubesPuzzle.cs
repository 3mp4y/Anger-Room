using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;


public class CubesPuzzle : MonoBehaviour
{
    public GamesManager gm;
    public AudioManager am;
    public Logger logger;
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
    [SerializeField] float time_left;
    public float puzzTimer;
    
    [Tooltip("The game starts in tutorial mode.")]
    [SerializeField] bool tutorial;

    // definisco le 3 possibili configurazioni iniziali risolvibili
    private bool[][] ConfigTrue = {
    //DxSx
    new bool[] {false, false, true, false, false},
    //SxCntr
    new bool[] {false, true, true, false, true},
    //DxCtrl
    new bool[] {true, false, true, true, false},
    //SxDxCntrl
    new bool[] {false, true, false, true, false}
    
};

    // definisco le 3 possibili configurazioni iniziali non risolvibili
    private readonly bool[][] ConfigFalse = {
    new bool[] {false, false, false, false, true},
    new bool[] {true, true, true, false, false},
    new bool[] {true, false, true, false, true},
    new bool[] {true, true, true, true, false}
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
                CleanCards();
                gm.addTries(0, timer_txt);
                am.playBad(0);
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
        foreach (GameObject card in cards)
        {
            card.SetActive(false);
        }
    }
    
    public void StartPuzzle()
    {
        tutorial = false;
        CleanCards();
        level_txt.text = string.Format("Nivel \n" + level + "/3" );
        round_txt.text = string.Format("Round \n" + round + "/3");
        StartCards(true);
        isPlaying = true;
        logger.Log( "Cards are" + solved[0] + solved[1] + solved[2] + solved[3] + solved[4]);
    }

    public void StartTutorial()
    {
        am.playTutorial(0);
        gm.DoneTutorial(0);
        gm.Playing();
        solved = new bool[] {true, true, true, true, true};
        AdaptAll();
        level_txt.text = string.Format("Nivel");
        round_txt.text = string.Format("Ronda");
        timer_txt.text = string.Format("Reloj");
        StartCoroutine(wait_for_tutorial());
    }

    public IEnumerator wait_for_tutorial()
    {
        while (am.isPresentSpeaking())
            {
                yield return null;
            }
        gm.stoppedPlaying();
    }

    void StartCards(bool conf) {
        // sorteggio una delle 3 configurazioni non risolvibili
        time_left = puzzTimer;
        if (conf)
        {
            ConfigTrue[Random.Range(0, 4)].CopyTo(solved, 0); // conf sarà adesso la configurazione sorteggiata
        }
        else 
        {
            ConfigFalse[Random.Range(0, 4)].CopyTo(solved, 0); // conf sarà adesso la configurazione sorteggiata
        }
        AdaptAll();
        
    }

    public void ChangeCubeL() { //cambio i primi 3 cubi
        
        if(isPlaying) {
        Change(0,1,2);
        StartCoroutine(CheckResutls());
        logger.Log("Pressed card button left");
        }
        if(tutorial) {
        Change(0,1,2);
        logger.Log("Tutorial - Pressed card button left");
        }
    }

    public void ChangeCubeR() { //cambio gli ultimi 3 cubi
        
        if(isPlaying) {
        Change(2,3,4);
        StartCoroutine(CheckResutls());
        logger.Log("Pressed card button right");
        }
        if(tutorial) {
        Change(2,3,4);
        logger.Log("Tutorial - Pressed card button left");
        }
    }

    public void ChangeCubeW() { //cambio i 3 cubi centrali
        
        if(isPlaying) {
        Change(1,2,3);
        StartCoroutine(CheckResutls());
        logger.Log("Pressed card button center");
        }
        if(tutorial) {
        Change(1,2,3);
        logger.Log("Tutorial - Pressed card button left");
        }
    }

    public void Change(int x, int y, int z) {
        solved[x] = !solved[x];
        solved[y] = !solved[y];
        solved[z] = !solved[z];
        AdaptAll();
    }
    
    private IEnumerator lvlUp()
    {
        foreach (GameObject card in cards)
        {
            card.SetActive(false);
        }
        audioData.PlayOneShot(lvlup);
        level_txt.text = string.Format("Nivel \n" + level + "/3" );
        round_txt.text = string.Format("Ronda \n" + round + "/3");
        float timeLevelUp = 3.9f;
        while (timeLevelUp > 0f)
        {
            timeLevelUp -= Time.deltaTime;
            int seconds = Mathf.FloorToInt(timeLevelUp % 60);
            timer_txt.text = string.Format("Next level in... \n" + "{0:00}", seconds);
            yield return null;
        }
        foreach (GameObject card in cards)
        {
            card.SetActive(true);
        }
        
    }

    private void NextRound()
    {
        audioData.PlayOneShot(lvlup);
        round_txt.text = string.Format("Ronda \n" + round + "/3");
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
                            NextRound();
                            yield return new WaitForSeconds(0.8f);
                            logger.Log("Solved - Round:" + round + " Level:" + level);
                            StartCards(true);
                        }
                        else
                        {
                        logger.Log("Solved - Round:" + round + " Level:" + level);
                        round = 1;
                        level++;
                        StartCoroutine(lvlUp());
                        yield return new WaitForSeconds(4.1f);
                        time_shortening += 1.7f;
                        //timer.text = string.Format("Beggining Round " + rounds);
                        StartCards(true);
                        }
                        time_left = puzzTimer-time_shortening;
                        break;

                case 3: if (round < 3)
                        {
                            round++;
                            NextRound();
                            yield return new WaitForSeconds(0.6f);
                            int angerChance = Random.Range(round, round+3)+1;
                            Debug.Log(angerChance);
                            if (angerChance > 3 && anger_var)
                            {
                            logger.Log("AngerCombination - Round:" + round + " Level:" + level);
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
                        isPlaying = false;
                        timer_txt.text = string.Format("¡Has ganado!");
                        level_txt.text = string.Format("");
                        round_txt.text = string.Format("");
                        gm.GameWon(0);
                        gm.stoppedPlaying();
                        }
                        break;
                default:
                        level_txt.text = string.Format("    what" );
                        round_txt.text = string.Format("uh???");
                        break;
            }
        }   
    }
}



