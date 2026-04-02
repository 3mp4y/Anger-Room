using System.Collections;
using UnityEngine;

public class TargetMover : MonoBehaviour
{
    [Header("Movement")]
    [Tooltip("Minimum movement speed (units/sec)")]
    public float minSpeed = 0.5f;
    [Tooltip("Maximum movement speed (units/sec)")]
    public float maxSpeed = 2.0f;
    [Tooltip("How long the target moves before stopping")]
    public float moveDuration = 5f;
    [Tooltip("How often the speed randomizes (seconds)")]
    public float speedChangeInterval = 1f;

    private float _currentSpeed;
    private float _timeAlive = 0f;
    private float _timeSinceSpeedChange = 0f;
    private bool _isMoving = true;

    private void Start()
    {
        // Pick an initial speed immediately on spawn
        _currentSpeed = Random.Range(minSpeed, maxSpeed);
    }

    private void Update()
    {
        if (!_isMoving) return;

        _timeAlive += Time.deltaTime;
        _timeSinceSpeedChange += Time.deltaTime;

        // Stop after moveDuration
        if (_timeAlive >= moveDuration)
        {
            _isMoving = false;
            return;
        }

        // Randomize speed every speedChangeInterval seconds
        if (_timeSinceSpeedChange >= speedChangeInterval)
        {
            _currentSpeed = Random.Range(minSpeed, maxSpeed);
            _timeSinceSpeedChange = 0f;
        }

        // Move along local Z axis
        transform.Translate(Vector3.right * _currentSpeed * Time.deltaTime, Space.Self);
    }
}