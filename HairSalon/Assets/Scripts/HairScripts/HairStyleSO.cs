using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "HairStyleSO", menuName = "Scriptable Objects/HairStyleSO")]
public class HairStyleSO : ScriptableObject
{
    [Header("Style")]
    public GameObject hairPrefab; // the hair strand prefab this style uses
    public int strandCount = 50;
    public float minDistance = 0.5f;

    // Scene objects can't be saved in an asset, so this stays empty after a restart
    [SerializeField]
    public List<HairStrand> hairStrandsList;
}