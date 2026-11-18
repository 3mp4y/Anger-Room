using UnityEngine;

// Put this on a maze wall/pillar/doorway root. Its child colliders stop lasers.
// Mark the wall prefab instance, not the whole maze: floors, obstacle spheres,
// buttons and laser emitters must remain unmarked. Duplicating a marked wall
// in Unity keeps this component, so the copy also blocks lasers.
[DisallowMultipleComponent]
[AddComponentMenu("Anger Room/Labyrinth Laser Wall")]
public sealed class LabyrinthLaserWall : MonoBehaviour
{
}
