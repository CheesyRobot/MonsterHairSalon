using System.Collections.Generic;
using System.Linq;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Profiling;

[RequireComponent(typeof(LineRenderer))]
public class HairStrandJob : MonoBehaviour
{
    public struct HairSegment
    {
        public float3 currentPos;
        public float3 oldPos;
        public float radius;
        public Color color;

        /*public HairSegment(Vector3 pos, float rad, Color col)
        {
            currentPos = pos;
            oldPos = pos;
            radius = rad;
            color = col;
        }*/
    }
    private bool initialized = false;

    [Header("Hair Generation")]
    [SerializeField] private int segmentCount = 20;
    [SerializeField] private float totalLength = 2.0f;
    [SerializeField] private float hairThickness = 0.1f;

    [Header("Painting")]
    [SerializeField] private Color baseColor = Color.white;

    [Header("Physics Settings")]
    [SerializeField] private float3 gravity = new float3(0, -9.81f, 0);
    [SerializeField] private int physicsIterations = 5;
    [SerializeField] private LayerMask collisionMask;
    private float antiGravFloat = 3.5f;
    private int rootFloat = 5;
    [SerializeField] private float damping = 0.98f;
    private float factor1 = 0.5f;
    private float factor2 = 0.5f;
    [SerializeField] private float friction = 0.5f;

    private List<HairSegment> segments = new List<HairSegment>();
    private NativeArray<HairSegment> nativeSegments;
    private List<GameObject> colliderObjs = new List<GameObject>();

    private LineRenderer lineRenderer;
    public float segmentLength;

    private float startLength;
    public float StartLength => startLength;

    public Color BaseColor => baseColor; // the ClientGenerator reads this for the hair on the TV picture

    private bool colorsDirty;
    private const int MaxGradientKeys = 8;

    void Start()
    {
        //InitializeHairStrand();
    }

    void LateUpdate()
    {
        // Rebuild the gradient only when something was painted or cut
        if (colorsDirty) ApplyColors();
    }

    void Update()
    {
        if(!initialized) return;
        // Line Renderer visualization
        for (int i = 0; i < segments.Count; i++)
        {
            lineRenderer.SetPosition(i, segments[i].currentPos);

            // Sinchronizing collider positions with the hair segments
            if (colliderObjs != null || colliderObjs.Count > 0)
            {
                colliderObjs[i].transform.position = segments[i].currentPos;
            }
        }
    }

    void FixedUpdate()
    {
        if (!initialized) return;
        SimulatePhysics();
        ApplyConstraints();
    }

    public void InitializeHairStrand()
    {
        initialized = true;
        lineRenderer = GetComponent<LineRenderer>();
        segmentLength = totalLength / segmentCount;
        startLength = totalLength;
        // Generating hair segments upwards and initializing their positions
        Vector3 spawnPos = transform.position;
        segments.Clear();
        colliderObjs.Clear();
        for (int i = 0; i < segmentCount; i++)
        {
            HairSegment segment = new HairSegment
            { 
                currentPos = spawnPos,
                oldPos = spawnPos,
                radius = hairThickness / 2f,
                color = baseColor
            };

            // Create a collider for each segment to handle collisions
            CreateSegmentCollider(segment, i);

            segments.Add(segment);
            spawnPos += Vector3.up * hairThickness/2;
        }
        lineRenderer.positionCount = segments.Count;
        if (nativeSegments.IsCreated)
            nativeSegments.Dispose();
        nativeSegments = new NativeArray<HairSegment>(segments.Count, Allocator.Persistent);
        
        ApplyColors();
    }

