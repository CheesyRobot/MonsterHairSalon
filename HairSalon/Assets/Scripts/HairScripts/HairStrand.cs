using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(LineRenderer))]
public class HairStrand : MonoBehaviour
{
    private class HairSegment
    {
        public Vector3 currentPos;
        public Vector3 oldPos;
        public GameObject colliderObj;
        public float radius;
        public Color color;

        public HairSegment(Vector3 pos, float rad, Color col)
        {
            currentPos = pos;
            oldPos = pos;
            radius = rad;
            color = col;
        }
    }

    [Header("Hair Generation")]
    [SerializeField] private int segmentCount = 20;
    [SerializeField] private float totalLength = 2.0f;
    [SerializeField] private float hairThickness = 0.1f;

    [Header("Painting")]
    [SerializeField] private Color baseColor = Color.white;

    [Header("Physics Settings")]
    [SerializeField] private Vector3 gravity = new Vector3(0, -9.81f, 0);
    [SerializeField] private int physicsIterations = 5;
    [SerializeField] private LayerMask collisionMask;

    private List<HairSegment> segments = new List<HairSegment>();
    private LineRenderer lineRenderer;
    public float segmentLength;
    public Color BaseColor => baseColor; // the ClientGenerator reads this for the hair on the TV picture

    private bool colorsDirty;
    private const int MaxGradientKeys = 8;

    void Start()
    {
        lineRenderer = GetComponent<LineRenderer>();
        segmentLength = totalLength / segmentCount;

        // Generating hair segments downwards and initializing their positions
        Vector3 spawnPos = transform.position;
        for (int i = 0; i < segmentCount; i++)
        {
            HairSegment segment = new HairSegment(spawnPos, hairThickness / 2f, baseColor);

            // Create a collider for each segment to handle collisions
            CreateSegmentCollider(segment, i);

            segments.Add(segment);
            spawnPos += Vector3.up * segmentLength;
        }

        lineRenderer.positionCount = segments.Count;
        ApplyColors();
    }

    void LateUpdate()
    {
        // Rebuild the gradient only when something was painted or cut
        if (colorsDirty) ApplyColors();
    }

    void Update()
    {
        // Line Renderer visualization
        for (int i = 0; i < segments.Count; i++)
        {
            lineRenderer.SetPosition(i, segments[i].currentPos);

            // Sinchronizing collider positions with the hair segments
            if (segments[i].colliderObj != null)
            {
                segments[i].colliderObj.transform.position = segments[i].currentPos;
            }
        }
    }

    void FixedUpdate()
    {
        SimulatePhysics();
        ApplyConstraints();
    }

    private void SimulatePhysics()
    {
        // Verlet integration
        for (int i = 1; i < segments.Count; i++)
        {
            HairSegment s = segments[i];
            Vector3 velocity = s.currentPos - s.oldPos;
            s.oldPos = s.currentPos;
            s.currentPos += velocity + gravity * Time.fixedDeltaTime * Time.fixedDeltaTime;
        }
    }

    private void ApplyConstraints()
    {
        // First segment stays in place
        segments[0].currentPos = transform.position;

        // Keeping distance between segments consistent
        for (int iter = 0; iter < physicsIterations; iter++)
        {
            for (int i = 0; i < segments.Count - 1; i++)
            {
                HairSegment first = segments[i];
                HairSegment second = segments[i + 1];

                float dist = Vector3.Distance(first.currentPos, second.currentPos);
                float error = Mathf.Abs(dist - segmentLength);
                Vector3 changeDir = Vector3.zero;

                if (dist > segmentLength)
                {
                    changeDir = (first.currentPos - second.currentPos).normalized;
                }
                else if (dist < segmentLength)
                {
                    changeDir = (second.currentPos - first.currentPos).normalized;
                }

                Vector3 changeAmount = changeDir * error;
                if (i != 0)
                {
                    first.currentPos -= changeAmount * 0.5f;
                    second.currentPos += changeAmount * 0.5f;
                }
                else
                {
                    // If its the first segment, only move the second one
                    second.currentPos += changeAmount;
                }
            }
        }

        // Collisions using Physics.OverlapSphere
        for (int i = 1; i < segments.Count; i++)
        {
            HairSegment s = segments[i];
            Collider[] colliders = Physics.OverlapSphere(s.currentPos, s.radius, collisionMask);

            foreach (var col in colliders)
            {
                // Ignore colliders that are part of the hair strand itself
                if (col.transform.IsChildOf(transform)) continue;

                if (Physics.ComputePenetration(
                    s.colliderObj.GetComponent<SphereCollider>(), s.currentPos, Quaternion.identity,
                    col, col.transform.position, col.transform.rotation,
                    out Vector3 direction, out float distance))
                {
                    s.currentPos += direction * distance;
                }
            }
        }
    }

