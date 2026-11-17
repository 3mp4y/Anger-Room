using UnityEngine;

// Shared by labyrinth transitions, laser hits and obstacle hits.
// This project uses Meta's FloorLevel tracking origin: TrackingSpace's origin
// represents the participant's real floor, not the headset's eye height.
public static class PlatformTeleport
{
    public static bool TryTeleport(GameObject player, Transform platform)
    {
        var rig = player != null ? player.GetComponent<OVRCameraRig>() : null;
        if (rig == null || rig.centerEyeAnchor == null || rig.trackingSpace == null || platform == null)
        {
            Debug.LogError("Platform teleport needs the camera rig and a destination platform.", player);
            return false;
        }

        // The user positions the platform PIVOT at floor level. Do not read its
        // collider bounds or add half its thickness: the pivot is the destination.
        Vector3 destination = platform.position;
        // Unity's local red arrow is +X (right), not +Z (forward).
        // Flatten it so a platform cannot pitch or roll the participant's world.
        Vector3 facing = Vector3.ProjectOnPlane(platform.right, Vector3.up);
        if (facing.sqrMagnitude < .000001f)
        {
            Debug.LogError("Platform's red arrow must have a horizontal direction.", platform);
            return false;
        }

        Transform root = rig.transform;
        Transform head = rig.centerEyeAnchor;
        Vector3 headForward = Vector3.ProjectOnPlane(head.forward, Vector3.up);
        // Looking exactly up/down makes forward's horizontal projection vanish.
        // The head's right axis still gives a usable horizontal heading then.
        if (headForward.sqrMagnitude < .000001f)
            headForward = Vector3.Cross(head.right, Vector3.up);

        Quaternion yaw = Quaternion.AngleAxis(
            Vector3.SignedAngle(headForward, facing, Vector3.up), Vector3.up);
        Vector3 rotatedHeadOffset = yaw * (head.position - root.position);
        float trackingFloorOffset = rig.trackingSpace.position.y - root.position.y;

        // Put the HEAD's horizontal position over the marker, compensating for
        // physical movement within the play area. Put the TRACKING FLOOR at the
        // marker's Y. This preserves the participant's actual height/crouch.
        Vector3 rootPosition = new Vector3(
            destination.x - rotatedHeadOffset.x,
            destination.y - trackingFloorOffset,
            destination.z - rotatedHeadOffset.z);

        // Move only the rig. Never overwrite the tracked camera/controller poses.
        // Subtract the current head heading so the VIEW faces red even if the
        // participant had turned their head relative to the rig before teleporting.
        root.SetPositionAndRotation(rootPosition, yaw * root.rotation);
        Physics.SyncTransforms();
        return true;
    }
}
