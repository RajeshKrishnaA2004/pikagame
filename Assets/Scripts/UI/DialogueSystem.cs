using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// <summary>
/// Bottom-screen dialogue box for Borrowed Time.
///
/// Replaces the old timed toast: lines now stay until the player advances them,
/// and the box freezes the player while it is open. Typed out at
/// <see cref="charsPerSecond"/>.
///
/// Confirm while typing finishes the current line instantly; a second Confirm
/// advances to the next queued line. There is deliberately NO fade-out - a
/// conversation ends when the queue empties, not on a timer.
///
/// Kept separate from the Lab's legacy DialogueManager, which is still wired
/// into the old starter scene and drives a scene change.
/// </summary>
public class DialogueSystem : MonoBehaviour
{
    public static DialogueSystem Instance { get; private set; }

    [Header("UI")]
    public GameObject panel;
    public TMP_Text bodyText;

    [Tooltip("Speaker name line. Hidden when the current line has no speaker.")]
    public TMP_Text speakerText;

    [Tooltip("The little arrow that bobs when more lines are queued.")]
    public RectTransform nextArrow;

    public CanvasGroup canvasGroup;

    [Header("Behaviour")]
    [Tooltip("Typewriter speed. 40 reads as a brisk, readable RPG pace.")]
    public float charsPerSecond = 40f;

    [Tooltip("Seconds for one full up/down bob cycle of the next arrow.")]
    public float arrowBobPeriod = 0.9f;

    [Tooltip("Pixels the arrow travels between bob extremes.")]
    public float arrowBobPixels = 6f;

    [Header("Audio (optional)")]
    public AudioSource blipSource;
    public AudioClip blipClip;

    private struct Line
    {
        public string speaker;
        public string body;
        public Action onComplete;
    }

    private readonly Queue<Line> pending = new Queue<Line>();
    private Line current;
    private Coroutine routine;
    private bool showing;
    private bool typing;
    private int charsShown;
    private Vector2 arrowHome;
    private bool arrowHomeCaptured;

    public bool IsOpen { get { return showing; } }

    public bool IsTyping { get { return typing; } }

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

        if (nextArrow != null && !arrowHomeCaptured)
        {
            arrowHome = nextArrow.anchoredPosition;
            arrowHomeCaptured = true;
        }

        Hide();
    }

    void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    void Update()
    {
        UpdateArrow();

        if (!showing)
            return;
        if (!GameInput.ConfirmPressed)
            return;

        if (typing)
        {
            FinishTyping();
            return;
        }

        Advance();
    }

    public void ShowLine(string body)
    {
        ShowLine(null, body, null);
    }

    public void ShowLine(string speaker, string body, Action onComplete = null)
    {
        if (string.IsNullOrEmpty(body))
            return;

        pending.Enqueue(new Line { speaker = speaker, body = body, onComplete = onComplete });
        if (routine == null)
            routine = StartCoroutine(Pump());
    }

    public int ShowConversation(string speaker, IEnumerable<string> lines,
                                Action onComplete = null)
    {
        if (lines == null)
            return 0;

        int n = 0;
        foreach (string s in lines)
        {
            if (string.IsNullOrEmpty(s))
                continue;
            ShowLine(speaker, s);
            n++;
        }

        if (onComplete != null)
            pending.Enqueue(new Line { speaker = speaker, body = string.Empty,
                                        onComplete = onComplete });

        return n;
    }

    public void ShowMessage(string body, float duration = -1f, Action onComplete = null)
    {
        ShowLine(null, body, onComplete);
    }

    public void ShowMessage(string speaker, string body, float duration = -1f,
                            Action onComplete = null)
    {
        ShowLine(speaker, body, onComplete);
    }

    public void Clear()
    {
        if (routine != null)
        {
            StopCoroutine(routine);
            routine = null;
        }
        pending.Clear();
        current = default(Line);
        typing = false;
        charsShown = 0;
        Hide();
    }

    public void Advance()
    {
        if (current.onComplete != null)
        {
            Action cb = current.onComplete;
            current.onComplete = null;
            cb();
        }

        if (pending.Count > 0)
        {
            current = default(Line);
            typing = false;
            if (routine == null)
                routine = StartCoroutine(Pump());
        }
        else
        {
            Hide();
        }
    }

    private IEnumerator Pump()
    {
        while (pending.Count > 0)
        {
            current = pending.Dequeue();
            yield return Present(current);
        }
        routine = null;
    }

    private IEnumerator Present(Line line)
    {
        bool hasUI = bodyText != null || panel != null;

        if (!hasUI)
        {
            Debug.Log("[Dialogue] " + line.speaker + ": " + line.body);
            if (line.onComplete != null)
                line.onComplete();
            yield break;
        }

        Show();
        FreezePlayer(true);

        if (speakerText != null)
        {
            bool hasSpeaker = !string.IsNullOrEmpty(line.speaker);
            speakerText.gameObject.SetActive(hasSpeaker);
            if (hasSpeaker)
                speakerText.text = line.speaker;
        }

        if (blipSource != null && blipClip != null)
            blipSource.PlayOneShot(blipClip);

        typing = true;
        charsShown = 0;
        if (bodyText != null)
            bodyText.text = "";

        string body = line.body ?? "";
        while (charsShown < body.Length && typing)
        {
            charsShown++;
            if (bodyText != null)
                bodyText.text = body.Substring(0, charsShown);

            yield return new WaitForSeconds(1f / Mathf.Max(1f, charsPerSecond));
        }

        if (bodyText != null)
            bodyText.text = body;
        typing = false;
    }

    public void FinishTyping()
    {
        if (!typing)
            return;

        typing = false;
        if (bodyText != null)
            bodyText.text = current.body ?? "";

        if (routine != null)
        {
            StopCoroutine(routine);
            routine = null;
        }
    }

    private void UpdateArrow()
    {
        if (nextArrow == null)
            return;

        bool visible = showing && !typing && pending.Count > 0;
        if (nextArrow.gameObject.activeSelf != visible)
            nextArrow.gameObject.SetActive(visible);
        if (!visible)
            return;

        if (!arrowHomeCaptured)
        {
            arrowHome = nextArrow.anchoredPosition;
            arrowHomeCaptured = true;
        }

        float period = Mathf.Max(0.05f, arrowBobPeriod);
        float t = (Mathf.Sin(Time.unscaledTime * (Mathf.PI * 2f / period)) + 1f) * 0.5f;
        Vector2 p = arrowHome;
        p.y -= Mathf.Lerp(0f, arrowBobPixels, t);
        nextArrow.anchoredPosition = p;
    }

    private void Show()
    {
        showing = true;
        if (panel != null)
            panel.SetActive(true);
        if (canvasGroup != null)
        {
            canvasGroup.alpha = 1f;
            canvasGroup.blocksRaycasts = true;
        }
    }

    private void Hide()
    {
        showing = false;
        if (canvasGroup != null)
            canvasGroup.alpha = 0f;
        if (panel != null)
            panel.SetActive(false);
        if (nextArrow != null)
            nextArrow.gameObject.SetActive(false);
        if (speakerText != null)
            speakerText.gameObject.SetActive(false);

        FreezePlayer(false);
    }

    private static void FreezePlayer(bool frozen)
    {
        var player = GridPlayerController.Instance;
        if (player != null)
            player.frozen = frozen;
    }
}
