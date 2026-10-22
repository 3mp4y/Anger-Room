using TMPro;
using UnityEngine;

namespace AngerRoom.Localization.Examples
{
    // Optional proposal for a NEW static room label. Nothing searches for or
    // changes existing Dialogue objects. Attach this only to a chosen label.
    // A gameplay script should own changing text and call TextAdapter.Format.
    [DisallowMultipleComponent]
    [AddComponentMenu("Anger Room/Examples/Room Text Usage Example")]
    public sealed class RoomTextUsageExample : MonoBehaviour
    {
        [SerializeField] private TextAdapter textAdapter;
        [SerializeField] private TMP_Text target;
        [Tooltip("Choose a plain message without placeholders such as {0}.")]
        [SerializeField] private TextId textId = TextId.None;

        private void Start()
        {
            if (textAdapter == null || target == null || textId == TextId.None)
            {
                Debug.LogError(
                    "Assign Text Adapter, Target, and a Text Id on this room label.", this);
                enabled = false;
                return;
            }

            // Start also runs when an initially inactive label is first enabled.
            // The shared adapter has already captured the session's language,
            // or initializes on this request if it has not been accessed yet.
            target.text = textAdapter.Get(textId);
        }
    }
}
