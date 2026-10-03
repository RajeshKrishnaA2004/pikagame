using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;

public class BattleManager : MonoBehaviour
{
    [Header("Creatures")]
    public CreatureData[] creatureDatabase;
    public CreatureData wildCreature;
    public CreaturePartyManager partyManager;

    [Header("Managers")]
    public CreatureEncounter creatureEncounter;
    public BattleUI battleUI;

    [Header("Battle UI")]
    public GameObject battlePanel;
    public GameObject attackChoicesPanel;
    public Button attackButton;
    public Button runButton;
    public Button typeAttackButton;
    public Button normalAttackButton;

    [Header("Creature Display")]
    public Image wildCreatureImage;
    public TMP_Text wildCreatureNameText;
    public TMP_Text wildCreatureHPText;
    public Slider wildCreatureHPBar;

    public Image playerCreatureImage;
    public TMP_Text playerCreatureNameText;
    public TMP_Text playerCreatureHPText;
    public Slider playerCreatureHPBar;

    [Header("Creature Deployment")]
    public Button switchCreatureButton;

    [Header("Battle Message")]
    public TMP_Text battleMessage;
    public float messageDelay = 0.5f;

    private CreatureData starterCreature;
    private CreatureData activeCreature;

    private CreatureData playerCreature;

    private int playerHP;
    private int wildHP;

    private bool battleInProgress;
    private bool turnInProgress;

    // True once the player clicks Attack or Run.
    // After this point the deployed Pokémon cannot change.
    private bool deploymentLocked;

    void Start()
    {
        if (attackButton != null)
            attackButton.onClick.AddListener(ShowAttackChoices);

        if (runButton != null)
            runButton.onClick.AddListener(RunFromBattle);

        if (typeAttackButton != null)
            typeAttackButton.onClick.AddListener(UseTypeAttack);

        if (normalAttackButton != null)
            normalAttackButton.onClick.AddListener(UseNormalAttack);

        if (switchCreatureButton != null)
        {
            switchCreatureButton.onClick.AddListener(
                ChooseOtherCreature
            );

            switchCreatureButton.gameObject.SetActive(false);
        }
    }

    public void BeginBattle()
    {
        if (wildCreature == null)
        {
            Debug.LogError("No wild creature assigned!");
            return;
        }

        // -------------------------
        // GET STARTER
        // -------------------------

        string starterName =
            PlayerPrefs.GetString("StarterCreature", "");

        starterCreature = FindCreature(starterName);

        if (starterCreature == null)
        {
            Debug.LogError("Starter creature not found!");
            return;
        }

        // -------------------------
        // GET ACTIVE CAPTURED CREATURE
        // -------------------------

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

        // -------------------------
        // DEFAULT DEPLOYMENT
        // -------------------------

        // Starter is always the default.
        playerCreature = starterCreature;

        // -------------------------
        // HEALTH
        // -------------------------

        playerHP =
            playerCreature.maxHP;

        wildHP =
            wildCreature.maxHP;

        // -------------------------
        // RESET BATTLE STATE
        // -------------------------

        battleInProgress = true;
        turnInProgress = false;
        deploymentLocked = false;

        // -------------------------
        // SHOW BATTLE UI
        // -------------------------

        if (battlePanel != null)
            battlePanel.SetActive(true);

        if (battleUI != null)
            battleUI.ResetBattleUI();

        if (attackChoicesPanel != null)
            attackChoicesPanel.SetActive(false);

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
            typeAttackButton.interactable = true;

        if (normalAttackButton != null)
            normalAttackButton.interactable = true;

        // Show the Pokémon that can be chosen.
        UpdateDeploymentButton();

        UpdateBattleUI();

        SetMessage(
            "A wild " +
            wildCreature.creatureName +
            " appeared!"
        );
    }

