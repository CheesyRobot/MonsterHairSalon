using UnityEngine;

public class Shaver : CuttingTool
{
    private Collider cuttingCollider;
    private AudioSource audioSource;
    private void Start()
    {
        cuttingCollider = GetComponent<Collider>();
        cuttingCollider.enabled = false;
        audioSource = GetComponent<AudioSource>();
    }

    public void EnableCutting()
    {
        cuttingCollider.enabled = true;
        if (audioSource.generator != null)
        {
            audioSource.pitch = Random.Range(0.75f, 1.25f);
            audioSource.Play();
        }
    }

    public void DisableCutting()
    {
        cuttingCollider.enabled = false;
        if (audioSource.generator != null)
        {
            audioSource.Stop();
        }
    }
}
