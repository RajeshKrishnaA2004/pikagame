using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;

public class BattleManager : MonoBehaviour
{
    [Header("Battle UI")]
    public GameObject encounterPanel;
    public GameObject battlePanel;

    public TextMeshProUGUI encounterText;
    public TextMeshProUGUI battleText;

    public Button attackButton;
    public Button runButton;

    public GameObject attackChoicesPanel;
    public Button typeAttackButton;
    public Button normalAttackButton;

    [Header("Deployment")]
    public Button switchCreatureButton;
    public Image switchCreatureImage;

    [Header("Player")]
    public Image playerCreatureImage;
    public TextMeshProUGUI playerNameText;
    public TextMeshProUGUI playerHPText;
    public Slider playerHPBar;

    [Header("Wild")]
    public Image wildCreatureImage;
    public TextMeshProUGUI wildNameText;
    public TextMeshProUGUI wildHPText;
    public Slider wildHPBar;

    [Header("Creature Database")]
    public CreatureData[] creatures;

    [Header("Party")]
    public CreaturePartyManager partyManager;

    [Header("Encounter")]
    public CreatureEncounter creatureEncounter;

    [Header("Battle Settings")]
    public float messageDelay = 0.5f;

    private CreatureData starterCreature;
    private CreatureData activeCreature;
    private CreatureData slot1Creature;
    private CreatureData playerCreature;

    public CreatureData wildCreature;

    private int playerHP;
    private int wildHP;

    private bool inBattle = false;
    private bool turnInProgress = false;
    private bool endingBattle = false;
    private bool deploymentLocked = false;

    // ---------------------------------------------------------
    // Selected slot:
    //
    // 1 = Slot 1
    // 2 = Slot 2
    //
    // This tells us which creature is currently selected.
    // ---------------------------------------------------------
    private int selectedSlot = 1;

    // ---------------------------------------------------------
    // Deployed temporary slot:
    //
    // 0 = permanent starter / nothing temporary deployed
    // 1 = temporary Slot 1
    // 2 = temporary Slot 2
    //
    // IMPORTANT:
    // This stays 0 until the player presses ATTACK.
    // ---------------------------------------------------------
    private int deployedTemporarySlot = 0;


    // =========================================================
    // START
    // =========================================================

    void Start()
    {
        if (partyManager == null)
        {
            partyManager =
                FindFirstObjectByType<CreaturePartyManager>();
        }

        if (attackButton != null)
        {
            attackButton.onClick.AddListener(
                ShowAttackChoices
            );
        }

        if (runButton != null)
        {
            runButton.onClick.AddListener(
                RunFromBattle
            );
        }

        if (typeAttackButton != null)
        {
            typeAttackButton.onClick.AddListener(
                UseTypeAttack
            );
        }

        if (normalAttackButton != null)
        {
            normalAttackButton.onClick.AddListener(
                UseNormalAttack
            );
        }

        if (switchCreatureButton != null)
        {
            switchCreatureButton.onClick.AddListener(
                ChooseOtherCreature
            );

            switchCreatureButton.gameObject.SetActive(false);
        }

        if (encounterPanel != null)
        {
            encounterPanel.SetActive(false);
        }

        if (battlePanel != null)
        {
            battlePanel.SetActive(false);
        }

        if (attackChoicesPanel != null)
        {
            attackChoicesPanel.SetActive(false);
        }
    }


    // =========================================================
    // BEGIN BATTLE - STRING VERSION
    // =========================================================

    public void BeginBattle(string wildCreatureName)
    {
        if (inBattle)
            return;

        wildCreature =
            FindCreature(wildCreatureName);

        if (wildCreature == null)
        {
            Debug.LogError(
                "BattleManager: Wild creature not found: "
                + wildCreatureName
            );

            return;
        }

        StartBattle();
    }


    // =========================================================
    // BEGIN BATTLE - COMPATIBILITY VERSION
    // =========================================================

    public void BeginBattle()
    {
        if (inBattle)
            return;

        if (wildCreature == null)
        {
            Debug.LogError(
                "BattleManager: No wild creature assigned!"
            );

            return;
        }

        StartBattle();
    }


    // =========================================================
    // START BATTLE
    // =========================================================

