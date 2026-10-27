using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ShootScript : MonoBehaviour
{
    public AudioManager am;
    [SerializeField] Logger logger;
    public ParticleSystem smoke;
    public LayerMask mask;
    public LayerMask endTut;
    public OVRInput.RawButton shootingButton;
    public LineRenderer lineRend;
    public Transform shootingpoiint;
    public float maxLineDistance;
    public float lineTime = 0.3f;
    private bool reload = false;
    [SerializeField] [Range(0, 100)] public int failureChance; 
    private float timer;
    public float reloadTime = 1;
    public GunPuzzle gunpuz;
    public int FailureChance
{
    get => failureChance;
    set => failureChance = Mathf.Clamp(value, 0, 100);
}


    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        if (reload)
        {
            timer += Time.deltaTime;
        if (timer > reloadTime)
        {
            reload = false;
            timer = 0;
        }
        }

        if (OVRInput.GetDown(shootingButton) && !reload)
        {
            Shoot();
            smoke.Play();
            //smoke.Emit(1000);
        }
    }

    public void Shoot()
    {
        reload = true;
        am.playShot();
        Ray ray = new Ray(shootingpoiint.position, shootingpoiint.forward); //Inizializzi un laser "ray", position da dove parte il laser, forwward è la direzione BLU
        bool hasHit = Physics.Raycast(ray, out RaycastHit hit, maxLineDistance, mask); //Controlla se il laser ray ha colpito qualcosa entro distanza maxLineDistance e che ha layer "mask", definito da noi a inizio codice
        bool finishTut = Physics.Raycast(ray, out RaycastHit hit2, maxLineDistance, endTut); //Controlla se il laser ray ha colpito qualcosa entro distanza maxLineDistance e che ha layer "mask", definito da noi a inizio codice
        Vector3 endPoint = Vector3.zero;
        if (gunpuz.GetGunTutorial())
        {
            if (finishTut)
            {
            endPoint = hit2.point;
            gunpuz.Endtutorial();
            }
            else if (hasHit) 
            { //codice dove decidiamo che succede se viene colpito
            endPoint = hit.point;
            Target target = hit.transform.GetComponent<Target>();
            target.OnHit(); 
            }
            else
            {
            endPoint = shootingpoiint.position + shootingpoiint.forward * maxLineDistance;
            }
            LineRenderer line = Instantiate(lineRend);
            line.positionCount = 2;
            line.SetPosition(0, shootingpoiint.position);
            line.SetPosition(1, endPoint);   
            Destroy(line.gameObject, lineTime);
        }
        else
        {
            if (hasHit) 
            { //codice dove decidiamo che succede se viene colpito un bersaglio
            endPoint = hit.point;
            Target target = hit.transform.GetComponent<Target>();
                if (Random.Range(0,101) >= failureChance)
                {
                    target.OnHit();
                    gunpuz.GotHit();
                    logger.Log("Shot - Hit");
                }
                else
                {
                    logger.Log("Shot - Intentional miss");
                    if (Random.value < 0.80f) am.playBadSdound(); else am.playBad(1);
                }
            }
            else
            {  
                logger.Log("Shot -  Miss");
                if (Random.value < 0.80f) am.playBadSdound(); else am.playBad(1);
                //if (Random.value < 0.80f) {am.playBad(1);} else {}
                endPoint = shootingpoiint.position + shootingpoiint.forward * maxLineDistance;
            }
        }
        
        
    }

}
