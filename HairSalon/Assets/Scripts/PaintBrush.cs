using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables; // XRI 3.x, remove this line if you are on XRI 2.x

[RequireComponent(typeof(XRGrabInteractable))]
public class PaintBrush : MonoBehaviour
{
    [Header("Brush")]
    [SerializeField] private Transform tip; // empty object at the bristle tip
    [SerializeField] private Color brushColor = Color.red;
    [SerializeField] private float tipRadius = 0.05f;
    [Tooltip("How many knots either side of the touched knot get colored")]
    [SerializeField] private int knotRadius = 1;
    [Tooltip("Blend speed. 1 = fully colored after about a second of contact")]
    [SerializeField] private float paintSpeed = 2f;
    [SerializeField] private LayerMask hairLayers = ~0;

    [Header("Palette")]
    [SerializeField] private LayerMask paletteLayers = ~0;
    [Tooltip("0 = instant color swap. Higher = faster blend toward the swatch (gives a dipping feel)")]
    [SerializeField] private float pickupBlendSpeed = 0f;

    [Header("Painting Input")]
    [Tooltip("If on, hair is only painted while the controller's activate (trigger) is held")]
    [SerializeField] private bool requireTriggerToPaint = false;

    [Header("Optional")]
    [Tooltip("Renderer on the bristles that should show the current color")]
    [SerializeField] private Renderer bristleRenderer;
    [Tooltip("Shader color property. URP Lit uses _BaseColor, Built-in uses _Color")]
    [SerializeField] private string colorProperty = "_BaseColor";

    public bool IsPainting { get; set; } = true;
    public Color BrushColor => brushColor;

    private XRGrabInteractable grab;
    private readonly Collider[] hits = new Collider[32]; // reused every frame to avoid garbage
    private MaterialPropertyBlock block;
    private int colorId;

    void Awake()
    {
        grab = GetComponent<XRGrabInteractable>();
        colorId = Shader.PropertyToID(colorProperty);
        block = new MaterialPropertyBlock();

        // Subscribe here and not in OnEnable, this script disables itself while not held
        grab.selectEntered.AddListener(OnGrabbed);
        grab.selectExited.AddListener(OnReleased);
        grab.activated.AddListener(OnActivated);
        grab.deactivated.AddListener(OnDeactivated);

        if (requireTriggerToPaint) IsPainting = false;

        SetColor(brushColor);
        enabled = false; // idle until grabbed
    }

    void OnDestroy()
    {
        if (grab == null) return;
        grab.selectEntered.RemoveListener(OnGrabbed);
        grab.selectExited.RemoveListener(OnReleased);
        grab.activated.RemoveListener(OnActivated);
        grab.deactivated.RemoveListener(OnDeactivated);
    }

    void OnGrabbed(SelectEnterEventArgs args) { enabled = true; }

    void OnReleased(SelectExitEventArgs args)
    {
        enabled = false;
        if (requireTriggerToPaint) IsPainting = false;
    }

    void OnActivated(ActivateEventArgs args) { if (requireTriggerToPaint) IsPainting = true; }
    void OnDeactivated(DeactivateEventArgs args) { if (requireTriggerToPaint) IsPainting = false; }

    void Update()
    {
        if (tip == null) return;

        CheckPalette();

        if (IsPainting) PaintHair();
    }

    // Dip the brush into a swatch to pick up its color
    void CheckPalette()
    {
        int count = Physics.OverlapSphereNonAlloc(tip.position, tipRadius, hits,
                                                  paletteLayers, QueryTriggerInteraction.Collide);
        for (int i = 0; i < count; i++)
        {
            // the swatch color is read straight from its material
            if (!hits[i].TryGetComponent<Renderer>(out var swatchRenderer)) continue;
            Color swatchColor = swatchRenderer.sharedMaterial.color;

            if (pickupBlendSpeed <= 0f)
            {
                if (swatchColor != brushColor) SetColor(swatchColor);
            }
            else
            {
                SetColor(Color.Lerp(brushColor, swatchColor, pickupBlendSpeed * Time.deltaTime));
            }
            break; // one swatch at a time
        }
    }

    // Color the hair knots under the tip
    void PaintHair()
    {
        int count = Physics.OverlapSphereNonAlloc(tip.position, tipRadius, hits,
                                                  hairLayers, QueryTriggerInteraction.Collide);
        for (int i = 0; i < count; i++)
        {
            var knot = hits[i].GetComponent<HairKnotConnector>();
            if (knot == null || knot.Strand == null) continue;

            knot.Strand.PaintAtKnot(knot.KnotIndex, brushColor,
                                    knotRadius, paintSpeed * Time.deltaTime);
        }
    }

    // Change the brush color and update the bristles
    public void SetColor(Color color)
    {
        brushColor = color;

        if (bristleRenderer != null)
        {
            // property block avoids creating a new material instance
            bristleRenderer.GetPropertyBlock(block);
            block.SetColor(colorId, color);
            bristleRenderer.SetPropertyBlock(block);
        }
    }

    // Handy for UI buttons / XR events
    public void SetPainting(bool painting) { IsPainting = painting; }

    // Shows the tip radius in the scene view
    void OnDrawGizmosSelected()
    {
        if (tip == null) return;
        Gizmos.color = brushColor;
        Gizmos.DrawWireSphere(tip.position, tipRadius);
    }
}