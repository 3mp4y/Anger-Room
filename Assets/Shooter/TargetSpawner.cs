using System.Collections;
using System.Collections.Generic;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TargetSpawner : MonoBehaviour
{ 
    public static TargetSpawner Instance { get; private set; }

    [Header("Spawning")]
    public GameObject targetPrefab;
    [Tooltip("Min time between spawns in seconds")]
    public float minSpawnInterval = 1.0f;
    [Tooltip("Max time between spawns in seconds")]
    public float maxSpawnInterval = 3.0f;

    [Header("Spawn Area (relative to this transform)")]
    public Vector3 spawnAreaMin = new Vector3(-3f, 0.5f, 1f);
    public Vector3 spawnAreaMax = new Vector3(3f, 2.5f, 4f);

    public Vector3 targetSpawnRotation = new Vector3(0f, 0f, 0f);

    [Header("Limits")]
    public int maxActiveTargets = 10;

    private Coroutine _spawnCoroutine;
    private List<GameObject> _activeTargets = new List<GameObject>();

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    public void StartSpawning()
    {
        if (_spawnCoroutine != null) StopCoroutine(_spawnCoroutine);
        _spawnCoroutine = StartCoroutine(SpawnLoop());
    }

    public void StopSpawning()
    {
        if (_spawnCoroutine != null)
        {
            StopCoroutine(_spawnCoroutine);
            _spawnCoroutine = null;
        }
        DestroyAllTargets();
    }

    private IEnumerator SpawnLoop()
    {
        while (true)
        {
            // Clean up destroyed targets
            _activeTargets.RemoveAll(t => t == null);

            if (_activeTargets.Count < maxActiveTargets)
            {
                SpawnTarget();
            }

            float waitTime = Random.Range(minSpawnInterval, maxSpawnInterval);
            yield return new WaitForSeconds(waitTime);
        }
    }

    private void SpawnTarget()
    {
        if (targetPrefab == null) return;

        Vector3 spawnPos = new Vector3(
            Random.Range(0f, 0f),
            Random.Range(spawnAreaMin.y, spawnAreaMax.y),
            Random.Range(0f, 0f)
        );

        // Convert to world space
        spawnPos = transform.TransformPoint(spawnPos);
        Quaternion spawnRot = Quaternion.Euler(targetSpawnRotation);

        GameObject t = Instantiate(targetPrefab, spawnPos, spawnRot);
        _activeTargets.Add(t);
    }

    private void DestroyAllTargets()
    {
        foreach (var t in _activeTargets)
            if (t != null) Destroy(t);
        _activeTargets.Clear();
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0, 1, 0, 0.3f);
        Vector3 center = transform.TransformPoint((spawnAreaMin + spawnAreaMax) / 2f);
        Vector3 size = spawnAreaMax - spawnAreaMin;
        Gizmos.DrawCube(center, size);
    }
}
