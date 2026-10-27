using System.Collections;
using AngerRoom.Localization;
using UnityEngine;
using TMPro;

public class LabyrinthScript : MonoBehaviour
{
    public GameObject blacked;
    public GameObject player;
    public Transform newPos;
    [SerializeField] TextMeshPro TMPTimer;
    [SerializeField] TextMeshPro congrats;
    [Tooltip("The shared TextAdapter on the AudioManager object.")]
    [SerializeField] private TextAdapter textAdapter;
    [SerializeField] private TMP_Text startButtonText;
    [SerializeField] private TMP_Text tutorialSignText;
    [Tooltip("The signs inside labyrinths 1, 2 and 3, in that order.")]
    [SerializeField] private TMP_Text[] levelSigns = new TMP_Text[3];
    [SerializeField] float puzzleTimer;
    private float time_left;
    [SerializeField] bool started = false;
    public AudioSource headAudio;
    public GamesManager gm;
    public AudioManager am;
    public bool angerVar;
    // Read-only access for GameConditions; the existing Inspector flag stays here.
    public bool AngerEnabled => angerVar;
    private int level;
    [SerializeField] GameObject[] angerObstacles = new GameObject[3];
   
    // Start is called before the first frame update
    void Start()
    {
        time_left = puzzleTimer;
        congrats.text = string.Empty;
        level = 1;
        // These labels need translating only once: the adapter keeps the language
        // selected before Play. Direct references also work for inactive signs.
        startButtonText.text = textAdapter.Get(TextId.TutorialLabel);
        tutorialSignText.text = textAdapter.Get(TextId.TutorialLabel);
        for (int i = 0; i < levelSigns.Length; i++)
        {
            levelSigns[i].text = textAdapter.Format(TextId.LevelNumber, i + 1);
        }
    }

    // The tutorial button's UnityEvents call these instead of assigning English
    // strings directly to TMP. Keep these public methods available to the scene.
    public void ShowStartButtonLabel()
    {
        startButtonText.text = textAdapter.Get(TextId.StartLabel);
    }

    public void ShowTutorialTimerLabel()
    {
        TMPTimer.text = textAdapter.Get(TextId.TimerLabel);
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
        congrats.text = textAdapter.Get(TextId.Victory);
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
        // Wording and number placement belong to the translation; gameplay
        // supplies only the level, minutes and seconds, in this argument order.
        TMPTimer.text = textAdapter.Format(TextId.LabyrinthStatus, level, minutes, seconds);
        if (time_left <= 0)
            {
                StartCoroutine(DarkenerTeleport(newPos));
                started = false;
                TMPTimer.text = string.Empty;
                puzzleTimer = 93;
                am.playBad(2);
                // The same adapter localizes remaining attempts, game disabled
                // and the updated points label inside GamesManager.
                gm.addTries(2, congrats, textAdapter);
            }
        }
    }
    }


}
