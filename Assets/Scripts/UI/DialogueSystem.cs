using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// <summary>
/// Lightweight overworld message box used by gates, regions and pickups.
///
/// Deliberately separate from the Lab's <see cref="DialogueManager"/> (which
/// drives the starter-choosing conversation and changes scenes). This one is a
/// non-blocking toast/queue any world script can push text into without caring
/// whether a dialogue UI exists -- with no panel assigned it logs instead.
/// </summary>
public class DialogueSystem : MonoBehaviour
{
    public static DialogueSystem Instance { get; private set; }

    [Header("UI")]
    public GameObject panel;
    public TMP_Text bodyText;
    [Tooltip("Optional speaker line drawn above the body.")]
    public TMP_Text speakerText;
    public CanvasGroup canvasGroup;

    [Header("Behaviour")]
    [Tooltip("Default seconds a message stays on screen. 0 = stay until replaced.")]
    public float defaultDuration = 3f;
    public float fadeInTime = 0.12f;
    public float fadeOutTime = 0.25f;
    [Tooltip("Queue messages instead of overwriting the current one.")]
    public bool queueMessages = false;

    [Tooltip("Play a short blip whenever a message appears.")]
    public AudioSource blipSource;
    public AudioClip blipClip;

    private readonly Queue<Message> pending = new Queue<Message>();
    private Message current;
    private Coroutine routine;
    private bool showing;

    private struct Message
    {
        public string speaker;
        public string body;
        public float duration;
        public Action onComplete;
    }

    public bool IsVisible { get { return showing; } }

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
        if (canvasGroup != null)
            canvasGroup.alpha = 0f;
        if (panel != null)
            panel.SetActive(false);
    }

    void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    /// <summary>Show a one-shot line, replacing whatever is on screen.</summary>
    public void ShowMessage(string body, float duration = -1f, Action onComplete = null)
    {
        ShowMessage(null, body, duration, onComplete);
    }

    /// <summary>Show a line attributed to a speaker.</summary>
    public void ShowMessage(string speaker, string body, float duration = -1f,
                            Action onComplete = null)
    {
        if (string.IsNullOrEmpty(body))
            return;

        var msg = new Message
        {
            speaker = speaker,
            body = body,
            duration = duration < 0f ? defaultDuration : duration,
            onComplete = onComplete
        };

        if (queueMessages || !showing)
        {
            pending.Enqueue(msg);
            if (routine == null)
                routine = StartCoroutine(Pump());
        }
        else
        {
            // Interrupt so gameplay feedback is never queued behind itself.
            pending.Clear();
            pending.Enqueue(msg);
            if (routine != null)
                StopCoroutine(routine);
            routine = StartCoroutine(Pump());
        }
    }

    /// <summary>Hide immediately and drop anything queued.</summary>
    public void Clear()
    {
        pending.Clear();
        if (routine != null)
        {
            StopCoroutine(routine);
            routine = null;
        }
        current = default(Message);
        if (canvasGroup != null)
            canvasGroup.alpha = 0f;
        if (panel != null)
            panel.SetActive(false);
        showing = false;
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

    private IEnumerator Present(Message msg)
    {
        if (bodyText == null && panel == null)
        {
            Debug.Log("[Dialogue] " + msg.body);
            if (msg.onComplete != null)
                msg.onComplete();
            yield break;
        }

        if (panel != null)
            panel.SetActive(true);
        if (bodyText != null)
            bodyText.text = msg.body;
        if (speakerText != null)
        {
            bool hasSpeaker = !string.IsNullOrEmpty(msg.speaker);
            speakerText.gameObject.SetActive(hasSpeaker);
            if (hasSpeaker)
                speakerText.text = msg.speaker;
        }

        if (blipSource != null && blipClip != null)
            blipSource.PlayOneShot(blipClip);

        showing = true;
        yield return Fade(0f, 1f, fadeInTime);

        if (msg.duration > 0f)
            yield return new WaitForSeconds(msg.duration);
        else
            while (pending.Count == 0)
                yield return null;

        yield return Fade(canvasGroup != null ? canvasGroup.alpha : 1f, 0f, fadeOutTime);

        if (panel != null)
            panel.SetActive(false);
        showing = false;

        if (msg.onComplete != null)
            msg.onComplete();
    }

    private IEnumerator Fade(float from, float to, float time)
    {
        if (canvasGroup == null && panel != null)
            canvasGroup = panel.GetComponent<CanvasGroup>();

        if (canvasGroup == null || time <= 0f)
        {
            if (canvasGroup != null)
                canvasGroup.alpha = to;
            yield return null;
            yield break;
        }

        float t = 0f;
        while (t < time)
        {
            t += Time.unscaledDeltaTime;
            canvasGroup.alpha = Mathf.Lerp(from, to, Mathf.Clamp01(t / time));
            yield return null;
        }
        canvasGroup.alpha = to;
    }
}