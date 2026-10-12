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

        public HairSegment(Vector3 pos, float rad)
        {
            currentPos = pos;
            oldPos = pos;
            radius = rad;
        }
    }

    [Header("Hair Generation")]
    [SerializeField] private int segmentCount = 20;
    [SerializeField] private float totalLength = 2.0f;
    [SerializeField] private float hairThickness = 0.1f;

    [Header("Physics Settings")]
    [SerializeField] private Vector3 gravity = new Vector3(0, -9.81f, 0);
    [SerializeField] private int physicsIterations = 5;
    [SerializeField] private LayerMask collisionMask;

    private List<HairSegment> segments = new List<HairSegment>();
    private LineRenderer lineRenderer;
    private float segmentLength;

    void Start()
    {
        lineRenderer = GetComponent<LineRenderer>();
        segmentLength = totalLength / segmentCount;

        // Generating hair segments downwards and initializing their positions
        Vector3 spawnPos = transform.position;
        for (int i = 0; i < segmentCount; i++)
        {
            HairSegment segment = new HairSegment(spawnPos, hairThickness / 2f);

            // Create a collider for each segment to handle collisions
            CreateSegmentCollider(segment, i);

            segments.Add(segment);
            spawnPos += Vector3.down * segmentLength;
        }

        lineRenderer.positionCount = segments.Count;
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
            segments.RemoveAt(i);
        }

        // Updating Line Renderer segments count
        lineRenderer.positionCount = segments.Count;
    }
}
