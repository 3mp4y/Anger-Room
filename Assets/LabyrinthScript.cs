using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

public class LabyrinthScript : MonoBehaviour
{
    public GameObject blacked;
    public GameObject player;
    public Transform newPos;
    [SerializeField] TextMeshPro TMPTimer;
    [SerializeField] TextMeshPro congrats;
    [SerializeField] float puzzleTimer;
    private float time_left;
    [SerializeField] bool started = false;
    public AudioSource headAudio;
    public GamesManager gm;
    public AudioManager am;
    public bool angerVar;
    private int level;
    [SerializeField] GameObject[] angerObstacles = new GameObject[3];
   
    // Start is called before the first frame update
    void Start()
    {
        time_left = puzzleTimer;
        congrats.text = string.Format("");
        level = 1;
    }

    public void SetStarted(bool x)
    {
        started = x;
    } 
    
    public void TpLabirinth(Transform x)
    {
        time_left = puzzleTimer;
        StartCoroutine(DarkenerTeleport(x));
    }
    
    public void LastButton()
    {
        congrats.text = string.Format("¡Has ganado!");
        started = false;
        StartCoroutine(DarkenerTeleport(newPos));
        gm.GameWon(2);
        gm.stoppedPlaying();
    }

    IEnumerator DarkenerTeleport(Transform pos)
    {
        am.playElevator();
        blacked.SetActive(true);
        yield return new WaitForSeconds(3f); // wait 3 seconds
        player.transform.position = new Vector3 (pos.transform.position.x, player.transform.position.y, pos.transform.position.z);
        //player.transform.SetPositionAndRotation(new Vector3 (newPos.transform.position.x, player.transform.position.y, newPos.transform.position.z), pos.transform.rotation);
        blacked.SetActive(false);
    }   

    public void SpawnAngerObstacles(int i)
    {
        if (angerVar)
        {
        angerObstacles[i].SetActive(true);
        }
        else
        {
        angerObstacles[i].SetActive(false);
        }
    }
    
    public void setLevel(int i)
    {
        level = i;
    }

    public void setTimer(float time)
    {
        puzzleTimer = time;
    }
    // Update is called once per frame
    void Update()
    {
        {
        if (started) 
        {
        time_left -= Time.deltaTime;
        int minutes = Mathf.FloorToInt(time_left / 60);
        int seconds = Mathf.FloorToInt(time_left % 60);
        TMPTimer.text = string.Format("Level: " + level + "\n {0:00}:{1:00}", minutes, seconds);
        if (time_left <= 0)
            {
                StartCoroutine(DarkenerTeleport(newPos));
                started = false;
                TMPTimer.text = string.Format("");
                puzzleTimer = 90;
                am.playBad(2);
                gm.addTries(2, congrats);
            }
        }
    }
    }


}
