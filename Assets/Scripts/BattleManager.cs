
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

    [Header("Battle Message")]
    public TMP_Text battleMessage;
    public float messageDelay = 0.5f;

    private CreatureData playerCreature;
    private int playerHP;
    private int wildHP;
    private bool battleInProgress;
    private bool turnInProgress;

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
    }

    public void BeginBattle()
    {
        if (wildCreature == null)
        {
            Debug.LogError("No wild creature assigned!");
            return;
        }

        string starterName = PlayerPrefs.GetString("StarterCreature", "");
        playerCreature = null;

        if (creatureDatabase != null)
        {
            foreach (CreatureData creature in creatureDatabase)
            {
                if (creature != null && creature.creatureName == starterName)
                {
                    playerCreature = creature;
                    break;
                }
            }
        }

        if (playerCreature == null)
        {
            Debug.LogError("Starter creature not found!");
            return;
        }

        playerHP = playerCreature.maxHP;
        wildHP = wildCreature.maxHP;
        battleInProgress = true;
        turnInProgress = false;

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

        UpdateBattleUI();
        SetMessage("A wild " + wildCreature.creatureName + " appeared!");
    }

    void UpdateBattleUI()
    {
        if (wildCreature != null)
        {
            if (wildCreatureImage != null)
            {
                wildCreatureImage.sprite = wildCreature.battleSprite;
                wildCreatureImage.preserveAspect = true;
            }

            if (wildCreatureNameText != null)
                wildCreatureNameText.text = wildCreature.creatureName;

            if (wildCreatureHPBar != null)
            {
                wildCreatureHPBar.maxValue = wildCreature.maxHP;
                wildCreatureHPBar.value = wildHP;
            }

            if (wildCreatureHPText != null)
                wildCreatureHPText.text = wildHP + " / " + wildCreature.maxHP;
        }

        if (playerCreature != null)
        {
            if (playerCreatureImage != null)
            {
                playerCreatureImage.sprite = playerCreature.battleSprite;
                playerCreatureImage.preserveAspect = true;
            }

            if (playerCreatureNameText != null)
                playerCreatureNameText.text = playerCreature.creatureName;

            if (playerCreatureHPBar != null)
            {
                playerCreatureHPBar.maxValue = playerCreature.maxHP;
                playerCreatureHPBar.value = playerHP;
            }

            if (playerCreatureHPText != null)
                playerCreatureHPText.text = playerHP + " / " + playerCreature.maxHP;
        }
    }

    void SetMessage(string message)
    {
        if (battleMessage != null)
            battleMessage.text = message;
    }

    public void ShowAttackChoices()
    {
        if (!battleInProgress || turnInProgress)
            return;

        if (attackChoicesPanel != null)
            attackChoicesPanel.SetActive(true);

        if (attackButton != null)
            attackButton.interactable = false;

        if (runButton != null)
            runButton.interactable = false;
    }

    public void UseTypeAttack()
    {
        if (!battleInProgress || turnInProgress)
            return;

        if (attackChoicesPanel != null)
            attackChoicesPanel.SetActive(false);

        StartCoroutine(PlayerAttack(true));
    }

    public void UseNormalAttack()
    {
        if (!battleInProgress || turnInProgress)
            return;

        if (attackChoicesPanel != null)
            attackChoicesPanel.SetActive(false);

        StartCoroutine(PlayerAttack(false));
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
            float multiplier = TypeChart.GetMultiplier(
                playerCreature.type, wildCreature.type);

            damage = Mathf.RoundToInt(
                playerCreature.typeAttackDamage * multiplier);

            if (multiplier > 1f)
                SetMessage("It's super effective!");
            else if (multiplier < 1f)
                SetMessage("It's not very effective...");
            else
                SetMessage(playerCreature.creatureName + " used its type attack!");
        }
        else
        {
            damage = playerCreature.normalAttackDamage;
            SetMessage(playerCreature.creatureName + " used a normal attack!");
        }

        wildHP = Mathf.Max(0, wildHP - damage);
        UpdateBattleUI();

        yield return new WaitForSeconds(messageDelay);

        if (wildHP <= 0)
        {
            yield return StartCoroutine(CaptureAndEndBattle());
            yield break;
        }

        yield return StartCoroutine(WildAttack());

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
            SetMessage("What will you do?");
        }
    }

    IEnumerator WildAttack()
    {
        if (!battleInProgress)
            yield break;

        SetMessage(wildCreature.creatureName + " attacked!");
        yield return new WaitForSeconds(messageDelay);

        playerHP = Mathf.Max(0, playerHP - wildCreature.normalAttackDamage);
        UpdateBattleUI();

        if (playerHP <= 0)
        {
            SetMessage(playerCreature.creatureName + " has fainted!");
            yield return new WaitForSeconds(messageDelay);
            EndBattle();
        }
    }

    IEnumerator CaptureAndEndBattle()
    {
        SetMessage("Wild " + wildCreature.creatureName + " was defeated!");
        yield return new WaitForSeconds(messageDelay);

        if (partyManager != null)
        {
            bool captured = partyManager.AddCapturedCreature(
                wildCreature.creatureName);

            SetMessage(captured
                ? wildCreature.creatureName + " joined your party!"
                : "Your party is full!");
        }
        else
        {
            SetMessage("Battle won!");
        }

        yield return new WaitForSeconds(messageDelay);
        EndBattle();
    }

    public void RunFromBattle()
    {
        if (!battleInProgress)
            return;

        SetMessage("You ran away!");
        EndBattle();
    }

    void EndBattle()
    {
        if (!battleInProgress)
            return;

        battleInProgress = false;
        turnInProgress = false;

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
        Debug.Log("Battle ended.");
    }
}