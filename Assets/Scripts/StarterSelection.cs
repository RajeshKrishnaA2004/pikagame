
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class StarterSelection : MonoBehaviour
{
    public Button[] creatureButtons;
    public string[] creatureNames;
    public string[] creatureTypes;

    public TMP_Text selectedCreatureText;
    public Button confirmButton;
    public DialogueManager dialogueManager;

    private int selectedIndex = -1;

    void Start()
    {
        confirmButton.interactable = false;

        for (int i = 0; i < creatureButtons.Length; i++)
        {
            int index = i;
            creatureButtons[i].onClick.AddListener(
                () => SelectCreature(index)
            );
        }

        confirmButton.onClick.AddListener(ConfirmChoice);
        selectedCreatureText.text = "Selected: None";
    }

    void SelectCreature(int index)
    {
        selectedIndex = index;
        selectedCreatureText.text =
            "Selected: " + creatureNames[index];

        confirmButton.interactable = true;
    }

    string GetWeakness(string type)
    {
        switch (type.Trim().ToLower())
        {
            case "fire":
                return "Water";
            case "grass":
                return "Fire";
            case "electric":
                return "Ground";
            default:
                return "Unknown";
        }
    }

    void ConfirmChoice()
    {
        if (selectedIndex < 0)
            return;

        if (selectedIndex >= creatureNames.Length ||
            selectedIndex >= creatureTypes.Length)
        {
            Debug.LogError("Creature names or types are missing!");
            return;
        }

        string name = creatureNames[selectedIndex];
        string type = creatureTypes[selectedIndex];
        string weakness = GetWeakness(type);

        PlayerPrefs.SetString("StarterCreature", name);
        PlayerPrefs.SetString("StarterType", type);
        PlayerPrefs.Save();

        foreach (Button button in creatureButtons)
            button.interactable = false;

        confirmButton.interactable = false;

        dialogueManager.StartStarterDialogue(
            name, type, weakness
        );
    }
}