using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using UnityEditor.Events;
using Oculus.Interaction;
using Oculus.Interaction.Surfaces;
using UnityEngine.Events;
using System.Collections.Generic;

public static class DebugDialogSetup
{
    static string Path(Transform t) => t.parent == null ? t.name : Path(t.parent) + "/" + t.name;
    public static void ConfigureAndBuild()
    {
        Configure();
        AngerRoom.Shimmer.Editor.ShimmerTestBuild.BuildRecoveryQuest2Incremental();
    }

    public static void Configure()
    {
        var scene = EditorSceneManager.OpenScene("Assets/Plugins/Scenes/AngerRoom.unity");
        var all = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Transform>(true)).ToArray();
        var gm = all.Select(t => t.GetComponent<GamesManager>()).Single(c => c != null);
        var gate = all.Select(t => t.GetComponent<AngerRoom.Shimmer.AcquisitionGate>()).Single(c => c != null);
        gate.statusCanvas.overrideSorting = true;
        gate.statusCanvas.sortingOrder = 32760;
        EditorUtility.SetDirty(gate.statusCanvas);
        var roots = all.Where(t => new[] { "OldDialogue", "SpanishDialogues", "EnglishDialog", "ItaDialog" }.Contains(t.name)).ToArray();
        var hide = roots.SelectMany(t => t.GetComponentsInChildren<PointableCanvas>(true).Select(c => c.gameObject)).Concat(roots.Select(t => t.gameObject)).Distinct().ToArray();
        var activate = new HashSet<GameObject>();
        // Reuse the scene's actual end-state targets. Intermediate dialog activations
        // and narration are intentionally excluded; all dialog groups are hidden.
        foreach (var button in roots.SelectMany(r => r.GetComponentsInChildren<Button>(true)))
        {
            var calls = new SerializedObject(button).FindProperty("m_OnClick.m_PersistentCalls.m_Calls");
            for (int i = 0; i < calls.arraySize; i++)
            {
                var call = calls.GetArrayElementAtIndex(i);
                if (call.FindPropertyRelative("m_MethodName").stringValue != "SetActive" ||
                    !call.FindPropertyRelative("m_Arguments.m_BoolArgument").boolValue) continue;
                var target = call.FindPropertyRelative("m_Target").objectReferenceValue as GameObject;
                if (target != null && !roots.Any(r => target.transform.IsChildOf(r))) activate.Add(target);
            }
        }
        activate.Add(all.Single(t => t.name == "MainLight").gameObject);
        var serialized = new SerializedObject(gm);
        Assign(serialized, "openingDialogObjects", hide);
        Assign(serialized, "openingObjectsToActivate", activate.ToArray());
        serialized.FindProperty("openingStartObject").objectReferenceValue = all.Single(t => Path(t) == "Start").gameObject;
        serialized.FindProperty("openingLightFollow").objectReferenceValue = all.Single(t => t.name == "MainLight").GetComponent<LightFollows>();
        serialized.FindProperty("enableDebugOperations").boolValue = false;

