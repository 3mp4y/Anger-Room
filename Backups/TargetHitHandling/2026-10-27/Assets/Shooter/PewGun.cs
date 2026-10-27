using System.Collections;
using System.Collections.Generic;
using System.Collections;
using UnityEngine;
using Oculus.Interaction;

public class PewGun : MonoBehaviour
{
    [Header("Raycast Settings")]
    public Transform barrelPoint;
    public float range = 50f;
    public LayerMask targetLayerMask;

    [Header("Input")]
    public OVRInput.Button fireButton = OVRInput.Button.PrimaryIndexTrigger;
    public OVRInput.Controller controller = OVRInput.Controller.RTouch;

    [Header("VFX (optional)")]
    public ParticleSystem muzzleFlash;
    public AudioSource shootSound;

    [Header("Shot Trail")]
    [Tooltip("How long the shot trail stays visible")]
    public float trailDuration = 0.2f;
    [Tooltip("Width of the laser trail")]
    public float trailWidth = 0.005f;
    [Tooltip("Color of the trail (hit)")]
    public Color trailColorHit = Color.red;
    [Tooltip("Color of the trail (miss)")]
    public Color trailColorMiss = Color.yellow;

    private bool _isGrabbed = false;
    private float _fireCooldown = 0.3f;
    private float _lastFireTime = -999f;

    private Grabbable _grabbable;
    private LineRenderer _lineRenderer;

    private void Awake()
    {
        _grabbable = GetComponent<Grabbable>();
        if (_grabbable == null)
            Debug.LogError("PewGun: No Grabbable component found on this GameObject!");

        SetupLineRenderer();
    }

    private void SetupLineRenderer()
    {
        _lineRenderer = gameObject.AddComponent<LineRenderer>();
        _lineRenderer.positionCount = 2;
        _lineRenderer.startWidth = trailWidth;
        _lineRenderer.endWidth = trailWidth;
        _lineRenderer.useWorldSpace = true;

        // Use a simple unlit material so it glows cleanly in VR
        _lineRenderer.material = new Material(Shader.Find("Unlit/Color"));
        _lineRenderer.enabled = false;
    }

    private void OnEnable()
    {
        if (_grabbable == null) return;
        _grabbable.WhenPointerEventRaised += OnPointerEvent;
    }

    private void OnDisable()
    {
        if (_grabbable == null) return;
        _grabbable.WhenPointerEventRaised -= OnPointerEvent;
    }

    private void OnPointerEvent(PointerEvent pointerEvent)
    {
        switch (pointerEvent.Type)
        {
            case PointerEventType.Select:   OnGrabbed();  break;
            case PointerEventType.Unselect: OnReleased(); break;
        }
    }

    private void OnGrabbed()
    {
        _isGrabbed = true;
        GameManager.Instance.StartGame();
    }

    private void OnReleased()
    {
        _isGrabbed = false;
    }

    private void Update()
    {
        if (!_isGrabbed) return;
        if (!GameManager.Instance.IsGameRunning) return;

        if (OVRInput.GetDown(fireButton, controller))
            TryFire();
    }

    private void TryFire()
    {
        if (Time.time - _lastFireTime < _fireCooldown) return;
        _lastFireTime = Time.time;

        if (muzzleFlash != null) muzzleFlash.Play();
        if (shootSound != null) shootSound.Play();

        Transform origin = barrelPoint != null ? barrelPoint : transform;
        Ray ray = new Ray(origin.position, origin.forward);

        bool hitTarget = false;
        Vector3 endPoint;

        if (Physics.Raycast(ray, out RaycastHit hit, range, targetLayerMask))
        {
            endPoint = hit.point;
            Target target = hit.collider.GetComponent<Target>();
            if (target != null)
            {
                target.OnHit();
                GameManager.Instance.RegisterHit();
                hitTarget = true;
            }
            else
            {
                GameManager.Instance.RegisterMiss();
            }
        }
        else
        {
            // No hit — draw trail to max range
            endPoint = origin.position + origin.forward * range;
            GameManager.Instance.RegisterMiss();
        }

        ShowTrail(origin.position, endPoint, hitTarget ? trailColorHit : trailColorMiss);
    }

    private void ShowTrail(Vector3 start, Vector3 end, Color color)
    {
        StopAllCoroutines(); // Cancel any previous fade
        StartCoroutine(TrailCoroutine(start, end, color));
    }

    private IEnumerator TrailCoroutine(Vector3 start, Vector3 end, Color color)
    {
        _lineRenderer.SetPosition(0, start);
        _lineRenderer.SetPosition(1, end);
        _lineRenderer.material.color = color;
        _lineRenderer.enabled = true;

        // Fade out over trailDuration
        float elapsed = 0f;
        Color fadeColor = color;

        while (elapsed < trailDuration)
        {
            elapsed += Time.deltaTime;
            fadeColor.a = Mathf.Lerp(1f, 0f, elapsed / trailDuration);
            _lineRenderer.material.color = fadeColor;
            yield return null;
        }

        _lineRenderer.enabled = false;
    }

    private void OnDrawGizmosSelected()
    {
        if (barrelPoint == null) return;
        Gizmos.color = Color.red;
        Gizmos.DrawRay(barrelPoint.position, barrelPoint.forward * range);
    }
}