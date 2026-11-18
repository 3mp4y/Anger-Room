using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class LabyrinthLaserSetup
{
    const string ScenePath = "Assets/Plugins/Scenes/AngerRoom.unity";
    static int checks;
    static void Check(bool ok, string why) { if (!ok) throw new Exception(why); checks++; }
    static string PathOf(Transform t) => t.parent == null ? t.name : PathOf(t.parent) + "/" + t.name;
    static Transform[] All() => UnityEngine.Object.FindObjectsOfType<Transform>(true);
    static bool WallGeometry(Collider c) => c.name.StartsWith("Wall_") || c.name.StartsWith("Pilar") || c.name.StartsWith("Gap_");
    static void CheckNoOldReferences(Transform old)
    {
        foreach (var t in All().Where(t => !t.IsChildOf(old)))
        foreach (var c in t.GetComponents<Component>().Where(c => c != null && !(c is Transform)))
        {
            var so = new SerializedObject(c); var p=so.GetIterator();
            while(p.NextVisible(true))
            {
                if(p.propertyType!=SerializedPropertyType.ObjectReference)continue;
                var value=p.objectReferenceValue;
                Transform target=value is GameObject go ? go.transform : value is Component component ? component.transform : null;
                if(target!=null && target.IsChildOf(old))throw new Exception("Reference into archived maze: "+PathOf(t)+"."+p.propertyPath+" -> "+PathOf(target));
            }
        }
        checks++;
    }
    public static void ConfigureAndValidate()
    {
        if (!Application.isBatchMode) throw new Exception("Batch only");
        try
        {
            var scene = EditorSceneManager.OpenScene(ScenePath);
            var all = All(); var old = all.Single(t => t.name == "Labirinto3 - OLD");
            CheckNoOldReferences(old);
            old.gameObject.SetActive(false);
            old.gameObject.tag = "EditorOnly";
            EditorUtility.SetDirty(old.gameObject);
            var roots = all.Where(t => new[] { "Labirinto1", "Labirinto2", "Labirinto3", "LabirintoTutorial" }.Contains(t.name)).ToArray();
            Check(roots.Length == 4, "Expected tutorial and three current maze roots");
            int markers = 0;
            foreach (var root in roots)
            foreach (var wall in root.GetComponentsInChildren<Collider>(true).Where(WallGeometry).OrderBy(c => c.GetComponentsInParent<Transform>(true).Length))
            {
                if (wall.GetComponentInParent<LabyrinthLaserWall>(true) != null) continue;
                wall.gameObject.AddComponent<LabyrinthLaserWall>(); markers++;
            }
            EditorSceneManager.MarkSceneDirty(scene); Check(EditorSceneManager.SaveScene(scene), "Scene save failed");
            EditorSceneManager.OpenScene(ScenePath);
            all = All(); old = all.Single(t => t.name == "Labirinto3 - OLD");
            Check(!old.gameObject.activeSelf && old.CompareTag("EditorOnly"), "Archive active or still included in builds");
            CheckNoOldReferences(old);
            int wallCount=0, sphereCount=0;
            foreach (var root in all.Where(t => new[] { "Labirinto1", "Labirinto2", "Labirinto3", "LabirintoTutorial" }.Contains(t.name)))
            foreach(var c in root.GetComponentsInChildren<Collider>(true))
            {
                bool marked = c.GetComponentInParent<LabyrinthLaserWall>(true) != null;
                Check(marked == WallGeometry(c), "Wrong wall classification: "+PathOf(c.transform));
                if (marked) wallCount++;
                if (c.GetComponentInParent<OstacoloColpito>(true) != null) sphereCount++;
            }
            foreach (var laser in UnityEngine.Object.FindObjectsOfType<LaserSparatoLabirinto>(true).Where(l => !l.transform.IsChildOf(old)))
                Check(laser.Player.value == (1 << LayerMask.NameToLayer("Player")), "Laser mask includes non-player layers");
            PhysicsChecks();
            File.WriteAllText("Logs/labyrinth-laser-validation.txt", "PASS "+checks+" checks; "+markers+" wall markers added, covering "+wallCount+" wall colliders; "+sphereCount+" sphere colliders remain unmarked. Old maze inactive, EditorOnly, no external component references.");
            Debug.Log("LABYRINTH_LASER_PASS "+checks);
            LabyrinthButtonValidation.Run();
        }
        catch(Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
    }
    static void PhysicsChecks()
    {
        var fixture = new GameObject("Laser physics test");
        Vector3 origin = new Vector3(10000,10000,10000);
        bool triggers = Physics.queriesHitTriggers;
        try
        {
            var laser = fixture.AddComponent<LaserSparatoLabirinto>(); laser.Player = 1 << 7; laser.maxLineDistance=50;
            var wallRoot = new GameObject("Marked wall"); wallRoot.transform.SetParent(fixture.transform);
            var marker = wallRoot.AddComponent<LabyrinthLaserWall>();
            var wall = Box(wallRoot.transform, origin+Vector3.forward*12, 0, false);
            var player = Box(fixture.transform, origin+Vector3.forward*18, 7, true);
            var visible = new GameObject("Visible sphere"); visible.transform.SetParent(fixture.transform);
            visible.transform.position=origin+Vector3.forward*3; visible.AddComponent<SphereCollider>().isTrigger=true; visible.AddComponent<OstacoloColpito>();
            var invisible = new GameObject("Invisible sphere"); invisible.transform.SetParent(fixture.transform);
            invisible.layer=2; invisible.transform.position=origin+Vector3.forward*6; invisible.AddComponent<SphereCollider>().isTrigger=true; invisible.AddComponent<OstacoloColpito>();
            Box(fixture.transform,origin+Vector3.forward,0,false); // Ordinary prop must not block.
            Ray ray=new Ray(origin,Vector3.forward);
            Physics.queriesHitTriggers=false; Physics.SyncTransforms();
            Check(laser.TryGetBlockingHit(ray,out var hit) && hit.collider==wall, "Wall behind spheres did not block player");
            Check(Mathf.Abs(hit.distance-11.5f)<.01f, "Beam endpoint is not nearest wall surface");
            wall.transform.position=origin+Vector3.forward*25; Physics.SyncTransforms();
            Check(laser.TryGetBlockingHit(ray,out hit) && hit.collider==player, "Player in front of wall was missed");
            player.gameObject.SetActive(false); marker.enabled=false; Physics.SyncTransforms();
            Check(!laser.TryGetBlockingHit(ray,out hit), "Sphere/prop incorrectly stopped laser");
            marker.enabled=true; player.gameObject.SetActive(true); wall.transform.position=origin+Vector3.forward*12;
            laser.maxLineDistance=4; Physics.SyncTransforms();
            Check(!laser.TryGetBlockingHit(ray,out hit), "Hit beyond maximum beam range");
            laser.maxLineDistance=50;
            for(int i=0;i<40;i++)
            {
                var clutter=Box(fixture.transform,origin+Vector3.forward*(1+i*.2f),0,false);
                clutter.transform.localScale=Vector3.one*.1f;
            }
            Physics.SyncTransforms();
            Check(laser.TryGetBlockingHit(ray,out hit) && hit.collider==wall, "Full hit buffer lost the blocking wall");
            player.transform.position=origin+Vector3.forward*10; Physics.SyncTransforms();
            Check(laser.TryGetBlockingHit(ray,out hit) && hit.collider==player, "Dense-hit query selected wall behind player");
        }
        finally { Physics.queriesHitTriggers=triggers; UnityEngine.Object.DestroyImmediate(fixture); }
    }
    static BoxCollider Box(Transform parent, Vector3 position, int layer, bool trigger)
    {
        var go=new GameObject("Test collider"); go.layer=layer; go.transform.SetParent(parent); go.transform.position=position;
        var collider=go.AddComponent<BoxCollider>(); collider.isTrigger=trigger;return collider;
    }
}
