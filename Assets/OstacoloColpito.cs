using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class OstacoloColpito : MonoBehaviour
{
    public Transform InizioLabirinto;

    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    void OnTriggerEnter(Collider other)
    {
        // Verifica il tag dell'oggetto con cui sei entrato in contatto
        if (other.CompareTag("Ostacolo"))
        {
            Debug.Log("Ostacolo colpito, torna all'inizio del labirinto!");
            transform.position = InizioLabirinto.position;
            transform.rotation = InizioLabirinto.rotation;

            
        }
    }
}
