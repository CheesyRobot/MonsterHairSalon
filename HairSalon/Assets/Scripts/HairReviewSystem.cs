using UnityEngine;
using TMPro;

// Scores a finished hair from 1 to 5 stars in Length, Color, Speed and Overall experience
public class HairReviewSystem : MonoBehaviour
{
    [Header("References")]
    public MeshPrefabDistributor hairDistributor;

    [Header("Review board texts")]
    public TMP_Text lengthText;
    public TMP_Text colorText;
    public TMP_Text speedText;
    public TMP_Text experienceText;

    [Header("Length")]
    [Tooltip("Average length error (1 = the whole original strand length) that still gives 5 stars")]
    public float lengthPerfectError = 0.05f;
    [Tooltip("Average length error that gives 1 star")]
    public float lengthWorstError = 0.5f;

    [Header("Color")]
    [Tooltip("Average color error (0..1, white to black is 1) that still gives 5 stars")]
    public float colorPerfectError = 0.05f;
    [Tooltip("Average color error that gives 1 star")]
    public float colorWorstError = 0.4f;

    [Header("Speed")]
    public float baseSeconds = 20f;     
    public float secondsPerWish = 25f;  // extra par time for every wish
    [Tooltip("Taking this many times the par time gives 1 star")]
    public float slowestMultiplier = 3f;

    [Header("Overall experience weights")]
    public float lengthWeight = 0.35f;
    public float colorWeight = 0.35f;
    public float speedWeight = 0.30f;

    public struct Result
    {
        public int length, color, speed, overall;
    }
    public Result LastResult { get; private set; }

    private bool active;
    private float targetLengthRatio;
    private Color targetColor;
    private int wishCount;
    private float startTime;

    
    public void BeginSession(float lengthRatio, Color color, int wishes)
    {
        targetLengthRatio = lengthRatio;
        targetColor = color;
        wishCount = wishes;
        startTime = Time.time;
        active = true;
    }

    
    public Result Finish()
    {
        if (!active)
        {
            Debug.LogWarning("[HairReviewSystem] No client is being served.");
            return LastResult;
        }
        active = false;

        Result r = new Result();
        r.length = ScoreLength();
        r.color = ScoreColor();
        r.speed = ScoreSpeed();

        float totalWeight = lengthWeight + colorWeight + speedWeight;
        float weighted = (r.length * lengthWeight + r.color * colorWeight + r.speed * speedWeight) / totalWeight;
        r.overall = Mathf.Clamp(Mathf.RoundToInt(weighted), 1, 5);

        LastResult = r;
        ShowResult(r);
        return r;
    }

    // Every strand is compared to the wanted length, then the errors are averaged,
    private int ScoreLength()
    {
        var strands = hairDistributor != null ? hairDistributor.Strands : null;
        if (strands == null || strands.Count == 0) return 1;

        float errorSum = 0f;
        int counted = 0;
        foreach (HairStrand strand in strands)
        {
            if (strand == null || strand.StartLength <= 0f) continue;
            float ratio = strand.GetTotalLength() / strand.StartLength;
            errorSum += Mathf.Abs(ratio - targetLengthRatio);
            counted++;
        }
        if (counted == 0) return 1;

        return ErrorToStars(errorSum / counted, lengthPerfectError, lengthWorstError);
    }

    // Every strand's average color is compared to the wanted color
    private int ScoreColor()
    {
        var strands = hairDistributor != null ? hairDistributor.Strands : null;
        if (strands == null || strands.Count == 0) return 1;

        float errorSum = 0f;
        int counted = 0;
        foreach (HairStrand strand in strands)
        {
            if (strand == null) continue;
            errorSum += ColorDistance(strand.GetAverageColor(), targetColor);
            counted++;
        }
        if (counted == 0) return 1;

        return ErrorToStars(errorSum / counted, colorPerfectError, colorWorstError);
    }

    
    private int ScoreSpeed()
    {
        float par = baseSeconds + secondsPerWish * wishCount;
        float elapsed = Time.time - startTime;
        return ErrorToStars(elapsed / par, 1f, slowestMultiplier);
    }

    
    private static int ErrorToStars(float error, float perfect, float worst)
    {
        float t = Mathf.InverseLerp(worst, perfect, error); // 1 at perfect 0 at worst
        return 1 + Mathf.RoundToInt(t * 4f);
    }

    
    private static float ColorDistance(Color a, Color b)
    {
        return new Vector3(a.r - b.r, a.g - b.g, a.b - b.b).magnitude / 1.732f;
    }

    private void ShowResult(Result r)
    {
        if (lengthText != null) lengthText.text = "Length: " + Stars(r.length);
        if (colorText != null) colorText.text = "Color: " + Stars(r.color);
        if (speedText != null) speedText.text = "Speed: " + Stars(r.speed);
        if (experienceText != null) experienceText.text = "Over all experience: " + Stars(r.overall);
        Debug.Log($"Review - Length {r.length}, Color {r.color}, Speed {r.speed}, Overall {r.overall}");
    }

    
    private static string Stars(int n)
    {
        n = Mathf.Clamp(n, 0, 5);
        return new string('*', n);
    }
}
