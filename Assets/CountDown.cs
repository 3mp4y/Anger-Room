using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

public class CountDown : MonoBehaviour
{
    [SerializeField] TextMeshPro countd;
    [SerializeField] float time_left;

    public GameObject it;

    CubesPuzzle puzz;

    bool isPlaying = false;

    // Update is called once per frame
    void Update()
    {
        if (isPlaying) {
        if (time_left > 0)
        {
            it.SetActive(true);
            time_left -= Time.deltaTime;
        }
        else
        {
            time_left = 0;
            
            it.SetActive(false);
            isPlaying = false;
            //puzz.playSound();
            //puzz.CleanCubes();

        }
        int minutes = Mathf.FloorToInt(time_left / 60);
        int seconds = Mathf.FloorToInt(time_left % 60);
        countd.text = string.Format("{0:00}:{1:00}", minutes, seconds);
    }
    else
        {
            time_left = 15;
        }

    }

    public void SetTimer(int x)
    {
        time_left = x;
    }

    public void SetPlay(bool b)
    {
        isPlaying = b;
    }
}
