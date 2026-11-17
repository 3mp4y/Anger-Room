using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using Oculus.Interaction.Surfaces;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

// Editor-only scene wiring and regression checks; no runtime setup component.
public static class DebugMinigameSetup
{
    const string ScenePath = "Assets/Plugins/Scenes/AngerRoom.unity";
    const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    static int checks;
    static object Get(object o, string name) => o.GetType().GetField(name, Flags).GetValue(o);
    static void Set(object o, string name, object value) => o.GetType().GetField(name, Flags).SetValue(o, value);
    static object Call(object o, string name, params object[] args) => o.GetType().GetMethod(name, Flags).Invoke(o, args);
    static void Check(bool condition, string message) { if (!condition) throw new Exception(message); checks++; }
    static T Find<T>() where T : Component => UnityEngine.Object.FindObjectsOfType<T>(true).Single();
    static void EnableEditorClick(Button button)
    {
        // Saved UI listeners are correctly RuntimeOnly. Allow their invocation
        // only in this unsaved test scene, since batch checks are not Play Mode.
        button.onClick.SetPersistentListenerState(0, UnityEngine.Events.UnityEventCallState.EditorAndRuntime);
    }

    public static void ConfigureAndValidate()
    {
        if (!Application.isBatchMode) throw new InvalidOperationException("Use a separate Unity batch process.");
        var scene = EditorSceneManager.OpenScene(ScenePath);
        ConfigurePanel(Find<GamesManager>());
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Validate();
    }

    // Also called by the older dialog setup so rebuilding its controls cannot
    // reattach this panel to the camera or wire ten buttons as tutorial skips.
    public static void ConfigurePanel(GamesManager gm)
    {
        var so = new SerializedObject(gm);
        var panel = (GameObject)so.FindProperty("debugTutorialControls").objectReferenceValue;
        var start = (GameObject)so.FindProperty("openingStartObject").objectReferenceValue;
        if (panel == null || start == null) throw new InvalidOperationException("Configure the opening debug controls first.");
        var rect = (RectTransform)panel.transform;
        var camera = panel.GetComponent<Canvas>().worldCamera;
        // A root object survives Start.SetActive(false). Use Start's position,
        // but keep the panel upright: Start is a rotated physical button assembly.
        rect.SetParent(null, false);
        rect.position = start.transform.position;
        Vector3 facing = camera == null ? Vector3.forward : Vector3.ProjectOnPlane(rect.position - camera.transform.position, Vector3.up);
        rect.rotation = Quaternion.LookRotation(facing.sqrMagnitude > .001f ? facing : Vector3.forward, Vector3.up);
        rect.localScale = Vector3.one * .0008f;
        rect.sizeDelta = new Vector2(1200, 600);
        panel.GetComponent<BoundsClipper>().Size = new Vector3(1200, 600, 1);
        panel.layer = 5;
        string[] games = { "Cards", "Shooting", "Labyrinth" };
        var tutorials = so.FindProperty("debugTutorialButtons");
        var wins = so.FindProperty("debugWinButtons");
        var losses = so.FindProperty("debugLossButtons");
        tutorials.arraySize = wins.arraySize = losses.arraySize = 3;
        for (int i = 0; i < 3; i++)
        {
            var tutorial = (Button)tutorials.GetArrayElementAtIndex(i).objectReferenceValue;
            Layout(tutorial, i, 0, false);
            tutorial.GetComponentInChildren<Text>().text = "Skip " + games[i] + " tutorial";
            var win = Button(panel.transform, "Win " + games[i], new Color(.15f, .4f, .22f));
            Layout(win, i, 1, false);
            win.onClick = new Button.ButtonClickedEvent();
            UnityEventTools.AddIntPersistentListener(win.onClick, gm.DebugWinMinigame, i);
            wins.GetArrayElementAtIndex(i).objectReferenceValue = win;
            var loss = Button(panel.transform, "Lose " + games[i], new Color(.45f, .16f, .16f));
            Layout(loss, i, 2, false);
            loss.onClick = new Button.ButtonClickedEvent();
            UnityEventTools.AddIntPersistentListener(loss.onClick, gm.DebugLoseMinigame, i);
            losses.GetArrayElementAtIndex(i).objectReferenceValue = loss;
        }
        var timer = Button(panel.transform, "Overall timer - 1 second", new Color(.28f, .28f, .4f));
        Layout(timer, 0, 3, true);
        timer.onClick = new Button.ButtonClickedEvent();
        UnityEventTools.AddPersistentListener(timer.onClick, gm.DebugSetOverallTimerToOneSecond);
        so.FindProperty("debugTimerButton").objectReferenceValue = timer;
        so.ApplyModifiedPropertiesWithoutUndo();
        panel.SetActive(false);
        EditorUtility.SetDirty(gm);
    }

