using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;


public class CubesPuzzle : MonoBehaviour
{
    private bool[] solved = new bool[5];
    public GameObject[] cards = new GameObject[5];
    private int round = 1;
    private int level = 1;
    public CountDown counScript;
    private float time_shortening = 0.0f;
    public bool anger_var;
    public AudioSource audioData;
    public AudioClip shuffle;
    public AudioClip lvlup;
    [SerializeField] TextMeshPro timer;
    [SerializeField] TextMeshPro round_txt;
    [SerializeField] TextMeshPro level_txt;

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
        
    }
    void Start()
    {
     //StartCoroutine(waiter());
    }

    public void PlaySound()
    {
        audioData.Play(0);
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

    IEnumerator waiter()
    {
        yield return new WaitForSeconds(5);
    }
        
    public void CleanCards()
    {
        solved = new bool[5];
        level = 1;
        round = 1;
        level_txt.text = string.Format("");
        round_txt.text = string.Format("");
        foreach (GameObject cube in cards)
        {
            cube.SetActive(false);
        }
    }
    
    public void StartPuzzle()
    {
        CleanCards();
        level_txt.text = string.Format("Level \n" + level + "/3" );
        round_txt.text = string.Format("Round \n" + round + "/3");
        counScript.SetTimer(16);
        StartCards(true);
        Debug.Log( "Cards are" + solved[0] + solved[1] + solved[2] + solved[3] + solved[4]);
    }

    public void StartTutorial()
    {
        
       foreach (GameObject card in cards)
        {
            card.SetActive(true);
        }

    }

    void StartCards(bool conf) {
        // sorteggio una delle 3 configurazioni non risolvibili
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
        Change(0,1,2);
        StartCoroutine(CheckResutls());
    }

    public void ChangeCubeR() { //cambio gli ultimi 3 cubi
        Change(2,3,4);
        StartCoroutine(CheckResutls());
    }

    public void ChangeCubeW() { //cambio i 3 cubi centrali
        Change(1,2,3);
        StartCoroutine(CheckResutls());
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
                        time_shortening += 2.5f;
                        //timer.text = string.Format("Beggining Round " + rounds);
                        StartCards(true);
                        }
                        counScript.SetTimer(16-time_shortening);
                        break;

                case 3: if (round < 3)
                        {
                            round++;
                            lvlUp();
                            yield return new WaitForSeconds(0.7f);
                            //waiter();
                            Debug.Log("Solved" + round + "times");
                            if (Random.Range(round, round+2)+1 > 3 && anger_var)
                            {
                            StartCards(false);
                            } 
                            else
                            {
                            StartCards(true);
                            }
                            counScript.SetTimer(16-time_shortening);
                        }
                        else
                        {
                        Debug.Log("Solved!");
                        counScript.SetPlay(false);
                        timer.text = string.Format("Puzzle solved");
                        level_txt.text = string.Format("");
                        round_txt.text = string.Format("");
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



