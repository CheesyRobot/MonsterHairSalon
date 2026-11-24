using System.Collections.Generic;
using UnityEngine;

public class Scissors : CuttingTool
{
    private Collider cuttingCollider;
    private AudioSource audioSource;
    private Animator animator;
    public List<AudioClip> clipList = new List<AudioClip>();
    private void Start()
    {
        cuttingCollider = GetComponent<Collider>();
        cuttingCollider.enabled = false;
        audioSource = GetComponent<AudioSource>();
        animator = GetComponent<Animator>();
    }

    private void Update()
    {
        if (!audioSource.isPlaying) { DisableCutting(); animator.speed = 1f; }
    }

    public void EnableCutting()
    {
        cuttingCollider.enabled = true;
        if (audioSource != null && clipList.Count != 0 && !audioSource.isPlaying)
        {
            audioSource.pitch = Random.Range(0.75f, 1.25f);
            AudioClip clip = clipList[Random.Range(0, clipList.Count-1)];
            audioSource.PlayOneShot(clip);
            if (animator != null)
            {
                AnimatorStateInfo anim = animator.GetCurrentAnimatorStateInfo(0);
                animator.speed = (anim.length / clip.length) * audioSource.pitch;
            }
        }
        if (animator != null) {
            animator.SetTrigger("Cut");
        }
    }

    public void DisableCutting()
    {
        cuttingCollider.enabled = false;
    }
}
