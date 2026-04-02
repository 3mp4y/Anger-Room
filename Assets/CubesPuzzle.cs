using System.Collections;
using System.Collections.Generic;
using UnityEngine;


public class CubesPuzzle : MonoBehaviour
{
    public bool[] solved = {true, true, true};
    public GameObject[] cubes = new GameObject[3];
    public Material right;
    public Material wrong;
    private int tries = 0;
    CountDown counScript;
    public AudioSource audioData;

    public GameObject timer;
    public GameObject congrats;
    
    

    void Start()
    {
    }

    public void playSound()
    {
        audioData.Play(0); 
        Debug.Log("Suono");
    }
    
    public void StartPuzzlelay()
    {

        if (tries<=5) {
            StartFake();
        }
        else
        {
            StartTrue();
        }

        tries++;
    }

    void StartFake() {
        for (int i = 0; i<3; i++)
        {
            solved[i] = true;
            cubes[i].SetActive(true);
            cubes[i].GetComponent<Renderer>().material = right;
        }
        int randomInRange = Random.Range(0, 3);
        Debug.Log("The incorrect is" + (randomInRange+1));
        solved[randomInRange] = false;
        cubes[randomInRange].GetComponent<Renderer>().material = wrong;
        CheckResutls();
    }

    void StartTrue() {
        for (int i = 0; i<3; i++)
        {
            solved[i] = false;
            cubes[i].SetActive(true);
            cubes[i].GetComponent<Renderer>().material = wrong;
        }
        
        int randomInRange = Random.Range(0, 3);
        solved[randomInRange] = true;
        cubes[randomInRange].GetComponent<Renderer>().material = right;
        CheckResutls();
    }

    public void ChangeCubeL() {
        Change(0,1);
        CheckResutls();
    }

    public void ChangeCubeR() {
        Change(1,2);
        CheckResutls();
    }

    public void ChangeCubeW() {
        Change(0,2);
        CheckResutls();
    }

    public void Change(int x, int y) {
        //solved[x] = !solved[x];
        //solved[y] = !solved[y];
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


        }

    // Update is called once per frame
    void Update()
    {
        
    }

    

    private void CheckResutls()
    {
        if (solved[0] && solved[1] && solved[2]) {
            Debug.Log("Solved!");
            timer.SetActive(false);
            congrats.SetActive(true);
        }

        Debug.Log("Cubes are" + solved[0] + solved[1] + solved[2]);
    }
    
    public void CleanCubes()
    {
        foreach (GameObject cube in cubes)
        {
            cube.SetActive(false);
            playSound();
        }
    }
        
}



