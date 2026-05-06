using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class LabyrinthScript : MonoBehaviour
{
    public OVRInput.RawButton utton;
    public GameObject blacked;
    public GameObject player;
    public GameObject newPos;
    // Start is called before the first frame update
    void Start()
    {
    
    }
    public void StartFirstLabirinth()
    {
        StartCoroutine(ShowMessages());
    }

    IEnumerator ShowMessages()
    {
        blacked.SetActive(true);
        yield return new WaitForSeconds(2f); // wait 3 seconds
        //player.transform.position = new Vector3 (newPos.transform.position.x, player.transform.position.y, newPos.transform.position.z);
        player.transform.SetPositionAndRotation(new Vector3 (newPos.transform.position.x, player.transform.position.y, newPos.transform.position.z), player.transform.rotation);
        blacked.SetActive(false);
    }   
    // Update is called once per frame
    void Update()
    {
    }
}