    void StartBattle()
    {
        inBattle = true;
        endingBattle = false;
        turnInProgress = false;
        deploymentLocked = false;

        // IMPORTANT:
        // Nothing is deployed yet.
        deployedTemporarySlot = 0;

        selectedSlot = 1;


        // =====================================================
        // LOAD SLOT 1
        // =====================================================

        slot1Creature = null;
        starterCreature = null;

        if (partyManager != null &&
            partyManager.IsSlot1Temporary())
        {
            string slot1Name =
                partyManager.GetSlot1CreatureName();

            if (!string.IsNullOrEmpty(slot1Name))
            {
                slot1Creature =
                    FindCreature(slot1Name);
            }
        }
        else
        {
            // Permanent starter

            string starterName =
                PlayerPrefs.GetString(
                    "StarterCreature",
                    ""
                );

            if (!string.IsNullOrEmpty(starterName))
            {
                starterCreature =
                    FindCreature(starterName);

                slot1Creature =
                    starterCreature;
            }
        }


        // =====================================================
        // LOAD SLOT 2
        // =====================================================

        activeCreature = null;

        if (partyManager != null)
        {
            string activeName =
                partyManager.GetActiveCreatureName();

            if (!string.IsNullOrEmpty(activeName))
            {
                activeCreature =
                    FindCreature(activeName);
            }
        }


        // =====================================================
        // CHOOSE INITIAL CREATURE
        // =====================================================

        if (slot1Creature != null)
        {
            playerCreature =
                slot1Creature;

            selectedSlot = 1;
        }
        else if (activeCreature != null)
        {
            playerCreature =
                activeCreature;

            selectedSlot = 2;
        }
        else
        {
            Debug.LogError(
                "BattleManager: No player creature available!"
            );

            inBattle = false;
            return;
        }


        // =====================================================
        // SET HP
        // =====================================================

        playerHP =
            playerCreature.maxHP;

        wildHP =
            wildCreature.maxHP;


        // =====================================================
        // SHOW BATTLE UI
        // =====================================================

        if (encounterPanel != null)
        {
            encounterPanel.SetActive(false);
        }

        if (battlePanel != null)
        {
            battlePanel.SetActive(true);
        }

        if (attackChoicesPanel != null)
        {
            attackChoicesPanel.SetActive(false);
        }

        if (attackButton != null)
        {
            attackButton.gameObject.SetActive(true);
            attackButton.interactable = true;
        }

        if (runButton != null)
        {
            runButton.gameObject.SetActive(true);
            runButton.interactable = true;
        }

        if (typeAttackButton != null)
        {
            typeAttackButton.gameObject.SetActive(true);
            typeAttackButton.interactable = true;
        }

        if (normalAttackButton != null)
        {
            normalAttackButton.gameObject.SetActive(true);
            normalAttackButton.interactable = true;
        }

        UpdateBattleUI();
        UpdateDeploymentButton();

        SetMessage(
            "A wild " +
            wildCreature.creatureName +
            " appeared!"
        );

        Debug.Log(
            "Battle started. Selected slot: " +
            selectedSlot
        );
    }


    // =========================================================
    // SWITCH CREATURE
    // =========================================================

    void ChooseOtherCreature()
    {
        if (!inBattle)
            return;

        if (deploymentLocked)
            return;

        if (turnInProgress)
            return;

        if (slot1Creature == null ||
            activeCreature == null)
        {
            return;
        }


        // =====================================================
        // SLOT 1 -> SLOT 2
        // =====================================================

        if (selectedSlot == 1)
        {
            selectedSlot = 2;

            playerCreature =
                activeCreature;
        }


        // =====================================================
        // SLOT 2 -> SLOT 1
        // =====================================================

        else
        {
            selectedSlot = 1;

            playerCreature =
                slot1Creature;
        }


        // Reset HP for newly selected creature.

        playerHP =
            playerCreature.maxHP;

        UpdateBattleUI();
        UpdateDeploymentButton();

        SetMessage(
            playerCreature.creatureName +
            " was selected!"
        );

        Debug.Log(
            "Selected slot changed to: " +
            selectedSlot
        );
    }


    // =========================================================
    // DEPLOYMENT BUTTON
    // =========================================================

