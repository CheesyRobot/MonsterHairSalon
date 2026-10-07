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
    private Spline hairSpline;
    private List<GameObject> knotVisuals = new List<GameObject>();
    private MeshRenderer hairMeshRenderer;

    void Start()
    {
        hairContainer = GetComponent<SplineContainer>();
        hairSpline = hairContainer.Spline;
        //hairMeshRenderer = GetComponent<MeshRenderer>();
        //if (hairMeshRenderer == null) hairMeshRenderer = GetComponentInChildren<MeshRenderer>();
        CreateKnotVisuals();
    }


    void Update()
    { 

    }
    

    // Knot-level touch will be reported by individual KnotBehaviour instances.
    public void OnKnotTouched(int knotIndex, Collider other)
    {
        Debug.Log($"Knot touched: {knotIndex} by {other.gameObject.name}");
        //CutAtKnot(knotIndex);
    }

    // Called via SendMessage from KnotBehaviour to avoid compile-time coupling
    // TriggerEnterInfo contains the knot index and the other (touched) collider
    public void OnKnotTouchedByIndex(TriggerEnterInfo info)
    {
        Debug.Log($"OnKnotTouchedByIndex: {info.knotIndex} by {info.other.gameObject.name}");
        if (info.other != null)
        {
            if(info.other.gameObject.name == "Scissors")
            {
                CutAtKnot(info.knotIndex);
            }
            //Debug.Log($"Touched by: {info.other.gameObject.name}");
        }
    }

    // Create simple visuals (spheres) for each knot
    // Useful to see where the spline control point are
    private void CreateKnotVisuals()
    {
        // Clean existing
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

    
}
