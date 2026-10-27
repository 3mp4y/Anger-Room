using System.Collections;
using AngerRoom.Localization;
using TMPro;
using UnityEngine;
using UnityEngine.Serialization;

public class Card_Minigame : MonoBehaviour
{
    private enum CardGameState
    {
        Idle,
        Tutorial,
        Playing
    }

    private const int GameIndex = 0;
    private const int CardCount = 5;
    private const int RoundsPerLevel = 3;
    private const int LevelCount = 3;
    private const int InitialFailureChance = 5;
    private const int MaximumFailureChance = 99;
    private const float TimeReductionPerLevel = 1.7f;

    [Header("References")]
    [FormerlySerializedAs("gm")]
    [SerializeField] private GamesManager gamesManager;
    [FormerlySerializedAs("am")]
    [SerializeField] private AudioManager audioManager;
    [Tooltip("The shared TextAdapter on the AudioManager object.")]
    [SerializeField] private TextAdapter textAdapter;
    [SerializeField] private Logger logger;
    [SerializeField] private GameObject[] cards = new GameObject[CardCount];

    [Header("Settings")]
    [FormerlySerializedAs("anger_var")]
    [Tooltip("Allows ignored button presses and unsolvable configurations in later rounds.")]
    [SerializeField] private bool angerEnabled;
    // GameConditions reads the same checkbox used by this game's mechanics.
    public bool AngerEnabled => angerEnabled;
    [FormerlySerializedAs("puzzTimer")]
    [SerializeField] private float roundDurationSeconds;

    [Header("Audio")]
    [FormerlySerializedAs("audioData")]
    [SerializeField] private AudioSource cardAudioSource;
    [FormerlySerializedAs("shuffle")]
    [SerializeField] private AudioClip cardFlipSound;
    [FormerlySerializedAs("lvlup")]
    [SerializeField] private AudioClip levelUpSound;

    [Header("Display")]
    [FormerlySerializedAs("timer_txt")]
    [SerializeField] private TextMeshPro timerText;
    [FormerlySerializedAs("round_txt")]
    [SerializeField] private TextMeshPro roundText;
    [FormerlySerializedAs("level_txt")]
    [SerializeField] private TextMeshPro levelText;

    [Header("Current State")]
    [FormerlySerializedAs("round")]
    [SerializeField] private int currentRound = 1;
    [FormerlySerializedAs("level")]
    [SerializeField] private int currentLevel = 1;
    [FormerlySerializedAs("failChance")]
    [SerializeField] private int inputFailureChance;
    [FormerlySerializedAs("time_left")]
    [SerializeField] private float timeRemaining;

    private bool[] cardFacesUp = new bool[CardCount];
    private float timeReduction;
    private CardGameState state = CardGameState.Idle;

    private readonly bool[][] solvableConfigurations =
    {
        new bool[] { false, false, true, false, false },
        new bool[] { false, true, true, false, true },
        new bool[] { true, false, true, true, false },
        new bool[] { false, true, false, true, false }
    };

    private readonly bool[][] unsolvableConfigurations =
    {
        new bool[] { false, false, false, false, true },
        new bool[] { true, true, true, false, false },
        new bool[] { true, false, true, false, true },
        new bool[] { true, true, true, true, false }
    };

    private void Start()
    {
        ClearDisplay();
        timeReduction = 0f;
    }

    private void Update()
    {
        if (state != CardGameState.Playing)
        {
            return;
        }

        int minutes = Mathf.FloorToInt(timeRemaining / 60);
        int seconds = Mathf.FloorToInt(timeRemaining % 60);

        if (timeRemaining > 0)
        {
            timeRemaining -= Time.deltaTime;
            timerText.text = $"{minutes:00}:{seconds:00}";
        }
        else
        {
            ClearCards();
            gamesManager.addTries(GameIndex, timerText, textAdapter);
            audioManager.playBad(GameIndex);
        }
    }

    private void UpdateCardVisuals()
    {
        cardAudioSource.PlayOneShot(cardFlipSound);

        for (int i = 0; i < cardFacesUp.Length; i++)
        {
            cards[i].SetActive(true);
            cards[i].transform.eulerAngles = cardFacesUp[i]
                ? new Vector3(90f, 270f, 90f)
                : new Vector3(-90f, -180f, 0f);
        }
    }

    public void ClearCards()
    {
        // A pending round transition must not restore cards after this reset.
        StopAllCoroutines();
        state = CardGameState.Idle;
        cardFacesUp = new bool[CardCount];
        currentLevel = 1;
        currentRound = 1;
        ClearDisplay();
        SetCardsActive(false);
    }

    private void ClearDisplay()
    {
        levelText.text = string.Empty;
        roundText.text = string.Empty;
        timerText.text = string.Empty;
    }

    private void SetCardsActive(bool active)
    {
        foreach (GameObject card in cards)
        {
            card.SetActive(active);
        }
    }

    // Public methods are entry points for the buttons wired in the Unity Inspector.
    public void StartMinigame()
    {
        inputFailureChance = InitialFailureChance;
        ClearCards();
        levelText.text = textAdapter.Format(TextId.LevelProgress, currentLevel, LevelCount);
        roundText.text = textAdapter.Format(TextId.RoundProgress, currentRound, RoundsPerLevel);
        StartRound(true);
        state = CardGameState.Playing;
        logger.Log("Cards are" + string.Join(" ", cardFacesUp));
    }