    void UpdateDeploymentButton()
    {
        if (switchCreatureButton == null)
            return;

        if (deploymentLocked)
        {
            switchCreatureButton.gameObject.SetActive(false);
            return;
        }

        bool canSwitch =
            slot1Creature != null &&
            activeCreature != null;

        switchCreatureButton.gameObject.SetActive(
            canSwitch
        );

        if (!canSwitch)
            return;


        CreatureData otherCreature;

        if (selectedSlot == 1)
        {
            otherCreature =
                activeCreature;
        }
        else
        {
            otherCreature =
                slot1Creature;
        }


        if (switchCreatureImage != null &&
            otherCreature != null)
        {
            switchCreatureImage.sprite =
                otherCreature.battleSprite;

            switchCreatureImage.preserveAspect =
                true;
        }
    }


    // =========================================================
    // ATTACK BUTTON
    // =========================================================

    public void ShowAttackChoices()
    {
        if (!inBattle)
            return;

        if (turnInProgress)
            return;

        if (endingBattle)
            return;


        // =====================================================
        // THIS IS THE MOMENT THE CREATURE IS DEPLOYED.
        // =====================================================

        if (partyManager != null)
        {
            if (selectedSlot == 1)
            {
                if (partyManager.IsSlot1Temporary())
                {
                    deployedTemporarySlot = 1;
                }
                else
                {
                    // Permanent starter
                    deployedTemporarySlot = 0;
                }
            }
            else if (selectedSlot == 2)
            {
                if (activeCreature != null)
                {
                    deployedTemporarySlot = 2;
                }
                else
                {
                    deployedTemporarySlot = 0;
                }
            }
        }


        // =====================================================
        // LOCK DEPLOYMENT
        // =====================================================

        deploymentLocked = true;

        UpdateDeploymentButton();


        // =====================================================
        // SHOW ATTACK CHOICES
        // =====================================================

        if (attackChoicesPanel != null)
        {
            attackChoicesPanel.SetActive(true);
        }


        // Attack and Run disappear permanently for this battle.

        if (attackButton != null)
        {
            attackButton.gameObject.SetActive(false);
            attackButton.interactable = false;
        }

        if (runButton != null)
        {
            runButton.gameObject.SetActive(false);
            runButton.interactable = false;
        }


        if (typeAttackButton != null)
        {
            typeAttackButton.gameObject.SetActive(true);
            typeAttackButton.interactable = true;
        }

        if (normalAttackButton != null)
        {
            normalAttackButton.gameObject.SetActive(true);
            normalAttackButton.interactable = true;
        }

        Debug.Log(
            "Creature deployed from slot: " +
            deployedTemporarySlot
        );
    }


    // =========================================================
    // TYPE ATTACK
    // =========================================================

    public void UseTypeAttack()
    {
        if (!inBattle)
            return;

        if (turnInProgress)
            return;

        if (endingBattle)
            return;

        if (attackChoicesPanel != null)
        {
            attackChoicesPanel.SetActive(false);
        }

        StartCoroutine(
            PlayerAttack(true)
        );
    }


    // =========================================================
    // NORMAL ATTACK
    // =========================================================

    public void UseNormalAttack()
    {
        if (!inBattle)
            return;

        if (turnInProgress)
            return;

        if (endingBattle)
            return;

        if (attackChoicesPanel != null)
        {
            attackChoicesPanel.SetActive(false);
        }

        StartCoroutine(
            PlayerAttack(false)
        );
    }


    // =========================================================
    // PLAYER ATTACK
    // =========================================================

