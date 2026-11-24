using UnityEngine;

public class HairKnotConnector : MonoBehaviour
{
    private HairStrandJob mainHairScriptJob;
    private int knotIndex;

    // The paint brush reads these to know which strand and knot it touched
    public HairStrandJob Strand => mainHairScriptJob;
    public int KnotIndex => knotIndex;

    public void Setup(HairStrandJob hairScript, int index)
    {
        mainHairScriptJob = hairScript;
        knotIndex = index;
    }

    private void OnTriggerEnter(Collider other)
    {
        // Checking if the colliding object is the scissors or paintbrush
        if (other.CompareTag("CuttingTool"))// || other.CompareTag("Scissors"))
        {
            //Debug.Log($"Hair cut at segment: {knotIndex}");
            if (mainHairScriptJob != null)
            { mainHairScriptJob.CutHairAt(knotIndex, other.GetComponent<CuttingTool>()); }
        }
        else if (other.CompareTag("PaintTool"))
        {
            //Debug.Log("Hair touched with paintbrush!");
        }
    }
}