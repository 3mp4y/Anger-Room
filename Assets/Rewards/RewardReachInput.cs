using UnityEngine;

namespace AngerRoom.Rewards
{
    [DisallowMultipleComponent]
    public sealed class RewardReachInput : MonoBehaviour
    {
        [Tooltip("TrackingSpace of the scene's OVRCameraRig. Keeps reach tests correct after teleporting.")]
        [SerializeField] private Transform trackingSpace;

        public bool IsReaching(Vector3 worldPosition, float radius)
        {
            if (trackingSpace == null) return false;
            return IsNear(OVRInput.Controller.LHand, worldPosition, radius) ||
                   IsNear(OVRInput.Controller.RHand, worldPosition, radius) ||
                   IsNear(OVRInput.Controller.LTouch, worldPosition, radius) ||
                   IsNear(OVRInput.Controller.RTouch, worldPosition, radius);
        }

        private bool IsNear(OVRInput.Controller controller, Vector3 target, float radius)
        {
            // Lost tracking must not leave a stale hand position that collects a prize.
            if (!OVRInput.GetControllerPositionTracked(controller)) return false;
            Vector3 worldHand = trackingSpace.TransformPoint(OVRInput.GetLocalControllerPosition(controller));
            return (worldHand - target).sqrMagnitude <= radius * radius;
        }
    }
}