    IEnumerator PlayerAttack(bool useTypeAttack)
    {
        turnInProgress = true;


        if (attackButton != null)
        {
            attackButton.interactable = false;
        }

        if (runButton != null)
        {
            runButton.interactable = false;
        }

        if (typeAttackButton != null)
        {
            typeAttackButton.interactable = false;
        }

        if (normalAttackButton != null)
        {
            normalAttackButton.interactable = false;
        }


        int damage;


        // =====================================================
        // TYPE ATTACK
        // =====================================================

        if (useTypeAttack)
        {
            float multiplier =
                TypeChart.GetMultiplier(
                    playerCreature.type,
                    wildCreature.type
                );

            damage =
                Mathf.RoundToInt(
                    playerCreature.typeAttackDamage *
                    multiplier
                );

            if (multiplier > 1f)
            {
                SetMessage(
                    "It's super effective!"
                );
            }
            else if (multiplier < 1f)
            {
                SetMessage(
                    "It's not very effective..."
                );
            }
            else
            {
                SetMessage(
                    playerCreature.creatureName +
                    " used its type attack!"
                );
            }
        }
        else
        {
            // =================================================
            // NORMAL ATTACK
            // =================================================

            damage =
                playerCreature.normalAttackDamage;

            SetMessage(
                playerCreature.creatureName +
                " used a normal attack!"
            );
        }


        // =====================================================
        // DAMAGE WILD
        // =====================================================

        yield return new WaitForSecondsRealtime(
            messageDelay
        );

        wildHP =
            Mathf.Max(
                0,
                wildHP - damage
            );

        UpdateBattleUI();


        // =====================================================
        // WILD DEFEATED
        // =====================================================

        if (wildHP <= 0)
        {
            yield return StartCoroutine(
                CaptureAndEndBattle()
            );

            yield break;
        }


        // =====================================================
        // WILD ATTACK
        // =====================================================

        yield return new WaitForSecondsRealtime(
            messageDelay
        );

        yield return StartCoroutine(
            WildAttack()
        );


        // =====================================================
        // PLAYER CAN ATTACK AGAIN
        // =====================================================

        if (inBattle &&
            !endingBattle)
        {
            turnInProgress = false;


            // Attack / Run remain hidden.

            if (attackButton != null)
            {
                attackButton.gameObject.SetActive(false);
            }

            if (runButton != null)
            {
                runButton.gameObject.SetActive(false);
            }


            // Show attack choices again.

            if (attackChoicesPanel != null)
            {
                attackChoicesPanel.SetActive(true);
            }

            if (typeAttackButton != null)
            {
                typeAttackButton.gameObject.SetActive(true);
                typeAttackButton.interactable = true;
            }

            if (normalAttackButton != null)
            {
                normalAttackButton.gameObject.SetActive(true);
                normalAttackButton.interactable = true;
            }

            SetMessage(
                "Choose your attack!"
            );
        }
    }


    // =========================================================
    // WILD ATTACK
    // =========================================================

    IEnumerator WildAttack()
    {
        if (!inBattle)
            yield break;

        if (wildCreature == null ||
            playerCreature == null)
        {
            yield break;
        }


        int damage =
            Mathf.Max(
                0,
                wildCreature.normalAttackDamage
            );


        SetMessage(
            wildCreature.creatureName +
            " attacked!"
        );

        yield return new WaitForSecondsRealtime(
            messageDelay
        );


        playerHP =
            Mathf.Max(
                0,
                playerHP - damage
            );

        UpdateBattleUI();


        if (playerHP <= 0)
        {
            SetMessage(
                playerCreature.creatureName +
                " has fainted!"
            );

            yield return new WaitForSecondsRealtime(
                messageDelay
            );

            HandlePlayerFaint();
        }
    }


    // =========================================================
    // PLAYER FAINT
    // =========================================================

    void HandlePlayerFaint()
    {
        if (!inBattle)
            return;

        if (playerCreature == null)
            return;


        // =====================================================
        // PERMANENT STARTER FAINT
        // =====================================================

        if (partyManager != null &&
            !partyManager.IsSlot1Temporary() &&
            playerCreature == starterCreature)
        {
            int remaining =
                partyManager.LoseStarterStability();


            // Starter still has stability.

            if (remaining > 0)
            {
                SetMessage(
                    "The starter returned to its Pokéball."
                );

                EndBattle();

                return;
            }


            // =================================================
            // FIFTH FAINT
            // =================================================

            if (remaining == 0)
            {
                SetMessage(
                    "The starter's Pokéball broke!"
                );

                partyManager.ConvertSlot1ToTemporary();

                EndBattle();

                return;
            }
        }


        // =====================================================
        // TEMPORARY CREATURE FAINTED
        // =====================================================

        StartCoroutine(
            HandleTemporaryCreatureFaint()
        );
    }


    // =========================================================
    // TEMPORARY CREATURE FAINT MESSAGE
    // =========================================================

    IEnumerator HandleTemporaryCreatureFaint()
    {
        string usedCreatureName =
            playerCreature != null
                ? playerCreature.creatureName
                : "Your creature";


        SetMessage(
            usedCreatureName +
            " fainted. It disappeared and can't be called back."
        );


        yield return new WaitForSecondsRealtime(
            messageDelay
        );


        ConsumeDeployedTemporaryCreature();

        EndBattle();
    }


