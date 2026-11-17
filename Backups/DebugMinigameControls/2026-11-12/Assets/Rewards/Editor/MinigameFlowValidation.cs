// Batch-only regression checks. Changes the in-memory scene, never saves it.
using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using AngerRoom.Localization;
using AngerRoom.Rewards;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class MinigameFlowValidation
{
    const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    static int checks;
    static object Get(object o, string n) => o.GetType().GetField(n, Flags).GetValue(o);
    static void Set(object o, string n, object v) => o.GetType().GetField(n, Flags).SetValue(o, v);
    static object Call(object o, string n, params object[] args) => o.GetType().GetMethod(n, Flags).Invoke(o, args);
    static void Check(bool ok, string message) { if (!ok) throw new Exception(message); checks++; }

    public static void Run()
    {
        if (!Application.isBatchMode) throw new InvalidOperationException("Run in a separate Unity batch process.");
        try
        {
            var scene = EditorSceneManager.OpenScene("Assets/Plugins/Scenes/AngerRoom.unity");
            var all = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Transform>(true)).ToArray();
            var gm = all.Select(t => t.GetComponent<GamesManager>()).Single(c => c != null);
            var gun = all.Select(t => t.GetComponent<GunPuzzle>()).Single(c => c != null);
            var boxes = all.Select(t => t.GetComponent<MinigameRewardBox>()).Where(c => c != null)
                .OrderBy(b => (int)Get(b, "gameIndex")).ToArray();
            // Scene starts with stations hidden by opening dialogs.
            foreach (var t in gm.GetComponentsInParent<Transform>(true)) t.gameObject.SetActive(true);
            foreach (var box in boxes)
                foreach (var t in box.GetComponentsInParent<Transform>(true)) t.gameObject.SetActive(true);
            Call(gm, "Awake"); Call(gm, "Start");
            Directory.CreateDirectory("Logs/MinigameFlowChecks");
            Set(gm.GetComponent<Logger>(), "_logPath", "Logs/MinigameFlowChecks/events.txt");
            Call(gm.GetComponent<ScoreboardManager>(), "Start");
            Call(gm.am, "Awake"); Call(gm.am, "Start"); Call(gun, "Start");
            gm.SetAcquisitionBlocked(false);
            var cue = AudioClip.Create("test reward cue", 44100, 1, 44100, false);
            foreach (var box in boxes)
            {
                Set(box, "rewardAvailableClip", cue);
                Call(box, "Start");
                var source = (AudioSource)Get(box, "floatingPointsSource");
                Check(source != null && source.transform == ((TMP_Text)Get(box, "pointsText")).transform,
                    "Reward cue must come from the points");
                Check(source.loop && !source.playOnAwake && source.spatialBlend == 1 && source.volume <= .1f,
                    "Reward cue must be quiet, positional and looped");
            }
            var buttons = (GameObject[])Get(gm, "realButtons");
            for (int i = 0; i < 3; i++) gm.DoneTutorial(i);
            gm.stoppedPlaying();
            Set(gm, "isgoing", true);
            Check(gm.BeginAttempt(0, false), "First attempt rejected");
            gm.GameWon(0); gm.stoppedPlaying();
            Check(gm.HasPendingReward, "Winning must create a pending reward");
            Check(Get(boxes[0], "state").ToString() == "Ready", "Win must unlock the CLOSED box");
            Check(buttons.All(b => !b.activeSelf) && !gm.BeginAttempt(1, false), "Pending reward did not block next game");
            Check((bool)Get(gm, "isgoing"), "Timer stopped before all minigames were resolved");
            gm.enableDebugOperations = true;
            Set(gm, "openingDialogsComplete", true);
            Check(!gm.CanDebugSkipTutorial(1), "Debug entry bypassed reward collection");
            Check(gm.TryCollectReward(0) && !gm.TryCollectReward(0), "Reward award must be exactly once");
            Check(!gm.HasPendingReward && buttons[1].activeSelf && !buttons[0].activeSelf, "Collecting did not restore eligible controls");
            Check(gm.BeginAttempt(1, false), "Collection did not release attempt gate");
            gm.stoppedPlaying();
            var label = (TextMeshPro)Get(gun, "TMPTimer");
            for (int i = 0; i < 3; i++) gm.addTries(1, label);
            Check(gm.HasPendingReward && Get(boxes[1], "state").ToString() == "Ready", "Exhausting attempts must unlock the box");
            Check(!gm.BeginAttempt(2, true) && buttons.All(b => !b.activeSelf), "Failure reward did not block tutorials and games");
            gm.SetAcquisitionBlocked(true);
            Check(!gm.TryCollectReward(1), "Sensor outage allowed collection");
            gm.SetAcquisitionBlocked(false);
            Check(gm.TryCollectReward(1) && gm.BeginAttempt(2, false), "Recovery lost reward/next-attempt permission");
            gm.GameWon(2); gm.stoppedPlaying();
            Check(!(bool)Get(gm, "isgoing") && !gm.SessionEnding, "Final result must stop timer and wait for last reward");
            gm.StartOverAllTimer();
            Check(!(bool)Get(gm, "isgoing"), "Timer restarted after final result");
            Check(gm.TryCollectReward(2) && gm.SessionEnding, "Last reward did not begin completion ending");
            Check(!gm.BeginAttempt(0, false), "Ending accepted an attempt");

            // Reset only the in-memory ledger to test every combination of results.
            for (int mask = 0; mask < 8; mask++)
            {
                Reset(gm);
                for (int i = 0; i < 3; i++)
                {
                    ((int[])Get(gm, "wins"))[i] = (mask >> i) & 1;
                    ((int[])Get(gm, "gameTries"))[i] = ((mask >> i) & 1) == 0 ? 3 : 0;
                }
                gm.CheckLost();
                Check(!(bool)Get(gm, "isgoing") && !gm.SessionEnding, "Resolved combination ended before collection: " + mask);
                for (int i = 0; i < 2; i++) Check(gm.TryCollectReward(i) && !gm.SessionEnding, "Early ending: " + mask);
                Check(gm.TryCollectReward(2) && gm.SessionEnding, "Missing final ending: " + mask);
            }

            Reset(gm);
            Set(gm, "time_left_exp", 0f);
            var timer = (IEnumerator)Call(gm, "OverallTimer"); timer.MoveNext();
            Check(gm.SessionEnding && !((bool)Get(gm, "isgoing")), "Timeout must end without waiting for collection");
            Check(Enumerable.Range(0, 3).All(i => gm.IsRewardCollected(i) && gm.GetRewardPoints(i) == 10),
                "Timeout must settle unfinished rewards at ten points");

            Reset(gm);
            gun.StartGun();
            Check(!(bool)Get(gun, "started") && !(bool)Get(gun.Spawner, "started"), "Targets or round clock started during preparation");
            int sequence = (int)Get(gm, "attemptSequence");
            gun.StartGun();
            Check((int)Get(gm, "attemptSequence") == sequence, "Repeated start created another attempt");
            var preparation = (IEnumerator)Call(gun, "PrepareFirstRound");
            Check(preparation.MoveNext() && label.text.Contains("03"), "Missing localized preparation countdown");
            float roundTime = (float)Get(gun, "time_left");
            gm.SetAcquisitionBlocked(true); Call(gun, "Update");
            Check((float)Get(gun, "time_left") == roundTime, "Preparation consumed round time during outage");
            gun.CancelForAcquisitionLoss();
            Check(!(bool)Get(gun, "readoutsActive") && !(bool)Get(gun.Spawner, "started"), "Canceled preparation left targets/display active");
            gm.SetAcquisitionBlocked(false);
            Set(gun, "preparationSeconds", 0f);
            preparation = (IEnumerator)Call(gun, "PrepareFirstRound");
            Check(!preparation.MoveNext() && (bool)Get(gun, "started") && (bool)Get(gun.Spawner, "started"), "Round failed to start after preparation");
            gun.CancelForAcquisitionLoss();

            // A paused source reports isPlaying=false; this must NOT trigger quit.
            gm.SetAcquisitionBlocked(true);
            var ending = (IEnumerator)Call(gm.am, "PlayEndingAndQuit", cue);
            ending.MoveNext(); gm.am.host.Stop();
            Check(ending.MoveNext() && ending.Current == null, "Paused narration prematurely ended the app");
            gm.SetAcquisitionBlocked(false);
            Check(ending.MoveNext() && ending.Current is IEnumerator, "Finished narration did not enter recording shutdown");
            // Do not advance the shutdown enumerator: this check must not quit the editor.
            foreach (LanguageAdapter.Language lang in Enum.GetValues(typeof(LanguageAdapter.Language)))
            {
                var lookup = AssetDatabase.LoadAssetAtPath<TextCatalog>("Assets/Localization/GameTexts.asset").CreateLookup(lang);
                Check(string.Format(lookup[TextId.PreparationCountdown], 3).Contains("03"), "Missing preparation translation: " + lang);
            }
            Debug.Log("MINIGAME_FLOW_PASS checks=" + checks);
            File.WriteAllText("Logs/MinigameFlowChecks/result.txt", "PASS " + checks + " checks. Headset audio and shutdown still need hardware testing.");
            EditorApplication.Exit(0);
        }
        catch (Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
    }

    static void Reset(GamesManager gm)
    {
        gm.am.StopAllCoroutines(); gm.StopAllCoroutines();
        Set(gm, "<SessionEnding>k__BackingField", false);
        Set(gm, "experimentExpired", false); Set(gm, "minigamesFinished", false);
        Set(gm, "isgoing", true); Set(gm, "activeAttempt", -1);
        Set(gm, "gameTries", new int[3]); Set(gm, "wins", new int[3]);
        Set(gm, "gamePoints", new[] {100, 100, 100});
        Array.Clear((bool[])Get(gm, "collectedRewards"), 0, 3);
        Set(gm.am, "endingStarted", false);
        gm.SetAcquisitionBlocked(false);
    }
}