    private void SimulatePhysics()
    {
        Profiler.BeginSample("SimulateHairPhysics");
        // Verlet integration
        int segmentsCount = segments.Count;
        float floatingAmnt = antiGravFloat;
        if (segmentsCount < 10)
        {
            floatingAmnt = math.clamp(antiGravFloat - antiGravFloat * (segmentsCount / segmentCount), 0, antiGravFloat);
        }
        float3 upVector = floatingAmnt * transform.up.normalized*-math.dot(transform.up,gravity);
        for (int i = 1; i < segmentsCount; i++)
        {
            HairSegment s = segments[i];
            float gravityImpact = 1-math.clamp((float)i * i / (rootFloat*rootFloat), 0, 1);
            float3 velocity = damping*(s.currentPos - s.oldPos);
            s.oldPos = s.currentPos;
            s.currentPos += Time.fixedDeltaTime * Time.fixedDeltaTime*(upVector*gravityImpact + gravity) + velocity;
            segments[i] = s;
        }

        Profiler.EndSample();
    }

        private void ApplyConstraints()
    {
        Profiler.BeginSample("Constraints");
        nativeSegments.CopyFrom(segments.ToArray());
        ApplyConstraintsJob job = new ApplyConstraintsJob()
        {
            segments = nativeSegments,
            rootPos = transform.position,
            segmentLength = segmentLength,
            physicsIterations = physicsIterations,
            factor1 = factor1,
            factor2 = factor2
        };
        JobHandle handle = job.Schedule();
        handle.Complete();
        //segments.Clear();
        segments = job.segments.ToList();
        // First segment stays in place
        /*HairSegment fr = segments[0];
        fr.currentPos = transform.position;
        segments[0] = fr;
        // Keeping distance between segments consistent
        for (int iter = 0; iter < physicsIterations; iter++)
        {
            for (int i = 0; i < segments.Count - 1; i++)
            {
                HairSegment first = segments[i];
                HairSegment second = segments[i + 1];

                float dist = Vector3.Distance(first.currentPos, second.currentPos);
                float error = Mathf.Abs(dist - segmentLength);
                float3 changeDir = float3.zero;

                changeDir = math.normalize(first.currentPos - second.currentPos);

                float3 changeAmount = changeDir * error;
                if (i != 0)
                {
                    first.currentPos -= changeAmount * factor1;
                    second.currentPos += changeAmount * factor2;
                }
                else
                {
                    // If its the first segment, only move the second one
                    second.currentPos += changeAmount;
                }
                segments[i] = first;
                segments[i + 1] = second;
            }*/

            /*HairSegment fir = segments[0];
            HairSegment last = segments[segments.Count - 1];
            // Same distance calculation as above, but less optimal.
            float distance = Vector2.Distance(fir.currentPos, last.currentPos);
            if (distance > 0 && distance > segments.Count * segmentLength)
            {
                Vector3 dir = (last.currentPos - fir.currentPos).normalized;
                last.currentPos = fir.currentPos + segments.Count * segmentLength * dir;
            }*/
        //}



        // Collisions using Physics.OverlapSphere
        for (int i = 1; i < segments.Count; i++)
        {
            HairSegment s = segments[i];
            Collider[] colliders = Physics.OverlapSphere(s.currentPos, s.radius, collisionMask);

            foreach (var col in colliders)
            {
                // Ignore colliders that are part of the hair strand itself
                if (col.transform.IsChildOf(transform)) continue;
                //if (math.length(s.oldPos - s.currentPos) < minVelColCheck) break;

                if (Physics.ComputePenetration(
                    colliderObjs[i].GetComponent<SphereCollider>(), s.currentPos, Quaternion.identity,
                    col, col.transform.position, col.transform.rotation,
                    out Vector3 direction, out float distance))
                {
                    s.currentPos += (float3)(direction * distance);
                    float3 velocity = (s.currentPos - s.oldPos);
                    velocity *= (1f - friction);
                    s.oldPos = s.currentPos - velocity;

                    segments[i] = s;
                }
            }
        }

        
        Profiler.EndSample();
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

        // Component for collision detection
        HairKnotConnector connector = colObj.AddComponent<HairKnotConnector>();
        connector.Setup(this, index);

        colliderObjs.Add(colObj);
    }


