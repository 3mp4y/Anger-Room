namespace AngerRoom.Localization
{
    // An ID describes the meaning of a message, independently of its wording.
    // Values are explicit because Unity saves Inspector enum selections as numbers.
    // Keep existing numbers when adding or reorganizing entries.
    public enum TextId
    {
        None = 0,

        Victory = 10,

        LevelLabel = 20,
        RoundLabel = 21,
        TimerLabel = 22,
        TargetScoreLabel = 23,
        TutorialTimerLabel = 24,

        // Placeholder contracts: every translation must use the same arguments.
        LevelProgress = 30,       // {0} current level, {1} level count
        RoundProgress = 31,       // {0} current round, {1} rounds per level
        ScoreProgress = 32,       // {0} current score, {1} target score
        NextLevelCountdown = 33,  // {0:00} seconds
        LabyrinthStatus = 34,     // {0} level, {1:00} minutes, {2:00} seconds

        AttemptFailed = 40,       // {0} remaining attempts
        GameDisabled = 41,

        Points = 50,              // {0} awarded/available points
        ScoreboardEntry = 51      // {0} participant ID, {1} points, {2} rank
    }
}