    CreatureData FindCreature(string creatureName)
    {
        if (string.IsNullOrEmpty(creatureName))
            return null;

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

    // =========================================================
    // DEPLOYMENT CHOICE
    // =========================================================

    void UpdateDeploymentButton()
    {
        if (switchCreatureButton == null)
            return;

        // Once deployment is locked,
        // the button must never come back.
        if (deploymentLocked)
        {
            switchCreatureButton.gameObject.SetActive(false);
            return;
        }

        // No second Pokémon.
        if (activeCreature == null)
        {
            switchCreatureButton.gameObject.SetActive(false);
            return;
        }

        switchCreatureButton.gameObject.SetActive(true);

        Image buttonImage =
            switchCreatureButton.GetComponent<Image>();

        if (buttonImage == null)
            return;

        // The icon shows the Pokémon
        // that the player can choose.
        CreatureData choiceCreature;

        if (playerCreature == starterCreature)
        {
            choiceCreature = activeCreature;
        }
        else
        {
            choiceCreature = starterCreature;
        }

        if (choiceCreature != null)
        {
            buttonImage.sprite =
                choiceCreature.battleSprite;

            buttonImage.preserveAspect = true;
            buttonImage.enabled = true;
        }
    }

    void ChooseOtherCreature()
    {
        // Cannot switch after deployment is locked.
        if (!battleInProgress ||
            deploymentLocked ||
            turnInProgress ||
            activeCreature == null)
        {
            return;
        }

        // Starter -> Active
        if (playerCreature == starterCreature)
        {
            playerCreature = activeCreature;
        }
        // Active -> Starter
        else
        {
            playerCreature = starterCreature;
        }

        // The selected Pokémon gets its full battle HP.
        playerHP =
            playerCreature.maxHP;

        UpdateDeploymentButton();
        UpdateBattleUI();

        SetMessage(
            playerCreature.creatureName +
            " is ready for battle!"
        );
    }

    // =========================================================
    // BATTLE UI
    // =========================================================

    void UpdateBattleUI()
    {
        // -------------------------
        // WILD CREATURE
        // -------------------------

        if (wildCreature != null)
        {
            if (wildCreatureImage != null)
            {
                wildCreatureImage.sprite =
                    wildCreature.battleSprite;

                wildCreatureImage.preserveAspect = true;
            }

            if (wildCreatureNameText != null)
                wildCreatureNameText.text =
                    wildCreature.creatureName;

            if (wildCreatureHPBar != null)
            {
                wildCreatureHPBar.maxValue =
                    wildCreature.maxHP;

                wildCreatureHPBar.value =
                    wildHP;
            }

            if (wildCreatureHPText != null)
                wildCreatureHPText.text =
                    wildHP +
                    " / " +
                    wildCreature.maxHP;
        }

        // -------------------------
        // PLAYER CREATURE
        // -------------------------

        if (playerCreature != null)
        {
            if (playerCreatureImage != null)
            {
                playerCreatureImage.sprite =
                    playerCreature.battleSprite;

                playerCreatureImage.preserveAspect = true;
            }

            if (playerCreatureNameText != null)
                playerCreatureNameText.text =
                    playerCreature.creatureName;

            if (playerCreatureHPBar != null)
            {
                playerCreatureHPBar.maxValue =
                    playerCreature.maxHP;

                playerCreatureHPBar.value =
                    playerHP;
            }

            if (playerCreatureHPText != null)
                playerCreatureHPText.text =
                    playerHP +
                    " / " +
                    playerCreature.maxHP;
        }
    }

    void SetMessage(string message)
    {
        if (battleMessage != null)
            battleMessage.text = message;
    }

    // =========================================================
    // ATTACK SELECTION
    // =========================================================

    public void ShowAttackChoices()
    {
        if (!battleInProgress ||
            turnInProgress)
        {
            return;
        }

        // IMPORTANT:
        // The Pokémon is now locked for this battle.
        deploymentLocked = true;

        if (switchCreatureButton != null)
            switchCreatureButton.gameObject.SetActive(false);

        if (attackChoicesPanel != null)
            attackChoicesPanel.SetActive(true);

        if (attackButton != null)
            attackButton.interactable = false;

        if (runButton != null)
            runButton.interactable = false;
    }

    public void UseTypeAttack()
    {
        if (!battleInProgress ||
            turnInProgress)
        {
            return;
        }

        if (attackChoicesPanel != null)
            attackChoicesPanel.SetActive(false);

        StartCoroutine(
            PlayerAttack(true)
        );
    }

    public void UseNormalAttack()
    {
        if (!battleInProgress ||
            turnInProgress)
        {
            return;
        }

        if (attackChoicesPanel != null)
            attackChoicesPanel.SetActive(false);

        StartCoroutine(
            PlayerAttack(false)
        );
    }

    IEnumerator PlayerAttack(bool useTypeAttack)
    {
        turnInProgress = true;

        if (attackButton != null)
            attackButton.interactable = false;

        if (runButton != null)
            runButton.interactable = false;

        if (typeAttackButton != null)
            typeAttackButton.interactable = false;

        if (normalAttackButton != null)
            normalAttackButton.interactable = false;

        int damage;

        if (useTypeAttack)
        {
            float multiplier =
                TypeChart.GetMultiplier(
                    playerCreature.type,
                    wildCreature.type
                );

            damage = Mathf.RoundToInt(
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
            damage =
                playerCreature.normalAttackDamage;

            SetMessage(
                playerCreature.creatureName +
                " used a normal attack!"
            );
        }

        wildHP =
            Mathf.Max(
                0,
                wildHP - damage
            );

        UpdateBattleUI();

        yield return new WaitForSeconds(
            messageDelay
        );

        if (wildHP <= 0)
        {
            yield return StartCoroutine(
                CaptureAndEndBattle()
            );

            yield break;
        }

        yield return StartCoroutine(
            WildAttack()
        );

        if (battleInProgress)
        {
            if (attackButton != null)
                attackButton.interactable = true;

            if (runButton != null)
                runButton.interactable = true;

            if (typeAttackButton != null)
                typeAttackButton.interactable = true;

            if (normalAttackButton != null)
                normalAttackButton.interactable = true;

            turnInProgress = false;

            SetMessage(
                "What will you do?"
            );
        }
    }

    // =========================================================
    // WILD ATTACK
    // =========================================================

    IEnumerator WildAttack()
    {
        if (!battleInProgress)
            yield break;

        SetMessage(
            wildCreature.creatureName +
            " attacked!"
        );

        yield return new WaitForSeconds(
            messageDelay
        );

        playerHP =
            Mathf.Max(
                0,
                playerHP -
                wildCreature.normalAttackDamage
            );

        UpdateBattleUI();

        if (playerHP <= 0)
        {
            SetMessage(
                playerCreature.creatureName +
                " has fainted!"
            );

            yield return new WaitForSeconds(
                messageDelay
            );

            EndBattle();
        }
    }

    // =========================================================
    // CAPTURE
    // =========================================================

    IEnumerator CaptureAndEndBattle()
    {
        SetMessage(
            "Wild " +
            wildCreature.creatureName +
            " was defeated!"
        );

        yield return new WaitForSeconds(
            messageDelay
        );

        if (partyManager != null)
        {
            bool captured =
                partyManager.AddCapturedCreature(
                    wildCreature.creatureName
                );

            SetMessage(
                captured
                    ? wildCreature.creatureName +
                      " joined your party!"
                    : "Your party is full!"
            );
        }
        else
        {
            SetMessage(
                "Battle won!"
            );
        }

        yield return new WaitForSeconds(
            messageDelay
        );

        EndBattle();
    }

    // =========================================================
    // RUN
    // =========================================================

    public void RunFromBattle()
    {
        if (!battleInProgress ||
            turnInProgress)
        {
            return;
        }

        // Deployment is no longer relevant
        // because the battle is ending.
        deploymentLocked = true;

        if (switchCreatureButton != null)
            switchCreatureButton.gameObject.SetActive(false);

        SetMessage(
            "You ran away!"
        );

        EndBattle();
    }

    // =========================================================
    // END BATTLE
    // =========================================================

    void EndBattle()
    {
        if (!battleInProgress)
            return;

        battleInProgress = false;
        turnInProgress = false;
        deploymentLocked = true;

        if (switchCreatureButton != null)
            switchCreatureButton.gameObject.SetActive(false);

        if (attackChoicesPanel != null)
            attackChoicesPanel.SetActive(false);

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
            typeAttackButton.interactable = true;

        if (normalAttackButton != null)
            normalAttackButton.interactable = true;

        if (battlePanel != null)
            battlePanel.SetActive(false);

        if (creatureEncounter != null)
            creatureEncounter.EndEncounter();

        Time.timeScale = 1f;

        Debug.Log(
            "Battle ended."
        );
    }
}