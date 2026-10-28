// Editor-only verification utility. Does not save or modify the scene on disk.
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using AngerRoom.Rewards;
using AngerRoom.Localization;
using Oculus.Interaction;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;

public static class PhysicalRewardValidation
{
    static int checks;
    static readonly BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    static object Get(object o, string name) => o.GetType().GetField(name, Flags).GetValue(o);
    static void Set(object o, string name, object value) => o.GetType().GetField(name, Flags).SetValue(o, value);
    static void Call(object o, string name) => o.GetType().GetMethod(name, Flags).Invoke(o, null);
    static void Check(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
        checks++;
    }
    static string State(PuzzleRewardBox box) => Get(box, "state").ToString();
    static void SetState(PuzzleRewardBox box, string value) => Set(box, "state", Enum.Parse(Get(box, "state").GetType(), value));
    static int PlayerPoints(ScoreboardManager scoreboard) => (int)Get(Get(scoreboard, "player"), "points");

    public static void Run()
    {
        if (!Application.isBatchMode) throw new InvalidOperationException("Run this validation in a separate Unity batch process; it opens a scene and exits without saving.");
        Directory.CreateDirectory("Temp/PhysicalRewardChecks");
        try
        {
            var scene = EditorSceneManager.OpenScene("Assets/Plugins/Scenes/AngerRoom.unity");
            var all = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Transform>(true)).ToArray();
            var boxes = all.Select(t => t.GetComponent<PuzzleRewardBox>()).Where(b => b != null).OrderBy(b => (int)Get(b, "gameIndex")).ToArray();
            Check(boxes.Length == 3, "Three physical rewards required");
            foreach (var box in boxes)
            {
                foreach (var field in new[] { "gamesManager", "textAdapter", "reachInput", "lid", "pointsText", "buttonText", "puzzleLabel", "openButton" })
                    Check((UnityEngine.Object)Get(box, field) != null, box.name + " missing " + field);
                var poke = (PokeInteractable)Get(box, "openButton");
                var wrapper = poke.GetComponent<PointableUnityEventWrapper>();
                Check(wrapper != null, "Button wrapper missing");
                Check(wrapper.WhenSelect.GetPersistentEventCount() == 2, "Button callback count");
                Check(wrapper.WhenSelect.GetPersistentTarget(1) == box && wrapper.WhenSelect.GetPersistentMethodName(1) == "OpenBox", "Button is not wired to its own box");
                Check(wrapper.WhenSelect.GetPersistentListenerState(1) == UnityEventCallState.RuntimeOnly, "Button callback disabled");
                foreach (var component in box.GetComponentsInChildren<MonoBehaviour>(true)) Check(component != null, "Missing script in " + box.name);
                Check(box.GetComponentsInChildren<Grabbable>(true).All(g => !g.enabled), "Reward lid must not be manually grabbable");
                var baseBounds = box.transform.Find("Pedestal base").GetComponent<Renderer>().bounds;
                var topBounds = box.transform.Find("Pedestal top").GetComponent<Renderer>().bounds;
                Check(baseBounds.size.y > 1f && baseBounds.size.z < .3f, "Pedestal base is not upright");
                Check(topBounds.size.y < .1f && topBounds.size.z > .2f, "Pedestal top is not horizontal");
                foreach (var text in box.GetComponentsInChildren<TMP_Text>(true)) text.ForceMeshUpdate(true);
            }
            var manager = (GamesManager)Get(boxes[0], "gamesManager");
            var scoreboard = manager.GetComponent<ScoreboardManager>();
            var logger = manager.GetComponent<Logger>();
            Set(logger, "_logPath", Path.GetFullPath("Temp/PhysicalRewardChecks/game-events.log"));
            Call(manager, "Awake");
            Call(manager, "Start");
            Call(scoreboard, "Start");
            var scores = (TextMeshPro[])Get(manager, "scores");
            foreach (var box in boxes)
            {
                int game = (int)Get(box, "gameIndex");
                Check(scores[game] == (TMP_Text)Get(box, "pointsText"), "Score points to old display");
                Call(box, "Start");
                Check(State(box) == "Locked", "New reward must be locked");
                box.OpenBox();
                Check(State(box) == "Locked" && !manager.TryCollectReward(game), "Unfinished game unlocked or collected");
                Check(!((PokeInteractable)Get(box, "openButton")).enabled, "Locked button enabled");
            }
            var catalog = AssetDatabase.LoadAssetAtPath<TextCatalog>("Assets/Localization/GameTexts.asset");
            foreach (LanguageAdapter.Language language in Enum.GetValues(typeof(LanguageAdapter.Language)))
            {
                var lookup = catalog.CreateLookup(language);
                foreach (TextId id in Enum.GetValues(typeof(TextId))) if ((int)id >= 60)
                    Check(lookup.ContainsKey(id) && !string.IsNullOrWhiteSpace(lookup[id]), "Missing reward translation: " + language + " " + id);
            }
            Render(boxes[1], "closed");
            // Exercise actual score methods across every original outcome for each puzzle.
            var message = new GameObject("Validation message").AddComponent<TextMeshPro>();
            for (int game = 0; game < 3; game++) for (int failures = 0; failures <= 3; failures++)
            {
                Set(manager, "gameTries", new[] { 0, 0, 0 });
                Set(manager, "gamePoints", new[] { 100, 100, 100 });
                Set(manager, "wins", new[] { 0, 0, 0 });
                Array.Clear((bool[])Get(manager, "collectedRewards"), 0, 3);
                int before = PlayerPoints(scoreboard);
                for (int i = 0; i < failures; i++) manager.addTries(game, message, (TextAdapter)Get(boxes[game], "textAdapter"));
                Check(manager.GetRewardPoints(game) == 100 - failures * 30, "Wrong deduction");
                if (failures < 3) manager.GameWon(game);
                Check(PlayerPoints(scoreboard) == before, "Points awarded before collection");
                Check(manager.CanCollectReward(game), "Resolved game has no reward");
                Check(manager.TryCollectReward(game), "Reward collection failed");
                Check(PlayerPoints(scoreboard) == before + 100 - failures * 30, "Wrong collected points");
                Check(!manager.TryCollectReward(game), "Duplicate collection accepted");
                manager.addTries(game, message);
                manager.GameWon(game);
                Check(manager.GetRewardPoints(game) == 100 - failures * 30 && PlayerPoints(scoreboard) == before + 100 - failures * 30, "Repeated completion changed awarded reward");
                Check(!manager.CanCollectReward((game + 1) % 3), "Different puzzle unlocked");
            }
            Check(!manager.TryCollectReward(-1) && !manager.TryCollectReward(3), "Invalid game accepted");
            // Drive animation time explicitly in Edit Mode; physical reaching is a headset check.
            Set(manager, "gameTries", new[] { 0, 0, 0 });
            Set(manager, "gamePoints", new[] { 100, 100, 100 });
            Set(manager, "wins", new[] { 0, 0, 0 });
            Array.Clear((bool[])Get(manager, "collectedRewards"), 0, 3);
            var animated = boxes[1];
            var points = (TMP_Text)Get(animated, "pointsText");
            points.gameObject.SetActive(true);
            points.color = Color.white;
            SetState(animated, "Locked");
            Call(animated, "RefreshReward");
            manager.GameWon(1);
            Check(State(animated) == "Ready", "Win event did not unlock box");
            int animationBefore = PlayerPoints(scoreboard);
            animated.OpenBox();
            Check(State(animated) == "Opening", "Button did not open lid");
            Set(animated, "elapsed", .3f);
            animated.OpenBox();
            Check((float)Get(animated, "elapsed") == .3f, "Repeated button restarted animation");
            Set(animated, "elapsed", .65f);
            Call(animated, "Update");
            Check(State(animated) == "Rising", "Opening did not transition to rise");
            Set(animated, "elapsed", .8f);
            Call(animated, "Update");
            Check(State(animated) == "Available", "Rise did not finish");
            Check(Mathf.Abs(points.transform.localPosition.y - ((Vector3)Get(animated, "initialTextPosition")).y - .32f) <= .019f, "Incorrect rise height");
            Render(animated, "open");
            SetState(animated, "Collecting");
            Set(animated, "collectionStartPosition", points.transform.localPosition);
            Set(animated, "elapsed", .25f);
            Call(animated, "Update");
            Check(PlayerPoints(scoreboard) == animationBefore, "Awarded before disappearance");
            animated.gameObject.SetActive(false);
            animated.gameObject.SetActive(true);
            Check(State(animated) == "Collecting", "Hide/show reset collection");
            Set(animated, "elapsed", .55f);
            Call(animated, "Update");
            Check(State(animated) == "Collected" && !points.gameObject.activeSelf, "Reward did not disappear");
            Check(PlayerPoints(scoreboard) == animationBefore + 100, "Animation did not award once");
            Call(animated, "Update");
            Check(PlayerPoints(scoreboard) == animationBefore + 100, "Animation awarded twice");
            File.WriteAllText("Temp/PhysicalRewardChecks/result.txt", "PASS: " + checks + " checks. Headset reach/poke still requires hardware testing.");
            Debug.Log("PHYSICAL_REWARDS_PASS " + checks);
            EditorApplication.Exit(0);
        }
        catch (Exception e)
        {
            Debug.LogException(e);
            File.WriteAllText("Temp/PhysicalRewardChecks/result.txt", "FAIL after " + checks + " checks: " + e);
            EditorApplication.Exit(1);
        }
    }

    static void Render(PuzzleRewardBox box, string name)
    {
        var camera = new GameObject("Reward validation camera").AddComponent<Camera>();
        camera.transform.position = box.transform.position + new Vector3(.6f, 1.7f, -1.7f);
        camera.transform.LookAt(box.transform.position + Vector3.up * 1.05f);
        camera.fieldOfView = 45;
        camera.nearClipPlane = .03f;
        camera.farClipPlane = 100;
        var target = new RenderTexture(1000, 1000, 24);
        camera.targetTexture = target;
        camera.Render();
        RenderTexture.active = target;
        var image = new Texture2D(1000, 1000, TextureFormat.RGB24, false);
        image.ReadPixels(new Rect(0, 0, 1000, 1000), 0, 0);
        image.Apply();
        File.WriteAllBytes("Temp/PhysicalRewardChecks/" + name + ".png", image.EncodeToPNG());
        RenderTexture.active = null;
        UnityEngine.Object.DestroyImmediate(camera.gameObject);
        UnityEngine.Object.DestroyImmediate(target);
        UnityEngine.Object.DestroyImmediate(image);
    }
}
