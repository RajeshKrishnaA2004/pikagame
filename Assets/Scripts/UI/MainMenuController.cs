using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;

#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Title screen: PLAY / CONTINUE / QUIT.
///
/// Up/Down move the arrow, Confirm picks. CONTINUE is greyed out when no save
/// exists, so the only way into a new game is PLAY - and PLAY deliberately
/// deletes any existing save first, because starting over is the point of it.
///
/// The scene is built by ChapterBuilder ("4. Build MainMenu"), so the menu
/// items are wired in code rather than left dangling in a hand-made scene.
/// </summary>
public class MainMenuController : MonoBehaviour
{
    public const string MainMenuSceneName = "MainMenu";
    public const string LabSceneName = "Lab";

    // Item order. KEEP IN SYNC with BuildMainMenu.
    public const int ItemPlay = 0;
    public const int ItemContinue = 1;
    public const int ItemQuit = 2;

    [Header("Menu")]
    [Tooltip("PLAY, CONTINUE, QUIT - in that order.")]
    public RectTransform[] items = new RectTransform[0];

    [Tooltip("The arrow that slides to the active row.")]
    public RectTransform selector;

    [Tooltip("Horizontal offset from the active row to the arrow.")]
    public float selectorOffsetX = -26f;

    [Tooltip("Seconds a repeat input is ignored, so a held key cannot spin.")]
    public float moveCooldown = 0.16f;

    [Header("Colours")]
    public Color activeColor = Color.white;
    public Color inactiveColor = new Color(1f, 1f, 1f, 0.45f);
    public Color disabledColor = new Color(1f, 1f, 1f, 0.22f);

    [Header("Fade")]
    public CanvasGroup fadeGroup;
    public float fadeTime = 0.45f;

    private int index = ItemPlay;
    private float cooldown;
    private bool busy;

    private bool HasSave { get { return GameSave.Exists(); } }

    void OnEnable()
    {
        GameInput.Enable();
    }

    void OnDisable()
    {
        GameInput.Disable();
    }

    void Start()
    {
        if (GameSave.Exists())
            Debug.Log("[MainMenu] save found: " + GameSave.FilePath);
        else
            Debug.Log("[MainMenu] no save - CONTINUE stays greyed out");

        Refresh();
    }

    void Update()
    {
        if (busy)
            return;

        if (cooldown > 0f)
        {
            cooldown -= Time.unscaledDeltaTime;
            return;
        }

        float v = GameInput.Move.y;
        if (v > 0.4f)
        {
            Select(index - 1);
            return;
        }
        if (v < -0.4f)
        {
            Select(index + 1);
            return;
        }

        if (GameInput.ConfirmPressed)
            Activate();
    }

    /// <summary>Moves the selector, skipping nothing - greyed items still select.</summary>
    private void Select(int i)
    {
        int n = items != null ? items.Length : 0;
        if (n <= 0)
            return;

        index = ((i % n) + n) % n;
        cooldown = moveCooldown;
        Refresh();
    }

    private void Refresh()
    {
        bool save = HasSave;

        if (items != null)
        {
            for (int i = 0; i < items.Length; i++)
            {
                RectTransform t = items[i];
                if (t == null)
                    continue;

                bool on = i == index;
                bool disabled = i == ItemContinue && !save;

                var label = t.GetComponent<TMP_Text>();
                if (label != null)
                    label.color = disabled ? disabledColor : (on ? activeColor : inactiveColor);
            }
        }

        if (selector != null)
        {
            bool visible = items != null && index < items.Length && items[index] != null;
            selector.gameObject.SetActive(visible);
            if (visible)
            {
                Vector3 p = items[index].anchoredPosition;
                selector.anchoredPosition =
                    new Vector2(p.x + selectorOffsetX, p.y);
            }
        }
    }

    private void Activate()
    {
        if (index == ItemContinue && !HasSave)
        {
            Debug.Log("[MainMenu] CONTINUE ignored - no save file");
            return;
        }

        switch (index)
        {
            case ItemPlay:
                StartCoroutine(FadeThenLoad(LabSceneName, true));
                break;

            case ItemContinue:
                StartCoroutine(FadeThenLoad(SceneForChapter(), false));
                break;

            case ItemQuit:
                Quit();
                break;
        }
    }

    /// <summary>Chapter 0 is the route; later chapters are added as they exist.</summary>
    private static string SceneForChapter()
    {
        GameSaveData data = GameSave.Load();
        int chapter = data != null ? data.chapterIndex : 0;
        return chapter <= 0 ? "Chapter1_Route" : "Chapter" + (chapter + 1) + "_Route";
    }

    private IEnumerator FadeThenLoad(string sceneName, bool wipeSave)
    {
        busy = true;
        GameInput.Disable();

        // PLAY is a fresh start, so it discards the old run.
        if (wipeSave && GameSave.Exists())
        {
            GameSave.Delete();
            Debug.Log("[MainMenu] PLAY wiped the existing save");
        }

        if (fadeGroup != null)
        {
            float t = 0f;
            while (t < fadeTime)
            {
                t += Time.unscaledDeltaTime;
                fadeGroup.alpha = Mathf.Clamp01(t / fadeTime);
                yield return null;
            }
            fadeGroup.alpha = 1f;
        }
        else
        {
            yield return null;
        }

        SceneManager.LoadScene(sceneName);
    }

    private static void Quit()
    {
        Debug.Log("[MainMenu] QUIT");
#if UNITY_EDITOR
        // Quit() is a no-op in the editor, so stop play mode instead.
        EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
