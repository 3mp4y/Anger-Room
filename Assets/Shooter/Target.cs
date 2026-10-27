using UnityEngine;

public class Target : MonoBehaviour
{
    [Header("Settings")]
    public float lifetime = 5f;           // Auto-destroy if not shot
    public int scoreValue = 10;

    [Header("Hit Effect")]
    public GameObject hitVFXPrefab;       // Optional particle on hit
    public AudioClip hitSound;

    private AudioSource _audioSource; 
    private bool _hasBeenHit = false;

    private void Start()
    {
        _audioSource = GetComponent<AudioSource>();
        // Auto-destroy after lifetime
        Destroy(gameObject, lifetime);
    }

    public void OnHit()
    {
        // Compatibility entry point for existing UnityEvents. Callers that award
        // points must use TryHit(), because they need to know if it was accepted.
        TryHit();
    }

    public bool TryHit()
    {
        // Destroy is deferred until the end of the Update loop. Record acceptance
        // BEFORE effects or destruction, so another call in this frame is rejected.
        if (_hasBeenHit)
        {
            return false;
        }

        _hasBeenHit = true;

        // Remove the target from subsequent physics queries immediately, including
        // colliders on children. The boolean also protects direct method calls.
        foreach (Collider targetCollider in GetComponentsInChildren<Collider>(true))
        {
            targetCollider.enabled = false;
        }

        // Spawn VFX
        if (hitVFXPrefab != null)
            Instantiate(hitVFXPrefab, transform.position, Quaternion.identity);

        // Unity creates a separate temporary AudioSource at this world position
        // and cleans it up after playback. Destroying this target won't cut it off.
        // Preserve the old source's volume; other settings (mixer, pitch, distance
        // rolloff, etc.) are NOT copied by PlayClipAtPoint. Test the 3D sound in VR.
        if (hitSound != null)
        {
            float volume = _audioSource != null ? _audioSource.volume : 1f;
            AudioSource.PlayClipAtPoint(hitSound, transform.position, volume);
        }

        Destroy(gameObject);
        return true;
    }


}