    static Button Button(Transform parent, string name, Color color)
    {
        var existing = parent.Find(name);
        var go = existing != null ? existing.gameObject : new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);
        go.layer = 5;
        go.GetComponent<Image>().color = color;
        var button = go.GetComponent<Button>();
        button.targetGraphic = go.GetComponent<Image>();
        var colors = button.colors;
        colors.highlightedColor = new Color(.65f, .85f, 1f);
        colors.disabledColor = new Color(.4f, .4f, .4f, .65f);
        button.colors = colors;
        var label = go.transform.Find("Label")?.gameObject;
        if (label == null)
        {
            label = new GameObject("Label", typeof(RectTransform), typeof(Text));
            label.transform.SetParent(go.transform, false);
        }
        var labelRect = (RectTransform)label.transform;
        labelRect.anchorMin = Vector2.zero; labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = new Vector2(8, 4); labelRect.offsetMax = new Vector2(-8, -4);
        var text = label.GetComponent<Text>();
        text.text = name; text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.fontSize = 34; text.alignment = TextAnchor.MiddleCenter; text.raycastTarget = false;
        return button;
    }

    static void Layout(Button button, int column, int row, bool wide)
    {
        var rect = (RectTransform)button.transform;
        rect.anchorMin = new Vector2(wide ? .01f : column / 3f + .01f, 1f - (row + 1) / 4f + .015f);
        rect.anchorMax = new Vector2(wide ? .99f : (column + 1) / 3f - .01f, 1f - row / 4f - .015f);
        rect.offsetMin = rect.offsetMax = Vector2.zero;
    }

    static GamesManager Load(bool left = false)
    {
        EditorSceneManager.OpenScene(ScenePath);
        var gm = Find<GamesManager>();
        Set(gm, "leftHanded", left);
        Call(gm, "Awake"); Call(gm, "Start");
        Directory.CreateDirectory("Logs/DebugMinigameChecks");
        Set(gm.GetComponent<Logger>(), "_logPath", "Logs/DebugMinigameChecks/events.txt");
        Call(gm.GetComponent<ScoreboardManager>(), "Start");
        Call(gm.am, "Awake"); Call(gm.am, "Start");
        Call(Find<GunPuzzle>(), "Start"); Call(Find<Card_Minigame>(), "Start"); Call(Find<LabyrinthScript>(), "Start");
        foreach (var t in gm.GetComponentsInParent<Transform>(true)) t.gameObject.SetActive(true);
        foreach (var minigame in new Component[] { Find<GunPuzzle>(), Find<Card_Minigame>(), Find<LabyrinthScript>() })
            foreach (var t in minigame.GetComponentsInParent<Transform>(true)) t.gameObject.SetActive(true);
        gm.SetAcquisitionBlocked(false);
        return gm;
    }

    public static void Validate()
    {
        if (!Application.isBatchMode) throw new InvalidOperationException("Batch-only checks do not save scene changes.");
        checks = 0;
        var gm = Load();
        var panel = (GameObject)Get(gm, "debugTutorialControls");
        var start = (GameObject)Get(gm, "openingStartObject");
        Check(panel.transform.parent == null && Vector3.Distance(panel.transform.position, start.transform.position) < .001f,
            "Debug controls must stay at Start, independently of the camera and Start's active state");
        var buttons = panel.GetComponentsInChildren<Button>(true);
        Check(buttons.Length == 10 && buttons.All(b => b.onClick.GetPersistentEventCount() == 1), "Expected ten wired buttons");
        Check(!panel.activeSelf, "Panel should start hidden");
        // Preserve the user's saved testing preference; explicitly test the off
        // state in memory rather than requiring the scene checkbox to be off.
        gm.enableDebugOperations = false;
        gm.DebugWinMinigame(0); gm.DebugLoseMinigame(1); gm.DebugSetOverallTimerToOneSecond();
        Check(!gm.IsGameResolved(0) && !gm.IsGameResolved(1) && gm.RemainingExperimentSeconds != 1, "Disabled debug calls changed session");
        gm.enableDebugOperations = true;
        Call(gm, "Update"); Check(!panel.activeSelf, "Panel appeared during opening dialogs");
        Set(gm, "openingDialogsComplete", true);
        Call(gm, "Update"); Check(!panel.activeSelf, "Panel overlaps active Start object");
        start.SetActive(false);
        Call(gm, "Update"); Check(panel.activeSelf, "Panel did not appear after opening");
        gm.SetAcquisitionBlocked(true);
        gm.DebugWinMinigame(0); Call(gm, "Update");
        Check(!panel.activeSelf && !gm.IsGameResolved(0), "Sensor outage did not block debug actions");

        // Test each actual UnityEvent, cleanup during play/preparation, reward
        // values, repeated clicks and the collect-before-next-minigame gate.
        for (int game = 0; game < 3; game++)
        for (int win = 0; win < 2; win++)
        {
            gm = Load(win == 1);
            gm.enableDebugOperations = true;
            Set(gm, "openingDialogsComplete", true); Set(gm, "isgoing", true);
            ((GameObject)Get(gm, "openingStartObject")).SetActive(false);
            gm.addTries(game, null); // A prior real failure leaves 70 points.
            if (game == 0) Find<Card_Minigame>().StartMinigame();
            if (game == 1) Find<GunPuzzle>().StartGun(); // Includes preparation cleanup.
            if (game == 2)
            {
                Check(gm.BeginAttempt(2, false), "Labyrinth attempt rejected");
                Set(Find<LabyrinthScript>(), "inLabyrinth", true);
                Set(Find<LabyrinthScript>(), "started", true);
            }
            gm.DebugWinMinigame((game + 1) % 3);
            Check(!gm.IsGameResolved((game + 1) % 3), "Debug resolved another active minigame");
            Call(gm, "Update");
            var action = ((Button[])Get(gm, win == 1 ? "debugWinButtons" : "debugLossButtons"))[game];
            EnableEditorClick(action);
            action.onClick.Invoke();
            int value = win == 1 ? 70 : 10;
            Check(gm.IsGameResolved(game) && gm.GetRewardPoints(game) == value && gm.CanCollectReward(game), "Wrong forced result/reward");
            Check(gm.getRemainingTries(game) == (win == 1 ? 2 : 0), "Wrong forced attempt count");
            Check((int)Get(gm, "activeAttempt") == -1 && !gm.BeginAttempt((game + 1) % 3, false), "Reward gate not preserved");
            if (game == 0) Check(Get(Find<Card_Minigame>(), "state").ToString() == "Idle", "Card game still running");
            if (game == 1) Check(!(bool)Get(Find<GunPuzzle>(), "readoutsActive") && !(bool)Get(Find<GunPuzzle>().Spawner, "started") && !Find<GunPuzzle>().realgun.activeSelf, "Gun cleanup failed");
            if (game == 2) Check(!(bool)Get(Find<LabyrinthScript>(), "started") && !Find<LabyrinthScript>().blacked.activeSelf, "Labyrinth cleanup failed");
            action.onClick.Invoke();
            Check(gm.GetRewardPoints(game) == value, "Repeated result changed points");
            Check(gm.TryCollectReward(game) && !gm.TryCollectReward(game) && gm.CanDebugResolveMinigame((game + 1) % 3), "Collection did not release next debug control");
        }

        foreach (bool left in new[] { false, true })
        {
            gm = Load(left); gm.enableDebugOperations = true;
            Set(gm, "openingDialogsComplete", true); Set(gm, "isgoing", true);
            ((GameObject)Get(gm, "openingStartObject")).SetActive(false);
            Find<GunPuzzle>().StartGun(); Call(gm, "Update");
            var button = ((Button[])Get(gm, "debugWinButtons"))[1];
            EnableEditorClick(button);
            Vector3 center = button.transform.TransformPoint(((RectTransform)button.transform).rect.center);
            var ray = new Ray(center - button.transform.forward, button.transform.forward);
            Check(!gm.TryPressDebugButtonWithGun(ray, .1f), "Gun debug click ignored distance");
            Check(gm.TryPressDebugButtonWithGun(ray, 10f) && gm.CanCollectReward(1), "Gun cannot press its debug result button");
        }

        gm = Load(); gm.enableDebugOperations = true;
        Set(gm, "openingDialogsComplete", true); Set(gm, "isgoing", true);
        gm.DebugSetOverallTimerToOneSecond();
        Check(gm.RemainingExperimentSeconds == 1 && !gm.SessionEnding, "Timer button must shorten, not directly end session");
        Set(gm, "time_left_exp", 0f);
        ((IEnumerator)Call(gm, "OverallTimer")).MoveNext();
        Check(gm.SessionEnding && Enumerable.Range(0, 3).All(i => gm.IsRewardCollected(i) && gm.GetRewardPoints(i) == 10), "Timer did not use real timeout flow");
        gm.DebugSetOverallTimerToOneSecond();
        Check(gm.RemainingExperimentSeconds == 0, "Timer restarted during ending");

        gm = Load(); gm.enableDebugOperations = true;
        Set(gm, "openingDialogsComplete", true); Set(gm, "isgoing", true);
        for (int game = 0; game < 3; game++)
        {
            gm.DebugLoseMinigame(game);
            if (game < 2) Check(gm.TryCollectReward(game), "Earlier failure reward unavailable");
        }
        Check(!(bool)Get(gm, "isgoing") && !gm.SessionEnding, "Final loss did not stop timer and wait for collection");
        float remaining = gm.RemainingExperimentSeconds;
        gm.DebugSetOverallTimerToOneSecond();
        Check(gm.RemainingExperimentSeconds == remaining, "Debug timer restarted after all results");
        Check(gm.TryCollectReward(2) && gm.SessionEnding, "Final collected loss did not begin ending");
        File.WriteAllText("Logs/DebugMinigameChecks/result.txt", "PASS " + checks + " checks. VR pointer aiming/layout still needs headset testing.");
        Debug.Log("DEBUG_MINIGAME_PASS " + checks);
    }

    public static void ValidateRegression()
    {
        HandednessSetup.Validate();
        MinigameFlowValidation.Run();
    }
}
