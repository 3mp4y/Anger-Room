using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AudioManager : MonoBehaviour
{
    [SerializeField] Logger logger;
    
    public GamesManager gm;

    public AudioSource[] games = new AudioSource[3];
    public AudioSource audienceRight;
    public AudioSource audienceLeft;
    public AudioSource presenter;
    public AudioSource clock;
    [SerializeField] bool audioCooldown;
    [SerializeField] AudioClip first_intro;
    [SerializeField] AudioClip[] tutorials = new AudioClip[3];
    [SerializeField] List<AudioClip> presenter_Time_insults;
    [SerializeField] List<AudioClip> presenter_error_insults;
    [SerializeField] List<AudioClip> audience_generci_insults;
    [SerializeField] List<AudioClip> audience_timed_insults;
    [SerializeField] List<AudioClip> audience_card_insults;
    [SerializeField] List<AudioClip> audience_gun_insults;
    [SerializeField] List<AudioClip> audience_lab_insults;
    [SerializeField] List<AudioClip>[] audience_specific_insults = new List<AudioClip>[3];
    [SerializeField] AudioClip Presenterloss;
    [SerializeField] AudioClip bad;
    [SerializeField] AudioClip lvlUP;
    [SerializeField] AudioClip won;
    [SerializeField] AudioClip shot;
    [SerializeField] AudioClip elevator;
    // Start is called before the first frame update
    void Start()
    {
        audioCooldown = false;   
        audience_specific_insults[0] = audience_card_insults;
        audience_specific_insults[1] = audience_gun_insults;
        audience_specific_insults[2] = audience_lab_insults;
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    public void PlayIntro()
    {
    StartCoroutine(Introduction());
    }
    private IEnumerator Introduction()
    {
        Debug.Log("Inizio Intro");
        yield return new WaitForSeconds(3);
        presenter.PlayOneShot(first_intro);
        while (presenter.isPlaying)
            {
                yield return null;
            }
        Debug.Log("fine intro");
        gm.StartOverAllTimer();
    }

    public bool isPresentSpeaking()
    {
        return presenter.isPlaying;
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
     presenter.PlayOneShot(tutorials[game]);
    }
    public void playShot()
    {
        games[1].PlayOneShot(shot);
    }

    public void playEnd()
    {
        presenter.PlayOneShot(won);
    }

    public void playLoss()
    {
        presenter.PlayOneShot(Presenterloss);
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
            if (audience_generci_insults == null || audience_generci_insults.Count == 0)
                yield return null;
            // Pick random clip from A
            clip = audience_generci_insults[Random.Range(0, audience_generci_insults.Count)];
            // Randomly choose x or y
            targetSource = (Random.value < 0.5f) ? audienceLeft : audienceRight;

            logger.Log("Generic insult from audience");
            
        }
        else
        {
            // Safety check
            if (presenter_Time_insults == null || presenter_Time_insults.Count == 0)
                yield return null;

            // Pick random clip from B
            targetSource = presenter;
            clip = presenter_Time_insults[Random.Range(0, presenter_Time_insults.Count)];
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
        if (audience_generci_insults == null || audience_generci_insults.Count == 0) {yield return null;}
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
            if (presenter_error_insults == null || presenter_error_insults.Count == 0)
                yield return null;

            // Pick random clip from B
            targetSource = presenter;
            clip = presenter_error_insults[Random.Range(0, presenter_Time_insults.Count)];
            logger.Log("Specific insult from host");
        }

        while (targetSource.isPlaying)
            {
                yield return null;
            }

        targetSource.PlayOneShot(clip);
    }


}