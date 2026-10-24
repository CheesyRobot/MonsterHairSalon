using UnityEngine;

public class HairKnotConnector : MonoBehaviour
{
    private HairStrand mainHairScript;
    private int knotIndex;

    // The paint brush reads these to know which strand and knot it touched
    public HairStrand Strand => mainHairScript;
    public int KnotIndex => knotIndex;

    public void Setup(HairStrand hairScript, int index)
    {
        mainHairScript = hairScript;
        knotIndex = index;
    }

    private void OnTriggerEnter(Collider other)
    {
        // Checking if the colliding object is the scissors or paintbrush
        if (other.gameObject.name == "Scissors")// || other.CompareTag("Scissors"))
        {
            Debug.Log($"Hair cut at segment: {knotIndex}");
            mainHairScript.CutHairAt(knotIndex);
        }
        else if (other.gameObject.name == "PaintBrush")
        {
            Debug.Log("Hair touched with paintbrush!");
        }
    }
}