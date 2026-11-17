using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Reads the saved scene without changing it. Null fields are reported for manual
// review: optional references are not automatically classified as errors.
public static class SceneReferenceAudit
{
    static string PathOf(Transform t) => t.parent == null ? t.name : PathOf(t.parent) + "/" + t.name;
    static string Describe(UnityEngine.Object o) => o is Component c ? PathOf(c.transform) + " (" + c.GetType().Name + ")" : o == null ? "null" : o.name;
    public static void Run()
    {
        if (!Application.isBatchMode) throw new Exception("Use a separate Unity batch process.");
        var scene = EditorSceneManager.OpenScene("Assets/Plugins/Scenes/AngerRoom.unity");
        int missingScripts = 0, dangling = 0, brokenEvents = 0, nullFields = 0;
        using (var report = new StreamWriter("Logs/scene-reference-audit.txt"))
        foreach (var t in scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Transform>(true)))
        {
            int missing = GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject);
            if (missing > 0) { missingScripts += missing; report.WriteLine("MISSING_SCRIPT " + PathOf(t)); }
            foreach (var c in t.GetComponents<MonoBehaviour>().Where(c => c != null))
            {
                string scriptPath = AssetDatabase.GetAssetPath(MonoScript.FromMonoBehaviour(c));
                bool own = scriptPath.StartsWith("Assets/") && !scriptPath.StartsWith("Assets/Samples/") && !scriptPath.StartsWith("Assets/TextMesh Pro/");
                var so = new SerializedObject(c); var p = so.GetIterator();
                while (p.NextVisible(true))
                {
                    if (p.propertyType == SerializedPropertyType.ObjectReference && p.objectReferenceValue == null)
                    {
                        if (p.objectReferenceInstanceIDValue != 0) { dangling++; report.WriteLine("DANGLING " + Describe(c) + "." + p.propertyPath); }
                        else if (own && !p.name.StartsWith("m_")) { nullFields++; report.WriteLine("NULL " + Describe(c) + "." + p.propertyPath); }
                    }
                    if (p.name == "m_MethodName" && p.propertyType == SerializedPropertyType.String && !string.IsNullOrEmpty(p.stringValue))
                    {
                        string prefix = p.propertyPath.Substring(0, p.propertyPath.Length - "m_MethodName".Length);
                        var target = so.FindProperty(prefix + "m_Target")?.objectReferenceValue;
                        var state = so.FindProperty(prefix + "m_CallState");
                        if (state != null && state.intValue != 0 && (target == null || !target.GetType().GetMethods().Any(m => m.Name == p.stringValue)))
                        { brokenEvents++; report.WriteLine("BROKEN_EVENT " + Describe(c) + " -> " + Describe(target) + "." + p.stringValue); }
                        if (p.stringValue == "setLevel" || p.stringValue == "setTimer")
                            report.WriteLine("LEVEL_EVENT " + Describe(c) + " -> " + Describe(target) + "." + p.stringValue +
                                " int=" + so.FindProperty(prefix + "m_Arguments.m_IntArgument").intValue + " float=" + so.FindProperty(prefix + "m_Arguments.m_FloatArgument").floatValue);
                    }
                    if (own && p.propertyType == SerializedPropertyType.ObjectReference && p.objectReferenceValue != null)
                        report.WriteLine("REF " + Describe(c) + "." + p.propertyPath + " -> " + Describe(p.objectReferenceValue));
                }
            }
        }
        File.AppendAllText("Logs/scene-reference-audit.txt", "SUMMARY missingScripts=" + missingScripts + " dangling=" + dangling + " brokenEvents=" + brokenEvents + " optionalOrRequiredNullFields=" + nullFields + "\n");
        Debug.Log("SCENE_REFERENCE_AUDIT_DONE");
        EditorApplication.Exit(0);
    }
}