    // =========================================================
    // WILD DEFEATED / CAPTURE
    // =========================================================

    IEnumerator CaptureAndEndBattle()
    {
        if (!inBattle)
            yield break;

        if (endingBattle)
            yield break;

        endingBattle = true;


        // =====================================================
        // WILD DEFEATED
        // =====================================================

        SetMessage(
            "Wild " +
            wildCreature.creatureName +
            " was defeated!"
        );

        yield return new WaitForSecondsRealtime(
            messageDelay
        );


        // =====================================================
        // TEMPORARY CREATURE USED
        // =====================================================

        if (deployedTemporarySlot != 0)
        {
            string usedCreatureName =
                playerCreature != null
                    ? playerCreature.creatureName
                    : "Your creature";


            SetMessage(
                "Your temporary " +
                playerCreature.creatureName +
                " disappeared!"
            );


            yield return new WaitForSecondsRealtime(
                messageDelay
            );


            // IMPORTANT:
            // Remove the deployed creature BEFORE
            // adding the newly captured creature.

            ConsumeDeployedTemporaryCreature();
        }


        // =====================================================
        // CAPTURE WILD CREATURE
        // =====================================================

        bool captured = false;

        if (partyManager != null)
        {
            captured =
                partyManager.AddCapturedCreature(
                    wildCreature.creatureName
                );
        }


        if (captured)
        {
            SetMessage(
                wildCreature.creatureName +
                " joined your party!"
            );
        }
        else
        {
            SetMessage(
                "Your creature storage is full!"
            );
        }


        yield return new WaitForSecondsRealtime(
            messageDelay
        );


        EndBattle();
    }


    // =========================================================
    // RUN
    // =========================================================

    public void RunFromBattle()
    {
        if (!inBattle)
            return;

        if (endingBattle)
            return;

        if (turnInProgress)
            return;


        // =====================================================
        // ATTACK WAS NEVER CHOSEN
        // =====================================================

        if (deployedTemporarySlot == 0)
        {
            StartCoroutine(
                RunWithoutDeployment()
            );

            return;
        }


        // =====================================================
        // TEMPORARY CREATURE WAS DEPLOYED
        // =====================================================

        StartCoroutine(
            RunAfterDeployment()
        );
    }


    // =========================================================
    // RUN WITHOUT DEPLOYMENT
    // =========================================================

    IEnumerator RunWithoutDeployment()
    {
        endingBattle = true;


        SetMessage(
            "You ran away!"
        );


        yield return new WaitForSecondsRealtime(
            messageDelay
        );


        EndBattle();
    }


    // =========================================================
    // RUN AFTER TEMPORARY DEPLOYMENT
    // =========================================================

    IEnumerator RunAfterDeployment()
    {
        endingBattle = true;


        string usedCreatureName =
            playerCreature != null
                ? playerCreature.creatureName
                : "Your creature";


        SetMessage(
            usedCreatureName +
            " disappeared after being deployed. " +
            "It can't be called back."
        );


        yield return new WaitForSecondsRealtime(
            messageDelay
        );


        ConsumeDeployedTemporaryCreature();

        EndBattle();
    }


    // =========================================================
    // CONSUME DEPLOYED TEMPORARY
    // =========================================================

    void ConsumeDeployedTemporaryCreature()
    {
        if (partyManager == null)
        {
            deployedTemporarySlot = 0;
            return;
        }


        // =====================================================
        // SLOT 1
        // =====================================================

        if (deployedTemporarySlot == 1)
        {
            partyManager.ConsumeTemporarySlot1();

            Debug.Log(
                "Temporary Pokémon from Slot 1 was consumed."
            );
        }


        // =====================================================
        // SLOT 2
        // =====================================================

        else if (deployedTemporarySlot == 2)
        {
            partyManager.ConsumeTemporarySlot2();

            Debug.Log(
                "Temporary Pokémon from Slot 2 was consumed."
            );
        }


        // =====================================================
        // RESET
        // =====================================================

        // 0 = permanent starter / nothing deployed.

        deployedTemporarySlot = 0;
    }


    // =========================================================
    // END BATTLE
    // =========================================================

