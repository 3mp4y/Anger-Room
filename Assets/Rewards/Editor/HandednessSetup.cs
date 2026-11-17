using System;
using System.IO;
using System.Linq;
using System.Reflection;
using AngerRoom.Rewards;
using AngerRoom.Shimmer;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class HandednessSetup
{
    const string ScenePath = "Assets/Plugins/Scenes/AngerRoom.unity";
    static string PathOf(Transform t) => t.parent == null ? t.name : PathOf(t.parent) + "/" + t.name;
    const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    static object Get(object o, string name) => o.GetType().GetField(name, Flags).GetValue(o);
    static void Call(object o, string name) => o.GetType().GetMethod(name, Flags).Invoke(o, null);
    static void Assign(UnityEngine.Object o, string name, UnityEngine.Object value)
    {
        var so = new SerializedObject(o);
        so.FindProperty(name).objectReferenceValue = value;
        so.ApplyModifiedPropertiesWithoutUndo();
        PrefabUtility.RecordPrefabInstancePropertyModifications(o);
    }
    static Transform[] SceneTransforms() => UnityEngine.SceneManagement.SceneManager.GetActiveScene()
        .GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Transform>(true)).ToArray();
    static Transform Mount(Transform parent, string name, Vector3 position, Quaternion rotation)
    {
        var t = parent.Find(name);
        if (t != null) return t; // Keep any later manual grip adjustments.
        t = new GameObject(name).transform;
        t.SetParent(parent, false);
        t.localPosition = position;
        t.localRotation = rotation;
        return t;
    }

    public static void ConfigureAndValidate()
    {
        if (!Application.isBatchMode) throw new InvalidOperationException("Run setup in a separate Unity batch process.");
        var scene = EditorSceneManager.OpenScene(ScenePath);
        var all = SceneTransforms();
        var gm = all.Select(t => t.GetComponent<GamesManager>()).Single(c => c != null);
        var gun = all.Select(t => t.GetComponent<GunPuzzle>()).Single(c => c != null);
        var rig = all.Single(t => t.name == "[BuildingBlock] OVRInteractionComprehensive");
        var leftVisual = rig.Find("OVRLeftControllerVisual");
        var rightVisual = rig.Find("OVRRightControllerVisual");
        var leftModel = leftVisual.Find("OVRControllerPrefab");
        var rightModel = rightVisual.Find("OVRControllerPrefab");
        Assign(gm, "leftInteractors", rig.Find("LeftInteractions/Interactors").gameObject);
        Assign(gm, "rightInteractors", rig.Find("RightInteractions/Interactors").gameObject);
        Assign(gm, "leftControllerVisual", leftVisual.gameObject);
        Assign(gm, "rightControllerVisual", rightVisual.gameObject);
        Assign(gm, "leftControllerModel", leftModel);
        Assign(gm, "rightControllerModel", rightModel);
        Assign(gm, "controllerReadout", all.Single(t => t.name == "TimeCounter_Labyrinth"));
        const string locomotion = "/Interactors/Controller/LocomotionControllerInteractorGroup";
        var leftLocomotion = rig.Find("LeftInteractions" + locomotion);
        var rightLocomotion = rig.Find("RightInteractions" + locomotion);
        Assign(gm, "leftTeleportInteractor", leftLocomotion.Find("TeleportControllerInteractor").gameObject);
        Assign(gm, "rightTeleportInteractor", rightLocomotion.Find("TeleportControllerInteractor").gameObject);
        // The prefab originally gives the two hands different locomotion roles.
        // Copy activation choices, not controller references: the left side must
        // still read the left controller and feed the SAME existing locomotor.
        foreach (Transform right in rightLocomotion.GetComponentsInChildren<Transform>(true))
        {
            string relative = right == rightLocomotion ? "" : PathOf(right).Substring(PathOf(rightLocomotion).Length + 1);
            var left = relative == "" ? leftLocomotion : leftLocomotion.Find(relative);
            if (left == null) throw new Exception("Missing left locomotion counterpart: " + relative);
            left.gameObject.SetActive(right.gameObject.activeSelf);
            PrefabUtility.RecordPrefabInstancePropertyModifications(left.gameObject);
        }
        var realGun = gun.realgun.transform;
        Vector3 position = realGun.localPosition;
        Quaternion rotation = realGun.localRotation;
        Vector3 oldWorldPosition = realGun.position;
        Quaternion oldWorldRotation = realGun.rotation;
        var rightMount = Mount(rightModel, "Gun Mount - Right", position, rotation);
        var leftMount = Mount(leftModel, "Gun Mount - Left", new Vector3(-position.x, position.y, position.z),
            new Quaternion(rotation.x, -rotation.y, -rotation.z, rotation.w));
        Assign(gun, "leftGunMount", leftMount);
        Assign(gun, "rightGunMount", rightMount);
        realGun.SetParent(rightMount, false);
        realGun.localPosition = Vector3.zero;
        realGun.localRotation = Quaternion.identity;
        PrefabUtility.RecordPrefabInstancePropertyModifications(realGun);
        if (Vector3.Distance(realGun.position, oldWorldPosition) > .0001f || Quaternion.Angle(realGun.rotation, oldWorldRotation) > .01f)
            throw new Exception("Setup changed the existing right-hand grip.");
        var shoot = realGun.GetComponent<ShootScript>();
        var shootData = new SerializedObject(shoot);
        shootData.FindProperty("shootingInput").intValue = (int)OVRInput.Button.PrimaryIndexTrigger;
        shootData.ApplyModifiedPropertiesWithoutUndo();
        foreach (var reach in all.Select(t => t.GetComponent<RewardReachInput>()).Where(c => c != null))
            Assign(reach, "gamesManager", gm);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Validate();
    }

    public static void Validate()
    {
        if (!Application.isBatchMode) throw new InvalidOperationException("Batch validation only; changes scene state in memory.");
        int checks = 0;
        void Check(bool value, string message) { if (!value) throw new Exception(message); checks++; }
        foreach (bool left in new[] { false, true })
        {
            EditorSceneManager.OpenScene(ScenePath);
            var all = SceneTransforms();
            var gm = all.Select(t => t.GetComponent<GamesManager>()).Single(c => c != null);
            var gun = all.Select(t => t.GetComponent<GunPuzzle>()).Single(c => c != null);
            var gate = all.Select(t => t.GetComponent<AcquisitionGate>()).Single(c => c != null);
            var so = new SerializedObject(gm);
            so.FindProperty("leftHanded").boolValue = left;
            so.ApplyModifiedPropertiesWithoutUndo();
            Call(gm, "Awake");
            var active = (GameObject)Get(gm, left ? "leftInteractors" : "rightInteractors");
            var inactive = (GameObject)Get(gm, left ? "rightInteractors" : "leftInteractors");
            Check(gm.IsLeftHanded == left && gm.ActiveController == (left ? OVRInput.Controller.LTouch : OVRInput.Controller.RTouch), "Wrong selected controller");
            Check(active.activeSelf && !inactive.activeSelf, "Both hands can interact or neither can");
            Check(((GameObject)Get(gm, left ? "leftTeleportInteractor" : "rightTeleportInteractor")).activeInHierarchy, "Selected teleport is disabled");
            Check(!((GameObject)Get(gm, left ? "rightTeleportInteractor" : "leftTeleportInteractor")).activeInHierarchy, "Unused controller can teleport");
            Check(((Transform)Get(gm, "controllerReadout")).parent == gm.ActiveControllerModel, "Labyrinth timer stayed on the other hand");
            Check(gm.ActiveControllerModel.parent.gameObject.activeSelf, "Selected controller visual disabled");
            var inactiveVisual = (GameObject)Get(gm, left ? "rightControllerVisual" : "leftControllerVisual");
            Check(!inactiveVisual.activeSelf, "Unused controller visual enabled");
            foreach (var reach in all.Select(t => t.GetComponent<RewardReachInput>()).Where(c => c != null))
                Check((GamesManager)Get(reach, "gamesManager") == gm, "Reward collection missing hand selection");
            Call(gun, "AttachGunToSelectedHand");
            var mount = (Transform)Get(gun, left ? "leftGunMount" : "rightGunMount");
            Check(gun.realgun.transform.parent == mount && mount.parent == gm.ActiveControllerModel, "Gun mounted on wrong hand");
            Check(gun.realgun.transform.localPosition == Vector3.zero && Quaternion.Angle(gun.realgun.transform.localRotation, Quaternion.identity) < .01f, "Grip offset applied twice");
            Check(all.Count(t => t.GetComponent<ShootScript>() != null) == 1, "Duplicated the shooting implementation");
            Check(gun.realgun.GetComponent<ShootScript>().shootingInput == OVRInput.Button.PrimaryIndexTrigger, "Shooting still uses a raw right button");
            var muzzle = gun.realgun.GetComponent<ShootScript>().shootingpoiint;
            Check(muzzle.IsChildOf(gun.realgun.transform), "Muzzle did not follow gun");
            // Preserve exact controller model visibility across shooting and recovery.
            var children = gm.ActiveControllerModel.GetComponentsInChildren<Transform>(true)
                .Where(t => t.parent != null && t.parent.parent == gm.ActiveControllerModel && t.parent != mount).ToArray();
            var flags = children.Select(t => t.gameObject.activeSelf).ToArray();
            gun.Begin();
            Check(!active.activeSelf && !inactive.activeSelf && gm.ActiveControllerModel.parent.gameObject.activeSelf, "Shooting disabled tracking or left UI/teleport active");
            Check(children.All(t => !t.gameObject.activeSelf), "Controller geometry remained visible around gun");
            gun.Reset();
            Check(active.activeSelf && !inactive.activeSelf && flags.SequenceEqual(children.Select(t => t.gameObject.activeSelf)), "Reset failed to restore selected controller");
            gun.Begin(); gm.SetAcquisitionBlocked(true); gun.CancelForAcquisitionLoss();
            Check(!active.activeSelf && !inactive.activeSelf, "Recovery allowed controller interactions while blocked");
            gm.SetAcquisitionBlocked(false);
            Check(active.activeSelf && !inactive.activeSelf, "Recovery restored the wrong hand");
            typeof(AcquisitionGate).GetProperty("AwaitingResume").SetValue(gate, true);
            typeof(AcquisitionGate).GetMethod("UpdateStatus", Flags).Invoke(gate, new object[] {true, true});
            Check(gate.statusText.text.Contains(left ? "X + Y" : "A + B") && gate.statusText.text.Contains(left ? "LEFT" : "RIGHT"), "Recovery instructions use wrong buttons");
            so.Update(); so.FindProperty("leftHanded").boolValue = !left; so.ApplyModifiedPropertiesWithoutUndo();
            Check(gm.IsLeftHanded == left, "Inspector edit switched hands mid-session");
        }
        File.WriteAllText("Logs/handedness-validation.txt", "PASS " + checks + " checks, both hands. Hardware grip and interaction testing remains necessary.");
        Debug.Log("HANDEDNESS_PASS " + checks);
    }

    public static void ValidateExistingFlows()
    {
        SessionRecoveryValidation.Run();
        MinigameFlowValidation.Run();
    }

    public static void Inspect()
    {
        var scene = EditorSceneManager.OpenScene(ScenePath);
        using var output = new StreamWriter("Logs/handedness-inventory.txt");
        foreach (var c in scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Component>(true)))
        {
            if (c == null) continue;
            string path = PathOf(c.transform), type = c.GetType().Name;
            if (!(path.Contains("OVR") || path.Contains("ControllerAnchor") || type == "ShootScript" ||
                type == "GunPuzzle" || type.Contains("Locomot") || type.Contains("Teleport"))) continue;
            output.WriteLine(type + " | " + path + " | activeSelf=" + c.gameObject.activeSelf + " | " + GlobalObjectId.GetGlobalObjectIdSlow(c));
            if (c is Transform t)
                output.WriteLine("  pose=" + t.localPosition.ToString("F5") + " rotation=" + t.localEulerAngles.ToString("F3") + " scale=" + t.localScale.ToString("F5"));
            if (!(c is MonoBehaviour)) continue;
            var so = new SerializedObject(c);
            var p = so.GetIterator();
            while (p.NextVisible(true))
            {
                if (p.name.StartsWith("m_")) continue;
                if (p.propertyType == SerializedPropertyType.ObjectReference)
                {
                    var v = p.objectReferenceValue;
                    output.WriteLine("  " + p.propertyPath + "=" + (v is Component component ? PathOf(component.transform) : v is GameObject go ? PathOf(go.transform) : v != null ? v.name : "null"));
                }
                else if (p.propertyType == SerializedPropertyType.Boolean) output.WriteLine("  " + p.propertyPath + "=" + p.boolValue);
                else if (p.propertyType == SerializedPropertyType.Enum || p.propertyType == SerializedPropertyType.Integer) output.WriteLine("  " + p.propertyPath + "=" + p.intValue);
            }
        }
    }
}
