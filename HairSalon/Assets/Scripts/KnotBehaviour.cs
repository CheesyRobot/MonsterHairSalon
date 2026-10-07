using UnityEngine;

// Simple component attached to each knot visual. Reports trigger events to the parent HairStrandBehaviour.
public class KnotBehaviour : MonoBehaviour
{
    public TriggerEnterInfo triggerEnterInfo;

    private void OnTriggerEnter(Collider other)
    {
        triggerEnterInfo.other = other;
        // Use SendMessageUpwards to avoid a compile-time dependency on HairStrandBehaviour
        // The parent (or ancestor) can implement OnKnotTouchedByIndex(int index)
        try
        {
            gameObject.SendMessageUpwards("OnKnotTouchedByIndex", triggerEnterInfo, SendMessageOptions.DontRequireReceiver);
        }
        catch { }
    }
}

[System.Serializable]
public class TriggerEnterInfo
{
    public int knotIndex;
    public Collider other;
}