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

    [Header("Swap Buttons")]
    public Button moveToActiveButton;
    public Button moveToSlot1Button;

    [Header("Creature Database")]
    public CreatureData[] creatureDatabase;

    private const int BackpackCapacity = 5;
    private const int MaxStarterStability = 5;

    private const string StarterKey =
        "StarterCreature";

    private string capturedCreature = "";
    private string temporarySlot1Creature = "";

    private List<string> backpack =
        new List<string>();

    private int selectedSlot = -1;

    // Runtime-only state.
    private int starterStability =
        MaxStarterStability;

    private bool slot1IsTemporary = false;

    void Start()
    {
        capturedCreature = "";
        temporarySlot1Creature = "";

        backpack.Clear();

        selectedSlot = -1;

        starterStability =
            MaxStarterStability;

        slot1IsTemporary = false;

        if (bagPanel != null)
            bagPanel.SetActive(false);

        // Backpack buttons
        if (backpackSlotButtons != null)
        {
            for (int i = 0;
                 i < backpackSlotButtons.Length;
                 i++)
            {
                int index = i;

                if (backpackSlotButtons[i] != null)
                {
                    backpackSlotButtons[i].onClick.AddListener(
                        () => SelectBackpackSlot(index));
                }
            }
        }

        // Existing "Swap To Slot 2" button
        if (moveToActiveButton != null)
        {
            moveToActiveButton.onClick.AddListener(
                () => MoveSelectedToSlot(2));
        }

        // New "Swap To Slot 1" button
        if (moveToSlot1Button != null)
        {
            moveToSlot1Button.onClick.AddListener(
                () => MoveSelectedToSlot(1));
        }

        RefreshUI();
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.E) &&
            bagPanel != null)
        {
            bagPanel.SetActive(
                !bagPanel.activeSelf);

            RefreshUI();
        }
    }

    // =========================================================
    // CREATURE DATABASE
    // =========================================================

    CreatureData FindCreature(
        string creatureName)
    {
        if (string.IsNullOrEmpty(creatureName))
            return null;

        if (creatureDatabase == null)
            return null;

        foreach (CreatureData creature
                 in creatureDatabase)
        {
            if (creature != null &&
                creature.creatureName ==
                creatureName)
            {
                return creature;
            }
        }

        return null;
    }

    // =========================================================
    // UI
    // =========================================================

    void SetSlotImage(
        Image slot,
        string creatureName)
    {
        if (slot == null)
            return;

        CreatureData creature =
            FindCreature(creatureName);

        slot.sprite =
            creature != null
                ? creature.battleSprite
                : null;

        slot.enabled =
            creature != null;

        slot.preserveAspect = true;
    }

    void RefreshUI()
    {
        string starter =
            PlayerPrefs.GetString(
                StarterKey,
                "");

        // -------------------------------------------------
        // SLOT 1
        // -------------------------------------------------

        if (slot1IsTemporary)
        {
            SetSlotImage(
                starterSlot,
                temporarySlot1Creature);
        }
        else
        {
            SetSlotImage(
                starterSlot,
                starter);
        }

        // -------------------------------------------------
        // SLOT 2
        // -------------------------------------------------

        bool hasCaptured =
            !string.IsNullOrEmpty(
                capturedCreature);

        if (capturedSlot != null)
        {
            SetSlotImage(
                capturedSlot,
                capturedCreature);

            capturedSlot.gameObject.SetActive(
                hasCaptured);
        }

        // -------------------------------------------------
        // BAG SLOT 1 DISPLAY
        // -------------------------------------------------

        if (slot1IsTemporary)
        {
            SetSlotImage(
                starterImage,
                temporarySlot1Creature);
        }
        else
        {
            SetSlotImage(
                starterImage,
                starter);
        }

        // -------------------------------------------------
        // BAG SLOT 2 DISPLAY
        // -------------------------------------------------

        SetSlotImage(
            activeImage,
            capturedCreature);

        // -------------------------------------------------
        // BACKPACK
        // -------------------------------------------------

        if (backpackSlotImages != null)
        {
            for (int i = 0;
                 i < backpackSlotImages.Length;
                 i++)
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

        // -------------------------------------------------
        // BACKPACK BUTTONS
        // -------------------------------------------------

        if (backpackSlotButtons != null)
        {
            for (int i = 0;
                 i < backpackSlotButtons.Length;
                 i++)
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

        // -------------------------------------------------
        // SWAP TO SLOT 2
        // -------------------------------------------------

        if (moveToActiveButton != null)
        {
            moveToActiveButton.interactable =
                validSelection;
        }

        // -------------------------------------------------
        // SWAP TO SLOT 1
        // -------------------------------------------------

        if (moveToSlot1Button != null)
        {
            // Slot 1 cannot be modified while the
            // original starter is still permanent.
            moveToSlot1Button.gameObject.SetActive(
                slot1IsTemporary);

            moveToSlot1Button.interactable =
                validSelection &&
                slot1IsTemporary;
        }
    }

    // =========================================================
    // STARTER POKÉBALL STABILITY
    // =========================================================

    public int GetStarterStability()
    {
        return starterStability;
    }

    public int GetMaxStarterStability()
    {
        return MaxStarterStability;
    }

    public bool IsStarterBroken()
    {
        return starterStability <= 0;
    }

    public int LoseStarterStability()
    {
        if (starterStability <= 0)
            return 0;

        starterStability--;

        Debug.Log(
            "Starter Pokéball stability: " +
            starterStability +
            "/" +
            MaxStarterStability);

        RefreshUI();

        return starterStability;
}

    // =========================================================
    // SLOT 1 STATE
    // =========================================================

    public bool IsSlot1Temporary()
    {
        return slot1IsTemporary;
    }

    public string GetSlot1CreatureName()
    {
        if (slot1IsTemporary)
            return temporarySlot1Creature;

        return PlayerPrefs.GetString(
            StarterKey,
            "");
    }

    public void ConvertSlot1ToTemporary()
    {
        slot1IsTemporary = true;

        temporarySlot1Creature = "";

        RefreshUI();

        Debug.Log(
            "Starter Pokéball broke. " +
            "Slot 1 is now temporary.");
    }

    public bool SetTemporarySlot1Creature(
        string creatureName)
    {
        if (!slot1IsTemporary)
            return false;

        if (string.IsNullOrEmpty(
                creatureName))
            return false;

        if (FindCreature(creatureName) == null)
            return false;

        temporarySlot1Creature =
            creatureName;

        RefreshUI();

        return true;
    }

    public string RemoveTemporarySlot1Creature()
    {
        if (!slot1IsTemporary)
            return "";

        string removed =
            temporarySlot1Creature;

        temporarySlot1Creature = "";

        RefreshUI();

        return removed;
    }

    // =========================================================
    // CAPTURE
    // =========================================================

    public bool AddCapturedCreature(
        string creatureName)
    {
        if (string.IsNullOrWhiteSpace(
                creatureName))
        {
            return false;
        }

        if (FindCreature(creatureName) == null)
            return false;

        // If Slot 1 has become temporary and is empty,
        // the new Pokémon goes there first.
        if (slot1IsTemporary &&
            string.IsNullOrEmpty(
                temporarySlot1Creature))
        {
            temporarySlot1Creature =
                creatureName;

            RefreshUI();

            return true;
        }

        // Otherwise fill Slot 2.
        if (string.IsNullOrEmpty(
                capturedCreature))
        {
            capturedCreature =
                creatureName;
        }
        // Then backpack.
        else if (backpack.Count <
                 BackpackCapacity)
        {
            backpack.Add(
                creatureName);
        }
        else
        {
            return false;
        }

        RefreshUI();

        return true;
    }

    // =========================================================
    // BACKPACK SELECTION
    // =========================================================

    void SelectBackpackSlot(
        int index)
    {
        if (index < 0 ||
            index >= backpack.Count)
        {
            return;
        }

        selectedSlot = index;

        RefreshUI();
    }

    // =========================================================
    // SWAP SYSTEM
    // =========================================================

    public void MoveSelectedToSlot(
        int targetSlot)
    {
        if (selectedSlot < 0 ||
            selectedSlot >= backpack.Count)
        {
            return;
        }

        // Slot 1 is protected while the starter
        // is still permanent.
        if (targetSlot == 1 &&
            !slot1IsTemporary)
        {
            return;
        }

        string selectedCreature =
            backpack[selectedSlot];

        // -------------------------------------------------
        // TARGET SLOT 1
        // -------------------------------------------------

        if (targetSlot == 1)
        {
            string oldSlot1 =
                temporarySlot1Creature;

            temporarySlot1Creature =
                selectedCreature;

            if (string.IsNullOrEmpty(
                    oldSlot1))
            {
                backpack.RemoveAt(
                    selectedSlot);
            }
            else
            {
                backpack[selectedSlot] =
                    oldSlot1;
            }
        }

        // -------------------------------------------------
        // TARGET SLOT 2
        // -------------------------------------------------

        else if (targetSlot == 2)
        {
            string oldSlot2 =
                capturedCreature;

            capturedCreature =
                selectedCreature;

            if (string.IsNullOrEmpty(
                    oldSlot2))
            {
                backpack.RemoveAt(
                    selectedSlot);
            }
            else
            {
                backpack[selectedSlot] =
                    oldSlot2;
            }
        }
        else
        {
            return;
        }

        selectedSlot = -1;

        RefreshUI();
    }

    // =========================================================
    // OLD METHOD NAME
    // =========================================================
    // Kept so anything else in the project that might still
    // call MoveSelectedToActiveSlot does not break.

    public void MoveSelectedToActiveSlot()
    {
        MoveSelectedToSlot(2);
    }

    // =========================================================
    // RELEASE ACTIVE SLOT 2
    // =========================================================

    public string ReleaseCapturedCreature()
    {
        if (string.IsNullOrEmpty(
                capturedCreature))
        {
            return "";
        }

        string releasedCreature =
            capturedCreature;

        capturedCreature = "";

        RefreshUI();

        return releasedCreature;
    }

    // =========================================================
    // GETTERS
    // =========================================================

    public bool HasCapturedCreature()
    {
        return !string.IsNullOrEmpty(
            capturedCreature);
    }

    public string GetActiveCreatureName()
    {
        return capturedCreature;
    }

    // Removes the temporary Pokémon currently occupying Slot 1.
    public void ConsumeTemporarySlot1()
    {
        if (!slot1IsTemporary)
            return;

        temporarySlot1Creature = "";
        RefreshUI();
    }


    // Removes the temporary Pokémon currently occupying Slot 2.
    public void ConsumeTemporarySlot2()
    {
        if (string.IsNullOrEmpty(capturedCreature))
            return;

        capturedCreature = "";
        RefreshUI();
    }
}