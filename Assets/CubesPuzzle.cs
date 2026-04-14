using System.Collections;
using System.Collections.Generic;
using UnityEngine;


public class CubesPuzzle : MonoBehaviour
{
    private bool[] solved = {false,false,false,false,false};
    public GameObject[] cubes = new GameObject[5];
    public Material right;
    public Material wrong;
    private int tries = 0;
    private int succeses = 0;
    public CountDown counScript;
    public AudioSource audioData;
    public GameObject timer;
    public GameObject congrats;

    // definisco le 3 possibili configurazioni iniziali risolvibili
    private bool[][] ConfigTrue = {
    new bool[] {false, false, true, false, false},
    new bool[] {false, true, true, false, true},
    new bool[] {false, true, false, true, false}
};

    // definisco le 3 possibili configurazioni iniziali non risolvibili
    private bool[][] ConfigFalse = {
    new bool[] {false, false, false, false, false},
    new bool[] {true, true, true, false, false},
    new bool[] {true, false, true, false, true}
};

    void Start()
    {
     StartCoroutine(waiter());
    }

    IEnumerator waiter()
    {
        yield return new WaitForSeconds(5);
    }

    public void playSound()
    {
        audioData.Play(0); 
        Debug.Log("Suono");
    }
    
    public void StartPuzzlelay()
    {
        
        CleanCubes();
        congrats.SetActive(false);

        if (tries<=2) {
            StartTrue();
        }
        else
        {
            StartFake();
        }

        tries++;
        Debug.Log("Cubes are" + solved[0] + solved[1] + solved[2] + solved[3] + solved[4]);

    }

    void StartFake() {
        // sorteggio una delle 3 configurazioni non risolvibili
        int randomInRange = Random.Range(0, 3);
        bool[] conf = ConfigFalse[randomInRange]; // conf sarà adesso la configurazione sorteggiata
        
        for (int i = 0; i < 5; i++)
        {
            if (conf[i])
            {
                cubes[i].GetComponent<Renderer>().material = right;
            }
            else
            {
                cubes[i].GetComponent<Renderer>().material = wrong;
            }
            solved[i] = conf[i];
            cubes[i].SetActive(true);
        }
        Debug.Log("Fake");
    }


    void StartTrue() {
        // sorteggio una delle 3 configurazioni risolvibili
        int randomInRange = Random.Range(0, 3);
        solved = ConfigTrue[randomInRange]; // conf sarà adesso la configurazione sorteggiata

        for (int i = 0; i<5; i++)
        {
            if (solved[i])
            {
                cubes[i].GetComponent<Renderer>().material = right;
            }
            else
            {
                cubes[i].GetComponent<Renderer>().material = wrong;
            }
            cubes[i].SetActive(true);   
        }
        Debug.Log("True");

    }



    public void ChangeCubeL() { //cambio i primi 3 cubi
        Change(0,1,2);
        CheckResutls();
    }

    public void ChangeCubeR() { //cambio gli ultimi 3 cubi
        Change(2,3,4);
        CheckResutls();
    }

    public void ChangeCubeW() { //cambio i 3 cubi centrali
        Change(1,2,3);
        CheckResutls();
    }

    public void Change(int x, int y, int z) {
        solved[x] = !solved[x];
        solved[y] = !solved[y];
        solved[z] = !solved[z];
        Debug.Log(solved);
        for (int i = 0; i < 5; i++)
        {
            if (solved[i])
            {
                cubes[i].GetComponent<Renderer>().material = right;
            }
            else
            {
                cubes[i].GetComponent<Renderer>().material = wrong;
            }
        }
        /*
        if (solved[x] == true) {
            solved[x] = false;
            cubes[x].GetComponent<Renderer>().material = wrong;
        }
        else
        {
            solved[x] = true;
            cubes[x].GetComponent<Renderer>().material = right;
        }

        if (solved[y] == true) {
            solved[y] = false;
            cubes[y].GetComponent<Renderer>().material = wrong;
        }
        else
        {
            solved[y] = true;
            cubes[y].GetComponent<Renderer>().material = right;
        }

        if (solved[z] == true)
        {
            solved[z] = false;
            cubes[z].GetComponent<Renderer>().material = wrong;
        }
        else
        {
            solved[z] = true;
            cubes[z].GetComponent<Renderer>().material = right;
        }
        */

    }

    // Update is called once per frame
    void Update()
    {
        
    }

    private void CheckResutls()
    {
        if (solved[0] && solved[1] && solved[2] && solved[3] && solved[4]) {
            if (succeses < 2)
            {
                succeses++;
                waiter();
                StartTrue();
                counScript.SetTimer(16);
            }
            else
            {
            Debug.Log("Solved!");
            timer.SetActive(false);
            congrats.SetActive(true);
            }
        }

        Debug.Log("Cubes are" + solved[0] + solved[1] + solved[2] + solved[3] + solved[4]);
    }
    
    public void CleanCubes()
    {
        for (int i = 0; i < 5; i++)
        {
            solved[i] = false;
        }
        foreach (GameObject cube in cubes)
        {
            cube.SetActive(false);
        }
    }
        
}



