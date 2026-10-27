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
        // Spawn2 configures this instance immediately after Instantiate, before
        // Start runs. Pick the first speed from those supplied settings. Keeping
        // this initialization in Start (not Awake) allows the spawner to supply
        // them first. Standalone targets can still use their Inspector values.
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

        // Move along local X, keeping the direction chosen at startup.
        transform.Translate(Vector3.right * _currentSpeed * direction * Time.deltaTime, Space.Self);
    }

    public void SetSpeeds(float min, float max, float interval)
    {
        // Store settings on THIS target instance. Spawn2 calls this once before
        // Start; we do not consult the prefab or spawner again during movement.
        // Later spawner configuration changes therefore affect only new targets.
        // If another caller reconfigures a live target, its current speed lasts
        // until the next speed randomization; this method only changes the range
        // and interval. GunPuzzle no longer calls this on the prefab asset.
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
