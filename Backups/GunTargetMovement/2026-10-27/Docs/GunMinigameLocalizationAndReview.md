The gun minigame now uses the same session language and text catalogue as the card minigame. Its timer, level, score, tutorial labels, transition countdown, victory and failure messages are localized. The child text under `EndTut` is also localized. Language remains fixed for each Play session.

The localization is implemented. The gameplay improvements discussed below are recommendations for a separate change. The shared anger control and insult audio remain pending your review.

**Unity setup and checks**

The saved `Assets/Plugins/Scenes/AngerRoom.unity` scene already contains the two new assignments on **Minigame 2 - Shooting > GunPuzzle**:

| Field | Assigned object |
| --- | --- |
| Text Adapter | The existing TextAdapter on AudioManager |
| End Tutorial Text | EndTut / Text (TMP) |

No extra component or catalogue asset is needed. If the scene is open in Unity while files change, exit Play Mode and allow Unity to import and reload the saved scene. If you have unsaved scene edits you need to keep, retain them and assign those two fields manually before saving, rather than replacing your unsaved scene state. The three original TMP references remain assigned.

1. Select a language on **AudioManager > LanguageAdapter** before pressing Play.
2. Start the gun tutorial. Check the level, target-score and tutorial-timer labels.
3. After the narration ends, check the visible EndTut label. Shoot it to end the tutorial.
4. Start the real game. Check the score after a hit, the level label, the next-level countdown and the victory message.
5. In a separate run, let the timer expire. Check the two remaining-attempt messages and the third-attempt disabled message.
6. Repeat in the other languages, checking accents and wrapping at the actual headset viewing distance.

The new `EndTutorial` entry has stable ID **25** in `TextId.cs`. Its translations preserve the two-line presentation:

| English | Italian | Spanish |
| --- | --- | --- |
| End / Tutorial | Termina / il tutorial | Finalizar / tutorial |

Here the slash represents a line break. Edit these strings in **GameTexts > Entries > EndTutorial**. Use an actual line break in the Inspector. The adapter caches translations, so restart Play Mode after editing wording or the selected language.

`GunPuzzle.Start()` assigns the EndTut string through a direct `TMP_Text` reference even while its parent GameObject is inactive. This avoids waiting for a component on the hidden object to initialize. `TMP_Text` is the shared base type for TextMeshPro's world and canvas text components; the scene currently uses a world-space TextMeshPro component.

The existing `GamesManager.addTries` overload receives the same adapter for gun failures. Its attempt counting, score arithmetic and timing are unchanged. No new manager dependency was necessary.

**Files reviewed and responsibilities**

The saved scene uses `GunPuzzle.cs`, `ShootScript.cs`, `Spawn2.cs`, and `TargetMover.cs`, plus `Shooter/Target.cs` on the spawned target prefab. The scene's GunPuzzle and Spawn2 both reference `Shooter/ShootingTarget_Collider Variant.prefab`.

The older `Shooter/PewGun.cs`, `Shooter/TargetSpawner.cs` and `Shooter/GameManager.cs` form another implementation. Their script GUIDs were not found in the saved scenes or prefabs searched. They are not the implementation wired to this minigame. Keep them preserved, but clearly identify them as legacy so future work does not accidentally modify the wrong system.

**Fix first: spawning resumes after victory**

In `GunPuzzle.CoroutineHit()`, the victory branch hides the real gun and displays the victory text, then calls `Spawner.StartSpawning()`. That starts new targets after the game is over. This behavior was also reproduced with the real GunPuzzle code in the isolated checks.

The proposed fix is to finish in an idle/completed state with spawning stopped and targets cleared. Put that cleanup in one completion method shared by the appropriate exit paths. A completed activity should not retain a background spawning process.

**Make the gun's activity state explicit**

`started` currently controls the countdown, while `tutorial` controls hit scoring. `CoroutineHit()` accepts a hit whenever `tutorial` is false, including when gameplay has stopped. `ShootScript.Update()` has no check for a level transition or completed game; it relies on the gun object's activation. During a level transition the real gun remains active, so it can still fire and play miss reactions. The timeout condition also checks time and tutorial mode without requiring `started`.

I recommend `Idle`, `TutorialNarration`, `TutorialPractice`, `Playing`, and `LevelTransition` states. Only `Playing` should accept score changes and timeouts. Tutorial practice should accept shots without changing the real score. Entering an exit state should cancel any pending tutorial/transition routine that could later reactivate gameplay.

This extends the approach used for the card fix. State transitions would explicitly start and stop input, spawning and timing together, rather than relying on several methods to update separate flags consistently. `StartGun()` should establish its complete initial state; currently it relies on `Endtutorial()` having already cleared the tutorial flag.

**Resolve the nearest valid shot hit once**

`ShootScript.Shoot()` performs separate raycasts for regular targets and EndTut. During a tutorial it always prioritizes the EndTut result. Consequently, if a regular target is in front of EndTut along the ray, the farther EndTut can still end the tutorial. Objects excluded from both masks cannot block either ray.

Use one raycast covering the intended hittable objects and any objects that should obstruct shots, then handle the nearest collider according to its role. Decide explicitly which scenery blocks a shot. If separate raycasts are retained, compare their hit distances and account for blockers.

The target lookup also assumes `hit.transform.GetComponent<Target>()` succeeds. A collider on the target layer but without that component on the same GameObject causes a null reference. Resolve the Target on the collider or an appropriate parent, check the result, and handle a non-target collider deliberately. The current target root has both the Target component and a collider; this recommendation protects against changes to prefab hierarchies and layers.

**Make a hit a single accepted action**

