using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "HairStyleSO", menuName = "Scriptable Objects/HairStyleSO")]
public class HairStyleSO : ScriptableObject
{
    [Header("Style")]
    public GameObject hairPrefab; // the hair strand prefab this style uses
    public int strandCount = 50;
    public float minDistance = 0.016f;

    // Scene objects can't be saved in an asset, so this stays empty after a restart
    //public List<HairStrand> hairStrandsList;
    public List<HairStrandProperty> hairList;

    
}

[System.Serializable]
public class HairStrandProperty
{
    public string sName;
    public Vector3 transformPos;
    public Quaternion transformRot;
    public float slength;
    public int segments;
    // A color property can be added here for the singular hair strand
}