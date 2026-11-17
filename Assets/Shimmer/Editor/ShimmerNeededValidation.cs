using System;
using System.IO;
using System.Reflection;
using AngerRoom.Shimmer;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class ShimmerNeededValidation
{
    static readonly BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic;
    static void Call(object o, string name, params object[] args) => o.GetType().GetMethod(name, Flags).Invoke(o, args);
    static void Set(object o, string name, object value) => o.GetType().GetProperty(name).SetValue(o, value);
    static int checks;
    static void Check(bool value, string message) { if (!value) throw new Exception(message); checks++; }

    public static void Run()
    {
        if (!Application.isBatchMode) throw new InvalidOperationException("Run in a separate Unity batch process.");
        checks = 0;
        EditorSceneManager.OpenScene("Assets/Plugins/Scenes/AngerRoom.unity");
        var session = UnityEngine.Object.FindObjectOfType<RecordingSession>();
        var gate = UnityEngine.Object.FindObjectOfType<AcquisitionGate>();
        var receiver = gate.shimmer;
        string folder = Path.GetFullPath("Logs/ShimmerNeededChecks/" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        var prior = RecordingSession.Instance;
        try
        {
            Check(session.ShimmerNeeded, "ShimmerNeeded must default to enabled in the scene");
            session.ShimmerNeeded = false;
            Call(session, "CaptureStartupSettings");
            Set(session, "Folder", folder); Set(session, "SessionId", "shimmer_disabled_fixture");
            Set(session, "OriginNanos", session.ClockNanos());
            typeof(RecordingSession).GetProperty("Instance").SetValue(null, session);
            Call(session, "InitializeMetadata", DateTime.UtcNow);
            Call(session, "SaveMetadata"); Call(session, "OpenRecorders");
            Check(!session.IsShimmerRequired, "Disabled setting was not captured");
            session.ShimmerNeeded = true;
            Check(!session.IsShimmerRequired, "Inspector change affected a running session");
            receiver.StartRecording(); Call(receiver, "Update");
            Check(receiver.Status == "Shimmer disabled for testing" && receiver.CsvPath == null &&
                !receiver.IsConnectingOrRunning && receiver.ConnectionSegment == 0, "Disabled receiver attempted acquisition");
            Check(!receiver.HasFreshSamples(1), "Disabled receiver claimed real samples");
            gate.requireEyeTracking = true;
            Call(gate, "Awake"); Call(gate, "Start"); Call(gate, "Update");
            Check(gate.IsBlocked && gate.requireEyeTracking, "Shimmer bypass also bypassed eye tracking");
            Check(gate.statusText.text.Contains("Shimmer: disabled for testing"), "Wrong bypass status");
            gate.requireEyeTracking = false;
            Call(gate, "Update");
            Check(!gate.IsBlocked && !gate.games.AcquisitionBlocked, "Simulator did not start with both requirements disabled");
            foreach (var light in gate.startupLights) Check(light.enabled, "Startup light did not turn on");
            gate.Interrupt("Shimmer samples stopped");
            Call(gate, "OnApplicationPause", true); Call(gate, "OnApplicationPause", false); Call(gate, "Update");
            Check(!gate.IsBlocked && !gate.AwaitingResume, "Disabled Shimmer triggered recovery/resume");
            session.LogApplication("Simulator gameplay event");
            string metadata = File.ReadAllText(Path.Combine(folder, "session.json"));
            Check(metadata.Contains("\"shimmer_required\": false") && metadata.Contains("Shimmer disabled for testing"), "Missing test-mode metadata");
            Call(receiver, "Release");
            Check(Directory.GetFiles(folder, "shimmer*", SearchOption.AllDirectories).Length == 0, "Bypass created Shimmer files");
            Check(File.Exists(Path.Combine(folder, "Logger.csv")) && File.Exists(Path.Combine(session.DiagnosticsFolder, "clock_sync.csv")), "Bypass stopped other recorders");
            // A new session captures the new Inspector value and restores normal receiver eligibility.
            Call(session, "CaptureStartupSettings");
            Check(session.IsShimmerRequired && receiver.ShimmerEnabled, "New session did not restore the enabled requirement");
            Debug.Log("SHIMMER_NEEDED_TESTS_OK checks=" + checks);
        }
        finally
        {
            foreach (string name in new[] { "logger", "events", "clockSync" })
            {
                (typeof(RecordingSession).GetField(name, Flags).GetValue(session) as IDisposable)?.Dispose();
                typeof(RecordingSession).GetField(name, Flags).SetValue(session, null);
            }
            typeof(RecordingSession).GetProperty("Instance").SetValue(null, prior);
            EditorSceneManager.OpenScene("Assets/Plugins/Scenes/AngerRoom.unity");
        }
    }
}
