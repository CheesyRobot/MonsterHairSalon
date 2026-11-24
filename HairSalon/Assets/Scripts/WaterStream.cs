using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables; 


public class WaterStream : MonoBehaviour
{
    [Header("Switch")]
    [Tooltip("The shower, the water only runs while it is held. Found in the parents when empty")]
    [SerializeField] private XRBaseInteractable shower;
    [Tooltip("Other water objects that should turn on and off together with this one")]
    [SerializeField] private GameObject[] alsoSwitch;

    [Header("Length")]
    [SerializeField] private float maxScaleY = 1f;
    [SerializeField] private float minScaleY = 0.01f;
    [SerializeField] private LayerMask hitLayers = ~0;
    [Tooltip("Turn this on if the water grows the wrong way")]
    [SerializeField] private bool flipDirection = false;

    [Header("Splash")]
    [Tooltip("Particle system at the free end of the water, it only emits while the water hits something")]
    [SerializeField] private ParticleSystem splash;

    private float meshCenterY; 
    private float meshHalfLength; 
    private float nearY; 
    private float farSign = 1f; 
    private Vector3 anchorLocal; 
    private readonly RaycastHit[] hits = new RaycastHit[8]; 
    private Renderer[] renderers; 
    private bool flowing;

    private AudioSource audioSource;

    void Awake()
    {
        if (splash == null) splash = GetComponentInChildren<ParticleSystem>();
        renderers = GetComponentsInChildren<Renderer>(true);

        Bounds bounds = GetComponent<MeshFilter>().sharedMesh.bounds;
        meshCenterY = bounds.center.y;
        meshHalfLength = bounds.size.y * 0.5f;

        audioSource = GetComponent<AudioSource>();


        if (splash != null)
        {
            farSign = transform.InverseTransformPoint(splash.transform.position).y >= meshCenterY ? 1f : -1f;
        }
        if (flipDirection) farSign = -farSign;

        nearY = meshCenterY - farSign * meshHalfLength;

        
        Vector3 startWorld = transform.TransformPoint(new Vector3(0f, nearY, 0f));
        anchorLocal = transform.parent != null ? transform.parent.InverseTransformPoint(startWorld) : startWorld;

        
        if (shower == null) shower = GetComponentInParent<XRBaseInteractable>();
        if (shower != null)
        {
            shower.selectEntered.AddListener(OnGrabbed);
            shower.selectExited.AddListener(OnReleased);
            SetFlowing(shower.isSelected);
            
        }
        else
        {
            Debug.LogWarning("[WaterStream] No grab interactable found, the water stays on. Assign the shower in the Shower field.", this);
            SetFlowing(true);
        }
    }

    void OnDestroy()
    {
        if (shower == null) return;
        shower.selectEntered.RemoveListener(OnGrabbed);
        shower.selectExited.RemoveListener(OnReleased);
    }

    void OnGrabbed(SelectEnterEventArgs args) { SetFlowing(true); }

    void OnReleased(SelectExitEventArgs args) { SetFlowing(shower.isSelected); }

    // Turns the whole water effect on or off
    private void SetFlowing(bool on)
    {
        flowing = on;

        foreach (var r in renderers) if (r != null) r.enabled = on;
        foreach (var go in alsoSwitch) if (go != null) go.SetActive(on);

        if (splash != null)
        {
            
            var emission = splash.emission;
            emission.enabled = false;

            if (on) { if (!splash.isPlaying) splash.Play(); }
            else splash.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }
       
    }

    // LateUpdate so it runs after the shower head was moved
    void LateUpdate()
    {
        if (!flowing) return;

        Vector3 start = transform.parent != null ? transform.parent.TransformPoint(anchorLocal) : anchorLocal;
        Vector3 direction = transform.TransformDirection(Vector3.up * farSign);

        
        float lengthPerScale = WorldLengthPerScale();

        
        float maxLength = lengthPerScale * maxScaleY;
        bool hitSomething = FindHit(start, direction, maxLength, out float hitDistance);
        float length = hitSomething ? hitDistance : maxLength;

        
        Vector3 scale = transform.localScale;
        scale.y = Mathf.Clamp(length / lengthPerScale, minScaleY, maxScaleY);
        transform.localScale = scale;
        transform.position += start - transform.TransformPoint(new Vector3(0f, nearY, 0f));

        
        if (splash != null)
        {
            var emission = splash.emission;
            emission.enabled = hitSomething;
        }

        
        
    }

    // Closest hit that isn't part of the shower itself
    private bool FindHit(Vector3 start, Vector3 direction, float maxLength, out float distance)
    {
        distance = maxLength;
        int count = Physics.RaycastNonAlloc(start, direction, hits, maxLength, hitLayers, QueryTriggerInteraction.Ignore);

        bool found = false;
        for (int i = 0; i < count; i++)
        {
            
            if (hits[i].collider.transform.IsChildOf(transform.root)) continue;

            if (hits[i].distance < distance)
            {
                distance = hits[i].distance;
                found = true;
            }
        }
        return found;
    }

    // World length of the mesh for every 1 of Y scale
    private float WorldLengthPerScale()
    {
        Vector3 a = transform.TransformPoint(new Vector3(0f, meshCenterY - meshHalfLength, 0f));
        Vector3 b = transform.TransformPoint(new Vector3(0f, meshCenterY + meshHalfLength, 0f));
        return (a - b).magnitude / Mathf.Max(transform.localScale.y, 0.0001f);
    }

    public void EnableSound()
    {
        if (audioSource.generator != null)
        {
            audioSource.pitch = Random.Range(0.75f, 1.25f);
            audioSource.Play();
        }
    }

    public void DisableSound()
    {
        if (audioSource.generator != null)
        {
            audioSource.Stop();
        }
    }
}