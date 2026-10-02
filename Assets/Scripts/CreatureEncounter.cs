
using UnityEngine;
using System.Collections;

public class CreatureEncounter : MonoBehaviour
{
    [Header("Encounter Settings")]
    [Range(0f, 1f)]
    public float encounterChance = 0.3f;

    public CreatureData[] wildCreatures;

    [Header("Encounter UI")]
    public GameObject encounterPanel;
    public CanvasGroup encounterCanvasGroup;
    public TMPro.TMP_Text encounterText;
    public GameObject battlePanel;

    [Header("Battle Manager")]
    public BattleManager battleManager;

    private bool encounterActive;
    private PlayerMovement playerMovement;
    private Rigidbody2D playerRb;

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag("Player") || encounterActive)
            return;

        if (Random.value >= encounterChance)
            return;

        if (wildCreatures == null || wildCreatures.Length == 0)
        {
            Debug.LogError("No wild creatures assigned!");
            return;
        }

        CreatureData creature =
            wildCreatures[Random.Range(0, wildCreatures.Length)];

        if (creature == null || battleManager == null)
        {
            Debug.LogError("Creature or BattleManager is missing!");
            return;
        }

        battleManager.wildCreature = creature;
        encounterActive = true;

        playerMovement = other.GetComponent<PlayerMovement>();
        playerRb = other.GetComponent<Rigidbody2D>();

        if (playerMovement != null)
            playerMovement.enabled = false;

        if (playerRb != null)
            playerRb.linearVelocity = Vector2.zero;

        StartCoroutine(EncounterSequence());
    }

    private IEnumerator EncounterSequence()
    {
        if (encounterPanel != null)
            encounterPanel.SetActive(true);

        if (encounterText != null)
            encounterText.text = "A wild " +
                battleManager.wildCreature.creatureName +
                " appeared!";

        if (encounterCanvasGroup != null)
        {
            encounterCanvasGroup.alpha = 1f;
            yield return new WaitForSecondsRealtime(0.8f);

            for (int i = 0; i < 3; i++)
            {
                encounterCanvasGroup.alpha = 0f;
                yield return new WaitForSecondsRealtime(0.12f);

                encounterCanvasGroup.alpha = 1f;
                yield return new WaitForSecondsRealtime(0.12f);
            }
        }

        if (encounterPanel != null)
            encounterPanel.SetActive(false);

        if (battlePanel != null)
            battlePanel.SetActive(true);

        battleManager.BeginBattle();
        Debug.Log("Battle started!");
    }

    public void EndEncounter()
    {
        encounterActive = false;

        if (encounterPanel != null)
            encounterPanel.SetActive(false);

        if (battlePanel != null)
            battlePanel.SetActive(false);

        if (playerMovement != null)
            playerMovement.enabled = true;

        if (playerRb != null)
            playerRb.linearVelocity = Vector2.zero;

        Debug.Log("Encounter ended.");
    }
}