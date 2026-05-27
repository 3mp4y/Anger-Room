using System.Collections;
using System.Collections.Generic;
using System.Collections;
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

        // Spawn VFX
        if (hitVFXPrefab != null)
            Instantiate(hitVFXPrefab, transform.position, Quaternion.identity);

        // Play sound
        if (hitSound != null && _audioSource != null)
            _audioSource.PlayOneShot(hitSound);

        Destroy(gameObject);
    }


}
