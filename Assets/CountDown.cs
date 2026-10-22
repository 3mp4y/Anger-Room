using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using UnityEngine.Serialization;

public class CountDown : MonoBehaviour
{
    [SerializeField] TextMeshPro countd;
    [SerializeField] float time_left;
    [FormerlySerializedAs("puzz")]
    public Card_Minigame cardMinigame;
    bool isPlaying = false;

    public GamesManager gm;
    // Update is called once per frame
    void Update()
    {
        int minutes = Mathf.FloorToInt(time_left / 60);
        int seconds = Mathf.FloorToInt(time_left % 60);

        if (isPlaying) {
            if (time_left > 0)
            {
                time_left -= Time.deltaTime;
                countd.text = string.Format("{0:00}:{1:00}", minutes, seconds);
            }
            else
            {   
                isPlaying = false;
                //gm.addTries(0);
                cardMinigame.ClearCards();
                countd.text = string.Format("");
            }
        }

    }

    public void SetTimer(float x)
    {
        time_left = x;
    }

    public void SetPlay(bool b)
    {
        isPlaying = b;
    }
}
