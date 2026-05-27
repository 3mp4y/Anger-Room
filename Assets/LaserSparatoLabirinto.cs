using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class LaserSparatoLabirinto : MonoBehaviour
{
    public Transform OrigineLaser;
    public float maxLineDistance = 20;
    public LayerMask Player;
    public LayerMask Any;
    public GameObject Giocatore;
    public Transform InizioLabirinto;
    public LineRenderer lineRend;
    public float lineTime = 0.3f;
    public float intervalloSparo = 5f; //tempo tra un laser e l'altro
    public float timer = 0f;

    AudioSource thisAudio;
    // Start is called before the first frame update
    void Start()
    {
        thisAudio = GetComponent<AudioSource>();
    }

    // Update is called once per frame
    void Update()
    {
        // Contatore per sparare a intervalli regolari
        timer += Time.deltaTime;
        if (timer >= intervalloSparo)
        {
            Shoot();
            timer = 0f; // azzero il timer dopo aver sparato
        }
    }

    public void Shoot()
    {
        thisAudio.Play();
        Ray ray = new Ray(OrigineLaser.position, OrigineLaser.forward);
        bool hasHitAny = Physics.Raycast(ray, out RaycastHit hitAny, maxLineDistance, Any);
        bool hasHit = Physics.Raycast(ray, out RaycastHit hit, maxLineDistance, Player);
        Vector3 endPoint = Vector3.zero;
        if (hasHitAny)
        {
            endPoint = hitAny.point;
            if (hasHit)
            {
            Giocatore.transform.position = new Vector3 (InizioLabirinto.position.x, Giocatore.transform.position.y, InizioLabirinto.position.z);
            }
        }
        else //se non colpisce il giocatore, va dritto fino alla fine
        {
            endPoint = OrigineLaser.position + OrigineLaser.forward * maxLineDistance;
        }
        
        LineRenderer line = Instantiate(lineRend);
        line.positionCount = 2;
        line.SetPosition(0, OrigineLaser.position);
        line.SetPosition(1, endPoint);
        line.startWidth = 0.3f;
        //line.endWidth = 0.05f;
        Destroy(line.gameObject, lineTime);


    }


}
