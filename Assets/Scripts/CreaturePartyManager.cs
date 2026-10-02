
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class CreaturePartyManager : MonoBehaviour
{
    [Header("Party Slots")]
    public Image starterSlot;
    public Image activeSlot;

    [Header("Bag")]
    public GameObject bagPanel;
    public Image[] bagSlotImages;
    public Button[] bagSlotButtons;

    [Header("Bag Actions")]
    public Button swapButton;
    public Button sellButton;
    public int sellValue = 10;

    [Header("Creature Database")]
    public CreatureData[] creatureDatabase;

    private const int BagCapacity = 5;
    private const string ActiveKey = "Party_Active";
    private const string BagKey = "Party_Bag";

    private string activeCreature = "";
    private List<string> bag = new List<string>();
    private int selectedSlot = -1;

    void Start()
    {
        LoadParty();

        if (bagPanel != null)
            bagPanel.SetActive(false);

        if (bagSlotButtons != null)
        {
            for (int i = 0; i < bagSlotButtons.Length; i++)
            {
                int index = i;

                if (bagSlotButtons[i] != null)
                    bagSlotButtons[i].onClick.AddListener(
                        () => SelectBagSlot(index));
            }
        }

        if (swapButton != null)
            swapButton.onClick.AddListener(SwapCreature);

        if (sellButton != null)
            sellButton.onClick.AddListener(SellCreature);

        RefreshUI();
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.E) && bagPanel != null)
            bagPanel.SetActive(!bagPanel.activeSelf);
    }

    void LoadParty()
    {
        activeCreature = PlayerPrefs.GetString(ActiveKey, "");
        bag.Clear();

        string savedBag = PlayerPrefs.GetString(BagKey, "");

        if (!string.IsNullOrEmpty(savedBag))
            bag.AddRange(savedBag.Split('\n'));
    }

    void SaveParty()
    {
        PlayerPrefs.SetString(ActiveKey, activeCreature);
        PlayerPrefs.SetString(BagKey, string.Join("\n", bag));
        PlayerPrefs.Save();
    }

    CreatureData FindCreature(string name)
    {
        if (creatureDatabase == null)
            return null;

        foreach (CreatureData creature in creatureDatabase)
        {
            if (creature != null && creature.creatureName == name)
                return creature;
        }

        return null;
    }

    void SetSlotImage(Image slot, string name)
    {
        if (slot == null)
            return;

        CreatureData creature = FindCreature(name);
        slot.sprite = creature != null ? creature.battleSprite : null;
        slot.enabled = true;
        slot.preserveAspect = true;
    }

    void RefreshUI()
    {
        SetSlotImage(starterSlot,
            PlayerPrefs.GetString("StarterCreature", ""));

        SetSlotImage(activeSlot, activeCreature);

        if (bagSlotImages != null)
        {
            for (int i = 0; i < bagSlotImages.Length; i++)
            {
                string name = i < bag.Count ? bag[i] : "";
                SetSlotImage(bagSlotImages[i], name);
            }
        }

        bool validSelection = selectedSlot >= 0 &&
                              selectedSlot < bag.Count;

        if (swapButton != null)
            swapButton.interactable = validSelection;

        if (sellButton != null)
            sellButton.interactable = validSelection;
    }

    public bool AddCapturedCreature(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return false;

        if (string.IsNullOrEmpty(activeCreature))
            activeCreature = name;
        else if (bag.Count < BagCapacity)
            bag.Add(name);
        else
            return false;

        SaveParty();
        RefreshUI();
        return true;
    }

    void SelectBagSlot(int index)
    {
        if (index < 0 || index >= bag.Count)
            return;

        selectedSlot = index;
        RefreshUI();
    }

    void SwapCreature()
    {
        if (selectedSlot < 0 || selectedSlot >= bag.Count)
            return;

        string selected = bag[selectedSlot];
        bag.RemoveAt(selectedSlot);

        if (!string.IsNullOrEmpty(activeCreature))
            bag.Insert(selectedSlot, activeCreature);

        activeCreature = selected;
        selectedSlot = -1;

        SaveParty();
        RefreshUI();
    }

    void SellCreature()
    {
        if (selectedSlot < 0 || selectedSlot >= bag.Count)
            return;

        bag.RemoveAt(selectedSlot);

        int coins = PlayerPrefs.GetInt("Coins", 0) + sellValue;
        PlayerPrefs.SetInt("Coins", coins);

        selectedSlot = -1;
        SaveParty();
        RefreshUI();
    }
}