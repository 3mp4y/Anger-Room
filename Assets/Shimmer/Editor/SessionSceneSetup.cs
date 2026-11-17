using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using AngerRoom.Shimmer;

public static class SessionSceneSetup
{
    [MenuItem("Anger Room/Recording/Configure session and recovery")]
    public static void Configure()
    {
        var scene = EditorSceneManager.OpenScene("Assets/Plugins/Scenes/AngerRoom.unity");
        var all = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Transform>(true)).ToArray();
        var root = all.FirstOrDefault(t => t.name == "Recording Session");
        var go = root != null ? root.gameObject : new GameObject("Recording Session");
        if (go.GetComponent<RecordingSession>() == null) go.AddComponent<RecordingSession>();
        var receiver = go.GetComponent<ShimmerReceiver>() ?? go.AddComponent<ShimmerReceiver>();
        receiver.connectOnStart = true;
        receiver.recordingDurationSeconds = 0;
        receiver.showDiagnostics = false;
        var gate = go.GetComponent<AcquisitionGate>() ?? go.AddComponent<AcquisitionGate>();
        gate.shimmer = receiver;
        gate.eyes = all.Select(t => t.GetComponent<EyeStateLogger>()).First(x => x != null);
        gate.games = all.Select(t => t.GetComponent<GamesManager>()).First(x => x != null);
        gate.requireEyeTracking = true;
        gate.startupLights = all.First(t => Path(t) == "Start/StartBulb").GetComponentsInChildren<Light>(true);
        foreach (var light in gate.startupLights) { light.enabled = false; EditorUtility.SetDirty(light); }
        if (gate.statusCanvas == null)
        {
            var panel = new GameObject("Recording status and recovery", typeof(RectTransform), typeof(Canvas), typeof(Image));
            panel.transform.SetParent(go.transform, false);
            panel.transform.localPosition = new Vector3(0, 1.5f, 1.1f);
            panel.transform.localScale = Vector3.one * 0.001f;
            panel.GetComponent<RectTransform>().sizeDelta = new Vector2(1000, 270);
            var canvas = panel.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            var image = panel.GetComponent<Image>();
            image.color = new Color(0.025f, 0.035f, 0.05f, 0.96f);
            image.raycastTarget = false;
            var label = new GameObject("Connection status", typeof(RectTransform), typeof(Text));
            label.transform.SetParent(panel.transform, false);
            var rect = label.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(24, 18); rect.offsetMax = new Vector2(-24, -18);
            var text = label.GetComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = 28;
            text.alignment = TextAnchor.MiddleCenter;
            text.raycastTarget = false;
            text.text = "PREPARING RECORDING\nWaiting for Shimmer and eye tracking";
            gate.statusCanvas = canvas;
            gate.statusText = text;
        }
        gate.statusCanvas.overrideSorting = true;
        gate.statusCanvas.sortingOrder = 32760;
        EditorUtility.SetDirty(gate.statusCanvas);
        EditorUtility.SetDirty(gate);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("SESSION_SCENE_CONFIGURED lights=" + gate.startupLights.Length);
        SessionRecoveryValidation.Run();
    }
    public static void Inspect()
    {
        var scene = EditorSceneManager.OpenScene("Assets/Plugins/Scenes/AngerRoom.unity");
        foreach (var light in scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Light>(true)))
            if (light.enabled && light.gameObject.activeInHierarchy)
                Debug.Log("SESSION_START_LIGHT " + Path(light.transform) + " id=" + GlobalObjectId.GetGlobalObjectIdSlow(light));
    }
    static string Path(Transform t) => t.parent == null ? t.name : Path(t.parent) + "/" + t.name;
}
