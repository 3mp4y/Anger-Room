using System.Collections;
using UnityEngine;

public class TargetMover : MonoBehaviour
{
    [Header("Movement")]
    [Tooltip("Minimum movement speed (units/sec)")]
    public float minSpeed;
    [Tooltip("Maximum movement speed (units/sec)")]
    public float maxSpeed;
    [Tooltip("How often the speed randomizes (seconds)")]
    public float speedChangeInterval;

    private float _currentSpeed;
    private float _timeAlive = 0f;
    private float _timeSinceSpeedChange = 0f;
    private bool _isMoving = true;

    private int direction;

    private void Start()
    {
        // Pick an initial speed immediately on spawn
        _currentSpeed = Random.Range(minSpeed, maxSpeed);
        if (Random.Range(0,101) > 50)
        {
            direction = 1;
        }
        else
        {
            direction = -1;
        }
    }

    private void Update()
    {
        if (!_isMoving) return;

        _timeAlive += Time.deltaTime;
        _timeSinceSpeedChange += Time.deltaTime;

        // Randomize speed every speedChangeInterval seconds
        if (_timeSinceSpeedChange >= speedChangeInterval)
        {
            _currentSpeed = Random.Range(minSpeed, maxSpeed);
            _timeSinceSpeedChange = 0f;
        }

        // Move along local Z axis
        transform.Translate(Vector3.right * _currentSpeed * direction * Time.deltaTime, Space.Self);
    }

    public void SetSpeeds(float min, float max, float interval)
    {
        minSpeed = min;
        maxSpeed = max;
        speedChangeInterval = interval;
    }  

    public float getMin()
    {
        return minSpeed;
    }

    public float getMax()
    {
        return maxSpeed;
    }
    public float getInt()
    {
        return speedChangeInterval;
    }
}