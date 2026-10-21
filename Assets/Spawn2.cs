using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;

public class Spawn2 : MonoBehaviour
{
    [SerializeField] float spawnTimer;
    //public GameObject spawn;
    public float timer;
    public GameObject target;
    [SerializeField] private bool started;
    [SerializeField] List<float> _possibleSpawPos = new List<float>();
    private List<GameObject> _spawnedTargets = new List<GameObject>();
    [SerializeField] private int targetRows = 5;
    [SerializeField] private int _excludeCount = 1;
    private List<float> _lastChosen = new List<float>();
    // Start is called before the first frame update
    void Start()
    {
        float increment = 0f;
        for (int i = 0; i< targetRows; i++)
        {
            _possibleSpawPos.Add(transform.position.y + increment);
            increment += 1f;
        }
        started = false;
    }

    public void StartSpawning()
    {
        timer = 0;
        started = true;
    }

    public void StopSpawning()
    {
        started = false;
    }
    void Update()
    {
        if (started) {
            if (timer <= 0)
            {
                timer = spawnTimer;
                SpawnTarget();
            }
            timer -= Time.deltaTime;
        }
    }

    public float GetRandomTransformYValue()
    {
    List<float> availableValues = _possibleSpawPos.Where(v => !_lastChosen.Contains(v)).ToList();
    float chosen = availableValues[Random.Range(0, availableValues.Count)];
    _lastChosen.Add(chosen);
    if (_lastChosen.Count > _excludeCount)
        _lastChosen.RemoveAt(0);

    return chosen;
    }
    public void SpawnTarget() {
        //Vector3 spawnPos = new Vector3(transform.position.x, transform.position.y + Random.Range(-1.5f, 1.5f), transform.position.z);
        Vector3 spawnPos = new Vector3(transform.position.x, GetRandomTransformYValue(), transform.position.z);
        GameObject instance = Instantiate(target, spawnPos, Quaternion.identity);
        _spawnedTargets.Add(instance);
    }

    public void ClearTargets()
    {
        foreach (GameObject target in _spawnedTargets)
        {
            if (target != null)
            {
                Destroy(target);
            }
        }
        _spawnedTargets.Clear();
    }
    public void setSpawnTime(float newTime)
    {
        spawnTimer = newTime;
    }
}
