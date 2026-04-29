using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class LabyrinthScript : MonoBehaviour
{
    public OVRInput.RawButton utton;
    public GameObject cam1;
    public GameObject player;

    public GameObject newPos;
    // Start is called before the first frame update
    void Start()
    {
    
    }
    public void StartCor()
    {
        StartCoroutine(ShowMessages());
    }

    IEnumerator ShowMessages()
    {
        cam1.SetActive(true);
        yield return new WaitForSeconds(1f); // wait 3 seconds
        player.transform.position = new Vector3 (newPos.transform.position.x, player.transform.position.y, newPos.transform.position.z);
        yield return new WaitForSeconds(1.3f); // wait 3 seconds
        cam1.SetActive(false);
    }   
    // Update is called once per frame
    void Update()
    {
    }
}
