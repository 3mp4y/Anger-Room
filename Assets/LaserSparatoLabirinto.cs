using UnityEngine;

public class LaserSparatoLabirinto : MonoBehaviour
{
    public AudioManager am;
    [SerializeField] Logger logger;
    public Transform OrigineLaser;
    public float maxLineDistance = 20;
    [Tooltip("Layers identifying player colliders. Lasers stop only at these colliders or a LabyrinthLaserWall.")]
    public LayerMask Player;
    public GameObject Giocatore;
    public Transform InizioLabirinto;
    public LineRenderer lineRend;
    public float lineTime = 0.3f;
    public float intervalloSparo = 5f; //tempo tra un laser e l'altro
    public float timer = 0f;
    [SerializeField] float _lineStartingWidth;

    AudioSource thisAudio;
    private RaycastHit[] hitBuffer = new RaycastHit[16];

    // Raycasts can report a sphere before the wall/player behind it. Inspect
    // all hits and choose the nearest VALID blocker; a separate player-only
    // ray would incorrectly detect participants through walls.
    public bool TryGetBlockingHit(Ray ray, out RaycastHit blockingHit)
    {
        int count;
        // NonAlloc results are not sorted, and a full buffer can omit a closer
        // wall. Grow and repeat until every hit fits. Subsequent shots reuse it.
        while ((count = Physics.RaycastNonAlloc(ray, hitBuffer, maxLineDistance,
            Physics.AllLayers, QueryTriggerInteraction.Collide)) == hitBuffer.Length)
            System.Array.Resize(ref hitBuffer, hitBuffer.Length * 2);

        blockingHit = default;
        float nearest = float.PositiveInfinity;
        for (int i = 0; i < count; i++)
        {
            Collider collider = hitBuffer[i].collider;
            bool isPlayer = (Player.value & (1 << collider.gameObject.layer)) != 0;
            var wall = collider.GetComponentInParent<LabyrinthLaserWall>();
            if (!isPlayer && (wall == null || !wall.enabled)) continue;
            if (hitBuffer[i].distance >= nearest) continue;
            blockingHit = hitBuffer[i];
            nearest = blockingHit.distance;
        }
        return nearest < float.PositiveInfinity;
    }
    // Start is called before the first frame update
    void Start()
    {
        thisAudio = GetComponent<AudioSource>();
    }

    // Update is called once per frame
    void Update()
    {
        if (AngerRoom.Shimmer.AcquisitionGate.GameplayBlocked) return;
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
        if (AngerRoom.Shimmer.AcquisitionGate.GameplayBlocked) return;
        thisAudio.Play();
        Ray ray = new Ray(OrigineLaser.position, OrigineLaser.forward);
        // Sphere obstacles still collide with the participant, but are not
        // laser blockers. The selected hit drives BOTH the beam and the reset.
        bool hasHitAny = TryGetBlockingHit(ray, out RaycastHit hitAny);
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
                PlatformTeleport.TryTeleport(Giocatore, InizioLabirinto);
            }
        }
        
        LineRenderer line = Instantiate(lineRend);
        line.positionCount = 2;
        line.SetPosition(0, OrigineLaser.position);
        line.SetPosition(1, endPoint);
        line.startWidth = _lineStartingWidth;
        //line.endWidth = 0.05f;
        Destroy(line.gameObject, lineTime);


    }


}
