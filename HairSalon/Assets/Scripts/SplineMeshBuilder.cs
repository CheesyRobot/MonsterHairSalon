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

    private void SampleSplineWidth(int splineIndex,float time, out Vector3 p1, out Vector3 p2)
    {
        // Sample the spline at the given time to get position, tangent, and up vector
        hairContainer.Evaluate(splineIndex, time, out aposition, out tangent, out upVector);
        float3 right = Vector3.Cross(upVector, tangent).normalized;
        if(time != 1f)
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
        float uvOffset = 0f;
        int offset = 0;
        int length = m_vertsP2.Count;
        for (int currentSpline = 0; currentSpline < hairContainer.Splines.Count; currentSpline++)
        {
            int splineOffset = (int)(resolution * currentSpline);
            splineOffset += currentSpline; 
            // iterate verts and build a face
            for (int currentPoint = 1; currentPoint < resolution+1; currentPoint++)
            {
                int vertoffset = splineOffset + currentPoint;
                Vector3 p1 = m_vertsP1[vertoffset - 1];
                Vector3 p2 = m_vertsP2[vertoffset - 1];
                Vector3 p3 =m_vertsP1[vertoffset];
                Vector3 p4 = m_vertsP2[vertoffset];

                offset = (int)(4 * resolution * currentSpline);
                offset += 4* (currentPoint - 1);

                int t1 = offset + 0;
                int t2 = offset + 2;
                int t3 = offset + 3;

                int t4 = offset + 3;
                int t5 = offset + 1;
                int t6 = offset + 0;

                verts.AddRange(new List<Vector3> { p1, p2, p3, p4 });
                tris.AddRange(new List<int> { t1, t2, t3, t4, t5, t6 });

                float distance = Vector3.Distance(p1, p3)/4f;
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
        meshFilter.mesh = mesh;
    }
}
