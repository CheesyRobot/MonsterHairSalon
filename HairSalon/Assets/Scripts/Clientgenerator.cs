using System.Collections.Generic;
using UnityEngine;
using TMPro;

public class ClientGenerator : MonoBehaviour
{
    
    public TMP_Text clientText;
    public TMP_Text typeText;
    public TMP_Text wantsText;

    //Lists the random client is picked from, edit these in the Inspector or here
    public string[] names = { "Morgana", "Lilith", "Isolde", "Beatrice", "Seraphina", "Willow", "Rowena", "Esmeralda", "Ophelia", "Griselda", "Mavis", "Persephone" };
    public string[] types = { "Zombie", "Vampire", "Werewolf", "Ghost", "Witch", "Mummy", "Banshee", "Ghoul" };
    public string[] wants = { "Haircut", "Hair dyeing", "Hair washing", "Hair styling", "Hair braiding", "Hair trim" };

    //How many things a client can want at most, every client wants at least 1
    public int maxWants = 3;

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

    //Hook this to the On Pressed event of the yes button
    public void Accept()
    {
        Debug.Log("Accepted " + currentName + " the " + currentType + " who wants: " + string.Join(", ", currentWants));
        //TODO: start the haircut / dyeing job for this client here
    }

    //Hook this to the On Pressed event of the no button
    //Turns the client away and shows a new one
    public void Decline()
    {
        Debug.Log("Declined " + currentName);
        NextClient();
    }
}