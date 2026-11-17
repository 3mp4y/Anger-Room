using UnityEngine;

namespace AngerRoom.Rewards
{
    [DisallowMultipleComponent]
    public sealed class RewardReachInput : MonoBehaviour
    {
        [Tooltip("TrackingSpace of the scene's OVRCameraRig. Keeps reach tests correct after teleporting.")]
        [SerializeField] private Transform trackingSpace;
        [SerializeField] private GamesManager gamesManager;

        public bool IsReaching(Vector3 worldPosition, float radius)
        {
            if (trackingSpace == null || gamesManager == null) return false;
            // This experiment uses one controller. An unused controller or a
            // hand wearing sensors must not accidentally claim a reward.
            return IsNear(gamesManager.ActiveController, worldPosition, radius);
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
