using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Spawn2 : MonoBehaviour
{
    public float spawnTimer;
    //public GameObject spawn;
    public float timer;
    public GameObject target;
    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        timer += Time.deltaTime;
        if (timer > spawnTimer)
        {
            SpawnTarget();
            timer -= spawnTimer;
        }

        //Debug.Log(Random.Range(-1.5f, 1.5f));
    }
    public void SpawnTarget() {
        Vector3 spawnPos = new Vector3(transform.localPosition.x, transform.localPosition.y + Random.Range(-1.5f, 1.5f), transform.localPosition.z);
        //Vector3 spawnPos = new Vector3(spawn.transform.localPosition.x, Random.Range(1.5f, 3f), spawn.transform.localPosition.z);
        Instantiate(target, spawnPos, Quaternion.identity);
    }
}