    public void StartTutorial()
    {
        ClearCards();
        state = CardGameState.Tutorial;
        audioManager.playTutorial(GameIndex);
        gamesManager.DoneTutorial(GameIndex);
        gamesManager.Playing();
        cardFacesUp = new bool[] { true, true, true, true, true };
        UpdateCardVisuals();
        levelText.text = textAdapter.Get(TextId.LevelLabel);
        roundText.text = textAdapter.Get(TextId.RoundLabel);
        timerText.text = textAdapter.Get(TextId.TimerLabel);
        StartCoroutine(WaitForTutorial());
    }

    private IEnumerator WaitForTutorial()
    {
        while (audioManager.isPresentSpeaking())
        {
            yield return null;
        }

        gamesManager.stoppedPlaying();
    }

    private void StartRound(bool useSolvableConfiguration)
    {
        timeRemaining = roundDurationSeconds;
        bool[][] configurations = useSolvableConfiguration
            ? solvableConfigurations
            : unsolvableConfigurations;

        configurations[Random.Range(0, configurations.Length)].CopyTo(cardFacesUp, 0);
        UpdateCardVisuals();
    }

    public void FlipLeftCards()
    {
        TryFlipCards(0, 1, 2, "left");
    }

    public void FlipRightCards()
    {
        TryFlipCards(2, 3, 4, "right");
    }

    public void FlipCenterCards()
    {
        TryFlipCards(1, 2, 3, "center");
    }

    private void TryFlipCards(int first, int second, int third, string buttonName)
    {
        if (state == CardGameState.Idle)
        {
            return;
        }

        // Practice always responds to input, even in an anger-enabled game.
        if (state == CardGameState.Playing && ShouldIgnorePress())
        {
            logger.Log($"Failed to press card button {buttonName}");
            return;
        }

        cardFacesUp[first] = !cardFacesUp[first];
        cardFacesUp[second] = !cardFacesUp[second];
        cardFacesUp[third] = !cardFacesUp[third];
        UpdateCardVisuals();

        if (state == CardGameState.Tutorial)
        {
            logger.Log($"Tutorial - Pressed card button {buttonName}");
        }
        else
        {
            logger.Log($"Pressed card button {buttonName}");
            StartCoroutine(CheckResults());
        }
    }

    private bool ShouldIgnorePress()
    {
        // A guard clause: exit before increasing the chance or rolling a random value.
        if (!angerEnabled)
        {
            return false;
        }

        if (inputFailureChance < MaximumFailureChance)
        {
            inputFailureChance++;
        }

        // Preserve the original anger-mode probabilities: integers 0 through 100,
        // inclusive. This is a threshold, not an exact percentage.
        return Random.Range(0, 101) <= inputFailureChance;
    }

    private IEnumerator ShowLevelTransition()
    {
        SetCardsActive(false);
        cardAudioSource.PlayOneShot(levelUpSound);
        levelText.text = textAdapter.Format(TextId.LevelProgress, currentLevel, LevelCount);
        roundText.text = textAdapter.Format(TextId.RoundProgress, currentRound, RoundsPerLevel);

        float transitionTimeRemaining = 3.9f;
        while (transitionTimeRemaining > 0f)
        {
            transitionTimeRemaining -= Time.deltaTime;
            int seconds = Mathf.FloorToInt(transitionTimeRemaining % 60);
            timerText.text = textAdapter.Format(TextId.NextLevelCountdown, seconds);
            yield return null;
        }

        SetCardsActive(true);
    }

    private void ShowNextRound()
    {
        cardAudioSource.PlayOneShot(levelUpSound);
        roundText.text = textAdapter.Format(TextId.RoundProgress, currentRound, RoundsPerLevel);
    }

    private bool AreAllCardsFaceUp()
    {
        foreach (bool faceUp in cardFacesUp)
        {
            if (!faceUp)
            {
                return false;
            }
        }

        return true;
    }

    private IEnumerator CheckResults()
    {
        if (state != CardGameState.Playing || !AreAllCardsFaceUp())
        {
            yield break;
        }

        switch (currentLevel)
        {
            case 1:
            case 2:
                logger.Log($"Solved - Round:{currentRound} Level:{currentLevel}");
                if (currentRound < RoundsPerLevel)
                {
                    currentRound++;
                    ShowNextRound();
                    yield return new WaitForSeconds(0.8f);
                    StartRound(true);
                }
                else
                {
                    currentRound = 1;
                    currentLevel += 3;
                    StartCoroutine(ShowLevelTransition());
                    yield return new WaitForSeconds(4.1f);
                    timeReduction += TimeReductionPerLevel;
                    StartRound(true);
                }

                timeRemaining = roundDurationSeconds - timeReduction;
                break;

            case LevelCount:
                if (currentRound < RoundsPerLevel)
                {
                    logger.Log($"Solved - Round:{currentRound} Level:{currentLevel}");
                    currentRound++;
                    ShowNextRound();
                    yield return new WaitForSeconds(0.6f);
                    int angerRoll = Random.Range(currentRound, currentRound + 3) + 1;
                    bool useUnsolvableConfiguration = angerEnabled && angerRoll > 3;
                    StartRound(!useUnsolvableConfiguration);

                    if (useUnsolvableConfiguration)
                    {
                        logger.Log("AngerCombination");
                    }

                    timeRemaining = roundDurationSeconds - timeReduction * 2;
                }
                else
                {
                    state = CardGameState.Idle;
                    timerText.text = textAdapter.Get(TextId.Victory);
                    levelText.text = string.Empty;
                    roundText.text = string.Empty;
                    gamesManager.GameWon(GameIndex);
                    gamesManager.stoppedPlaying();
                }

                break;

            default:
                Debug.LogError($"Unexpected card minigame level: {currentLevel}", this);
                break;
        }
    }
}
