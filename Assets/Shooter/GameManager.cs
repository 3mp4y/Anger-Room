using System.Collections;
using System.Collections.Generic;
using System.Collections;
using UnityEngine;
using TMPro; // Requires TextMeshPro

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    [Header("Game Settings")]
    public float gameDuration = 60f;     // How long the game lasts

    [Header("UI")]
    public TextMeshProUGUI scoreText;      // Displays current score
    public TextMeshProUGUI timerText;      // Displays remaining time
    public TextMeshProUGUI missText;       // "Miss!" message
    public TextMeshProUGUI gameOverText;   // End-game screen
    public Canvas worldCanvas;            // The world-space UI canvas

    [Header("Miss Message")]
    public float missDisplayDuration = 1f;

    // State
    public bool IsGameRunning { get; private set; } = false;
    private int _score = 0;
    private float _timeRemaining;
    private Coroutine _missCoroutine;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    private void Start()
    {
        if (missText != null)   missText.gameObject.SetActive(false);
        if (gameOverText != null) gameOverText.gameObject.SetActive(false);
        UpdateScoreUI();
    }

    public void StartGame()
    {
        if (IsGameRunning) return;

        _score = 0;
        _timeRemaining = gameDuration;
        IsGameRunning = true;

        UpdateScoreUI();
        if (gameOverText != null) gameOverText.gameObject.SetActive(false);

        TargetSpawner.Instance?.StartSpawning();

        StartCoroutine(GameTimerCoroutine());
    }

    private IEnumerator GameTimerCoroutine()
    {
        while (_timeRemaining > 0f)
        {
            _timeRemaining -= Time.deltaTime;
            if (timerText != null)
                timerText.text = $"Time: {Mathf.CeilToInt(_timeRemaining)}";
            yield return null;
        }

        EndGame();
    }

    public void RegisterHit()
    {
        if (!IsGameRunning) return;
        _score += 10;
        UpdateScoreUI();
    }

    public void RegisterMiss()
    {
        if (!IsGameRunning) return;
        ShowMissMessage();
    }

    private void ShowMissMessage()
    {
        if (missText == null) return;
        if (_missCoroutine != null) StopCoroutine(_missCoroutine);
        _missCoroutine = StartCoroutine(MissMessageCoroutine());
    }

    private IEnumerator MissMessageCoroutine()
    {
        missText.gameObject.SetActive(true);
        missText.text = "MISS!";
        yield return new WaitForSeconds(missDisplayDuration);
        missText.gameObject.SetActive(false);
    }

    private void EndGame()
    {
        IsGameRunning = false;
        TargetSpawner.Instance?.StopSpawning();

        if (gameOverText != null)
        {
            gameOverText.gameObject.SetActive(true);
            gameOverText.text = $"Game Over!\nScore: {_score}";
        }

        if (timerText != null) timerText.text = "Time: 0";
    }

    private void UpdateScoreUI()
    {
        if (scoreText != null)
            scoreText.text = $"Score: {_score}";
    }
}
