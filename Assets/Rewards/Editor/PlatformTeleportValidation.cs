using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Tests the production teleport helper against the saved rig/platforms in memory; never saves the scene.
public static class PlatformTeleportValidation
{
    static int checks;
    static void Check(bool ok, string why) { if (!ok) throw new Exception(why); checks++; }
    // The platform pivot is the user-positioned floor marker; no collider offset.
    static Vector3 FloorPoint(Transform platform) => platform.position;
    public static void Run()
    {
        if (!Application.isBatchMode) throw new Exception("Batch only");
        try
        {
            EditorSceneManager.OpenScene("Assets/Plugins/Scenes/AngerRoom.unity");
            var rig = UnityEngine.Object.FindObjectsOfType<OVRCameraRig>(true).Single();
            var platforms = UnityEngine.Object.FindObjectsOfType<Transform>(true).Where(t => new[] { "TheaterPlatform", "TutorialPlatform", "Lab1Platform", "Lab2Platform", "Lab3Platform" }.Contains(t.name)).ToArray();
            Check(platforms.Length == 5, "Expected five platforms");
            var manager = rig.GetComponent<OVRManager>();
            Check(new SerializedObject(manager).FindProperty("_trackingOriginType").intValue == 1, "Expected floor-level tracking");
            using (var report = new StreamWriter("Logs/platform-teleport-validation.txt"))
            {
                report.WriteLine("Rig="+rig.transform.position+" trackingSpace="+rig.trackingSpace.position+" scale="+rig.transform.lossyScale);
                foreach (var p in platforms)
                {
                    Vector3 target = FloorPoint(p), facing = Vector3.ProjectOnPlane(p.right, Vector3.up).normalized;
                    report.WriteLine(p.name+" position="+p.position.ToString("F5")+" floor="+target.ToString("F5")+" right="+p.right.ToString("F5")+" up="+p.up.ToString("F5"));
                    Check(facing.sqrMagnitude > .99f, "Platform red axis is vertical: "+p.name);
                    foreach (float height in new[] { .8f, 1.6f, 2f })
                    foreach (float yaw in new[] { -175f, -30f, 0f, 115f })
                    foreach (float offset in new[] { -1.2f, 0f, 1.2f })
                    foreach (float pitch in new[] { -90f, -25f, 25f, 90f })
                    {
                        rig.transform.SetPositionAndRotation(new Vector3(5, -3, 9), Quaternion.Euler(0, 47, 0));
                        rig.trackingSpace.localPosition = new Vector3(.1f, .25f, -.15f);
                        rig.centerEyeAnchor.localPosition = new Vector3(offset, height, -.7f);
                        rig.centerEyeAnchor.localRotation = Quaternion.Euler(pitch,yaw,12);
                        var localPosition = rig.centerEyeAnchor.localPosition; var localRotation = rig.centerEyeAnchor.localRotation;
                        float aboveFloor = rig.centerEyeAnchor.position.y - rig.trackingSpace.position.y;
                        Check(PlatformTeleport.TryTeleport(rig.gameObject, p), "Teleport rejected a valid platform");
                        Pose pose = new Pose(rig.transform.position, rig.transform.rotation);
                        Check(Mathf.Abs(rig.trackingSpace.position.y-target.y)<.0001f, "Tracking floor mismatch");
                        Check(Mathf.Abs(rig.centerEyeAnchor.position.y-target.y-aboveFloor)<.0001f, "Head height changed");
                        Check(Vector3.ProjectOnPlane(rig.centerEyeAnchor.position-target,Vector3.up).magnitude<.0001f, "Head did not land at centre");
                        Vector3 heading = Vector3.ProjectOnPlane(rig.centerEyeAnchor.forward,Vector3.up);
                        if (heading.sqrMagnitude < .000001f) heading = Vector3.Cross(rig.centerEyeAnchor.right,Vector3.up);
                        Check(Vector3.Angle(heading,facing)<.1f, "Wrong heading");
                        Check(rig.centerEyeAnchor.localPosition==localPosition && Quaternion.Angle(rig.centerEyeAnchor.localRotation,localRotation)<.001f, "Tracking pose modified");
                        Check(PlatformTeleport.TryTeleport(rig.gameObject, p), "Repeated teleport failed");
                        var repeated = new Pose(rig.transform.position,rig.transform.rotation);
                        Check(Vector3.Distance(repeated.position,pose.position)<.0001f && Quaternion.Angle(repeated.rotation,pose.rotation)<.1f, "Repeated teleport drifted");
                    }
                }
                foreach(var laser in UnityEngine.Object.FindObjectsOfType<LaserSparatoLabirinto>(true))
                    Check(laser.Giocatore==rig.gameObject && platforms.Contains(laser.InizioLabirinto),"Unexpected laser rig/destination: "+laser.name);
                foreach(var obstacle in UnityEngine.Object.FindObjectsOfType<OstacoloColpito>(true))
                    report.WriteLine("Obstacle="+obstacle.name+" player="+obstacle.Giocatore+" destination="+obstacle.InizioLabirinto);
                report.WriteLine("PASS "+checks+" production teleport checks; scene unchanged.");
            }
            Debug.Log("PLATFORM_TELEPORT_PASS "+checks);
            LabyrinthButtonValidation.Run(); // Includes debug-result and minigame-flow regression checks.
        }
        catch(Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
    }
}
