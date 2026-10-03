using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class CreaturePartyManager : MonoBehaviour
{
    [Header("Main Pokémon Slots")]
    public Image starterSlot;
    public Image capturedSlot;

    [Header("Backpack Display")]
    public GameObject bagPanel;
    public Image starterImage;
    public Image activeImage;

    [Header("Backpack Slots")]
    public Image[] backpackSlotImages;
    public Button[] backpackSlotButtons;
    public Button moveToActiveButton;

    [Header("Creature Database")]
    public CreatureData[] creatureDatabase;

    private const int BackpackCapacity = 5;

    private const string StarterKey = "StarterCreature";
    private const string CapturedKey = "Party_Captured";
    private const string BackpackKey = "Party_Bag";

    private string capturedCreature = "";
    private List<string> backpack = new List<string>();

    private int selectedSlot = -1;
    void Start()
    {
        capturedCreature = "";
        backpack.Clear();

        if (bagPanel != null)
            bagPanel.SetActive(false);

        if (backpackSlotButtons != null)
        {
            for (int i = 0; i < backpackSlotButtons.Length; i++)
            {
                int index = i;

                if (backpackSlotButtons[i] != null)
                {
                    backpackSlotButtons[i].onClick.AddListener(
                        () => SelectBackpackSlot(index));
                }
            }
        }

        if (moveToActiveButton != null)
        {
            moveToActiveButton.onClick.AddListener(
                MoveSelectedToActiveSlot);
        }

        RefreshUI();
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.E) && bagPanel != null)
        {
            bagPanel.SetActive(!bagPanel.activeSelf);
            RefreshUI();
        }
    }

    void LoadParty()
    {
        capturedCreature = "";
        backpack.Clear();
    }

    void SaveParty()
    {
        PlayerPrefs.SetString(CapturedKey, capturedCreature);
        PlayerPrefs.SetString(
            BackpackKey,
            string.Join("\n", backpack));

        PlayerPrefs.Save();
    }

    CreatureData FindCreature(string creatureName)
    {
        if (creatureDatabase == null)
            return null;

        foreach (CreatureData creature in creatureDatabase)
        {
            if (creature != null &&
                creature.creatureName == creatureName)
            {
                return creature;
            }
        }

        return null;
    }

    void SetSlotImage(Image slot, string creatureName)
    {
        if (slot == null)
            return;

        CreatureData creature = FindCreature(creatureName);

        slot.sprite = creature != null
            ? creature.battleSprite
            : null;

        slot.enabled = creature != null;
        slot.preserveAspect = true;
    }

    void RefreshUI()
    {
        string starter = PlayerPrefs.GetString(StarterKey, "");

        // Main screen slots
        SetSlotImage(starterSlot, starter);

        bool hasCaptured =
            !string.IsNullOrEmpty(capturedCreature);

        if (capturedSlot != null)
        {
            SetSlotImage(capturedSlot, capturedCreature);

            capturedSlot.gameObject.SetActive(hasCaptured);
        }

        // Big backpack display
        SetSlotImage(starterImage, starter);
        SetSlotImage(activeImage, capturedCreature);

        // Backpack slots
        if (backpackSlotImages != null)
        {
            for (int i = 0; i < backpackSlotImages.Length; i++)
            {
                string creatureName =
                    i < backpack.Count
                    ? backpack[i]
                    : "";

                SetSlotImage(
                    backpackSlotImages[i],
                    creatureName);
            }
        }

        // Backpack slot buttons
        if (backpackSlotButtons != null)
        {
            for (int i = 0; i < backpackSlotButtons.Length; i++)
            {
                if (backpackSlotButtons[i] != null)
                {
                    backpackSlotButtons[i].interactable =
                        i < backpack.Count;
                }
            }
        }

        bool validSelection =
            selectedSlot >= 0 &&
            selectedSlot < backpack.Count;

        if (moveToActiveButton != null)
        {
            moveToActiveButton.interactable =
                validSelection;
        }
    }

    // Capture:
    // 1st wild Pokémon -> active slot
    // Next 5 -> backpack
    public bool AddCapturedCreature(string creatureName)
    {
        if (string.IsNullOrWhiteSpace(creatureName))
            return false;

        if (FindCreature(creatureName) == null)
            return false;

        if (string.IsNullOrEmpty(capturedCreature))
        {
            capturedCreature = creatureName;
        }
        else if (backpack.Count < BackpackCapacity)
        {
            backpack.Add(creatureName);
        }
        else
        {
            return false;
        }

        SaveParty();
        RefreshUI();

        return true;
    }

    void SelectBackpackSlot(int index)
    {
        if (index < 0 || index >= backpack.Count)
            return;

        selectedSlot = index;

        RefreshUI();
    }

    // Move selected backpack Pokémon into active slot.
    // The old active Pokémon moves into the selected backpack slot.
    public void MoveSelectedToActiveSlot()
    {
        if (selectedSlot < 0 ||
            selectedSlot >= backpack.Count)
            return;

        string selectedCreature =
            backpack[selectedSlot];

        if (string.IsNullOrEmpty(capturedCreature))
        {
            capturedCreature = selectedCreature;

            backpack.RemoveAt(selectedSlot);
        }
        else
        {
            backpack[selectedSlot] =
                capturedCreature;

            capturedCreature =
                selectedCreature;
        }

        selectedSlot = -1;

        SaveParty();
        RefreshUI();
    }

    // Permanently release the active wild Pokémon.
    public string ReleaseCapturedCreature()
    {
        if (string.IsNullOrEmpty(capturedCreature))
            return "";

        string releasedCreature =
            capturedCreature;

        capturedCreature = "";

        SaveParty();
        RefreshUI();

        return releasedCreature;
    }

    public bool HasCapturedCreature()
    {
        return !string.IsNullOrEmpty(capturedCreature);
    }

    public string GetActiveCreatureName()
    {
        return capturedCreature;
    }
}