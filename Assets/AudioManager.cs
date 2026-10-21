using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AudioManager : MonoBehaviour
{
    [SerializeField] Logger logger;
    private LanguageAdapter language_adapter;
    
    public GamesManager gm;

    public AudioSource[] games = new AudioSource[3];
    public AudioSource audienceRight;
    public AudioSource audienceLeft;
    public AudioSource host;
    public AudioSource clock;
    [SerializeField] bool audioCooldown;
    private int tutorial_index;
    [Header("Dialogues")]
    [SerializeField] List<AudioClip> host_intros;
    private List<AudioClip> tutorials;
    private List<AudioClip> host_time_insults;
    private List<AudioClip> host_error_insults;
    private List<AudioClip> host_loss;
    private List<AudioClip> audience_generic_insults;
    private List<AudioClip> audience_time_insults;
    private List<AudioClip> audience_card_insults;
    private List<AudioClip> audience_gun_insults;
    private List<AudioClip> audience_lab_insults;
    private List<AudioClip>[] audience_specific_insults = new List<AudioClip>[3];
    
    [Header("Sounds")]
    [SerializeField] AudioClip bad;
    [SerializeField] AudioClip lvlUP;
    [SerializeField] AudioClip won;
    [SerializeField] AudioClip shot;
    [SerializeField] AudioClip elevator;

    void Awake() {
        language_adapter = GetComponent<LanguageAdapter>();

        if (language_adapter == null)
        {
            Debug.LogError("Adapter is missing from this GameObject!", this);
        }
    }
    // Start is called before the first frame update
    void Start()
    {
        tutorial_index = 0;
        host_intros = language_adapter.HostIntro;
        tutorials = language_adapter.HostTutorials;
        host_time_insults = language_adapter.HostTimeInsults;
        host_error_insults = language_adapter.HostErrorInsults;
        host_loss =  language_adapter.HostLoss;
        audience_generic_insults = language_adapter.AudienceGenericInsults;
        audience_time_insults = language_adapter.AudienceTimeInsults;
        audience_card_insults = language_adapter.AudienceCardInsults;
        audience_gun_insults = language_adapter.AudienceGunInsults;
        audience_lab_insults = language_adapter.AudienceLabInsults;

        audioCooldown = false;   
        audience_specific_insults[0] = audience_card_insults;
        audience_specific_insults[1] = audience_gun_insults;
        audience_specific_insults[2] = audience_lab_insults;
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    public void PlayIntro(GameObject UI_button)
    {
    StartCoroutine(Introduction(UI_button));
    }

    private IEnumerator Introduction(GameObject UI_button)
    {
        host.PlayOneShot(host_intros[tutorial_index]);
        while (host.isPlaying)
            {
                yield return null;
            }
        UI_button.SetActive(true);
        if (tutorial_index == host_intros.Count-1); {
        logger.Log("Introduction Over");
        //gm.StartOverAllTimer();
        }
        tutorial_index++;
    }
    /*
    private IEnumerator Introduction()
    {
        Debug.Log("Inizio Intro");
        yield return new WaitForSeconds(3);
        host.PlayOneShot(first_intro);
        while (host.isPlaying)
            {
                yield return null;
            }
        Debug.Log("fine intro");
        gm.StartOverAllTimer();
    }
    */

    public bool isPresentSpeaking()
    {
        return host.isPlaying;
    }
    public void playBad(int game)
    {
        games[game].PlayOneShot(bad);
        StartCoroutine(specific_insult(game)); 
    }

    public void playBadSdound()
    {
        games[1].PlayOneShot(bad);
    }

    public void playLevelUp(int game)
    {
        games[game].PlayOneShot(lvlUP);
    }
    public void playTutorial(int game)
    {
     host.PlayOneShot(tutorials[game]);
    }
    public void playShot()
    {
        games[1].PlayOneShot(shot);
    }

    public void playEnd()
    {
        host.PlayOneShot(won);
    }

    public void playLoss()
    {
        host.PlayOneShot(host_loss[0]);
    }

    public void playElevator()
    {
     games[2].PlayOneShot(elevator);
    }

    public void play_gen_insult()
    {
        StartCoroutine(gen_insult());
    }

    private IEnumerator gen_insult()
    {
        AudioSource targetSource;
        AudioClip clip;
        // Randomly choose between A and B
        //bool useA = Random.value < 0.5f;
        if (Random.value < 0.33f)
        {
            // Safety check
            if (audience_generic_insults == null || audience_generic_insults.Count == 0)
                yield return null;
            // Pick random clip from A
            clip = audience_generic_insults[Random.Range(0, audience_generic_insults.Count)];
            // Randomly choose x or y
            targetSource = (Random.value < 0.5f) ? audienceLeft : audienceRight;

            logger.Log("Generic insult from audience");
            
        }
        else
        {
            // Safety check
            if (host_time_insults == null || host_time_insults.Count == 0)
                yield return null;

            // Pick random clip from B
            targetSource = host;
            clip = host_time_insults[Random.Range(0, host_time_insults.Count)];
            logger.Log("Generic insult from host");
        }

        while (targetSource.isPlaying)
            {
                yield return null;
            }
        targetSource.PlayOneShot(clip);
    }
    private IEnumerator specific_insult(int i)
    {
        AudioSource targetSource;
        AudioClip clip;
        if (Random.value < 0.60f)
        {
        // Randomly choose between A and B
        //bool useA = Random.value < 0.5f
            // Safety check
        if (audience_generic_insults == null || audience_generic_insults.Count == 0) {yield return null;}
            // Pick random clip from A
        clip = audience_specific_insults[i][Random.Range(0, audience_specific_insults[i].Count)];
            // Randomly choose x or y
        targetSource = (Random.value < 0.5f) ? audienceLeft : audienceRight;
        logger.Log("Specific insult from audience");
        /*nif (Random.value < 0.5f) {targetSource = audienceLeft;}
        else { targetSource = audienceRight; } */
        }
        else
        {
            if (host_error_insults == null || host_error_insults.Count == 0)
                yield return null;

            // Pick random clip from B
            targetSource = host;
            clip = host_error_insults[Random.Range(0, host_time_insults.Count)];
            logger.Log("Specific insult from host");
        }

        while (targetSource.isPlaying)
            {
                yield return null;
            }

        targetSource.PlayOneShot(clip);
    }


}