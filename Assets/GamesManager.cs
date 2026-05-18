using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GamesManager : MonoBehaviour
{
    private int gmTriesGun = 0;
    private int gmTriesCards = 0;
    private int gmTriesLab = 0;

    private int[] gameTries = {0,0,0};
    public GameObject[] bulbs= new GameObject[3];
    public Material[] lights = new Material[3];
    public GameObject[] activeButtons= new GameObject[3];

    
    // Start is called before the first frame update
    void Start()
    {
        
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
        bulbs[i].GetComponent<Renderer>().material = lights[gameTries[i]-1];
    }

    public void Playing()
    {
        foreach (GameObject button in activeButtons)
        {
            button.SetActive(false);
        }
    }

    public void stoppedPlaying()
    {
        for (int i = 0; i<3; i++) {
            if (gameTries[i] < 3)
            {
            activeButtons[i].SetActive(true);
            }
        }
    }

}
