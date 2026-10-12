using UnityEngine;

public class HairKnotConnector : MonoBehaviour
{
    private HairStrand mainHairScript;
    private int knotIndex;

    public void Setup(HairStrand hairScript, int index)
    {
        mainHairScript = hairScript;
        knotIndex = index;
    }

    private void OnTriggerEnter(Collider other)
    {
        // 1. Tikriname pagal objekto pavadinimą arba geriau pagal Tag / Komponentą
        if (other.gameObject.name == "Scissors" || other.CompareTag("Scissors"))
        {
            Debug.Log($"Plaukas nukirptas ties segmentu: {knotIndex}");
            mainHairScript.CutHairAt(knotIndex);
        }
        else if (other.gameObject.name == "PaintBrush")
        {
            // Čia galite įrašyti kodą, kuris nudažo Line Renderer spalvą
            Debug.Log("Plaukas liečiamas teptuku!");
        }
    }
}
