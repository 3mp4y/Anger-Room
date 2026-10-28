// One-off, batch-only scene migration. Uses Unity's prefab APIs to preserve
// existing placement and overrides, including manually unpacked buttons.
using System;
using System.Linq;
using AngerRoom.Rewards;
using Oculus.Interaction;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class RewardButtonUpgrade
{
    public static void Run()
    {
        if (!Application.isBatchMode) throw new InvalidOperationException("Run this migration in a separate Unity batch process.");
        var scene = EditorSceneManager.OpenScene("Assets/Plugins/Scenes/AngerRoom.unity");
        var boxes = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<MinigameRewardBox>(true)).ToArray();
        if (boxes.Length != 3) throw new InvalidOperationException("Expected three reward boxes.");
        var locked = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/PNGs/locked-icons8-bloquear-50.png");
        var unlocked = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/PNGs/unlocked-icons8-desbloquear-50.png");
        if (locked == null || unlocked == null) throw new InvalidOperationException("Both icons must be imported as sprites.");
        foreach (var box in boxes)
        {
            var controller = new SerializedObject(box);
            var poke = (PokeInteractable)controller.FindProperty("openButton").objectReferenceValue;
            if (poke == null) throw new InvalidOperationException("Missing poke button on " + box.name);
            var visual = poke.GetComponentInChildren<InteractableColorVisual>(true);
            if (visual == null) throw new InvalidOperationException("Missing InteractableColorVisual on " + poke.name);
            // Remove text only inside this reward button, preserving puzzle UI.
            foreach (var text in poke.GetComponentsInChildren<TMP_Text>(true))
                UnityEngine.Object.DestroyImmediate(text.gameObject);
            foreach (var ray in poke.GetComponentsInChildren<RayInteractable>(true))
                UnityEngine.Object.DestroyImmediate(ray);

            var face = poke.GetComponentInChildren<PokeInteractableVisual>(true);
            if (face == null) throw new InvalidOperationException("Missing moving button face.");
            var iconTransform = face.transform.Find("Lock Icon");
            var icon = iconTransform != null ? iconTransform.GetComponent<SpriteRenderer>() : new GameObject("Lock Icon").AddComponent<SpriteRenderer>();
            icon.transform.SetParent(face.transform, false);
            icon.transform.localPosition = new Vector3(0f, 0f, -.02f);
            icon.transform.localRotation = Quaternion.identity;
            float size = .72f / Mathf.Max(locked.bounds.size.x, locked.bounds.size.y);
            icon.transform.localScale = Vector3.one * size;
            icon.sprite = locked;
            icon.color = Color.white; // Preserve the black artwork and transparent PNG background.
            icon.sortingOrder = 1;

            controller.FindProperty("buttonIcon").objectReferenceValue = icon;
            controller.FindProperty("lockedIcon").objectReferenceValue = locked;
            controller.FindProperty("unlockedIcon").objectReferenceValue = unlocked;
            controller.ApplyModifiedPropertiesWithoutUndo();

            var palette = new SerializedObject(visual);
            palette.FindProperty("_interactableView").objectReferenceValue = poke;
            palette.FindProperty("_colorShaderPropertyName").stringValue = "_Color";
            SetColor(palette, "_disabledColorState", new Color(.9f, .12f, .12f, .4f));
            SetColor(palette, "_normalColorState", new Color(.12f, .85f, .25f, .4f));
            SetColor(palette, "_hoverColorState", new Color(.18f, 1f, .35f, .55f));
            SetColor(palette, "_selectColorState", new Color(.3f, 1f, .45f, .7f));
            palette.ApplyModifiedPropertiesWithoutUndo();
            visual.enabled = true;
            // RoundedBoxProperties owns the shader's dimensions and initial edit-mode
            // colour; InteractableColorVisual owns its colour during gameplay.
            var rounded = visual.GetComponent<RoundedBoxProperties>();
            var initial = new SerializedObject(rounded);
            initial.FindProperty("_color").colorValue = new Color(.9f, .12f, .12f, .4f);
            initial.ApplyModifiedPropertiesWithoutUndo();
            // Start locked in the serialized scene too, avoiding a usable button
            // before MinigameRewardBox.Start reconciles the gameplay state.
            poke.enabled = false;
            EditorUtility.SetDirty(poke);
            if (PrefabUtility.IsPartOfPrefabInstance(poke)) PrefabUtility.RecordPrefabInstancePropertyModifications(poke);
        }
        EditorSceneManager.MarkSceneDirty(scene);
        if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("Could not save reward button changes.");
        PhysicalRewardValidation.Run();
    }

    static void SetColor(SerializedObject palette, string state, Color color)
    {
        palette.FindProperty(state).FindPropertyRelative("Color").colorValue = color;
        palette.FindProperty(state).FindPropertyRelative("ColorTime").floatValue = .1f;
    }
}
