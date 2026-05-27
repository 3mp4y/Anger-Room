using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GamesManager : MonoBehaviour
{
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
    
    // Start is called before the first frame update
    void Start()
    {
        foreach (GameObject butt in tutorialButtons)
        {
            _activationList.Add(butt);
        }
    }
    // Update is called once per frame
    public void addTries(int i)
    {
        stoppedPlaying();
        if (gameTries[i] < 3)
        {
            gameTries[i] += 1;
            Debug.Log(gameTries[i]);
        }
        else
        {
        _reactivationBlacklist.Add(realButtons[i]);
        }

        bulbs[i].GetComponent<Renderer>().material = lights[gameTries[i]-1];
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
        foreach (GameObject butt in _activationList)
        {
            if (!_reactivationBlacklist.Contains(butt))
                butt.SetActive(true);
        }
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
