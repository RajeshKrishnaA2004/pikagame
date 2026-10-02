using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Overworld HUD: the region label, the compass/position readout and the zoom
/// indicator. Subscribes to <see cref="MinimapController"/> and
/// <see cref="GameState"/> so nothing has to poll.
/// </summary>
public class HUDController : MonoBehaviour
{
    public static HUDController Instance { get; private set; }

    [Header("Text")]
    public TMP_Text regionLabel;
    public TMP_Text subLabel;
    [Tooltip("Shows e.g. \"FACING S   POS S\".")]
    public TMP_Text compassLabel;
    [Tooltip("Shows e.g. \"ZOOM 1.4x\".")]
    public TMP_Text zoomLabel;

    [Header("Minimap")]
    public MinimapController minimap;
    [Tooltip("Optional: the label under the minimap showing where you are.")]
    public TMP_Text minimapPositionLabel;

    [Header("Panels")]
    public CanvasGroup regionPanel;
    public float regionFadeSpeed = 6f;

    [Header("Zoom readout")]
    [Tooltip("Orthographic size the '1.0x' reading corresponds to.")]
    public float referenceOrthoSize = 5.4f;

    private float targetRegionAlpha;
    private string shownRegion = "";

    void Awake()
    {
        Instance = this;
    }

    void OnDestroy()
    {
        Unsubscribe();
        if (Instance == this)
            Instance = null;
    }

    void Start()
    {
        Subscribe();

        if (regionPanel != null)
        {
            regionPanel.alpha = 0f;
            targetRegionAlpha = 0f;
        }

        SetRegion("", "");
    }

    private void Subscribe()
    {
        if (minimap == null)
            minimap = MinimapController.Instance;

        if (minimap != null)
        {
            minimap.CardinalChanged += OnCardinalChanged;
            OnCardinalChanged(minimap.Cardinal);
        }

        if (GameState.Instance != null)
            GameState.Instance.RosterChanged += OnRosterChanged;
    }

    private void Unsubscribe()
    {
        if (minimap != null)
            minimap.CardinalChanged -= OnCardinalChanged;

        if (GameState.Instance != null)
            GameState.Instance.RosterChanged -= OnRosterChanged;
    }

    void Update()
    {
        if (regionPanel != null)
        {
            regionPanel.alpha = Mathf.MoveTowards(regionPanel.alpha, targetRegionAlpha,
                                                  Time.deltaTime * regionFadeSpeed);
        }

        UpdatePositionLabels();
        UpdateZoomLabel();
    }

    /// <summary>Called by <see cref="MapRegion"/> on entry/exit.</summary>
    public void SetRegion(string name, string subtitle)
    {
        shownRegion = name ?? "";

        if (regionLabel != null)
            regionLabel.text = shownRegion.ToUpperInvariant();

        if (subLabel != null)
        {
            bool hasSub = !string.IsNullOrEmpty(subtitle);
            subLabel.gameObject.SetActive(hasSub);
            if (hasSub)
                subLabel.text = subtitle;
        }

        targetRegionAlpha = string.IsNullOrEmpty(shownRegion) ? 0f : 1f;
    }

    /// <summary>Show a transient message (delegates to DialogueSystem if present).</summary>
    public void ShowToast(string message, float duration = 3f)
    {
        if (DialogueSystem.Instance != null)
            DialogueSystem.Instance.ShowMessage(message, duration);
        else
            Debug.Log("[HUD] " + message);
    }

    private void OnCardinalChanged(string cardinal)
    {
        UpdateCompassLabel(cardinal);
    }

    private void OnRosterChanged()
    {
        // Reserved: the party/upgrade readout will hang off this once the
        // Borrowed Time party system exists.
    }

    private void UpdateCompassLabel(string cardinal)
    {
        if (compassLabel == null)
            return;

        string vertical = minimap != null ? minimap.VerticalPosition : "Center";
        compassLabel.text = "FACING " + cardinal + "   POS " + vertical;
    }

    private void UpdatePositionLabels()
    {
        if (minimapPositionLabel == null || minimap == null)
            return;

        Vector2 n = minimap.NormalisedPosition;
        minimapPositionLabel.text = string.Format("x{0:0}%  y{1:0}%",
                                                  n.x * 100f, n.y * 100f);
    }

    private void UpdateZoomLabel()
    {
        if (zoomLabel == null)
            return;

        CameraRig rig = FindRig();
        if (rig == null)
            return;

        float factor = referenceOrthoSize / Mathf.Max(0.01f, rig.Zoom);
        zoomLabel.text = string.Format("ZOOM {0:0.0}x", factor);
    }

    private CameraRig cachedRig;

    private CameraRig FindRig()
    {
        if (cachedRig == null)
        {
            Camera cam = Camera.main;
            if (cam != null)
                cachedRig = cam.GetComponent<CameraRig>();
            if (cachedRig == null)
                cachedRig = Object.FindFirstObjectByType<CameraRig>();
        }
        return cachedRig;
    }
}