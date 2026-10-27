using UnityEngine;

public class LaserSparatoLabirinto : MonoBehaviour
{
    public AudioManager am;
    [SerializeField] Logger logger;
    public Transform OrigineLaser;
    public float maxLineDistance = 20;
    [Tooltip("Layers identifying player colliders. All layers can block the laser.")]
    public LayerMask Player;
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
        // One raycast supplies both the visible endpoint and the gameplay hit.
        // All layers can block the beam; triggers follow the project physics setting.
        // A separate player-only ray would ignore walls and could hit through them.
        bool hasHitAny = Physics.Raycast(ray, out RaycastHit hitAny, maxLineDistance, Physics.AllLayers);
        Vector3 endPoint = ray.GetPoint(maxLineDistance);
        if (hasHitAny)
        {
            endPoint = hitAny.point;

            // A LayerMask is a set of layers. Shift 1 to the hit collider's layer,
            // then use & to check whether that layer belongs to the Player mask.
            int hitLayer = hitAny.collider.gameObject.layer;
            bool hitPlayer = (Player.value & (1 << hitLayer)) != 0;
            if (hitPlayer)
            {
                am.playBad(2);
                logger.Log(gameObject.name);
                Giocatore.transform.position = new Vector3 (InizioLabirinto.position.x, Giocatore.transform.position.y, InizioLabirinto.position.z);
            }
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
