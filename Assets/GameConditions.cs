using UnityEngine;

// A shared, read-only view of the EXISTING minigame anger checkboxes.
// There are no extra copies of those booleans here: gameplay and audio therefore
// cannot disagree because somebody changed only one of two duplicate settings.
[DisallowMultipleComponent]
[AddComponentMenu("Anger Room/Game Conditions")]
public sealed class GameConditions : MonoBehaviour
{
    [SerializeField] private Card_Minigame cardMinigame;
    [SerializeField] private GunPuzzle gunMinigame;
    [SerializeField] private LabyrinthScript labyrinthMinigame;

    // Generic/time insults are session-wide. They do NOT depend on the active
    // game: one enabled game is enough; only all three disabled makes this false.
    public bool AnyAngerEnabled =>
        IsAngerEnabled(0) || IsAngerEnabled(1) || IsAngerEnabled(2);

    public bool IsAngerEnabled(int game)
    {
        // Preserve the IDs already used by playBad and the audio clip arrays.
        // A missing reference or invalid ID never grants permission to insult.
        switch (game)
        {
            case 0: return cardMinigame != null && cardMinigame.AngerEnabled;
            case 1: return gunMinigame != null && gunMinigame.AngerEnabled;
            case 2: return labyrinthMinigame != null && labyrinthMinigame.AngerEnabled;
            default: return false;
        }
    }

    private void Awake()
    {
        if (cardMinigame == null || gunMinigame == null || labyrinthMinigame == null)
        {
            Debug.LogError("Assign all three minigames on GameConditions. Missing games are treated as non-anger.", this);
        }
    }
}
