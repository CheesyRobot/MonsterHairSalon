using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Splines;
using UnityEngine.Rendering;
using Unity.Mathematics;
using UnityEditor;

public class HairStrandBehaviour : MonoBehaviour
{
    [SerializeField]
    private SplineContainer hairContainer;
    [SerializeField]
    private SplineMeshBuilder meshBuilder;
    [SerializeField]
    private Color baseColor = Color.white;
    [SerializeField]
    private bool showKnotVisuals = true;
    private Spline hairSpline;
    private List<GameObject> knotVisuals = new List<GameObject>();
    private MeshRenderer hairMeshRenderer;
    private Color[] knotColors; // paint color of each knot
    private float[] knotT; // normalised distance along the strand of each knot

    void Start()
    {
        hairContainer = GetComponent<SplineContainer>();
        hairSpline = hairContainer.Spline;
        //hairMeshRenderer = GetComponent<MeshRenderer>();
        //if (hairMeshRenderer == null) hairMeshRenderer = GetComponentInChildren<MeshRenderer>();
        if (meshBuilder == null) meshBuilder = GetComponent<SplineMeshBuilder>();
        if (meshBuilder == null) meshBuilder = GetComponentInChildren<SplineMeshBuilder>();
        CreateKnotVisuals();

        // Every knot starts with the base color, the mesh builder asks for colors through EvaluateColor
        knotColors = Enumerable.Repeat(baseColor, hairSpline.Count).ToArray();
        RecalcKnotT();
        if (meshBuilder != null)
        {
            meshBuilder.ColorAt = EvaluateColor;
            meshBuilder.UpdateColors();
        }
    }


    void Update()
    {

    }

    // Called via SendMessage from KnotBehaviour to avoid compile-time coupling
    // TriggerEnterInfo contains the knot index and the other (touched) collider
    public void OnKnotTouchedByIndex(TriggerEnterInfo info)
    {
        Debug.Log($"OnKnotTouchedByIndex: {info.knotIndex} by {info.other.gameObject.name}");
        if (info.other != null)
        {
            if (info.other.gameObject.name == "Scissors")
            {
                CutAtKnot(info.knotIndex);
            }
            //Debug.Log($"Touched by: {info.other.gameObject.name}");
        }
    }

    // Create simple visuals (spheres) for each knot
    // Useful to see where the spline control point are and adds colliders
    private void CreateKnotVisuals()
    {
        
        foreach (var go in knotVisuals) if (go != null) Destroy(go);
        knotVisuals.Clear();

        if (hairSpline == null) return;

        int index = 0;
        foreach (var knot in hairSpline.Knots)
        {
            GameObject sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            sphere.name = "KnotVisual_" + index;
            sphere.transform.SetParent(transform, false);
            sphere.transform.localPosition = knot.Position;
            sphere.transform.localScale = Vector3.one * 0.04f; // small indicator
            var rend = sphere.GetComponent<Renderer>();
            if (rend != null)
            {
                rend.material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                rend.material.color = Color.white;
                rend.enabled = showKnotVisuals;
            }
            // make sure visual doesn't interfere with physics, but allow trigger detection
            var col = sphere.GetComponent<Collider>();
            if (col != null) col.isTrigger = true;
            // add a kinematic Rigidbody so triggers work reliably
            var rb = sphere.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            // add per-knot behaviour so each knot can detect touches itself
            var kb = sphere.AddComponent<KnotBehaviour>();
            kb.triggerEnterInfo = new TriggerEnterInfo { knotIndex = index };
            knotVisuals.Add(sphere);
            index++;
        }
    }

    // Cut the spline at knotIndex: knots at and after knotIndex are removed
    public void CutAtKnot(int knotIndex)
    {
        if (hairSpline == null) return;
        if (knotIndex < 0) return;

        int keepCount = Mathf.Clamp(knotIndex, 0, hairSpline.Knots.Count());
        if (keepCount == hairSpline.Knots.Count()) return; // nothing to cut

        // Trim the colors first, changing the knots rebuilds the mesh right away
        if (knotColors != null) knotColors = knotColors.Take(keepCount).ToArray();

        // Create a new array of knots to keep based on keepCount
        BezierKnot[] newKnots = hairSpline.Knots.Take(keepCount).
            Select(k => new BezierKnot(k.Position, k.TangentIn, k.TangentOut, k.Rotation)).ToArray();
        hairSpline.Knots = newKnots;

        // destroy visuals for removed knots
        for (int i = knotVisuals.Count - 1; i >= keepCount; i--)
        {
            if (knotVisuals[i] != null) Destroy(knotVisuals[i]);
            knotVisuals.RemoveAt(i);
        }

        // The strand is shorter now so the knot positions along it changed
        RecalcKnotT();
        if (meshBuilder != null) meshBuilder.UpdateColors();
    }

