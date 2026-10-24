using System.Collections.Generic;
using UnityEngine;

public class MeshPrefabDistributor : MonoBehaviour
{
    [Header("Settings")]
    [Tooltip("The prefab you want to distribute across the mesh.")]
    [SerializeField] private GameObject prefab;

    [Tooltip("The target number of prefabs to instantiate.")]
    [SerializeField] private int targetPrefabCount = 50;

    [Tooltip("The minimum allowed world-space distance between any two spawned prefabs.")]
    [SerializeField] private float minDistance = 0.5f;

    [Tooltip("How many times the script will try to find a valid spot for a single prefab before skipping it to prevent freezing.")]
    [SerializeField] private int maxAttemptsPerPrefab = 30;

    // A standard List<T> is used dynamically during generation, 
    // then trimmed to a tight, performant continuous layout.
    private List<GameObject> instantiatedPrefabsList = new List<GameObject>();
    private GameObject[] instantiatedPrefabs;

    /// <summary>
    /// Public property to access the optimized collection from other scripts.
    /// </summary>
    public GameObject[] InstantiatedPrefabs => instantiatedPrefabs;

    private void Start()
    {
        DistributePrefabs();
    }

    public void DistributePrefabs()
    {
        Mesh mesh = GetMesh();
        if (mesh == null)
        {
            Debug.LogError($"[MeshPrefabDistributor] No Mesh found on {gameObject.name}!", this);
            return;
        }

        if (prefab == null)
        {
            Debug.LogError("[MeshPrefabDistributor] No Prefab assigned in the inspector!", this);
            return;
        }

        ClearExistingPrefabs();
        instantiatedPrefabsList.Clear();

        Vector3[] vertices = mesh.vertices;
        Vector3[] normals = mesh.normals;
        int[] triangles = mesh.triangles;

        // 1. Calculate triangle cumulative areas for weighting
        int numTriangles = triangles.Length / 3;
        float[] cumulativeAreas = new float[numTriangles];
        float totalArea = 0f;

        for (int i = 0; i < numTriangles; i++)
        {
            Vector3 vA = vertices[triangles[i * 3]];
            Vector3 vB = vertices[triangles[i * 3 + 1]];
            Vector3 vC = vertices[triangles[i * 3 + 2]];

            float triArea = Vector3.Cross(vB - vA, vC - vA).magnitude * 0.5f;
            totalArea += triArea;
            cumulativeAreas[i] = totalArea;
        }

        float minDistanceSqr = minDistance * minDistance;

        // 2. Rejection Sampling Loop
        for (int i = 0; i < targetPrefabCount; i++)
        {
            bool validSpotFound = false;
            Vector3 finalWorldPos = Vector3.zero;
            Vector3 finalWorldNormal = Vector3.zero;

            // Attempt to find a valid coordinate up to maxAttemptsPerPrefab times
            for (int attempt = 0; attempt < maxAttemptsPerPrefab; attempt++)
            {
                // Area-weighted distribution sample
                float targetAreaSample = Random.value * totalArea;
                int triIndex = System.Array.BinarySearch(cumulativeAreas, targetAreaSample);
                if (triIndex < 0) triIndex = ~triIndex;
                triIndex = Mathf.Clamp(triIndex, 0, numTriangles - 1);

                int iA = triangles[triIndex * 3];
                int iB = triangles[triIndex * 3 + 1];
                int iC = triangles[triIndex * 3 + 2];

                // Uniform Barycentric Coordinates
                float r1 = Random.value;
                float r2 = Random.value;
                //float r1 = 0f;
                //float r2 = 0f;
                if (r1 + r2 > 1f)
                {
                    r1 = 1f - r1;
                    r2 = 1f - r2;
                }
                float r3 = 1f - r1 - r2;
                //float r3 = 1f;

                Vector3 localPos = r1 * vertices[iA] + r2 * vertices[iB] + r3 * vertices[iC];
                Vector3 localNormal = (r1 * normals[iA] + r2 * normals[iB] + r3 * normals[iC]).normalized;

                Vector3 candidateWorldPos = transform.TransformPoint(localPos);

                // 3. Proximity Check
                if (IsPositionValid(candidateWorldPos, minDistanceSqr))
                {
                    finalWorldPos = candidateWorldPos;
                    finalWorldNormal = transform.TransformDirection(localNormal);
                    validSpotFound = true;
                    break; // Exit the attempt loop early
                }
            }

            // If a valid spot was secured, instantiate the asset
            if (validSpotFound)
            {
                Quaternion worldRot = Quaternion.FromToRotation(Vector3.up, finalWorldNormal);
                GameObject spawnedInstance = Instantiate(prefab, finalWorldPos, worldRot, transform);
                instantiatedPrefabsList.Add(spawnedInstance);
            }
        }

        // Convert the dynamic list back to a highly optimal contiguous array for external runtime lookups
        instantiatedPrefabs = instantiatedPrefabsList.ToArray();
    }

    /// <summary>
    /// Checks if a candidate position is far enough from all previously spawned prefabs.
    /// Uses squared magnitude calculation to avoid costly square root operations.
    /// </summary>
    private bool IsPositionValid(Vector3 candidatePos, float minDistanceSqr)
    {
        for (int i = 0; i < instantiatedPrefabsList.Count; i++)
        {
            // Vector3.sqrMagnitude avoids heavy CPU calculations compared to Vector3.Distance
            if ((instantiatedPrefabsList[i].transform.position - candidatePos).sqrMagnitude < minDistanceSqr)
            {
                return false; // Too close!
            }
        }
        return true; // Distance constraint satisfied
    }

    private Mesh GetMesh()
    {
        if (TryGetComponent(out MeshFilter filter) && filter.sharedMesh != null)
        {
            return filter.sharedMesh;
        }
        if (TryGetComponent(out SkinnedMeshRenderer skinned) && skinned.sharedMesh != null)
        {
            return skinned.sharedMesh;
        }
        return null;
    }

    private void ClearExistingPrefabs()
    {
        if (instantiatedPrefabs != null)
        {
            for (int i = 0; i < instantiatedPrefabs.Length; i++)
            {
                if (instantiatedPrefabs[i] != null)
                {
                    if (Application.isPlaying) Destroy(instantiatedPrefabs[i]);
                    else DestroyImmediate(instantiatedPrefabs[i]);
                }
            }
        }
        instantiatedPrefabs = null;
    }
}
