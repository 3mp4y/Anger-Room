# Dialog distance interaction and reward buttons

The repair changes one setting in `Assets/Plugins/Scenes/AngerRoom.unity`:
the root GameObject named **EventSystem** is now inactive. It is preserved
in the scene. The separate root named **PCM** remains active.

## Why this setting matters

The scene contained two active EventSystem components:

| Object | Input module |
| --- | --- |
| EventSystem | InputSystemUIInputModule |
| PCM | Meta PointableCanvasModule |

Unity's EventSystem processes input only when it is the current EventSystem.
With two active systems, the current one can be the general UI system instead
of the system processing Meta's pointable canvases. Having an enabled Meta
module on a different object does not guarantee that it will process clicks.

The SDK's Exclusive Mode only disables other input modules on its own
GameObject. It does not remove or disable a second EventSystem elsewhere.

Keeping one active EventSystem, on PCM with PointableCanvasModule, removes
that ambiguity. This is a scene configuration repair; no runtime button,
dialogue progression, language or minigame scoring scripts were changed.

## The two kinds of interaction

For Dialog UI, the existing connection is:

1. A hand/controller **RayInteractor** points at a Dialog's **RayInteractable**.
2. The RayInteractable forwards interaction through its **PointableCanvas**.
3. **PointableCanvasModule** processes the input for Unity's UI system.
4. The UI Button executes its existing OnClick actions.

For reward boxes, the existing connection is:

1. A nearby **PokeInteractor** physically presses the button's **PokeInteractable**.
2. **PointableUnityEventWrapper** forwards Select to `MinigameRewardBox.OpenBox()`.
3. The reward script checks whether opening is permitted.

Reward buttons have no RayInteractable. Enabling distance interaction for
the Dialogs therefore does not make those reward buttons remotely clickable.
The name PointableUnityEventWrapper does not itself mean ray interaction:
it forwards events from whichever pointable object it references.

## What to inspect in Unity

- In the scene Hierarchy, **EventSystem** should be inactive.
- **PCM** should be active, with its EventSystem and PointableCanvasModule enabled.
- The player rig retains its left/right hand and controller ray interactors.
- Dialog objects retain their RayInteractable and PointableCanvas references.
  Dialogs that are not currently being shown may legitimately be inactive.
- The reward buttons retain PokeInteractable without RayInteractable.

Do not disable the player rig's rays to make an individual button poke-only.
Choose the supported interaction types on that button instead.

## Verification and remaining hardware check

The batch-only `DialogInteractionDiagnostics` editor utility loads the scene
without saving, checks the active EventSystem and the Dialog/reward connections,
and writes its report under Logs. It is excluded from game builds.

In the headset, open a Dialog and confirm remote hover and click using the
tracking mode you normally use. Then unlock a reward box: pointing and selecting
at a distance should not open it, while a physical poke should. Repeat a Dialog
click after closing a previous Dialog to check progression.

Scene and metadata before the repair are preserved in
`Backups/DialogRayInteraction/2026-10-29`. Your other saved scene changes were retained.
