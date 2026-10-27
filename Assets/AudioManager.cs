using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AudioManager : MonoBehaviour
{
    [SerializeField] Logger logger;
    [Tooltip("Reads the existing anger checkboxes on the three minigames.")]
    [SerializeField] private GameConditions gameConditions;
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

        if (gameConditions == null)
        {
            Debug.LogError("Assign GameConditions on AudioManager. Insults are disabled until it is assigned.", this);
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
        // The ordinary error sound is feedback in BOTH conditions. Only the
        // accompanying insult depends on this particular game's anger checkbox.
        games[game].PlayOneShot(bad);
        if (CanPlaySpecificInsult(game))
        {
            StartCoroutine(specific_insult(game));
        }
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
        // Keep this API game-independent: GamesManager's existing timer can
        // continue calling it without knowing which game the player is using.
        if (CanPlayGenericInsults)
        {
            StartCoroutine(gen_insult());
        }
    }

    private bool CanPlayGenericInsults =>
        gameConditions != null && gameConditions.AnyAngerEnabled;

    private bool CanPlaySpecificInsult(int game) =>
        gameConditions != null && gameConditions.IsAngerEnabled(game);

    private IEnumerator gen_insult()
    {
        if (!CanPlayGenericInsults)
        {
            yield break;
        }

        AudioSource targetSource;
        AudioClip clip;
        string logMessage;
        if (Random.value < 0.33f)
        {
            if (!TryChooseInsult(audience_generic_insults, out clip))
                yield break;

            targetSource = (Random.value < 0.5f) ? audienceLeft : audienceRight;
            logMessage = "Generic insult from audience";
        }
        else
        {
            if (!TryChooseInsult(host_time_insults, out clip))
                yield break;

            targetSource = host;
            logMessage = "Generic insult from host";
        }

        if (targetSource == null)
            yield break;

        while (targetSource.isPlaying)
        {
            // A request may wait behind narration. Recheck the flags while
            // waiting and before playback, so disabling all three cancels it.
            if (!CanPlayGenericInsults)
                yield break;

            yield return null;
        }

        if (!CanPlayGenericInsults)
            yield break;

        targetSource.PlayOneShot(clip);
        logger.Log(logMessage);
    }

    private IEnumerator specific_insult(int i)
    {
        if (!CanPlaySpecificInsult(i))
            yield break;

        AudioSource targetSource;
        AudioClip clip;
        string logMessage;
        if (Random.value < 0.60f)
        {
            // Validate the actual game's list, not the unrelated generic list.
            if (!TryChooseInsult(audience_specific_insults[i], out clip))
                yield break;

            targetSource = (Random.value < 0.5f) ? audienceLeft : audienceRight;
            logMessage = "Specific insult from audience";
        }
        else
        {
            if (!TryChooseInsult(host_error_insults, out clip))
                yield break;

            targetSource = host;
            logMessage = "Specific insult from host";
        }

        if (targetSource == null)
            yield break;

        while (targetSource.isPlaying)
        {
            if (!CanPlaySpecificInsult(i))
                yield break;

            yield return null;
        }

        // Do not require an active game here. A failure insult can accompany
        // the end of an attempt, after GamesManager has stopped its timer.
        if (!CanPlaySpecificInsult(i))
            yield break;

        targetSource.PlayOneShot(clip);
        logger.Log(logMessage);
    }

    private static bool TryChooseInsult(List<AudioClip> clips, out AudioClip clip)
    {
        clip = null;
        if (clips == null || clips.Count == 0)
            return false;

        // Use THIS list's length. A missing clip is skipped while the language
        // recordings are being completed; waiting one frame would not fix it.
        clip = clips[Random.Range(0, clips.Count)];
        return clip != null;
    }
}
