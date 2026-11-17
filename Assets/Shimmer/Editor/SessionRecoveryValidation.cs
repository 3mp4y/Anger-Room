using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using AngerRoom.Shimmer;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class SessionRecoveryValidation
{
    static readonly BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    static object Get(object o, string n) => o.GetType().GetField(n, Flags).GetValue(o);
    static void Set(object o, string n, object v) => o.GetType().GetField(n, Flags).SetValue(o, v);
    static void Call(object o, string n) => o.GetType().GetMethod(n, Flags).Invoke(o, null);
    static int checks;
    static void Check(bool value, string message) { if (!value) throw new Exception(message); checks++; }

    public static void Run()
    {
        if (!Application.isBatchMode) throw new InvalidOperationException("Use a separate Unity batch process; validation changes only the in-memory scene.");
        var scene = EditorSceneManager.OpenScene("Assets/Plugins/Scenes/AngerRoom.unity");
        var all = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Transform>(true)).ToArray();
        var gate = all.Select(t => t.GetComponent<AcquisitionGate>()).Single(x => x != null);
        var gm = gate.games;
        Check(gate.requireEyeTracking, "Production scene must require actual eye tracking");
        Check(gate.startupLights.Length == 2 && gate.startupLights.All(l => !l.enabled), "Both startup lights must be off in the saved scene");
        Check(gate.eyes.initialDelaySeconds == 4 && gate.eyes.readyAfterSeconds == 1, "Existing eye delays changed");
        Check(gate.shimmer.recordingDurationSeconds == 0 && gate.shimmer.connectOnStart, "Production receiver must stream until stopped");
        Check(gate.statusText != null && gate.statusCanvas != null, "Missing recovery indicator");
        typeof(AcquisitionGate).GetMethod("UpdateStatus", Flags).Invoke(gate, new object[] { true, true });
        Check(gate.statusText.text.Contains("Shimmer: ready"), "Stable Shimmer stream must show ready while eyes are pending");
        Check(!gate.statusText.text.Contains("confirming samples"), "Eye readiness must not be reported as unconfirmed Shimmer samples");
        Check((gate.statusCanvas.isRootCanvas || gate.statusCanvas.overrideSorting) && gate.statusCanvas.sortingOrder == 32760, "Recovery panel must use priority sorting");
        Call(gate, "Start");
        var dialogCanvases = (Canvas[])Get(gate, "interactiveCanvases");
        Check(dialogCanvases.Length > 0 && !dialogCanvases.Contains(gate.statusCanvas), "Dialog suppression must exclude recovery panel");
        // Include an already-disabled canvas and repeat suppression to verify exact restoration.
        dialogCanvases[0].enabled = false;
        var canvasFlags = dialogCanvases.Select(c => c.enabled).ToArray();
        var objectFlags = dialogCanvases.Select(c => c.gameObject.activeSelf).ToArray();
        Call(gate, "HideCoveredCanvases"); Call(gate, "HideCoveredCanvases");
        Check(dialogCanvases.All(c => !c.enabled) && gate.statusCanvas.enabled, "Dialog covered the recovery panel");
        Check(objectFlags.SequenceEqual(dialogCanvases.Select(c => c.gameObject.activeSelf)), "Suppression changed dialog activation state");
        Call(gate, "RestoreCoveredCanvases");
        Check(canvasFlags.SequenceEqual(dialogCanvases.Select(c => c.enabled)), "Resume failed to restore prior canvas visibility");
        Call(gm, "Awake");
        Call(gm, "Start");
        var logger = gm.GetComponent<Logger>();
        Directory.CreateDirectory("Logs/SessionChecks");
        Set(logger, "_logPath", "Logs/SessionChecks/events-test.txt");
        float beforeTime = gm.RemainingExperimentSeconds;
        var beforePoints = ((int[])Get(gm, "gamePoints")).ToArray();
        var beforeTries = ((int[])Get(gm, "gameTries")).ToArray();
        gm.SetAcquisitionBlocked(true);
        Check(!gm.BeginAttempt(0, false), "Blocked acquisition allowed attempt");
        gm.StartOverAllTimer();
        Check(!(bool)Get(gm, "isgoing"), "Timer started before acquisition readiness");
        gm.GameWon(0);
        gm.addTries(0, null);
        Check(((int[])Get(gm, "wins"))[0] == 0, "Blocked callback awarded win");
        Check(beforeTries.SequenceEqual((int[])Get(gm, "gameTries")), "Blocked callback cost an attempt");
        // A live timer coroutine must preserve time while acquisition is blocked.
        Set(gm, "isgoing", true);
        var timer = (IEnumerator)typeof(GamesManager).GetMethod("OverallTimer", Flags).Invoke(gm, null);
        for (int i = 0; i < 100; i++) timer.MoveNext();
        Check(gm.RemainingExperimentSeconds == beforeTime, "Overall timer advanced during outage");
        Set(gm, "isgoing", false);

        var card = all.Select(t => t.GetComponent<Card_Minigame>()).Single(x => x != null);
        var gun = all.Select(t => t.GetComponent<GunPuzzle>()).Single(x => x != null);
        var lab = all.Select(t => t.GetComponent<LabyrinthScript>()).Single(x => x != null);
        // Seed an interrupted card level transition and verify a clean first-level retry.
        Set(card, "state", Enum.Parse(Get(card, "state").GetType(), "Playing"));
        Set(card, "currentLevel", 3); Set(card, "timeReduction", 3.4f);
        card.CancelForAcquisitionLoss();
        Check(Get(card, "state").ToString() == "Idle" && (int)Get(card, "currentLevel") == 1 && (float)Get(card, "timeReduction") == 0, "Card cancellation did not reset progression");
        // Transition between shooting levels has started=false; it still must cancel.
        Set(gun, "readoutsActive", true); Set(gun, "level", 3); Set(gun, "hits", 5);
        gun.CancelForAcquisitionLoss();
        Check(!(bool)Get(gun, "readoutsActive") && (int)Get(gun, "level") == 1 && !gun.realgun.activeSelf, "Gun transition cancellation failed");
        Set(lab, "inLabyrinth", true); Set(lab, "started", true); Set(lab, "initialPuzzleTimer", 93f);
        lab.CancelForAcquisitionLoss();
        Check(!(bool)Get(lab, "started") && !(bool)Get(lab, "inLabyrinth") && !lab.blacked.activeSelf, "Labyrinth cancellation failed");
        Check(Mathf.Abs(lab.player.transform.position.x - lab.newPos.position.x) < 0.001f, "Labyrinth did not return player to room");
        Check(beforePoints.SequenceEqual((int[])Get(gm, "gamePoints")) && beforeTries.SequenceEqual((int[])Get(gm, "gameTries")), "Canceled attempts changed points/tries");
        Check(!gm.enableDebugOperations, "Production scene must disable debug operations");
        var debugButtons = (UnityEngine.UI.Button[])Get(gm, "debugTutorialButtons");
        Check(debugButtons.Length == 3 && debugButtons.All(b => b != null && b.onClick.GetPersistentEventCount() == 1), "Three debug buttons must be wired");
        var activationTargets = (GameObject[])Get(gm, "openingObjectsToActivate");
        foreach (string name in new[] { "GameComponents", "Minigame Rewards", "ScreenObjects", "ScoreBoards", "OverallTimer", "MainLight" })
            Check(activationTargets.Any(g => g.name == name), "Debug dialog skip misses " + name);
        gm.enableDebugOperations = true;
        Set(gm, "openingDialogsComplete", true);
        gm.SetAcquisitionBlocked(false);
        var tutorialButtons = (GameObject[])Get(gm, "tutorialButtons");
        var realButtons = (GameObject[])Get(gm, "realButtons");
        for (int i = 0; i < 3; i++)
        {
            Check(gm.CanDebugSkipTutorial(i), "Debug tutorial skip unavailable for game " + i);
            gm.DebugSkipTutorial(i);
            Check(!tutorialButtons[i].activeSelf && realButtons[i].activeSelf && !gm.CanDebugSkipTutorial(i), "Tutorial skip did not unlock exactly one game " + i);
        }
        Check(beforePoints.SequenceEqual((int[])Get(gm, "gamePoints")) && beforeTries.SequenceEqual((int[])Get(gm, "gameTries")), "Debug tutorial skip changed points or attempts");
        gm.SetAcquisitionBlocked(true);
        // Completed games survive cancellation; repeat cancellation is harmless.
        ((int[])Get(gm, "wins"))[1] = 1;
        ((bool[])Get(gm, "collectedRewards"))[1] = true;
        gm.CancelAttemptForAcquisitionLoss(); gm.CancelAttemptForAcquisitionLoss();
        Check(gm.IsGameResolved(1) && gm.IsRewardCollected(1), "Completed progress was lost");
        Check(gm.RemainingExperimentSeconds == beforeTime, "Cancellation reset overall time");
        gate.ResumeAfterReconnect();
        Check(gate.IsBlocked, "Resume was accepted without stable samples");
        var chord = new ResumeChord();
        Check(!chord.Poll(false, true, true), "Chord accepted before sensors ready");
        Check(!chord.Poll(true, true, true), "Held chord resumed immediately on reconnection");
        chord.Poll(true, false, false);
        Check(!chord.Poll(true, true, false), "A alone resumed");
        Check(chord.Poll(true, true, true), "A+B chord did not resume");
        Check(!chord.Poll(true, true, true), "Held chord repeated");
        chord.Poll(true, false, false);
        Check(!chord.Poll(true, false, true), "B alone resumed");
        Check(chord.Poll(true, true, true), "B then A chord did not resume");
        var hold = new DebugGripHold();
        Check(!hold.Poll(false, true, 0) && !hold.Poll(false, true, 10), "Disabled debug grip triggered");
        Check(!hold.Poll(true, true, 10) && !hold.Poll(true, true, 14.99), "Grip triggered before 5 seconds");
        Check(hold.Poll(true, true, 15) && !hold.Poll(true, true, 25), "Grip must fire once per hold");
        hold.Poll(true, false, 26);
        Check(!hold.Poll(true, true, 27), "Released grip failed to reset hold duration");
        hold.Poll(false, true, 30);
        Check(!hold.Poll(true, true, 32), "Paused acquisition retained old grip duration");
        Debug.Log("SESSION_RECOVERY_TESTS_OK checks=" + checks);
    }
}