    void EndBattle()
    {
        if (!inBattle &&
            !endingBattle)
        {
            return;
        }


        inBattle = false;
        turnInProgress = false;
        deploymentLocked = false;


        if (switchCreatureButton != null)
        {
            switchCreatureButton.gameObject.SetActive(false);
        }


        if (attackChoicesPanel != null)
        {
            attackChoicesPanel.SetActive(false);
        }


        if (attackButton != null)
        {
            attackButton.interactable = true;
            attackButton.gameObject.SetActive(true);
        }


        if (runButton != null)
        {
            runButton.interactable = true;
            runButton.gameObject.SetActive(true);
        }


        if (typeAttackButton != null)
        {
            typeAttackButton.interactable = true;
            typeAttackButton.gameObject.SetActive(true);
        }


        if (normalAttackButton != null)
        {
            normalAttackButton.interactable = true;
            normalAttackButton.gameObject.SetActive(true);
        }


        if (battlePanel != null)
        {
            battlePanel.SetActive(false);
        }


        // =====================================================
        // RESTORE EXPLORATION
        // =====================================================

        if (creatureEncounter != null)
        {
            creatureEncounter.EndEncounter();
        }


        GameObject player =
            GameObject.FindGameObjectWithTag(
                "Player"
            );


        if (player != null)
        {
            PlayerMovement movement =
                player.GetComponent<PlayerMovement>();

            if (movement != null)
            {
                movement.enabled = true;
            }


            Rigidbody2D rb =
                player.GetComponent<Rigidbody2D>();

            if (rb != null)
            {
                rb.simulated = true;
                rb.linearVelocity =
                    Vector2.zero;
            }
        }


        Time.timeScale = 1f;

        endingBattle = false;

        Debug.Log(
            "Battle ended."
        );
    }


    // =========================================================
    // UPDATE BATTLE UI
    // =========================================================

    void UpdateBattleUI()
    {
        // =====================================================
        // WILD
        // =====================================================

        if (wildCreature != null)
        {
            if (wildCreatureImage != null)
            {
                wildCreatureImage.sprite =
                    wildCreature.battleSprite;

                wildCreatureImage.preserveAspect =
                    true;
            }


            if (wildNameText != null)
            {
                wildNameText.text =
                    wildCreature.creatureName;
            }


            if (wildHPBar != null)
            {
                wildHPBar.maxValue =
                    wildCreature.maxHP;

                wildHPBar.value =
                    wildHP;
            }


            if (wildHPText != null)
            {
                wildHPText.text =
                    wildHP +
                    " / " +
                    wildCreature.maxHP;
            }
        }


        // =====================================================
        // PLAYER
        // =====================================================

        if (playerCreature != null)
        {
            if (playerCreatureImage != null)
            {
                playerCreatureImage.sprite =
                    playerCreature.battleSprite;

                playerCreatureImage.preserveAspect =
                    true;
            }


            if (playerNameText != null)
            {
                playerNameText.text =
                    playerCreature.creatureName;
            }


            if (playerHPBar != null)
            {
                playerHPBar.maxValue =
                    playerCreature.maxHP;

                playerHPBar.value =
                    playerHP;
            }


            if (playerHPText != null)
            {
                playerHPText.text =
                    playerHP +
                    " / " +
                    playerCreature.maxHP;
            }
        }
    }


    // =========================================================
    // MESSAGE
    // =========================================================

    void SetMessage(string message)
    {
        if (battleText != null)
        {
            battleText.text =
                message;
        }

        Debug.Log(message);
    }


    // =========================================================
    // FIND CREATURE
    // =========================================================

    CreatureData FindCreature(
        string creatureName
    )
    {
        if (string.IsNullOrEmpty(creatureName))
            return null;

        if (creatures == null)
            return null;


        foreach (
            CreatureData creature
            in creatures
        )
        {
            if (creature == null)
                continue;


            if (creature.creatureName ==
                creatureName)
            {
                return creature;
            }
        }


        Debug.LogWarning(
            "Creature not found: " +
            creatureName
        );

        return null;
    }


    // =========================================================
    // PUBLIC ACCESS
    // =========================================================

    public string GetActiveCreatureName()
    {
        if (playerCreature == null)
            return "";

        return playerCreature.creatureName;
    }
}