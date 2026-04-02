using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Spawn2 : MonoBehaviour
{
    public float spawnTimer = 1;
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
    }
    public void SpawnTarget() {
        Vector3 spawnPos = new Vector3(0f, Random.Range(1.5f, 3f), -5.7f);
        //Vector3 spawnPos = new Vector3(0f, 1f, 0f);
        Instantiate(target, spawnPos, Quaternion.identity);
    }
}
