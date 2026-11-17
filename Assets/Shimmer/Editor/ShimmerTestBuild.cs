using System;
using System.Globalization;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace AngerRoom.Shimmer.Editor
{
    public static class ShimmerTestBuild
    {
        [MenuItem("Tools/Anger Room/Build Quest 2 Test APK")]
        public static void BuildQuest2FromEditor()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || BuildPipeline.isBuildingPlayer)
                throw new InvalidOperationException("Wait for compilation/building to finish and exit Play mode before building.");
            // The user explicitly chooses the test build. Production scene settings and
            // persistent scripting defines stay unchanged; the override lives in this APK.
            if (!UnityEditor.SceneManagement.EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            BuildInternal(false, true);
            EditorUtility.RevealInFinder("Builds/ShimmerStream/AngerRoomShimmerTest.apk");
        }

        public static void Build()
        {
            BuildInternal(false);
        }

        public static void BuildClean()
        {
            BuildInternal(true);
        }

        public static void BuildRecoveryQuest2()
        {
            SessionRecoveryValidation.Run();
            // Validation mutates only the in-memory scene. Reload the saved production scene.
            UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/Plugins/Scenes/AngerRoom.unity");
            BuildInternal(true, true);
        }

        public static void BuildRecoveryQuest2Incremental()
        {
            SessionRecoveryValidation.Run();
            UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/Plugins/Scenes/AngerRoom.unity");
            BuildInternal(false, true);
        }

        public static void ValidateRecordingFiles()
        {
            SessionRecoveryValidation.Run();
            ValidateParser();
            TimestampSynchronizationValidation.Run();
            RecordingFileValidation.Run();
            ShimmerNeededValidation.Run();
            // Validation changes only temporary in-memory scenes; exit explicitly
            // rather than leaving a dirty-scene shutdown prompt in unattended runs.
            if (Application.isBatchMode) EditorApplication.Exit(0);
        }

        // File/timing checks do not depend on the scene's simulator sensor settings.
        public static void ValidateCsvFormats()
        {
            ShimmerSignalValidation.Run();
            TimestampSynchronizationValidation.Run();
            RecordingFileValidation.Run();
            ShimmerNeededValidation.Run();
            if (Application.isBatchMode) EditorApplication.Exit(0);
        }

        static void BuildInternal(bool clean, bool recovery = false)
        {
            // Unity/XR build processors also change preloaded assets and the ID override flag.
            // Restore the complete original file so the test build leaves no project-setting edits.
            string settingsPath = "ProjectSettings/ProjectSettings.asset";
            byte[] originalSettings = File.ReadAllBytes(settingsPath);
            string originalIdentifier = PlayerSettings.GetApplicationIdentifier(BuildTargetGroup.Android);
            string originalProductName = PlayerSettings.productName;
            AndroidSdkVersions originalTargetSdk = PlayerSettings.Android.targetSdkVersion;
            try
            {
                ValidateParser();
                TimestampSynchronizationValidation.Run();
                // The integration check creates temporary scene objects and is restricted
                // to batch mode; never run it against the user's open editing session.
                if (Application.isBatchMode) RecordingFileValidation.Run();
                PlayerSettings.SetApplicationIdentifier(BuildTargetGroup.Android, "com.angerroom.shimmerstreamtest");
                PlayerSettings.productName = "Anger Room Shimmer Test";
                PlayerSettings.Android.targetSdkVersion = (AndroidSdkVersions)34;
                Directory.CreateDirectory("Builds/ShimmerStream");
                var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
                {
                    scenes = new[] { "Assets/Plugins/Scenes/AngerRoom.unity" },
                    locationPathName = "Builds/ShimmerStream/AngerRoomShimmerTest.apk",
                    target = BuildTarget.Android,
                    options = BuildOptions.Development | (clean ? BuildOptions.CleanBuildCache : BuildOptions.None),
                    extraScriptingDefines = recovery ? new[] { "ACQUISITION_RECOVERY_QUEST2_TEST" } : new[] { "SHIMMER_STREAM_TEST" }
                });
                if (report.summary.result != BuildResult.Succeeded)
                    throw new Exception("Shimmer test build failed: " + report.summary.result);
                Debug.Log("SHIMMER_BUILD_OK " + report.summary.outputPath);
            }
            finally
            {
                // Restore live Editor settings as well as the serialized file so a later
                // normal Build does not retain this test APK's identity or target SDK.
                PlayerSettings.SetApplicationIdentifier(BuildTargetGroup.Android, originalIdentifier);
                PlayerSettings.productName = originalProductName;
                PlayerSettings.Android.targetSdkVersion = originalTargetSdk;
                AssetDatabase.SaveAssets();
                File.WriteAllBytes(settingsPath, originalSettings);
                AssetDatabase.Refresh();
            }
        }

        public static void ValidateParser()
        {
            CultureInfo old = CultureInfo.CurrentCulture;
            try
            {
                CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("fr-FR");
                var sample = ShimmerReceiver.ParseSample("1,256,256,0.0078125,1000000000,1794325000000,34768,2000,2,1.9304029304,true,false,2048,1500.3663,true,0");
                if (sample.GsrRange != 2 || sample.PpgRaw != 2048 || Math.Abs(sample.EdaMicroSiemens - 1.9304029304) > 1e-9)
                    throw new Exception("Shimmer CSV numeric decoding failed");
                var invalid = ShimmerReceiver.ParseSample("2,512,512,0.015625,2000000000,1794325001000,0,0,0,,false,true,0,0.0,false,2");
                if (!double.IsNaN(invalid.EdaMicroSiemens) || invalid.EdaConversionValid || invalid.EstimatedMissing != 2)
                    throw new Exception("Shimmer CSV validity decoding failed");
                Debug.Log("SHIMMER_CSHARP_TESTS_OK");
            }
            finally { CultureInfo.CurrentCulture = old; }
        }
    }
}
