using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using AngerRoom.Localization;
using AngerRoom.Rewards;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class ResultDisplaySetup
{
    const string ScenePath = "Assets/Plugins/Scenes/AngerRoom.unity";
    const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    static object Get(object o, string n) => o.GetType().GetField(n, Flags).GetValue(o);
    static void Set(object o, string n, object v) => o.GetType().GetField(n, Flags).SetValue(o, v);
    static object Call(object o, string n, params object[] args) => o.GetType().GetMethod(n, Flags).Invoke(o, args);
    static T Find<T>() where T : Component => UnityEngine.Object.FindObjectsOfType<T>(true).Single();
    static int checks;
    static void Check(bool ok, string message) { if (!ok) throw new Exception(message); checks++; }

    public static void ConfigureAndValidate()
    {
        if (!Application.isBatchMode) throw new Exception("Batch only");
        var scene = EditorSceneManager.OpenScene(ScenePath);
        var gun = Find<GunPuzzle>();
        var so = new SerializedObject(gun);
        var timer = (TMP_Text)so.FindProperty("TMPTimer").objectReferenceValue;
        so.FindProperty("OldTexts.root").objectReferenceValue = timer.transform.parent.gameObject;
        so.FindProperty("OldTexts.show").boolValue = false;
        so.FindProperty("screenReadout.show").boolValue = true;
        so.ApplyModifiedPropertiesWithoutUndo();
        // Keep the user's deleted camera readout deleted, and remove its stale
        // entries from the glow arrays without changing the remaining materials.
        var glow = gun.GetComponent<MinigameTutorialGlow>();
        var glowSo = new SerializedObject(glow);
        var labels = glowSo.FindProperty("labels");
        var materials = glowSo.FindProperty("glowMaterials");
        for (int i = labels.arraySize - 1; i >= 0; i--)
        {
            if (labels.GetArrayElementAtIndex(i).objectReferenceValue != null) continue;
            labels.GetArrayElementAtIndex(i).objectReferenceValue = null;
            materials.GetArrayElementAtIndex(i).objectReferenceValue = null;
            labels.DeleteArrayElementAtIndex(i); materials.DeleteArrayElementAtIndex(i);
        }
        glowSo.ApplyModifiedPropertiesWithoutUndo();
        EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
        LocalizationCompletionSetup.Validate();
        ValidateDebugResults();
        MinigameFlowValidation.Run(); // Exits batch process after regression checks.
    }

    public static void ValidateDebugResults()
    {
        checks = 0;
        foreach (LanguageAdapter.Language language in Enum.GetValues(typeof(LanguageAdapter.Language)))
        for (int game = 0; game < 3; game++)
        foreach (bool won in new[] { false, true })
        {
            var gm = (GamesManager)typeof(LocalizationCompletionSetup).GetMethod("Load", BindingFlags.Static | BindingFlags.NonPublic)
                .Invoke(null, new object[] { language });
            gm.enableDebugOperations = true; Set(gm, "openingDialogsComplete", true);
            var adapter = Find<TextAdapter>(); var gun = Find<GunPuzzle>();
            var message = game == 0 ? (TextMeshPro)Get(Find<Card_Minigame>(), "timerText") :
                game == 1 ? (TextMeshPro)Get(gun, "TMPTimer") : (TextMeshPro)Get(Find<LabyrinthScript>(), "congrats");
            // A previous retry message is still waiting when the debug result
            // arrives. Its old delay must not erase the final result later.
            var oldMessage = (IEnumerator)Call(gm, "lossMessage", 1, false, message, adapter);
            oldMessage.MoveNext();
            if (won) gm.DebugWinMinigame(game); else gm.DebugLoseMinigame(game);
            Call(gun, "LateUpdate");
            string expected = adapter.Get(won ? TextId.Victory : TextId.GameDisabled);
            Check(message.text == expected && message.gameObject.activeInHierarchy, "Debug result missing/hidden: " + game + " " + language);
            Check(gm.IsGameResolved(game) && gm.CanCollectReward(game) && !gm.IsRewardCollected(game), "Debug bypassed reward collection");
            oldMessage.MoveNext();
            Check(message.text == expected, "Old retry timer erased new result");
            if (game == 1)
            {
                Check(!((GameObject)Get(Get(gun, "screenReadout"), "root")).activeSelf, "Screen showed result");
                Check(((GameObject)Get(Get(gun, "OldTexts"), "root")).activeInHierarchy && message.transform.parent.name == "OldShootingTexts", "Wrong result location");
                Check((GameObject)Get(Get(gun, "centerEyeReadout"), "root") == null, "Deleted camera readout was recreated");
            }
        }
        File.WriteAllText("Logs/result-display-validation.txt", "PASS " + checks + " checks across all six debug results and three languages.");
        Debug.Log("RESULT_DISPLAY_PASS " + checks);
    }
}