    private void CreateSegmentCollider(HairSegment segment, int index)
    {
        GameObject colObj = new GameObject($"Knot_Collider_{index}");
        colObj.transform.parent = this.transform;
        colObj.transform.position = segment.currentPos;

        // Adding a trigger SphereCollider for collision detection
        SphereCollider sc = colObj.AddComponent<SphereCollider>();
        sc.radius = segment.radius;
        sc.isTrigger = true;

        // Pridedame pagalbinį komponentą susidūrimų sekimui
        HairKnotConnector connector = colObj.AddComponent<HairKnotConnector>();
        connector.Setup(this, index);

        segment.colliderObj = colObj;
    }


    public void CutHairAt(int cutIndex)
    {
        if (cutIndex <= 0 || cutIndex >= segments.Count) return;

        // Destroying colliders and removing segments from the list
        for (int i = segments.Count - 1; i > cutIndex; i--)
        {
            if (segments[i].colliderObj != null)
            {
                Destroy(segments[i].colliderObj);
            }
            // Updating length
            //totalLength -= segmentLength;
            segments.RemoveAt(i);
        }

        // Updating Line Renderer segments count
        lineRenderer.positionCount = segments.Count;
        //segmentCount = segments.Count;
        colorsDirty = true;
    }


    // Paint around a knot, radius is how many knots on each side are affected
    public void PaintAtKnot(int knotIndex, Color color, int radius = 1, float strength = 1f)
    {
        if (knotIndex < 0 || knotIndex >= segments.Count) return;

        for (int i = knotIndex - radius; i <= knotIndex + radius; i++)
        {
            if (i < 0 || i >= segments.Count) continue;
            // knots further from the touched one get less paint
            float falloff = 1f - (Mathf.Abs(i - knotIndex) / (float)(radius + 1));
            segments[i].color = Color.Lerp(segments[i].color, color, strength * falloff);
        }
        colorsDirty = true;
    }


    // Get the current paint color of a knot
    public Color GetKnotColor(int knotIndex)
    {
        if (knotIndex < 0 || knotIndex >= segments.Count) return baseColor;
        return segments[knotIndex].color;
    }

    // Turn the knot colors into the Line Renderer gradient
    // The gradient only has 8 keys so each key averages the knots around it
    private void ApplyColors()
    {
        colorsDirty = false;
        int knotCount = segments.Count;
        if (knotCount == 0) return;

        int keyCount = Mathf.Min(MaxGradientKeys, knotCount);
        var colorKeys = new GradientColorKey[keyCount];

        for (int k = 0; k < keyCount; k++)
        {
            // Where this key sits along the strand (0..1) and which knot is at that spot
            float t = keyCount == 1 ? 0f : k / (float)(keyCount - 1);
            float center = t * (knotCount - 1);

            // The knots around the key that get averaged
            float half = keyCount == 1 ? knotCount : (knotCount - 1) / (float)(keyCount - 1) * 0.5f;
            int from = Mathf.Max(0, Mathf.CeilToInt(center - half));
            int to = Mathf.Min(knotCount - 1, Mathf.FloorToInt(center + half));
            // Always use at least one knot
            if (to < from) from = to = Mathf.Clamp(Mathf.RoundToInt(center), 0, knotCount - 1);

            // Average the colors of those knots
            Color sum = Color.black;
            for (int i = from; i <= to; i++) sum += segments[i].color;
            colorKeys[k] = new GradientColorKey(sum / (to - from + 1), t);
        }

        // Alpha stays fully opaque
        var gradient = new Gradient();
        gradient.SetKeys(colorKeys, new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) });
        lineRenderer.colorGradient = gradient;
    }
}