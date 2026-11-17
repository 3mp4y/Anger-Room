using System;
using System.IO;
using System.Linq;
using System.Reflection;
using AngerRoom.Rewards;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class RewardAudioSetup
{
    const string ScenePath = "Assets/Plugins/Scenes/AngerRoom.unity";
    const string ClipPath = "Assets/Audio Files/freesound_community-shimmering-object-79354.mp3";
    const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    static object Get(object o, string n) => o.GetType().GetField(n, Flags).GetValue(o);
    static void Set(object o, string n, object v) => o.GetType().GetField(n, Flags).SetValue(o, v);
    static void Call(object o, string n) => o.GetType().GetMethod(n, Flags).Invoke(o, null);
    static int checks;
    static void Check(bool ok, string message) { if (!ok) throw new Exception(message); checks++; }
    static string PathOf(Transform t) => t.parent == null ? t.name : PathOf(t.parent) + "/" + t.name;

    public static void ConfigureAndValidate()
    {
        if (!Application.isBatchMode) throw new InvalidOperationException("Use a separate Unity batch process.");
        // Import conversion preserves the original MP3 bytes. Normalization is
        // applied by Unity when downmixing to mono, before spatial positioning.
        var importer = (AudioImporter)AssetImporter.GetAtPath(ClipPath);
        importer.forceToMono = true;
        var serializedImporter = new SerializedObject(importer);
        serializedImporter.FindProperty("m_Normalize").boolValue = true;
        serializedImporter.ApplyModifiedPropertiesWithoutUndo();
        importer.SaveAndReimport();
        var scene = EditorSceneManager.OpenScene(ScenePath);
        foreach (var box in UnityEngine.Object.FindObjectsOfType<MinigameRewardBox>(true))
        {
            var so = new SerializedObject(box);
            so.FindProperty("rewardAvailableVolume").floatValue = 1f;
            so.FindProperty("rewardNearDistance").floatValue = 1.5f;
            so.FindProperty("rewardStageDistance").floatValue = 25f;
            so.FindProperty("rewardStageVolume").floatValue = .65f;
            so.FindProperty("rewardFadeOutDistance").floatValue = 40f;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Validate();
    }

    public static void Validate()
    {
        if (!Application.isBatchMode) throw new InvalidOperationException("Batch-only checks; no test scene changes are saved.");
        checks = 0;
        EditorSceneManager.OpenScene(ScenePath);
        var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(ClipPath);
        clip.LoadAudioData();
        var samples = new float[clip.samples * clip.channels];
        Check(clip.GetData(samples, 0), "Cannot read normalized audio");
        float peak = samples.Max(s => Mathf.Abs(s));
        double rmsDb = 10 * Math.Log10(samples.Average(s => s * (double)s));
        Check(clip.channels == 1, "Reward cue should be a mono point source");
        Check(peak > .7f && peak < 1.1f && rmsDb > -30, "Quiet clip was not normalized as expected");
        var gm = UnityEngine.Object.FindObjectOfType<GamesManager>(true);
        foreach (var t in gm.GetComponentsInParent<Transform>(true)) t.gameObject.SetActive(true);
        Call(gm, "Awake"); Call(gm, "Start");
        Directory.CreateDirectory("Logs/RewardAudioChecks");
        Set(gm.GetComponent<Logger>(), "_logPath", "Logs/RewardAudioChecks/events.txt");
        Call(gm.GetComponent<ScoreboardManager>(), "Start");
        Call(gm.am, "Awake"); Call(gm.am, "Start");
        var boxes = UnityEngine.Object.FindObjectsOfType<MinigameRewardBox>(true);
        var stagePositions = boxes.Select(b => b.transform.position).Concat(
            ((GameObject[])Get(gm, "realButtons")).Select(b => b.transform.position)).Concat(
            new[] { ((GameObject)Get(gm, "openingStartObject")).transform.position }).ToArray();
        Check(boxes.Length == 3, "Expected three reward boxes");
        foreach (var box in boxes)
        {
            foreach (var t in box.GetComponentsInParent<Transform>(true)) t.gameObject.SetActive(true);
            Call(box, "Start");
            var source = (AudioSource)Get(box, "floatingPointsSource");
            Check(source != null && source.transform == ((TMPro.TMP_Text)Get(box, "pointsText")).transform, "Cue must originate at points");
            Check(source.clip == clip && source.loop && !source.playOnAwake && source.priority == 128, "Wrong cue/playback/priority");
            Check(source.spatialBlend == 1 && source.spread == 0 && source.dopplerLevel == 0, "Lost positional audio");
            Check(source.rolloffMode == AudioRolloffMode.Custom && source.volume == 1, "Wrong volume/fade mode");
            var curve = source.GetCustomCurve(AudioSourceCurveType.CustomRolloff);
            Check(Mathf.Approximately(curve.Evaluate(0), 1) && curve.Evaluate(25f / source.maxDistance) >= .649f &&
                Mathf.Abs(curve.Evaluate(1)) < .001f, "Coverage curve is incorrect");
            Check(stagePositions.All(p => Vector3.Distance(source.transform.position, p) < 25), "A known stage station is outside coverage");
            // Live Inspector tuning must reach the existing source, including
            // range changes, without recreating it or changing to a 2D sound.
            Set(box, "rewardAvailableVolume", .35f);
            Set(box, "rewardNearDistance", 2f); Set(box, "rewardStageDistance", 20f);
            Set(box, "rewardStageVolume", .5f); Set(box, "rewardFadeOutDistance", 35f);
            Call(box, "UpdateFloatingAudio");
            Check((AudioSource)Get(box, "floatingPointsSource") == source && Mathf.Approximately(source.volume, .35f), "Live volume change failed");
            Check(source.minDistance == 2 && source.maxDistance == 35 &&
                Mathf.Abs(source.GetCustomCurve(AudioSourceCurveType.CustomRolloff).Evaluate(20f / 35f) - .5f) < .001f,
                "Live coverage change failed");
            Set(box, "rewardNearDistance", 8f); Set(box, "rewardStageDistance", 2f); Set(box, "rewardFadeOutDistance", 1f);
            Call(box, "UpdateFloatingAudio");
            Check(source.maxDistance > source.minDistance, "Invalid Inspector distances were not guarded");
            Check(source.spatialBlend == 1 && source.clip == clip && source.transform == ((TMPro.TMP_Text)Get(box, "pointsText")).transform,
                "Live edits changed the sound origin or clip");
        }
        using var report = new StreamWriter("Logs/RewardAudioChecks/result.txt");
        report.WriteLine("PASS " + checks + " checks. Peak=" + (20 * Math.Log10(peak)) + " dBFS; RMS=" + rmsDb + " dBFS. Headset audibility needs listening validation.");
        // Inventory broad horizontal meshes near the stage, to review physical
        // coverage rather than relying only on distances between the stations.
        foreach (var r in UnityEngine.Object.FindObjectsOfType<Renderer>(true))
        {
            var b = r.bounds;
            if (b.size.x > 3 && b.size.z > 3 && b.min.y > -2 && b.max.y < 1.5f && b.min.z < -8 && b.max.z > -12)
                report.WriteLine("FLOOR " + PathOf(r.transform) + " bounds=" + b);
        }
        Debug.Log("REWARD_AUDIO_PASS " + checks + "; peak=" + peak + "; RMS_dBFS=" + rmsDb);
    }
    public static void Inspect()
    {
        EditorSceneManager.OpenScene(ScenePath);
        using var report = new StreamWriter("Logs/reward-audio-inspection.txt");
        foreach (var box in UnityEngine.Object.FindObjectsOfType<MinigameRewardBox>(true))
        {
            var so = new SerializedObject(box);
            var points = (TMPro.TMP_Text)so.FindProperty("pointsText").objectReferenceValue;
            report.WriteLine("REWARD " + PathOf(box.transform) + " points=" + points.transform.position.ToString("F3"));
        }
        foreach (var r in UnityEngine.Object.FindObjectsOfType<Renderer>(true))
        {
            string path = PathOf(r.transform);
            if (path.IndexOf("stage", StringComparison.OrdinalIgnoreCase) >= 0 ||
                path.Contains("TheaterPlatform") || path.EndsWith("/Ground"))
                report.WriteLine("GEOMETRY " + path + " bounds=" + r.bounds);
        }
        var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(ClipPath);
        clip.LoadAudioData();
        var samples = new float[clip.samples * clip.channels];
        if (!clip.GetData(samples, 0)) throw new Exception("Cannot read reward clip samples");
        double sum = 0; float peak = 0;
        foreach (float s in samples) { peak = Mathf.Max(peak, Mathf.Abs(s)); sum += s * (double)s; }
        report.WriteLine("CLIP channels=" + clip.channels + " seconds=" + clip.length + " peak_dBFS=" +
            20 * Math.Log10(Math.Max(peak, 1e-9)) + " rms_dBFS=" + 10 * Math.Log10(Math.Max(sum / samples.Length, 1e-18)));
        Debug.Log("REWARD_AUDIO_INSPECTED");
    }
}
