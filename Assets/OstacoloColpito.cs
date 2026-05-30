using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class OstacoloColpito : MonoBehaviour
{
    public Transform InizioLabirinto;
    //public Transform InizioLabirinto2;
    //public Transform InizioLabirinto3;
    public GameObject Giocatore;
    public AudioManager am;


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
        // Se la sfera colpisce l'oggetto con il tag player:
        if (other.CompareTag("Player"))
        {
            Debug.Log("Ostacolo colpito, torna all'inizio del labirinto!");
            Giocatore.transform.position = InizioLabirinto.position;
            Giocatore.transform.rotation = InizioLabirinto.rotation;
            am.playBad(2);
            //calcolo le distanze tra il giocatore e gli inizi dei 3 labirinti per capire velocemente in che labirinto siamo
            /*
            float distanza1 = Vector3.Distance(other.transform.position, InizioLabirinto1.position);
            float distanza2 = Vector3.Distance(other.transform.position, InizioLabirinto2.position);
            float distanza3 = Vector3.Distance(other.transform.position, InizioLabirinto3.position);
            if(distanza1 < distanza2 && distanza1 < distanza3) //sono nel labirinto1
            {
                Giocatore.transform.position = InizioLabirinto1.position;
                Giocatore.transform.rotation = InizioLabirinto1.rotation;
            }
            else if(distanza2 < distanza1 && distanza2 < distanza3) //sono nel labirinto2
            {
                Giocatore.transform.position = InizioLabirinto2.position;
                Giocatore.transform.rotation = InizioLabirinto2.rotation;
            }
            else //sono nel labirinto3
            {
                Giocatore.transform.position = InizioLabirinto3.position;
                Giocatore.transform.rotation = InizioLabirinto3.rotation;
            }
            */

        }
    }
}
