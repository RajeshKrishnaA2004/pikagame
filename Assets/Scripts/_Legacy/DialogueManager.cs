
using UnityEngine;
using TMPro;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

public class DialogueManager : MonoBehaviour
{
    public TMP_Text dialogueText;
    public GameObject selectionPanel;
    public string nextSceneName = "Route1";

    [TextArea(2, 4)]
    public string[] dialogueLines;

    private int currentLine = 0;
    private bool isStarterDialogue = false;

    void Start()
    {
        if (selectionPanel != null)
            selectionPanel.SetActive(false);

        currentLine = 0;
        ShowLine();
    }

    void Update()
    {
        if (Keyboard.current != null &&
            (Keyboard.current.enterKey.wasPressedThisFrame ||
             Keyboard.current.numpadEnterKey.wasPressedThisFrame))
        {
            NextLine();
        }
    }

    public void StartStarterDialogue(
        string creatureName,
        string creatureType,
        string weakAgainst)
    {
        isStarterDialogue = true;

        dialogueLines = new string[]
        {
            "Congratulations on choosing " + creatureName +
            "! May you and your new companion have a wonderful journey together!",

            "Remember, " + creatureName + "'s " + creatureType +
            " type is weak to " + weakAgainst + "!"
        };

        currentLine = 0;

        if (selectionPanel != null)
            selectionPanel.SetActive(false);

        gameObject.SetActive(true);
        ShowLine();
    }

    void ShowLine()
    {
        if (dialogueLines == null ||
            dialogueLines.Length == 0 ||
            currentLine >= dialogueLines.Length)
            return;

        dialogueText.text =
            "<align=left><color=#F5C66B><b>PROFESSOR OXE</b></color></align>\n\n" +
            "<align=left>" + dialogueLines[currentLine] + "</align>\n\n" +
            "<align=right><color=#93C5FD>PRESS ENTER ▼</color></align>";
    }

    void NextLine()
    {
        if (dialogueLines == null || dialogueLines.Length == 0)
            return;

        if (currentLine < dialogueLines.Length - 1)
        {
            currentLine++;
            ShowLine();
        }
        else
        {
            if (isStarterDialogue)
            {
                SceneManager.LoadScene(nextSceneName);
                return;
            }

            if (selectionPanel == null)
            {
                Debug.LogError("SelectionPanel is not assigned!");
                return;
            }

            selectionPanel.SetActive(true);
            gameObject.SetActive(false);
        }
    }
}