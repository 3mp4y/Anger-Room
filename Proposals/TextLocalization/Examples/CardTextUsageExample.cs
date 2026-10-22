using AngerRoom.Localization;
using TMPro;
using UnityEngine;

namespace AngerRoom.Localization.Examples
{
    // Reading example only. This is not a replacement for Card_Minigame.
    // At integration time, its field and calls can be incorporated into the
    // existing minigame. It owns no card rules, timers, scores, or translations.
    public sealed class CardTextUsageExample : MonoBehaviour
    {
        [SerializeField] private TextAdapter textAdapter;
        [SerializeField] private TMP_Text levelText;
        [SerializeField] private TMP_Text roundText;
        [SerializeField] private TMP_Text timerText;

        public void ShowProgress(int level, int levelCount, int round, int roundsPerLevel)
        {
            levelText.text = textAdapter.Format(TextId.LevelProgress, level, levelCount);
            roundText.text = textAdapter.Format(TextId.RoundProgress, round, roundsPerLevel);
        }

        public void ShowTutorialLabels()
        {
            levelText.text = textAdapter.Get(TextId.LevelLabel);
            roundText.text = textAdapter.Get(TextId.RoundLabel);
            timerText.text = textAdapter.Get(TextId.TimerLabel);
        }

        public void ShowNextLevelCountdown(int seconds)
        {
            timerText.text = textAdapter.Format(TextId.NextLevelCountdown, seconds);
        }

        public void ShowVictory()
        {
            timerText.text = textAdapter.Get(TextId.Victory);
            levelText.text = string.Empty;
            roundText.text = string.Empty;
        }

        public void ShowClock(int minutes, int seconds)
        {
            // A plain clock contains no language-dependent words.
            timerText.text = $"{minutes:00}:{seconds:00}";
        }
    }
}
