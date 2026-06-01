using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
public class GamesManager : MonoBehaviour
{
    [SerializeField] float timer_experiment;
    [SerializeField] float time_left_exp;
    [SerializeField] TextMeshPro TMPtime_left_exp;
    private int gmTriesGun = 0;
    private int gmTriesCards = 0;
    private int gmTriesLab = 0;
    private int[] gameTries = {0,0,0};

    [SerializeField] int[] wins = {0,0,0};
    public GameObject[] bulbs= new GameObject[3];
    public Material[] lights = new Material[3];
    [SerializeField] private GameObject[] tutorialButtons= new GameObject[3];
    [SerializeField] private GameObject[] realButtons= new GameObject[3];
    private List<GameObject> _reactivationBlacklist = new List<GameObject>();
    public List<GameObject> _activationList;
    public AudioManager am;
    private Coroutine audioloop;
    private bool isgoing;
    // Start is called before the first frame update
    void Start()
    {
        isgoing = false;
        foreach (GameObject butt in tutorialButtons)
        {
            _activationList.Add(butt);
        }
        am.PlayIntro();
        Playing();
        time_left_exp = timer_experiment;
    }

    public void StartOverAllTimer()
    {
        stoppedPlaying();
        isgoing = true;
        StartCoroutine(OverallTimer());
    }
    private IEnumerator OverallTimer()
    {
        while (isgoing)
        {
        int minutes = Mathf.FloorToInt(time_left_exp / 60);
        int seconds = Mathf.FloorToInt(time_left_exp % 60);
            if (time_left_exp > 0)
            {
            time_left_exp -= Time.deltaTime;
            TMPtime_left_exp.text = string.Format("{0:00}:{1:00}", minutes, seconds);
            }
            else
            {
            foreach (GameObject realButton in realButtons) {
            _reactivationBlacklist.Add(realButton);
            }
            DeactivatingButtons();
            am.playLoss();
            isgoing = false;
            }
            yield return null;
        }
    }
    
    private IEnumerator validInsults() {
        yield return new WaitForSeconds(9f);
        Debug.Log("Inizio Insulti");
        while (true)
        {
            if (Random.Range(0, 1) == 0)
            {
            am.play_gen_insult();
            yield return new WaitForSeconds(15f);
            }
            Debug.Log("Shucks");
            yield return new WaitForSeconds(5f);
        }
    }
    public void StartInsult()
    {
        audioloop = StartCoroutine(validInsults());
    }

    public void StopLoop()
    {
        if (audioloop != null)
        {
            StopCoroutine(audioloop);
            audioloop = null;
        }
    }
    // Update is called once per frame
    public void addTries(int i, TextMeshPro message)
    {
        stoppedPlaying();
        if (gameTries[i] < 3)
        {
            gameTries[i] += 1;
            StartCoroutine(lossMessage(gameTries[i], false, message));
        }
        else
        {
        StartCoroutine(lossMessage(gameTries[i], true, message));
        _reactivationBlacklist.Add(realButtons[i]);
        }
        bulbs[i].GetComponent<Renderer>().material = lights[gameTries[i]-1];
        CheckLost();
    }

     private IEnumerator lossMessage(int tries, bool loss, TextMeshPro message)
    {
        if (!loss) {
        int reamaingTries = 3 - tries;
        message.text = string.Format("Hai perso :( \n Tentativi rimasti: " + reamaingTries);
        yield return new WaitForSeconds(4);
        message.text = string.Format("");
        }
        else
        {
        message.text = string.Format("Hai perso! \n Puzzle disattivato.");
        }
    }

    public int getRemainingTries(int game)
    {
        return 3-gameTries[game];
    }
    public void CheckLost()
    {
        if (gameTries[0] == 3 && gameTries[1] == 3 && gameTries[2] == 3)
        {
            am.playLoss();
        }
    }

    public void Playing()
    {
        StartCoroutine(DeactivatingButtons());
    }

    public IEnumerator DeactivatingButtons()
    {
        yield return new WaitForSeconds(0.1f);
        foreach (GameObject button in _activationList)
        {
            button.SetActive(false);
        }
    }

    public void DoneTutorial(int i)
    {
        _reactivationBlacklist.Add(tutorialButtons[i]);
        _activationList.Add(realButtons[i]);
    }

    public void stoppedPlaying()
    {
        am.clock.Stop();
        foreach (GameObject butt in _activationList)
        {
            if (!_reactivationBlacklist.Contains(butt))
                butt.SetActive(true);
        }
        StopLoop();
    }
    private void PermanentlyDeactivate(GameObject obj)
    {
        _reactivationBlacklist.Add(obj);
        obj.SetActive(false);
        Debug.Log(obj.name + " permanently deactivated.");
    }

    public void GameWon(int game)
    {
        wins[game] = 1;
        _reactivationBlacklist.Add(realButtons[game]);
        if (wins[0] == 1 && wins[1] == 1 && wins[2] == 1)
        {
          am.playEnd();
        }
    }

}
