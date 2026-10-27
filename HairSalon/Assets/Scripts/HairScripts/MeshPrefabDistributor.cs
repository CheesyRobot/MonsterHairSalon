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

    private List<HairStrand> instantiatedPrefabsList = new List<HairStrand>();

    [Header("Data Saving")]
    [Tooltip("The ScriptableObject asset where references will be saved.")]
    [SerializeField] private HairStyleSO targetContainer;

    [Header("Client Hair")]
    [Tooltip("Turn this off when the ClientGenerator spawns the hair, otherwise the hair is spawned twice.")]
    [SerializeField] private bool distributeOnStart = false;

    private void Start()
    {
        if (!distributeOnStart) return;
        DistributePrefabs();
        Debug.Log(instantiatedPrefabsList.Count);
    }

    // Called by the ClientGenerator, swaps the old hair for the hair of the new client
    public void SetupAndDistribute(GameObject newPrefab, int count, float distance)
    {
        prefab = newPrefab;
        targetPrefabCount = count;
        minDistance = distance;

        ClearHair();
        DistributePrefabs();
    }

    // Same thing but reads everything from a HairStyleSO
    public void SetupAndDistribute(HairStyleSO style)
    {
        if (style == null)
        {
            Debug.LogError("[MeshPrefabDistributor] The hair profile has no Hair Style assigned!", this);
            return;
        }

        if (style.hairPrefab == null)
        {
            Debug.LogError($"[MeshPrefabDistributor] Hair Style {style.name} has no Hair Prefab assigned!", style);
            return;
        }

        SetupAndDistribute(style.hairPrefab, style.strandCount, style.minDistance);
    }

    // Removes all the hair strands that were spawned before
    public void ClearHair()
    {
        foreach (var strand in instantiatedPrefabsList)
        {
            if (strand != null) Destroy(strand.gameObject);
        }
        instantiatedPrefabsList.Clear();
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
            DestroyBakedMesh();
            return;
        }

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
                // For undos in editor
#if UNITY_EDITOR
                GameObject spawnedInstance = UnityEditor.PrefabUtility.InstantiatePrefab(prefab, transform) as GameObject;
                if (spawnedInstance != null)
                {
                    spawnedInstance.transform.position = finalWorldPos;
                    spawnedInstance.transform.rotation = worldRot;
                    UnityEditor.Undo.RegisterCreatedObjectUndo(spawnedInstance, "Distributed Prefab");
                }
#else
                GameObject spawnedInstance = Instantiate(prefab, finalWorldPos, worldRot, transform);
#endif
                instantiatedPrefabsList.Add(spawnedInstance.GetComponent<HairStrand>());
            }
        }

        DestroyBakedMesh();
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
            // The shared mesh is in the bind pose, so bake it to get the current pose of the character
            DestroyBakedMesh();
            bakedMesh = new Mesh();
            skinned.BakeMesh(bakedMesh);
            return bakedMesh;
        }
        return null;
    }

    // Snapshot of a skinned mesh, only needed while the hair is being placed
    private Mesh bakedMesh;

    private void DestroyBakedMesh()
    {
        if (bakedMesh == null) return;
        if (Application.isPlaying) Destroy(bakedMesh);
        else DestroyImmediate(bakedMesh);
        bakedMesh = null;
    }

    public void SaveToScriptableObject()
    {
        if (targetContainer == null)
        {
            Debug.LogError("[MeshPrefabDistributor] Target Container (ScriptableObject) is not assigned!", this);
            return;
        }

        if (instantiatedPrefabsList == null || instantiatedPrefabsList.Count == 0)
        {
            Debug.LogWarning("[MeshPrefabDistributor] No prefabs have been instantiated yet to save.", this);
            return;
        }

        targetContainer.hairStrandsList = instantiatedPrefabsList;

#if UNITY_EDITOR
        UnityEditor.EditorUtility.SetDirty(targetContainer);
        UnityEditor.AssetDatabase.SaveAssets();
        Debug.Log($"[MeshPrefabDistributor] Successfully saved {instantiatedPrefabsList.Count} prefab references to {targetContainer.name}!", targetContainer);
#endif
    }

    /* //function for clearing an array
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
    }*/
}