using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "HairStyleSO", menuName = "Scriptable Objects/HairStyleSO")]
public class HairStyleSO : ScriptableObject
{
    [SerializeField]
    public List<HairStrand> hairStrandsList;
}
