using UnityEngine;

/// <summary>
/// Blocks a path until the player satisfies a set of requirements.
///
/// Setup on a path tile:
///   1. Put this component on an empty GameObject at the choke point.
///   2. Add a trigger BoxCollider2D ("Is Trigger") sized to roughly one tile
///      so the gate can detect a player standing in the doorway.
///   3. Add a child GameObject with a NON-trigger BoxCollider2D and the
///      barrier artwork (Assets/Art/World/gate.png). This child is what
///      physically stops the player, and it is what gets disabled on open.
///   4. Fill in `requirements` and (optionally) `lockedMessage`.
///
/// The gate can also set a flag when it opens so the state survives a reload.
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class RequirementGate : MonoBehaviour
{
    [Header("Requirements")]
    [Tooltip("All must be met (unless requireAll is off, then any one is enough).")]
    public bool requireAll = true;
    public Requirement[] requirements = new Requirement[0];

    [Header("Barrier")]
    [Tooltip("The child that physically blocks movement. Disabled when open.")]
    public GameObject barrier;
    [Tooltip("Extra objects to disable on open (e.g. the closed artwork).")]
    public GameObject[] extraBlockers = new GameObject[0];

    [Header("Feedback")]
    [Tooltip("Overrides the auto-generated message when the gate is locked.")]
    [TextArea(2, 4)] public string lockedMessage = "";
    public string openMessage = "";
    [Tooltip("Only show the locked message once per entry, not every frame.")]
    public bool messageOnceWhileTouching = true;
    [Tooltip("Seconds before the locked message can be shown again.")]
    public float messageCooldown = 1.5f;

    [Header("State")]
    [Tooltip("Set this flag in GameState when the gate opens. Leave blank for none.")]
    public string openFlag = "";
    [Tooltip("Start already open if this flag is present in GameState.")]
    public string preOpenedFlag = "";

    [Header("Behaviour")]
    public bool oneShot = true;
    public bool hideBarrierWhenOpen = true;

    private bool isOpen;
    private bool playerTouching;
    private float lastMessageTime = -99f;
    private Collider2D trigger;

    /// <summary>True when the gate is currently open.</summary>
    public bool IsOpen { get { return isOpen; } }

    void Awake()
    {
        trigger = GetComponent<Collider2D>();
        trigger.isTrigger = true;
    }

    void Start()
    {
        GameState state = GameState.Instance;

        // Already unlocked in a previous session?
        bool preOpened = state != null && !string.IsNullOrEmpty(preOpenedFlag) &&
                         state.HasFlag(preOpenedFlag);
        bool satisfied = preOpened || RequirementsMet(state);

        if (satisfied)
            Open(false);
        else
            SetLocked(true);
    }

    void FixedUpdate()
    {
        if (isOpen && oneShot)
            return;
        if (!playerTouching)
            return;

        GameState state = GameState.Instance;
        if (RequirementsMet(state))
            Open(true);
        else if (messageOnceWhileTouching)
            ShowLockedMessage(state);
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (!IsPlayer(other))
            return;

        playerTouching = true;

        if (isOpen && oneShot)
            return;

        GameState state = GameState.Instance;
        if (RequirementsMet(state))
            Open(true);
        else
            ShowLockedMessage(state);
    }

    void OnTriggerExit2D(Collider2D other)
    {
        if (IsPlayer(other))
            playerTouching = false;
    }

    /// <summary>True if the configured requirements are satisfied.</summary>
    public bool RequirementsMet(GameState state)
    {
        if (requirements == null || requirements.Length == 0)
            return true;

        for (int i = 0; i < requirements.Length; i++)
        {
            if (requirements[i] == null)
                continue;

            bool met = requirements[i].IsMet(state);
            if (requireAll && !met)
                return false;
            if (!requireAll && met)
                return true;
        }
        return requireAll;
    }

    /// <summary>Auto-generate a readable "you need X" line.</summary>
    public string BuildLockedMessage()
    {
        if (!string.IsNullOrEmpty(lockedMessage))
            return lockedMessage;

        if (requirements == null || requirements.Length == 0)
            return "The way is blocked.";

        var parts = new System.Collections.Generic.List<string>();
        for (int i = 0; i < requirements.Length; i++)
        {
            if (requirements[i] == null)
                continue;
            if (requirements[i].kind == RequirementKind.AlwaysOpen)
                continue;
            parts.Add(requirements[i].Describe());
        }

        if (parts.Count == 0)
            return "The way is blocked.";

        string list;
        if (parts.Count == 1)
            list = parts[0];
        else
            list = string.Join(", ", parts.ToArray(), 0, parts.Count - 1) +
                   (requireAll ? " and " : " or ") + parts[parts.Count - 1];

        return "You need " + list + " to pass.";
    }

    private void ShowLockedMessage(GameState state)
    {
        if (Time.time - lastMessageTime < Mathf.Max(0f, messageCooldown))
            return;
        lastMessageTime = Time.time;

        DialogueSystem dialogue = DialogueSystem.Instance;
        if (dialogue != null)
            dialogue.ShowMessage(BuildLockedMessage());
        else
            Debug.Log("[Gate] " + BuildLockedMessage());
    }

    private void Open(bool announce)
    {
        if (isOpen && oneShot)
            return;

        isOpen = true;
        SetLocked(false);

        if (!string.IsNullOrEmpty(openFlag))
        {
            GameState state = GameState.Instance;
            if (state != null)
                state.SetFlag(openFlag);
        }

        if (announce)
        {
            if (!string.IsNullOrEmpty(openMessage))
            {
                DialogueSystem dialogue = DialogueSystem.Instance;
                if (dialogue != null)
                    dialogue.ShowMessage(openMessage);
            }
            else
            {
                Debug.Log("[Gate] '" + name + "' opened.");
            }
        }
    }

    private void SetLocked(bool locked)
    {
        if (barrier != null)
        {
            if (locked)
            {
                barrier.SetActive(true);
            }
            else if (hideBarrierWhenOpen)
            {
                barrier.SetActive(false);
            }
            else
            {
                var col = barrier.GetComponent<Collider2D>();
                if (col != null)
                    col.enabled = false;
                var sr = barrier.GetComponent<SpriteRenderer>();
                if (sr != null)
                    sr.color = new Color(1f, 1f, 1f, 0.25f);
            }
        }

        if (!locked && extraBlockers != null)
        {
            for (int i = 0; i < extraBlockers.Length; i++)
                if (extraBlockers[i] != null)
                    extraBlockers[i].SetActive(false);
        }
    }

    private static bool IsPlayer(Collider2D other)
    {
        if (other == null)
            return false;
        if (other.attachedRigidbody != null &&
            other.attachedRigidbody.CompareTag("Player"))
            return true;
        return other.CompareTag("Player");
    }

    void OnDrawGizmos()
    {
        var col = GetComponent<Collider2D>();
        Gizmos.color = Application.isPlaying && isOpen
            ? new Color(0.35f, 0.9f, 0.45f, 0.85f)
            : new Color(0.95f, 0.35f, 0.3f, 0.85f);

        var box = col as BoxCollider2D;
        if (box != null)
            Gizmos.DrawWireCube(transform.position + (Vector3)box.offset,
                                new Vector3(box.size.x, box.size.y, 0.1f));
    }
}