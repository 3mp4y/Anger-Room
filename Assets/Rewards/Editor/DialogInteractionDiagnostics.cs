using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using Oculus.Interaction;
using AngerRoom.Rewards;

public static class DialogInteractionDiagnostics
{
    static string PathOf(Transform t) => t.parent == null ? t.name : PathOf(t.parent) + "/" + t.name;
    public static void Run()
    {
        if (!Application.isBatchMode) throw new InvalidOperationException("Batch inspection only.");
        var scene = EditorSceneManager.OpenScene("Assets/Plugins/Scenes/AngerRoom.unity");
        using (var output = new StreamWriter("Logs/DialogInteractionReport.txt"))
        foreach (var root in scene.GetRootGameObjects())
        foreach (var component in root.GetComponentsInChildren<Component>(true))
        {
            if (component == null) continue;
            string type = component.GetType().Name;
            if (!(type.Contains("RayInteract") || type.Contains("InputModule") || type.Contains("CanvasModule") || type == "EventSystem" || type == "PointableCanvas" || type == "OVRRaycaster")) continue;
            string path = PathOf(component.transform);
            output.WriteLine(type + " | " + path + " | active=" + component.gameObject.activeInHierarchy + " enabled=" + (!(component is Behaviour b) || b.enabled));
            foreach (var t in component.GetComponentsInParent<Transform>(true))
                if (!t.gameObject.activeSelf) output.WriteLine("  inactive parent: " + PathOf(t));
            var data = new SerializedObject(component);
            var prop = data.GetIterator();
            while (prop.NextVisible(true))
                if (prop.propertyType == SerializedPropertyType.ObjectReference && !prop.name.StartsWith("m_"))
                    output.WriteLine("  " + prop.propertyPath + "=" + (prop.objectReferenceValue != null ? prop.objectReferenceValue.name : "NULL"));
        }
        var components = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Component>(true)).Where(c => c != null).ToArray();
        var systems = components.OfType<EventSystem>().Where(e => e.isActiveAndEnabled).ToArray();
        if (systems.Length != 1 || systems[0].GetComponent<PointableCanvasModule>() == null)
            throw new InvalidOperationException("Expected one active EventSystem with Meta's PointableCanvasModule.");
        var module = systems[0].GetComponent<PointableCanvasModule>();
        if (!module.enabled) throw new InvalidOperationException("Meta Canvas input is disabled.");
        var rays = components.OfType<RayInteractor>().Where(r => r.isActiveAndEnabled).ToArray();
        if (rays.Length != 4) throw new InvalidOperationException("Expected left/right controller and hand ray interactors.");
        int dialogs = 0;
        foreach (var canvas in components.OfType<PointableCanvas>())
        {
            if (!PathOf(canvas.transform).Contains("Dialog")) continue;
            var ray = canvas.GetComponent<RayInteractable>();
            if (ray == null || !ray.enabled) throw new InvalidOperationException("Missing enabled Dialog ray: " + PathOf(canvas.transform));
            var data = new SerializedObject(ray);
            if (data.FindProperty("_pointableElement").objectReferenceValue != canvas || data.FindProperty("_surface").objectReferenceValue == null)
                throw new InvalidOperationException("Broken Dialog ray-to-canvas connection: " + PathOf(canvas.transform));
            dialogs++;
        }
        if (dialogs == 0) throw new InvalidOperationException("No Dialog canvases found.");
        int rewards = 0;
        foreach (var reward in components.OfType<MinigameRewardBox>())
        {
            var poke = (PokeInteractable)new SerializedObject(reward).FindProperty("openButton").objectReferenceValue;
            if (poke == null || poke.GetComponentsInChildren<RayInteractable>(true).Length != 0)
                throw new InvalidOperationException("Reward button is not poke-only.");
            rewards++;
        }
        if (rewards != 3) throw new InvalidOperationException("Expected three reward buttons.");
        File.WriteAllText("Logs/DialogInteractionValidation.txt", $"PASS: one active Meta EventSystem, {rays.Length} active ray interactors, {dialogs} connected Dialog canvases, {rewards} poke-only reward buttons. Hardware click test still required.");
        EditorApplication.Exit(0);
    }
}
