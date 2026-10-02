
using UnityEngine;
using UnityEngine.UI;

public class BattleUI : MonoBehaviour
{
    public GameObject battlePanel;
    public GameObject attackChoicesPanel;
    public GameObject attackButton;
    public GameObject runButton;
    public CreatureEncounter creatureEncounter;

    public void ShowAttackChoices()
    {
        attackButton.SetActive(false);
        runButton.SetActive(false);
        attackChoicesPanel.SetActive(true);
    }

    public void ResetBattleUI()
    {
        attackChoicesPanel.SetActive(false);

        attackButton.SetActive(true);
        runButton.SetActive(true);

        attackButton.GetComponent<Button>().interactable = true;
        runButton.GetComponent<Button>().interactable = true;

        foreach (Button button in
                 attackChoicesPanel.GetComponentsInChildren<Button>(true))
        {
            button.interactable = true;
        }
    }

    public void RunFromBattle()
    {
        creatureEncounter.EndEncounter();
        Debug.Log("Player ran from battle!");
    }
}