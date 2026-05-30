using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Spawn2 : MonoBehaviour
{
    [SerializeField] float spawnTimer;
    //public GameObject spawn;
    public float timer;
    public GameObject target;
    public bool started;
    // Start is called before the first frame update
    void Start()
    {
        started = false;
    }

    // Update is called once per frame
    void Update()
    {
        if (started) {
            timer += Time.deltaTime;
            if (timer > spawnTimer)
            {
                SpawnTarget();
                timer -= spawnTimer;
            }
        }
        //Debug.Log(Random.Range(-1.5f, 1.5f));
    }
    public void SpawnTarget() {
        Vector3 spawnPos = new Vector3(transform.position.x, transform.position.y + Random.Range(-1.5f, 1.5f), transform.position.z);
        //Vector3 spawnPos = new Vector3(spawn.transform.localPosition.x, Random.Range(1.5f, 3f), spawn.transform.localPosition.z);
        Instantiate(target, spawnPos, Quaternion.identity);
    }
    public void setSpawnTime(float newTime)
    {
        spawnTimer = newTime;
    }
}
