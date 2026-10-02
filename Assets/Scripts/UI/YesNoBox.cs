using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// Reusable Yes/No choice box. Up/Down moves the selector, Confirm picks,
/// Cancel is always "No" and closes without invoking anything.
///
/// Coexists with the dialogue box: the dialogue freezes the player, this does
/// NOT, because a choice is not a conversation. It reads input itself and runs
/// its own coroutine, so it can be driven entirely through
/// <see cref="Ask"/>. Only one choice may be open at a time.
/// </summary>
public class YesNoBox : MonoBehaviour
{
    public static YesNoBox Instance { get; private set; }

    [Header("UI")]
    public GameObject panel;
    public CanvasGroup canvasGroup;

    [Tooltip("The two rows, in order. Row 0 is highlighted when the box opens.")]
    public GameObject[] choices = new GameObject[0];

    [Header("Labels")]
    public Color activeColor = Color.white;
    public Color inactiveColor = new Color(1f, 1f, 1f, 0.45f);

    private Coroutine routine;
    private int index;
    private Action onYes;
    private Action onNo;

    public bool IsOpen { get { return panel != null && panel.activeSelf; } }

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        if (canvasGroup == null && panel != null)
            canvasGroup = panel.GetComponent<CanvasGroup>();
        Hide();
    }

    void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    /// <summary>True choice; false means No (same as Cancel).</summary>
    public void Ask(string prompt, Action onYes, Action onNo = null)
    {
        if (routine != null)
            StopCoroutine(routine);

        this.onYes = onYes;
        this.onNo = onNo;

        if (panel == null)
        {
            Debug.LogWarning("[YesNo] no panel assigned - treating as Yes");
            if (onYes != null)
                onYes();
            return;
        }

        index = 0;
        panel.SetActive(true);
        if (canvasGroup != null)
            canvasGroup.alpha = 1f;
        Refresh();

        routine = StartCoroutine(Pump());
    }

    public void Close()
    {
        if (routine != null)
        {
            StopCoroutine(routine);
            routine = null;
        }
        Hide();
    }

    private IEnumerator Pump()
    {
        while (IsOpen)
        {
            float v = GameInput.Move.y;
            if (v > 0.4f)
            {
                SetIndex(index - 1);
                while (GameInput.Move.y > 0.4f)
                    yield return null;
            }
            else if (v < -0.4f)
            {
                SetIndex(index + 1);
                while (GameInput.Move.y < -0.4f)
                    yield return null;
            }

            if (GameInput.CancelPressed)
            {
                Choose(false);
                yield break;
            }
            if (GameInput.ConfirmPressed)
            {
                Choose(index == 0);
                yield break;
            }

            yield return null;
        }
        routine = null;
    }

    private void SetIndex(int i)
    {
        if (choices == null || choices.Length == 0)
        {
            index = 0;
            return;
        }
        int n = choices.Length;
        index = ((i % n) + n) % n;
        Refresh();
    }

    private void Refresh()
    {
        if (choices == null)
            return;

        for (int i = 0; i < choices.Length; i++)
        {
            if (choices[i] == null)
                continue;

            var g = choices[i].GetComponent<CanvasGroup>();
            if (g == null)
                g = choices[i].AddComponent<CanvasGroup>();

            bool on = i == index;
            g.alpha = on ? 1f : 0.45f;
            g.blocksRaycasts = on;
        }
    }

    private void Choose(bool yes)
    {
        Action yesCb = onYes;
        Action noCb = onNo;
        onYes = null;
        onNo = null;

        Hide();

        if (yes)
        {
            if (yesCb != null)
                yesCb();
        }
        else if (noCb != null)
        {
            noCb();
        }
    }

    private void Hide()
    {
        if (panel != null)
            panel.SetActive(false);
        if (canvasGroup != null)
            canvasGroup.alpha = 0f;
    }
}