    public void CutHairAt(int cutIndex)
    {
        if (cutIndex < 0 || cutIndex >= segments.Count) return;

        // Destroying colliders and removing segments from the list
        for (int i = segments.Count - 1; i > cutIndex; i--)
        {
            if (colliderObjs[i] != null)
            {
                Destroy(colliderObjs[i]);
            }
            // Updating length
            totalLength -= segmentLength;
            segments.RemoveAt(i);
        }
        if (nativeSegments.IsCreated)
            nativeSegments.Dispose();
        nativeSegments = new NativeArray<HairSegment>(segments.ToArray(), Allocator.Persistent);
        // Updating Line Renderer segments count
        lineRenderer.positionCount = segments.Count;
        segmentCount = segments.Count;
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
            HairSegment s = segments[i];
            s.color = Color.Lerp(segments[i].color, color, strength * falloff);
            segments[i] = s;
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

    public float GetTotalLength()
    {
        return totalLength;
    }

    public int GetTotalSegments()
    {
        return segmentCount;
    }

    public void SetTotalLength(float value)
    {
        totalLength = value;
    }

    public void SetTotalSegments(int value)
    {
        segmentCount = value;
    }

    public Color GetAverageColor()
    {
        if (segments.Count == 0) return baseColor;
        Color sum = Color.black;
        for (int i = 0; i < segments.Count; i++) sum += segments[i].color;
        return sum / segments.Count;
    }

    public void OnDestroy()
    {
        if (nativeSegments.IsCreated)
            nativeSegments.Dispose();
    }


    [BurstCompile]
    public struct ApplyConstraintsJob : IJob
    {
        public NativeArray<HairSegment> segments;
        [ReadOnly] public float3 rootPos;
        [ReadOnly] public float segmentLength;
        [ReadOnly] public int physicsIterations;
        [ReadOnly] public float factor1;
        [ReadOnly] public float factor2;
        public void Execute()
        {
            HairSegment rootSegment = segments[0];
            rootSegment.currentPos = rootPos;
            segments[0] = rootSegment;

            for (int iter = 0; iter < physicsIterations; iter++)
            {
                // Alternating constraint checks from top and bottom
                if (iter % 2 == 0)
                {
                    for (int i = 0; i < segments.Length - 1; i++)
                    {
                        HairSegment first = segments[i];
                        HairSegment second = segments[i + 1];

                        float3 changeDir = second.currentPos - first.currentPos;
                        float dist = math.distance(first.currentPos, second.currentPos);
                        if (dist == 0) return;

                        float3 n = changeDir / dist;
                        //float error = math.abs(dist - segmentLength);

                        float changeAmount = dist - segmentLength;
                        float3 off = changeAmount * n;
                        if (i != 0)
                        {
                            first.currentPos += off * factor1;
                            second.currentPos -= off * factor1;
                        }
                        else
                        {
                            // If its the first segment, only move the second one
                            second.currentPos -= off;
                        }
                        segments[i] = first;
                        segments[i + 1] = second;
                    }
                }
                else
                {
                    for (int i = segments.Length - 1; i > 0; i--)
                    {
                        HairSegment first = segments[i - 1];
                        HairSegment second = segments[i];

                        float3 changeDir = second.currentPos - first.currentPos;
                        float dist = math.distance(first.currentPos, second.currentPos);
                        if (dist == 0) return;

                        float3 n = changeDir / dist;
                        //float error = math.abs(dist - segmentLength);

                        float changeAmount = dist - segmentLength;
                        float3 off = changeAmount * n;
                        if (i != 0)
                        {
                            first.currentPos += off * factor1;
                            second.currentPos -= off * factor1;
                        }
                        else
                        {
                            // If its the first segment, only move the second one
                            second.currentPos -= off;
                        }
                        segments[i-1] = first;
                        segments[i] = second;
                    }
                }
            }
        }
    }
}

