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
    public GameObject TextTimer;
    public float puzzleTimer;
    private float time_left;
    //Others
    private bool started = false;

    public GameObject Spawner;
    //CountDown counScript;
    // Start is called before the first frame update
    void Start()
    {
        
    }

    public void StartGun()
    {   
        fakegun.SetActive(false);
        realgun.SetActive(true);
        controllervisual1.SetActive(false);
        controllervisual2.SetActive(false);
        TextTimer.SetActive(true);
        started = true;
        time_left = puzzleTimer;
        Spawner.SetActive(true);
    }

    public void StartTutorial()
    {   
        fakegun.SetActive(false);
        realgun.SetActive(true);
        controllervisual1.SetActive(false);
        controllervisual2.SetActive(false);
        TextTimer.SetActive(true);
        Spawner.SetActive(true);
    }

    // Update is called once per frame
    void Update()
    {
        if (started)
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
                TextTimer.SetActive(false);
                time_left = puzzleTimer;
                Spawner.SetActive(false);
            }
    }
}
