using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using AngerRoom.Localization;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Read-only audit of the saved scene; output is under Logs, never saved to scene.
public static class TextLocalizationAudit
{
    const string ScenePath = "Assets/Plugins/Scenes/AngerRoom.unity";
    static string PathOf(Transform t) => t.parent == null ? t.name : PathOf(t.parent) + "/" + t.name;
    static string Escape(string s) => (s ?? "").Replace("\r", "").Replace("\n", "\\n").Replace("\t", " ");
    static bool Dialog(Transform t) => PathOf(t).IndexOf("dialog", StringComparison.OrdinalIgnoreCase) >= 0;
    static string Describe(UnityEngine.Object o) => o is Component c ? PathOf(c.transform) + " (" + c.GetType().Name + ")" :
        o is GameObject g ? PathOf(g.transform) : o == null ? "null" : o.name;

    public static void Run()
    {
        if (!Application.isBatchMode) throw new InvalidOperationException("Run in a separate Unity batch process.");
        var scene = EditorSceneManager.OpenScene(ScenePath);
        var all = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Component>(true)).Where(c => c != null).ToArray();
        var texts = all.OfType<TMP_Text>().Where(t => !Dialog(t.transform)).ToArray();
        using var report = new StreamWriter("Logs/text-localization-audit.tsv");
        report.WriteLine("KIND\tPATH\tDETAILS");
        foreach (var text in texts)
            report.WriteLine("TMP\t" + PathOf(text.transform) + "\tid=" + GlobalObjectId.GetGlobalObjectIdSlow(text) +
                "; enabled=" + text.enabled + "; activeSelf=" + text.gameObject.activeSelf + "; activeHierarchy=" + text.gameObject.activeInHierarchy +
                "; text=" + Escape(text.text));
        foreach (var component in all.OfType<MonoBehaviour>())
        {
            if (component is TMP_Text) continue;
            var so = new SerializedObject(component);
            var p = so.GetIterator();
            while (p.NextVisible(true))
            {
                if (p.propertyType == SerializedPropertyType.ObjectReference && p.objectReferenceValue is TMP_Text text && !Dialog(text.transform))
                    report.WriteLine("REFERENCE\t" + PathOf(text.transform) + "\t" + Describe(component) + "." + p.propertyPath);
                if (p.name != "m_MethodName" || p.propertyType != SerializedPropertyType.String || string.IsNullOrEmpty(p.stringValue)) continue;
                string prefix = p.propertyPath.Substring(0, p.propertyPath.Length - "m_MethodName".Length);
                var target = so.FindProperty(prefix + "m_Target")?.objectReferenceValue;
                var argument = so.FindProperty(prefix + "m_Arguments.m_StringArgument")?.stringValue;
                if (target is TMP_Text targetText && Dialog(targetText.transform)) continue;
                if (target is TMP_Text || !Dialog(component.transform))
                    report.WriteLine("EVENT\t" + Describe(component) + "\t" + Describe(target) + "." + p.stringValue +
                        "; string=" + Escape(argument) + "; state=" + so.FindProperty(prefix + "m_CallState")?.intValue);
            }
            if (component is Card_Minigame || component is GunPuzzle || component is LabyrinthScript || component is TextAdapter || component is LanguageAdapter)
                report.WriteLine("COMPONENT\t" + Describe(component) + "\t" + Escape(EditorJsonUtility.ToJson(component)));
        }
        var catalog = AssetDatabase.LoadAssetAtPath<TextCatalog>("Assets/Localization/GameTexts.asset");
        int entries = 0;
        foreach (LanguageAdapter.Language lang in Enum.GetValues(typeof(LanguageAdapter.Language)))
        {
            var lookup = catalog.CreateLookup(lang);
            foreach (TextId id in Enum.GetValues(typeof(TextId)))
            {
                if (id == TextId.None) continue;
                if (!lookup.TryGetValue(id, out string value) || string.IsNullOrWhiteSpace(value)) throw new Exception("Missing " + lang + " " + id);
                string.Format(value, 2, 3, 4);
                entries++;
            }
        }
        foreach (string asset in AssetDatabase.GetDependencies(ScenePath, true).Where(p => p.EndsWith(".prefab")))
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(asset);
            if (prefab == null) continue;
            foreach (var text in prefab.GetComponentsInChildren<TMP_Text>(true).Where(t => !Dialog(t.transform)))
                report.WriteLine("PREFAB\t" + asset + ":" + PathOf(text.transform) + "\t" + Escape(text.text));
        }
        report.WriteLine("SUMMARY\t" + texts.Length + " non-Dialog TMP objects\t" + entries + " translations validated; excluded " + all.OfType<TMP_Text>().Count(t => Dialog(t.transform)) + " Dialog TMP objects");
        Debug.Log("TEXT_AUDIT_OK nonDialogTMP=" + texts.Length + "; translations=" + entries);
    }
}
