using System;
using System.IO;
using System.Linq;
using System.Reflection;
using AngerRoom.Localization;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class VisualTutorialLocalizationSetup
{
    const string ScenePath = "Assets/Plugins/Scenes/AngerRoom.unity";
    public static readonly string[] RootNames = { "Visual_Tutorial_Shooting", "Visual_Tutorial_Labirinth", "Visual_Tutorial_Cards", "Pre-Game Dialog" };
    static int checks;
    static void Check(bool ok, string why) { if (!ok) throw new Exception(why); checks++; }
    public static string PathOf(Transform t) => t.parent == null ? t.name : PathOf(t.parent) + "/" + t.name;
    static Transform[] Roots() => UnityEngine.Object.FindObjectsOfType<Transform>(true).Where(t => RootNames.Contains(t.name)).OrderBy(t => t.name).ToArray();
    public static void AuditRevision()
    {
        if (!Application.isBatchMode) throw new Exception("Batch only");
        EditorSceneManager.OpenScene(ScenePath);
        using (var report = new StreamWriter("Logs/visual-tutorial-revision-audit.txt"))
        {
            foreach (var root in Roots())
            foreach (var text in root.GetComponentsInChildren<TMP_Text>(true))
                report.WriteLine(PathOf(text.transform) + " | active=" + text.gameObject.activeSelf + " | " + text.text.Replace("\n", "\\n"));
            var adapter = UnityEngine.Object.FindObjectsOfType<TextAdapter>(true).Single();
            report.WriteLine(EditorJsonUtility.ToJson(adapter));
        }
    }
    static TextId IdFor(Transform root, TMP_Text text)
    {
        bool cards = root.name == "Visual_Tutorial_Cards";
        switch (text.name)
        {
            case "TitleTextLabel": return root.name == "Pre-Game Dialog" ? TextId.OpeningPrompt : TextId.RulesLabel;
            case "Up": return cards ? TextId.CardsTutorialFlip : TextId.ShootingTutorialGoal;
            case "Buoon": return cards ? TextId.CardsTutorialGoal : TextId.ShootingTutorialTrail;
            case "BodyText (2)": return TextId.TargetShooterTitle;
            case "BodyText (3)": return TextId.LabyrinthTutorialGoal;
            case "BodyText (5)": return TextId.LabyrinthTutorialObstacles;
            case "Label": return TextId.NextLabel;
            case "Subtitle": return TextId.SubtitlePlaceholder;
            default: throw new Exception("Unmapped tutorial text: " + PathOf(text.transform));
        }
    }
    public static void ConfigureAndValidate()
    {
        if (!Application.isBatchMode) throw new Exception("Batch only");
        try
        {
            var scene = EditorSceneManager.OpenScene(ScenePath);
            var roots = Roots(); Check(roots.Length == RootNames.Length, "Missing tutorial or Pre-Game Dialog root");
            var texts = roots.SelectMany(r => r.GetComponentsInChildren<TMP_Text>(true)).ToArray();
            Check(roots.All(r => r.GetComponentsInChildren<TMP_Text>(true).Length > 0), "Panel has no TMP text");
            var catalog = AssetDatabase.LoadAssetAtPath<TextCatalog>("Assets/Localization/GameTexts.asset");
            var defaults = ScriptableObject.CreateInstance<TextCatalog>();
            var cat = new SerializedObject(catalog); var entries = cat.FindProperty("entries");
            var known = catalog.CreateLookup(LanguageAdapter.Language.Spanish);
            // Append missing entries only. Existing user-edited translations stay intact.
            foreach (TextId id in Enum.GetValues(typeof(TextId)))
            {
                if ((int)id < 70 || (int)id > 80 || known.ContainsKey(id)) continue;
                var e = entries.GetArrayElementAtIndex(entries.arraySize++);
                e.FindPropertyRelative("id").intValue = (int)id;
                e.FindPropertyRelative("english").stringValue = defaults.CreateLookup(LanguageAdapter.Language.English)[id];
                e.FindPropertyRelative("italian").stringValue = defaults.CreateLookup(LanguageAdapter.Language.Italian)[id];
                e.FindPropertyRelative("spanish").stringValue = defaults.CreateLookup(LanguageAdapter.Language.Spanish)[id];
            }
            if (cat.ApplyModifiedPropertiesWithoutUndo()) EditorUtility.SetDirty(catalog);
            UnityEngine.Object.DestroyImmediate(defaults);
            var spanish = catalog.CreateLookup(LanguageAdapter.Language.Spanish);
            var adapter = UnityEngine.Object.FindObjectsOfType<TextAdapter>(true).Single();
            var so = new SerializedObject(adapter); var bindings = so.FindProperty("staticSceneTexts");
            var openingPrompt = roots.Single(r => r.name == "Pre-Game Dialog").GetComponentsInChildren<TMP_Text>(true).Single(t => t.name == "TitleTextLabel");
            // Reuse the existing opening-prompt slot instead of binding it twice.
            so.FindProperty("openingPrompt").objectReferenceValue = openingPrompt;
            // Discard deleted text references; preserve unrelated valid bindings.
            for (int i = bindings.arraySize - 1; i >= 0; i--)
                if (bindings.GetArrayElementAtIndex(i).FindPropertyRelative("text").objectReferenceValue == null ||
                    texts.Contains(bindings.GetArrayElementAtIndex(i).FindPropertyRelative("text").objectReferenceValue as TMP_Text))
                    bindings.DeleteArrayElementAtIndex(i);
            foreach (var root in roots)
            foreach (var text in root.GetComponentsInChildren<TMP_Text>(true))
            {
                TextId id = IdFor(root, text);
                if (text != openingPrompt)
                {
                    var binding = bindings.GetArrayElementAtIndex(bindings.arraySize++);
                    binding.FindPropertyRelative("text").objectReferenceValue = text;
                    binding.FindPropertyRelative("id").intValue = (int)id;
                }
                text.text = spanish[id];
                EditorUtility.SetDirty(text);
                // Overrides belong to these scene instances, never to the shared Meta prefab.
                PrefabUtility.RecordPrefabInstancePropertyModifications(text);
            }
            so.ApplyModifiedPropertiesWithoutUndo(); EditorUtility.SetDirty(adapter);
            AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(scene); Check(EditorSceneManager.SaveScene(scene), "Scene save failed");
            Validate();
            Debug.Log("VISUAL_TUTORIAL_LOCALIZATION_PASS " + checks);
            EditorApplication.Exit(0);
        }
        catch (Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
    }
    public static void Validate()
    {
        EditorSceneManager.OpenScene(ScenePath);
        var catalog = AssetDatabase.LoadAssetAtPath<TextCatalog>("Assets/Localization/GameTexts.asset");
        var spanish = catalog.CreateLookup(LanguageAdapter.Language.Spanish);
        foreach (var root in Roots())
        foreach (var text in root.GetComponentsInChildren<TMP_Text>(true))
            Check(text.text == spanish[IdFor(root, text)], "Spanish preview not persisted: " + PathOf(text.transform));

        using (var report = new StreamWriter("Logs/visual-tutorial-localization-validation.txt"))
        foreach (LanguageAdapter.Language language in Enum.GetValues(typeof(LanguageAdapter.Language)))
        {
            EditorSceneManager.OpenScene(ScenePath);
            var selection = UnityEngine.Object.FindObjectsOfType<LanguageAdapter>(true).Single();
            selection.currentLanguage = language;
            var adapter = UnityEngine.Object.FindObjectsOfType<TextAdapter>(true).Single();
            var roots = Roots(); var texts = roots.SelectMany(r => r.GetComponentsInChildren<TMP_Text>(true)).ToArray();
            var active = texts.Select(t => t.gameObject.activeSelf).ToArray();
            typeof(TextAdapter).GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(adapter, null);
            foreach (TextId id in Enum.GetValues(typeof(TextId)))
                if (id != TextId.None) Check(!adapter.Get(id).StartsWith("[Missing:"), "Missing translation " + language + " " + id);
            var so = new SerializedObject(adapter); var bindings = so.FindProperty("staticSceneTexts");
            var openingPrompt = so.FindProperty("openingPrompt").objectReferenceValue as TMP_Text;
            Check(openingPrompt != null && texts.Contains(openingPrompt), "Missing Pre-Game Dialog opening-prompt reference");
            var boundTexts = Enumerable.Range(0, bindings.arraySize).Select(i =>
                bindings.GetArrayElementAtIndex(i).FindPropertyRelative("text").objectReferenceValue as TMP_Text).ToArray();
            Check(boundTexts.All(t => t != null) && boundTexts.Distinct().Count() == boundTexts.Length, "Deleted or duplicate static text binding");
            Check(!boundTexts.Contains(openingPrompt) && texts.Where(t => t != openingPrompt).All(t => boundTexts.Contains(t)), "Panel text missing or opening prompt bound twice");
            foreach (var root in roots)
            foreach (var text in root.GetComponentsInChildren<TMP_Text>(true))
            {
                TextId id = IdFor(root, text);
                Check(text.text == adapter.Get(id), "Wrong runtime text " + language + " " + PathOf(text.transform));
                if (language == LanguageAdapter.Language.Spanish)
                {
                    Vector2 preferred = text.GetPreferredValues(text.text, text.rectTransform.rect.width, Mathf.Infinity);
                    report.WriteLine(PathOf(text.transform)+" | visibleSelf="+text.gameObject.activeSelf+" | rect="+text.rectTransform.rect.size+" | preferred="+preferred+" | text="+text.text.Replace("\n", "\\n"));
                    if (text.gameObject.activeSelf && preferred.y > text.rectTransform.rect.height + .1f)
                        report.WriteLine("OVERFLOW_HEIGHT " + PathOf(text.transform));
                }
            }
            Check(active.SequenceEqual(texts.Select(t => t.gameObject.activeSelf)), "Localization changed object visibility");
            selection.currentLanguage = language == LanguageAdapter.Language.Spanish ? LanguageAdapter.Language.English : LanguageAdapter.Language.Spanish;
            Check(adapter.SessionLanguage == language, "Session language changed during gameplay");
            report.WriteLine("PASS runtime " + language);
        }
    }
}