        var panel = all.FirstOrDefault(t => t.name == "Debug tutorial controls")?.gameObject;
        if (panel == null)
        {
            panel = new GameObject("Debug tutorial controls", typeof(RectTransform), typeof(Canvas), typeof(GraphicRaycaster));
            panel.transform.SetParent(roots.First().parent, false);
            panel.transform.localPosition = new Vector3(0.4f, -0.35f, 1.1f);
            panel.transform.localScale = Vector3.one * 0.0006f;
            var rect = panel.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(1000, 360);
            var canvas = panel.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.worldCamera = all.Select(t => t.GetComponent<Camera>()).FirstOrDefault(c => c != null && c.CompareTag("MainCamera"));
            var pointable = panel.AddComponent<PointableCanvas>();
            pointable.InjectAllPointableCanvas(canvas);
            var plane = panel.AddComponent<PlaneSurface>();
            plane.InjectAllPlaneSurface(PlaneSurface.NormalFacing.Backward, true);
            var bounds = panel.AddComponent<BoundsClipper>();
            bounds.Size = new Vector3(1000, 360, 1);
            var clipped = panel.AddComponent<ClippedPlaneSurface>();
            clipped.InjectAllClippedPlaneSurface(plane, new IBoundsClipper[] { bounds });
            var ray = panel.AddComponent<RayInteractable>();
            ray.InjectAllRayInteractable(clipped);
            ray.InjectOptionalPointableElement(pointable);
            string[] labels = { "Skip cards tutorial", "Skip shooting tutorial", "Skip labyrinth tutorial" };
            UnityAction[] actions = { gm.DebugSkipCardsTutorial, gm.DebugSkipShootingTutorial, gm.DebugSkipLabyrinthTutorial };
            for (int i = 0; i < 3; i++)
            {
                var go = new GameObject(labels[i], typeof(RectTransform), typeof(Image), typeof(Button));
                go.transform.SetParent(panel.transform, false);
                var buttonRect = go.GetComponent<RectTransform>();
                buttonRect.anchorMin = new Vector2(0.02f, 1f - (i + 1) / 3f + 0.025f);
                buttonRect.anchorMax = new Vector2(0.98f, 1f - i / 3f - 0.025f);
                buttonRect.offsetMin = buttonRect.offsetMax = Vector2.zero;
                go.GetComponent<Image>().color = new Color(0.18f, 0.2f, 0.23f, 1);
                var button = go.GetComponent<Button>();
                button.targetGraphic = go.GetComponent<Image>();
                var colors = button.colors;
                colors.highlightedColor = new Color(0.65f, 0.85f, 1); colors.disabledColor = new Color(0.4f, 0.4f, 0.4f, 0.65f);
                button.colors = colors;
                UnityEventTools.AddPersistentListener(button.onClick, actions[i]);
                var label = new GameObject("Label", typeof(RectTransform), typeof(Text));
                label.transform.SetParent(go.transform, false);
                var labelRect = label.GetComponent<RectTransform>();
                labelRect.anchorMin = Vector2.zero; labelRect.anchorMax = Vector2.one;
                labelRect.offsetMin = labelRect.offsetMax = Vector2.zero;
                var text = label.GetComponent<Text>();
                text.text = labels[i]; text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                text.fontSize = 36; text.alignment = TextAnchor.MiddleCenter; text.raycastTarget = false;
            }
        }
        panel.SetActive(false);
        serialized.FindProperty("debugTutorialControls").objectReferenceValue = panel;
        Assign(serialized, "debugTutorialButtons", panel.GetComponentsInChildren<Button>(true)
            .Where(b => b.name.StartsWith("Skip ")).ToArray());
        serialized.ApplyModifiedPropertiesWithoutUndo();
        DebugMinigameSetup.ConfigurePanel(gm);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("DEBUG_SCENE_CONFIGURED activationTargets=" + activate.Count + "; dialogs=" + hide.Length);
    }

    static void Assign(SerializedObject target, string name, Object[] objects)
    {
        var array = target.FindProperty(name);
        array.arraySize = objects.Length;
        for (int i = 0; i < objects.Length; i++) array.GetArrayElementAtIndex(i).objectReferenceValue = objects[i];
    }
    public static void Inspect()
    {
        var scene = EditorSceneManager.OpenScene("Assets/Plugins/Scenes/AngerRoom.unity");
        var example = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Oculus.Interaction.PointableCanvas>(true))
            .First(c => Path(c.transform).Contains("EnglishDialog"));
        foreach (var t in example.GetComponentsInChildren<Transform>(true))
            Debug.Log("DIALOG_GEOMETRY " + Path(t) + " pos=" + t.localPosition + " scale=" + t.localScale +
                (t is RectTransform rect ? " rect=" + rect.rect : "") + " components=" + string.Join(",", t.GetComponents<Component>().Select(c => c.GetType().Name)));
        foreach (var button in scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Button>(true)))
        {
            string path = Path(button.transform);
            if (!path.ToLowerInvariant().Contains("dialog")) continue;
            var calls = new SerializedObject(button).FindProperty("m_OnClick.m_PersistentCalls.m_Calls");
            for (int i = 0; i < calls.arraySize; i++)
            {
                var c = calls.GetArrayElementAtIndex(i);
                Object target = c.FindPropertyRelative("m_Target").objectReferenceValue;
                var targetTransform = target is GameObject go ? go.transform : (target as Component)?.transform;
                Debug.Log("DIALOG_CALL " + path + " | " + c.FindPropertyRelative("m_MethodName").stringValue + " | " +
                    (targetTransform == null ? "null" : Path(targetTransform)) + " | bool=" + c.FindPropertyRelative("m_Arguments.m_BoolArgument").boolValue);
            }
        }
    }
}
