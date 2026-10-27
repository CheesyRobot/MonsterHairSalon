using System.Collections.Generic;
using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class ClientGenerator : MonoBehaviour
{

    public TMP_Text clientText;
    public TMP_Text typeText;
    public TMP_Text wantsText;

    //Lists the random client is picked from, edit these in the Inspector or here
    public string[] names = { "Morgana", "Lilith", "Isolde", "Beatrice", "Seraphina", "Willow", "Rowena", "Esmeralda", "Ophelia", "Griselda", "Mavis", "Persephone" };
    public string[] types = { "Zombie", "Vampire", "Werewolf" };
    public string[] wants = { "Haircut", "Hair dyeing", "Hair washing", "Hair styling", "Hair braiding", "Hair trim" };

    //How many things a client can want at most, every client wants at least 1
    public int maxWants = 3;

    //The hair a client type gets, the type has to be written exactly like in the types list
    [System.Serializable]
    public class HairProfile
    {
        public string type;
        public HairStyleSO hairStyle;
    }

    [Header("Hair")]
    public MeshPrefabDistributor hairDistributor;
    public HairProfile[] hairProfiles;

    //The skin tint a client type gets, the type has to be written exactly like in the types list
    [System.Serializable]
    public class SkinProfile
    {
        public string type;
        [ColorUsage(false, true)] public Color skinColor = Color.white; //HDR, values above 1 make the skin brighter
    }

    //A mesh that shows skin and which of its materials is the skin
    [System.Serializable]
    public class SkinTarget
    {
        public Renderer renderer;
        public int materialIndex = 0;
    }

    [Header("Skin")]
    public SkinTarget[] skinTargets; //every mesh that should change color
    public string colorProperty = "_BaseColor"; //URP Lit uses _BaseColor, Built-in uses _Color
    public SkinProfile[] skinProfiles;

    private MaterialPropertyBlock skinBlock;

    [Header("Client Picture")]
    public Image faceImage; //gets the skin color of the client type
    public Image hairImage; //gets the color of the client's hair

    [Header("Client Character")]
    public GameObject clientObject; //StylizedFemale, only shown after the client is accepted

    //The client that is currently shown on the TV
    public string currentName { get; private set; }
    public string currentType { get; private set; }
    //All the things the current client wants
    public List<string> currentWants = new List<string>();

    private void Start()
    {
        NextClient();
    }

    //Picks a random name, monster type and 1 to maxWants different wishes and shows them on the TV
    public void NextClient()
    {
        currentName = names[Random.Range(0, names.Length)];
        currentType = types[Random.Range(0, types.Length)];

        //The character is hidden until the client is accepted, the TV picture shows the client before that
        if (clientObject != null) clientObject.SetActive(false);
        SetPicture();

        //How many things this client wants (Random.Range with ints leaves out the max value)
        int count = Mathf.Min(Random.Range(1, maxWants + 1), wants.Length);

        //Take items out of a copy of the list so the same thing is never picked twice
        List<string> pool = new List<string>(wants);
        currentWants.Clear();
        for (int i = 0; i < count; i++)
        {
            int pick = Random.Range(0, pool.Count);
            currentWants.Add(pool[pick]);
            pool.RemoveAt(pick);
        }

        clientText.text = "Client: " + currentName;
        typeText.text = "Type: " + currentType;
        wantsText.text = "Wants: " + string.Join(", ", currentWants);
    }

    //Finds the hair profile of the current type and spawns that hair on the head
    private void SpawnHair()
    {
        foreach (var profile in hairProfiles)
        {
            if (profile.type == currentType)
            {
                if (hairDistributor != null) hairDistributor.SetupAndDistribute(profile.hairStyle);
                return;
            }
        }
        Debug.LogWarning("No hair profile for type " + currentType);
    }

    //Colors the face and hair on the TV picture for the current type
    private void SetPicture()
    {
        foreach (var profile in skinProfiles)
        {
            if (profile.type == currentType)
            {
                if (faceImage != null) faceImage.color = ToUIColor(profile.skinColor);
                break;
            }
        }

        foreach (var profile in hairProfiles)
        {
            if (profile.type == currentType)
            {
                SetHairPicture(profile.hairStyle);
                break;
            }
        }
    }

    //UI colors can't go above 1, so bright HDR colors are scaled down instead of turning white
    private Color ToUIColor(Color color)
    {
        float max = Mathf.Max(color.r, color.g, color.b);
        if (max > 1f) color /= max;
        color.a = 1f;
        return color;
    }

    //Colors the hair on the TV picture with the color of the hair prefab
    private void SetHairPicture(HairStyleSO style)
    {
        if (hairImage == null || style == null || style.hairPrefab == null) return;
        if (style.hairPrefab.TryGetComponent(out HairStrand strand)) hairImage.color = strand.BaseColor;
    }

    //Finds the skin profile of the current type and tints the skin with it
    private void SetSkin()
    {
        foreach (var profile in skinProfiles)
        {
            if (profile.type == currentType)
            {
                //property block tints these renderers only and doesn't change the material asset
                if (skinBlock == null) skinBlock = new MaterialPropertyBlock();
                if (skinTargets != null)
                {
                    foreach (var target in skinTargets)
                    {
                        if (target.renderer == null) continue;
                        target.renderer.GetPropertyBlock(skinBlock, target.materialIndex);
                        skinBlock.SetColor(colorProperty, profile.skinColor);
                        target.renderer.SetPropertyBlock(skinBlock, target.materialIndex);
                    }
                }
                return;
            }
        }
        Debug.LogWarning("No skin profile for type " + currentType);
    }

    //Shows the character on the chair and gives her the hair and skin of the current type
    private void SpawnClient()
    {
        if (clientObject != null) clientObject.SetActive(true);
        SpawnHair();
        SetSkin();
    }

    //Hook this to the On Pressed event of the yes button
    public void Accept()
    {
        Debug.Log("Accepted " + currentName + " the " + currentType + " who wants: " + string.Join(", ", currentWants));
        SpawnClient();

    }

    //Turns the client away and shows a new one
    public void Decline()
    {
        Debug.Log("Declined " + currentName);
        NextClient();
    }
}