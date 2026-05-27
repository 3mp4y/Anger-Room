using System.Collections;
using System.Collections.Generic;
using UnityEngine;


public class Movimento_sfera : MonoBehaviour
{
    public float velocita = 3f;
    public float distanza = 5f;
    private Vector3 posizioneIniziale;

    void Start()
    {
        posizioneIniziale = transform.position;
    }

    void Update()
    {
        // Calcola l'offset oscillante
        float movimento = Mathf.PingPong(Time.time * velocita, distanza);

        // Applica il movimento sull'asse X
        transform.position = posizioneIniziale + transform.right * movimento;
    }
}