`Target` declares `_hasBeenHit` but never uses it. Its `OnHit()` has no guard against repeated calls. A `TryHit()` method that marks the target hit and returns whether this was the first accepted hit would let ShootScript award credit exactly once. Disable the collider immediately on acceptance, then perform effects and cleanup. This matters because destruction is deferred, and other callers can otherwise report the same target before it disappears.

`Target.OnHit()` also plays the hit sound on the target's own AudioSource and immediately destroys that object. The sound can be cut off with its source. A separate sound-emitter object or shared effects player should own the sound for its duration. If a hit VFX prefab is assigned later, give that effect its own cleanup policy too. The currently assigned target has a hit sound but no hit VFX prefab.

**Use a true percentage for shot failures**

`Random.Range(0, 101)` provides 101 possible integers, including 100. The current success test is `roll >= failureChance`. At a configured failureChance of 100, a roll of 100 still succeeds, so 100 does not mean a guaranteed failure. At 10, the failure probability is 10/101 rather than exactly 10%.

A clearer contract is a roll from 0 to 99 and a failure when the roll is less than the configured percentage. Then 0 means never fail and 100 means always fail. This would intentionally adjust existing probabilities and belongs in the later anger-mechanics review, not this localization change.

`ShootScript.FailureChance` already clamps values, but GunPuzzle writes directly to the public `failureChance` field, bypassing the property. Keep the backing field private and serialized, and route runtime changes through the property or a configuration method. This is encapsulation: one place owns the rule that the value must stay between 0 and 100.

**Configure spawned targets instead of modifying the prefab template**

The scene's `GunPuzzle.targetMover` reference points to the TargetMover component on the prefab asset. Calling `SetSpeeds()` changes the template that subsequent instances copy. Already spawned targets have their own components, so this is not a broadcast update to all targets.

Let GunPuzzle choose the level settings, let Spawn2 create the target instance, and let that instance's TargetMover receive its movement configuration explicitly. A small serializable list of level settings could contain duration, target count, spawn interval, minimum/maximum speed and speed-change interval. This also removes the hardcoded `time_left = 21` for later levels, which currently overrides the Inspector's puzzleTimer choice after level one.

Each script would then own one clear responsibility: GunPuzzle owns progression, Spawn2 owns target creation and cleanup, TargetMover owns motion, Target owns hit acceptance, and ShootScript owns input and shot resolution. Explicit setup is easier to reason about than changing shared prefab state.

**Validate spawning inputs and manage target lifetime records**

`Spawn2.GetRandomTransformYValue()` assumes at least one row remains after excluding recent choices. Zero available rows causes an index error. The saved scene has six rows and excludes three, which is valid; the problem arises with settings such as one row excluding one, or zero rows. Validate row count and exclusion count, and provide an explicit fallback if every row is excluded.

`Start()` appends generated positions to a serialized list. If a designer populates that list in the Inspector, generated entries are added too. Choose either generated positions or an intentionally authored list. For generated positions, keep the list private runtime data and rebuild it from row count and spacing.

Destroyed targets remain in `_spawnedTargets` until `ClearTargets()`. Remove expired entries or unregister targets when they are destroyed. This is currently a modest bookkeeping issue because levels clear the list. If profiling on the headset shows allocation spikes, consider pooling targets and shot trails; at the current spawning frequency, pooling should follow measurement rather than being the first change.

**Smaller maintainability improvements**

- `Shoot()` sets the reload flag but does not enforce cooldown itself; only Update checks it. A future UnityEvent or another script calling Shoot directly could bypass the cooldown. Put permission checks in the public action as well as input polling.
- Shot trails are created only in the tutorial branch. Confirm whether hiding them during real gameplay is intentional before changing this. If both modes should show them, share one trail-rendering method.
- `Begin()` records the active grandchildren it hides, but `Reset()` enables every object in `ObjectsToDeactivateWhilePlaying`, even if an object was already inactive beforehand. Record and restore prior activation state consistently.
- Use descriptive names and PascalCase public methods, such as `TargetSpawner`, `ShotOrigin`, `EndTutorial`, and `SetSpawnInterval`. Preserve serialized field names with migration attributes and update UnityEvent bindings if renaming methods. Avoid a broad rename until behavior is covered by tests.
- Remove duplicated imports, obsolete commented implementations and unused fields from active scripts after preserving their old versions. `Target.scoreValue` is currently unused; the game counts accepted hits instead. Make that scoring contract explicit.

I would address completion and state handling first, shot resolution second, and target configuration/spawning validation third. Probability changes can wait for the planned anger-condition work.

**Validation and preserved files**

The updated Assembly-CSharp sources compiled against the installed Unity 2022.3.45f1 libraries with zero errors. An isolated harness passed 87 assertions using the actual GunPuzzle, GamesManager and localization classes with engine/audio/spawner substitutes. It covered all three languages, the inactive EndTut label, tutorial completion, score and level changes, countdowns, victory, all three failed attempts, and fixed session language. It also reproduced the pre-existing victory spawning restart for the review.

Scene verification confirmed that only the two GunPuzzle reference fields changed. Existing catalogue entries were preserved and the new ID is present once. Dialogue objects, audio scripts, shooting/spawning/movement/target code, and the latest card fix were not modified. Actual Unity rendering, VR input, physics, and translated label fit still require the Play Mode/headset checks above.

Exact pre-change copies of the five edited files and their metadata are in `Backups/GunTextLocalization/2026-10-26`, with a hash manifest. These copies stay outside Assets to avoid duplicate C# classes and metadata GUIDs. Restoring that saved scene later would also replace any scene edits made since the backup, so restore individual changes deliberately.
