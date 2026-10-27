Insult permission now follows the existing anger checkboxes on the three minigames. Generic/time insults are allowed when **any** checkbox is enabled. Specific insults requested by `playBad(game)` are allowed only when that particular game's checkbox is enabled. The ordinary failure sound still plays in both conditions.

| Cards | Gun | Labyrinth | Generic insults | Specific insults allowed for |
| --- | --- | --- | --- | --- |
| Off | Off | Off | No | None |
| On | Off | Off | Yes | Cards |
| Off | On | Off | Yes | Gun |
| Off | Off | On | Yes | Labyrinth |
| On | On | Off | Yes | Cards and Gun |
| On | Off | On | Yes | Cards and Labyrinth |
| Off | On | On | Yes | Gun and Labyrinth |
| On | On | On | Yes | All three |

**Where to change the settings**

Keep using **Anger Enabled** on Card_Minigame and **Anger Var** on GunPuzzle and LabyrinthScript. These values remain the source of truth for their gameplay mechanics. No new or duplicated anger checkboxes were introduced.

The new GameConditions component is already added to **AudioManager** in the saved AngerRoom scene. It references all three minigame components. AudioManager's new **Game Conditions** field references it. No manual assignments are needed for those four references in this scene.

The former Presenter GameObject is now named **Host**, and its existing AudioSource is assigned to AudioManager's **Host** field in the saved scene. The existing audience references are also assigned. Playback also requires clips in the selected language's corresponding lists, which remain work in progress.

**How the code works**

Each minigame exposes an `AngerEnabled` property with a getter only. It returns the existing field's value; it does not store a second boolean. GameConditions provides a common read-only query for those three properties:

- `IsAngerEnabled(0)`: Cards.
- `IsAngerEnabled(1)`: Gun.
- `IsAngerEnabled(2)`: Labyrinth.
- `AnyAngerEnabled`: logical OR of all three results.

This keeps the per-game settings where they already are while giving audio one place to ask about permission. AudioManager does not need to inspect individual gameplay controllers or remember which game is currently being played.

`playBad(game)` first plays the ordinary buzzer. It requests a specific insult only if `IsAngerEnabled(game)` is true. `play_gen_insult()` requests a generic insult only if `AnyAngerEnabled` is true. Public method names and arguments are unchanged, so existing scripts and UnityEvent calls keep working.

The generic timer in GamesManager has not been changed. Enabling one checkbox permits its requests; it does not start a new timer or make insults play continuously. Existing start/stop calls and delays still control when generic requests occur.

An insult can wait for its selected speaker to finish another clip. The routines recheck their condition while waiting and immediately before playback. Disabling the relevant condition cancels a waiting request. A clip that has already started is allowed to finish; this change does not interrupt shared host narration or stop an AudioSource mid-clip. For experimental runs, choose conditions before starting the session.

Specific failure reactions do not require an active game: they may play after an attempt has ended. This preserves the existing call order in which GamesManager can stop the game's timer before `playBad()` is called.

Empty lists and missing clips now skip the insult request safely. The specific audience path checks the correct game's list, and the host-error path selects using the host-error list's length. The speaker-selection probabilities are unchanged. Logs are written when an insult actually starts, rather than when it is merely queued.

**Check in Unity**

1. Exit Play Mode and allow compilation/scene import to finish. Confirm the GameConditions references on AudioManager, plus the host AudioSource and the chosen language's clips.
2. Disable all three anger flags. Trigger normal failure feedback and let the existing generic timer request an insult: buzzers should play, but neither insult category should play.
3. Enable only Gun. Generic insults should be permitted regardless of which game is being played. A gun failure may produce a specific insult; card and labyrinth failures should produce only their ordinary failure sounds.
4. Repeat with only Cards and only Labyrinth, then with all three enabled.
5. For a waiting-request check, let an insult wait behind a busy speaker and disable its permission before the speaker becomes free. It should not start afterward. Already playing clips finish normally.

The isolated checks cover all eight flag combinations, both host/audience paths, the correct game-specific clip lists, ordinary failure sounds, waiting-request permission changes, and empty/missing clip data. Actual audio playback and scene lifecycle still require the Unity checks above. No backups were created for this change, as requested.
