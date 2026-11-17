using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using AngerRoom.Localization;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

// Exercises introduction gating in the saved scene without saving any changes.
public static class IntroductionRestrictionValidation
{
    const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
    static int checks;
    static object Get(object o, string n) => o.GetType().GetField(n, Flags).GetValue(o);
    static void Set(object o, string n, object v) => o.GetType().GetField(n, Flags).SetValue(o, v);
    static object Call(object o, string n) => o.GetType().GetMethod(n, Flags).Invoke(o, null);
    static T Find<T>() where T : Component => UnityEngine.Object.FindObjectsOfType<T>(true).Single();
    static void Check(bool ok, string why) { if (!ok) throw new Exception(why); checks++; }
    static string PathOf(Transform t) => t.parent == null ? t.name : PathOf(t.parent) + "/" + t.name;
    static GameObject[] Tutorials(GamesManager gm) => (GameObject[])Get(gm, "tutorialButtons");
    static GameObject[] Real(GamesManager gm) => (GameObject[])Get(gm, "realButtons");
    static void Locked(GamesManager gm)
    {
        Check(Tutorials(gm).Concat(Real(gm)).All(b => !b.activeSelf), "Minigame control enabled during introduction");
        for (int i = 0; i < 3; i++)
        {
            Check(!gm.BeginAttempt(i, true), "Introduction allowed tutorial " + i);
            Check(!gm.BeginAttempt(i, false), "Introduction allowed minigame " + i);
        }
    }
    static GamesManager Load()
    {
        EditorSceneManager.OpenScene("Assets/Plugins/Scenes/AngerRoom.unity");
        Find<LanguageAdapter>().currentLanguage = LanguageAdapter.Language.Spanish;
        Call(Find<TextAdapter>(), "Awake");
        var gm = Find<GamesManager>();
        var dialogs = UnityEngine.Object.FindObjectsOfType<Button>(true).Where(b => PathOf(b.transform).Contains("Dialog")).ToArray();
        Check(dialogs.Length > 0, "No Dialog buttons found");
        var states = dialogs.Select(b => (b.gameObject.activeSelf, b.enabled, b.interactable)).ToArray();
        Call(gm, "Awake"); Locked(gm);
        Call(gm, "Start"); Locked(gm);
        Check(dialogs.Select(b => (b.gameObject.activeSelf, b.enabled, b.interactable)).SequenceEqual(states), "Introduction lock changed Dialog controls");
        Directory.CreateDirectory("Logs/IntroductionChecks");
        Set(gm.GetComponent<Logger>(), "_logPath", "Logs/IntroductionChecks/events.txt");
        Call(gm.GetComponent<ScoreboardManager>(), "Start");
        Call(gm.am, "Awake"); Call(gm.am, "Start");
        gm.SetAcquisitionBlocked(false);
        Check(!gm.AcquisitionBlocked, "Introduction lock used sensor flag");
        return gm;
    }
    static void ReportInspectorCalls(GamesManager gm)
    {
        using (var report = new StreamWriter("Logs/IntroductionChecks/inspector-events.txt"))
        foreach (var c in UnityEngine.Object.FindObjectsOfType<MonoBehaviour>(true).Where(c => c != null))
        {
            var so = new SerializedObject(c); var p = so.GetIterator();
            while (p.NextVisible(true))
            {
                if (p.name != "m_MethodName" || p.propertyType != SerializedPropertyType.String ||
                    !(new[] { "SetAcquisitionBlocked", "StartOverAllTimer", "Playing", "stoppedPlaying" }).Contains(p.stringValue)) continue;
                string prefix = p.propertyPath.Substring(0, p.propertyPath.Length - "m_MethodName".Length);
                if (so.FindProperty(prefix + "m_Target")?.objectReferenceValue != gm) continue;
                report.WriteLine(PathOf(c.transform) + " -> " + p.stringValue + " bool=" + so.FindProperty(prefix + "m_Arguments.m_BoolArgument")?.boolValue);
            }
        }
    }
    public static void Run()
    {
        if (!Application.isBatchMode) throw new Exception("Batch only");
        try
        {
            var gm = Load(); ReportInspectorCalls(gm);
            var group = Tutorials(gm)[0].GetComponentsInParent<Transform>(true).Single(t => t.name == "GameComponents").gameObject;
            group.SetActive(false); group.SetActive(true); Locked(gm);
            // Playing's delayed refresh and stoppedPlaying must not release the intro.
            var refresh = (IEnumerator)Call(gm, "DeactivatingButtons"); refresh.MoveNext(); refresh.MoveNext(); Locked(gm);
            gm.stoppedPlaying(); Locked(gm);
            // A stale externally enabled button must not bypass the method guard.
            Tutorials(gm)[0].SetActive(true); Check(!gm.BeginAttempt(0, true), "Stale click bypassed intro");
            Call(gm, "RefreshStartControls"); Locked(gm);
            gm.SetAcquisitionBlocked(true); gm.StartOverAllTimer();
            Check(!(bool)Get(gm, "openingDialogsComplete"), "Sensor block released introduction");
            gm.SetAcquisitionBlocked(false); Locked(gm);
            gm.StartOverAllTimer();
            Check((bool)Get(gm, "openingDialogsComplete") && (bool)Get(gm, "isgoing"), "Final Start did not release intro/start timer");
            Check(Tutorials(gm).All(b => b.activeSelf) && Real(gm).All(b => !b.activeSelf), "Wrong controls unlocked after intro");
            Check(gm.BeginAttempt(0, true), "Tutorial rejected after intro");
            Call(gm, "RefreshStartControls");
            Check(Tutorials(gm).All(b => !b.activeSelf), "Active attempt failed to lock controls");
            gm.MarkAttemptGameplay(0); gm.GameWon(0); gm.stoppedPlaying();
            Check(gm.HasPendingReward && !gm.BeginAttempt(1, true) && Tutorials(gm).All(b => !b.activeSelf), "Intro change bypassed pending-reward gate");

            gm = Load(); gm.enableDebugOperations = true; gm.enableDebugButtons = false;
            gm.DebugSkipOpeningDialogs();
            Check((bool)Get(gm, "openingDialogsComplete") && (bool)Get(gm, "isgoing") && Tutorials(gm).All(b => b.activeSelf), "Skipper did not release intro");
            Call(gm, "Update");
            Check(!((GameObject)Get(gm, "debugTutorialControls")).activeSelf && !gm.CanDebugShortenTimer && !gm.CanDebugSkipTutorial(0), "Skipper-only mode enabled debug buttons");
            Check(gm.BeginAttempt(1, true), "Debug skip left minigames locked");
            Debug.Log("INTRODUCTION_RESTRICTION_PASS " + checks);
            File.WriteAllText("Logs/IntroductionChecks/result.txt", "PASS " + checks + " checks. Dialog events were inspected, not modified.");
            LabyrinthButtonValidation.Run();
        }
        catch (Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
    }
}