    // Change the color of a specific knot visual
    public void SetKnotColor(int knotIndex, Color color)
    {
        if (knotIndex < 0 || knotIndex >= knotVisuals.Count) return;
        var go = knotVisuals[knotIndex];
        if (go == null) return;
        var rend = go.GetComponent<Renderer>();
        if (rend == null) return;
        rend.material.color = color;
    }

    // Paint around a knot, radius is how many knots on each side are affected
    // strength is the 0..1 blend amount (use small values * Time.deltaTime for slow painting)
    public void PaintAtKnot(int knotIndex, Color color, int radius = 1, float strength = 1f)
    {
        if (knotColors == null || knotIndex < 0 || knotIndex >= knotColors.Length) return;

        for (int i = knotIndex - radius; i <= knotIndex + radius; i++)
        {
            if (i < 0 || i >= knotColors.Length) continue;
            // knots further from the touched one get less paint
            float falloff = 1f - (Mathf.Abs(i - knotIndex) / (float)(radius + 1));
            knotColors[i] = Color.Lerp(knotColors[i], color, strength * falloff);
            SetKnotColor(i, knotColors[i]);
        }
        if (meshBuilder != null) meshBuilder.UpdateColors();
    }

    // Paint from knotIndex to the tip of the strand (ombre / dipped ends)
    public void PaintFromKnotToTip(int knotIndex, Color color, float strength = 1f)
    {
        if (knotColors == null) return;
        for (int i = Mathf.Max(0, knotIndex); i < knotColors.Length; i++)
        {
            knotColors[i] = Color.Lerp(knotColors[i], color, strength);
            SetKnotColor(i, knotColors[i]);
        }
        if (meshBuilder != null) meshBuilder.UpdateColors();
    }

    // Paint the whole strand
    public void PaintStrand(Color color, float strength = 1f)
    {
        if (knotColors == null) return;
        for (int i = 0; i < knotColors.Length; i++)
        {
            knotColors[i] = Color.Lerp(knotColors[i], color, strength);
            SetKnotColor(i, knotColors[i]);
        }
        if (meshBuilder != null) meshBuilder.UpdateColors();
    }

    // Get the current paint color of a knot
    public Color GetKnotColor(int knotIndex)
    {
        if (knotColors == null || knotIndex < 0 || knotIndex >= knotColors.Length) return baseColor;
        return knotColors[knotIndex];
    }

    // Called by SplineMeshBuilder for every mesh vertex, t is 0..1 along the strand
    // Blends between the colors of the two knots the vertex sits between
    private Color EvaluateColor(float t)
    {
        if (knotColors == null || knotT == null) return baseColor;
        int n = Mathf.Min(knotColors.Length, knotT.Length);
        if (n == 0) return baseColor;
        if (n == 1 || t <= knotT[0]) return knotColors[0];
        if (t >= knotT[n - 1]) return knotColors[n - 1];

        int seg = 0;
        while (seg < n - 2 && t > knotT[seg + 1]) seg++;
        float span = Mathf.Max(knotT[seg + 1] - knotT[seg], 1e-5f);
        return Color.Lerp(knotColors[seg], knotColors[seg + 1], (t - knotT[seg]) / span);
    }

    // Work out where each knot sits along the strand (0..1) based on curve lengths
    private void RecalcKnotT()
    {
        int n = hairSpline.Count;
        knotT = new float[n];
        float total = 0f;
        for (int i = 1; i < n; i++)
        {
            total += hairSpline.GetCurveLength(i - 1);
            knotT[i] = total;
        }
        if (total > 0f) for (int i = 0; i < n; i++) knotT[i] /= total;
    }

}