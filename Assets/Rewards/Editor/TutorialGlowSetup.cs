using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using AngerRoom.Rewards;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class TutorialGlowSetup
{
    const string ScenePath = "Assets/Plugins/Scenes/AngerRoom.unity";
    const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    static object Get(object o, string n) => o.GetType().GetField(n, Flags).GetValue(o);
    static void Set(object o, string n, object v) => o.GetType().GetField(n, Flags).SetValue(o, v);
    static void Call(object o, string n, params object[] args) => o.GetType().GetMethod(n, Flags).Invoke(o, args);
    static T Find<T>() where T : Component => UnityEngine.Object.FindObjectsOfType<T>(true).Single();
    static int checks;
    static void Check(bool ok, string message) { if (!ok) throw new Exception(message); checks++; }
    static readonly Dictionary<Material, Material> materials = new Dictionary<Material, Material>();

    public static void ConfigureAndValidate()
    {
        if (!Application.isBatchMode) throw new Exception("Batch only");
        var scene = EditorSceneManager.OpenScene(ScenePath);
        if (!AssetDatabase.IsValidFolder("Assets/Rewards/TutorialGlowMaterials"))
            AssetDatabase.CreateFolder("Assets/Rewards", "TutorialGlowMaterials");
        materials.Clear();
        var cards = Find<Card_Minigame>(); var gun = Find<GunPuzzle>(); var lab = Find<LabyrinthScript>();
        Configure(cards, "cards", new[] { (TMP_Text)Get(cards, "timerText"), (TMP_Text)Get(cards, "levelText"), (TMP_Text)Get(cards, "roundText") });
        var gunLabels = new List<TMP_Text> { (TMP_Text)Get(gun, "TMPTimer"), (TMP_Text)Get(gun, "TMPLevel"), (TMP_Text)Get(gun, "TMPScore"), (TMP_Text)Get(gun, "endTutorialText") };
        foreach (string name in new[] { "screenReadout", "centerEyeReadout" })
        foreach (string field in new[] { "timerLabel", "levelLabel", "hitsLabel" }) gunLabels.Add((TMP_Text)Get(Get(gun, name), field));
        Configure(gun, "shooting", gunLabels.Distinct().ToArray());
        Configure(lab, "labyrinth", new[] { (TMP_Text)Get(lab, "TMPTimer"), (TMP_Text)Get(lab, "tutorialSignText") });
        AssetDatabase.SaveAssets(); EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
        Validate();
        // Explicit exit also discards the validation's temporary in-memory state.
        EditorApplication.Exit(0);
    }
    static void Configure(Component owner, string field, TMP_Text[] labels)
    {
        var glow = owner.GetComponent<MinigameTutorialGlow>() ?? owner.gameObject.AddComponent<MinigameTutorialGlow>();
        var so = new SerializedObject(glow);
        so.FindProperty(field).objectReferenceValue = owner;
        var textArray = so.FindProperty("labels"); var materialArray = so.FindProperty("glowMaterials");
        textArray.arraySize = materialArray.arraySize = labels.Length;
        for (int i = 0; i < labels.Length; i++)
        {
            var original = labels[i].fontSharedMaterial;
            if (!materials.TryGetValue(original, out var material))
            {
                AssetDatabase.TryGetGUIDAndLocalFileIdentifier(original, out string guid, out long id);
                string path = "Assets/Rewards/TutorialGlowMaterials/TutorialGold_" + guid + "_" + id + ".mat";
                material = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (material == null)
                {
                    material = new Material(original);
                    material.shader = Shader.Find("TextMeshPro/Distance Field");
                    material.renderQueue = original.renderQueue;
                    material.EnableKeyword("GLOW_ON");
                    material.SetColor("_GlowColor", new Color(1, .72f, .12f, .65f));
                    material.SetFloat("_GlowOuter", .15f); material.SetFloat("_GlowInner", .05f);
                    material.SetFloat("_GlowPower", .75f);
                    AssetDatabase.CreateAsset(material, path);
                }
                materials.Add(original, material);
            }
            textArray.GetArrayElementAtIndex(i).objectReferenceValue = labels[i];
            materialArray.GetArrayElementAtIndex(i).objectReferenceValue = material;
        }
        so.ApplyModifiedPropertiesWithoutUndo();
    }
    static void Tutorial(Component owner, bool active)
    {
        if (owner is Card_Minigame) Set(owner, "state", Enum.Parse(owner.GetType().GetField("state", Flags).FieldType, active ? "Tutorial" : "Playing"));
        else if (owner is GunPuzzle) Set(owner, "tutorial", active);
        else { Set(owner, "inLabyrinth", active); Set(owner, "started", false); }
    }
    public static void Validate()
    {
        checks = 0;
        EditorSceneManager.OpenScene(ScenePath);
        var groups = UnityEngine.Object.FindObjectsOfType<MinigameTutorialGlow>(true);
        Check(groups.Length == 3 && groups.Sum(g => ((TMP_Text[])Get(g, "labels")).Length) == 15, "Wrong tutorial glow scope");
        foreach (var glow in groups)
        {
            var labels = (TMP_Text[])Get(glow, "labels");
            var originals = labels.Select(t => t.fontSharedMaterial).ToArray();
            var colors = labels.Select(t => t.color).ToArray();
            var strings = labels.Select(t => t.text).ToArray();
            var materialJson = originals.Select(EditorJsonUtility.ToJson).ToArray();
            Component owner = glow.GetComponent<Card_Minigame>() as Component ?? glow.GetComponent<GunPuzzle>() as Component ?? glow.GetComponent<LabyrinthScript>();
            Tutorial(owner, false); Call(glow, "LateUpdate");
            Check(labels.Select(t => t.fontSharedMaterial).SequenceEqual(originals), "Glow changed ordinary gameplay material");
            Tutorial(owner, true); Call(glow, "LateUpdate"); Call(glow, "ApplyPulse", 1f);
            for (int i = 0; i < labels.Length; i++)
            {
                Check(labels[i].fontSharedMaterial != originals[i] && labels[i].fontSharedMaterial.IsKeywordEnabled("GLOW_ON"), "No isolated glow material");
                Check(labels[i].color == (Color)Get(glow, "gold") && labels[i].color.a == colors[i].a, "Pulse failed or made text transparent");
                Check(labels[i].text == strings[i] && EditorJsonUtility.ToJson(originals[i]) == materialJson[i], "Glow mutated text or shared font material");
            }
            Call(glow, "ApplyPulse", 0f);
            Check(labels[0].color != (Color)Get(glow, "gold"), "Pulse does not vary");
            Tutorial(owner, false); Call(glow, "LateUpdate");
            Check(labels.Select(t => t.fontSharedMaterial).SequenceEqual(originals) && labels.Select(t => t.color).SequenceEqual(colors), "Tutorial end did not restore original appearance");
            Tutorial(owner, true); Call(glow, "LateUpdate"); Call(glow, "OnDisable");
            Check(labels.Select(t => t.fontSharedMaterial).SequenceEqual(originals), "Disable did not restore appearance");
        }
        File.WriteAllText("Logs/tutorial-glow-validation.txt", "PASS " + checks + " checks. Headset halo appearance still needs visual testing.");
        Debug.Log("TUTORIAL_GLOW_PASS " + checks);
    }
}
