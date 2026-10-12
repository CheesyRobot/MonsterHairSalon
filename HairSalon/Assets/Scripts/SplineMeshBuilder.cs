using System.Collections.Generic;
using System.Linq;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;
using UnityEngine.Splines;

public class SplineMeshBuilder : MonoBehaviour
{
    [SerializeField]
    private SplineContainer hairContainer;
    [SerializeField]
    private MeshFilter meshFilter;

    float3 aposition;
    float3 tangent;
    float3 upVector;

    List<Vector3> m_vertsP1;
    List<Vector3> m_vertsP2;

    [SerializeField]
    float width;
    float resolution;

    // Set by HairStrandBehaviour, gives the paint color for a point (0..1) along the strand
    public System.Func<float, Color> ColorAt;
    // Where along the strand (0..1) every vertex of the mesh sits
    List<float> m_vertexT = new List<float>();

    void Awake()
    {
        GetVerts();
        CreateMesh();
    }

    private void OnEnable()
    {
        Spline.Changed += OnSplineChanged;
        GetVerts();
    }

    private void OnDisable()
    {
        Spline.Changed -= OnSplineChanged;
    }

    private void OnSplineChanged(Spline arg1, int arg2, SplineModification arg3)
    {
        GetVerts();
        CreateMesh();
    }

    private void OnDrawGizmos()
    {
        //GetVerts();
        //CreateMesh();
        // Visuals for mesh vertices vvv
        /*Handles.matrix = transform.localToWorldMatrix;
        foreach (var p in m_vertsP1)
        {
            Handles.SphereHandleCap(0, p, Quaternion.identity, 0.03f, EventType.Repaint);
        }
        foreach (var p in m_vertsP2)
        {
            Handles.SphereHandleCap(0, p, Quaternion.identity, 0.03f, EventType.Repaint);
        }*/
        //Debug.Log($"OnDrawGizmos: position={aposition}, tangent={tangent}, upVector={upVector}");
    }

    private void SampleSplineWidth(int splineIndex, float time, out Vector3 p1, out Vector3 p2)
    {
        // Sample the spline at the given time to get position, tangent, and up vector
        hairContainer.Evaluate(splineIndex, time, out aposition, out tangent, out upVector);
        float3 right = Vector3.Cross(upVector, tangent).normalized;
        if (time != 1f)
        {
            p1 = aposition + (right * width / 2) - (float3)transform.position;
            p2 = aposition - (right * width / 2) - (float3)transform.position;
        }
        else
        {
            p1 = aposition + (right * width / 4) - (float3)transform.position;
            p2 = aposition - (right * width / 4) - (float3)transform.position;
        }

    }
    private void GetVerts()
    {
        m_vertsP1 = new List<Vector3>();
        m_vertsP2 = new List<Vector3>();


        Vector3 p1;
        Vector3 p2;
        for (int j = 0; j < hairContainer.Splines.Count; j++)
        {
            resolution = hairContainer.Splines[j].Count;
            float step = 1f / (float)resolution;
            for (int i = 0; i <= resolution; i++)
            {
                float t = step * i;
                SampleSplineWidth(j, t, out p1, out p2);
                m_vertsP1.Add(p1);
                m_vertsP2.Add(p2);
            }
            SampleSplineWidth(j, 1f, out p1, out p2);
            m_vertsP1.Add(p1);
            m_vertsP2.Add(p2);
        }
    }

    private void CreateMesh()
    {
        List<Vector3> verts = new List<Vector3>();
        List<int> tris = new List<int>();
        List<Vector2> uvs = new List<Vector2>();
        m_vertexT.Clear();
        float uvOffset = 0f;
        int offset = 0;
        int length = m_vertsP2.Count;
        for (int currentSpline = 0; currentSpline < hairContainer.Splines.Count; currentSpline++)
        {
            int splineOffset = (int)(resolution * currentSpline);
            splineOffset += currentSpline;
            // iterate verts and build a face
            for (int currentPoint = 1; currentPoint < resolution + 1; currentPoint++)
            {
                int vertoffset = splineOffset + currentPoint;
                Vector3 p1 = m_vertsP1[vertoffset - 1];
                Vector3 p2 = m_vertsP2[vertoffset - 1];
                Vector3 p3 = m_vertsP1[vertoffset];
                Vector3 p4 = m_vertsP2[vertoffset];

                offset = (int)(4 * resolution * currentSpline);
                offset += 4 * (currentPoint - 1);

                int t1 = offset + 0;
                int t2 = offset + 2;
                int t3 = offset + 3;

                int t4 = offset + 3;
                int t5 = offset + 1;
                int t6 = offset + 0;

                verts.AddRange(new List<Vector3> { p1, p2, p3, p4 });
                tris.AddRange(new List<int> { t1, t2, t3, t4, t5, t6 });

                // remember where along the strand these 4 verts are, used for painting
                float tStart = (currentPoint - 1) / resolution;
                float tEnd = currentPoint / resolution;
                m_vertexT.AddRange(new List<float> { tStart, tStart, tEnd, tEnd });

                float distance = Vector3.Distance(p1, p3) / 4f;
                float uvDistance = uvOffset + distance;
                uvs.AddRange(new List<Vector2> { new Vector2(uvOffset, 0),
                    new Vector2(uvOffset, 1), new Vector2(uvDistance, 0), new Vector2(uvDistance, 1) });
                uvOffset += distance;
            }
        }
        Mesh mesh = new Mesh();
        mesh.SetVertices(verts);
        mesh.SetTriangles(tris, 0);
        mesh.SetUVs(0, uvs);
        mesh.SetColors(BuildColors());
        meshFilter.mesh = mesh;
    }

    // Ask for the paint color of every vertex (white if nothing is painting yet)
    private List<Color> BuildColors()
    {
        List<Color> colors = new List<Color>();
        for (int i = 0; i < m_vertexT.Count; i++)
        {
            colors.Add(ColorAt != null ? ColorAt(m_vertexT[i]) : Color.white);
        }
        return colors;
    }

    // Update only the vertex colors of the current mesh, cheaper than rebuilding it
    public void UpdateColors()
    {
        Mesh mesh = meshFilter.sharedMesh;
        if (mesh == null) return;
        mesh.SetColors(BuildColors());
    }
}