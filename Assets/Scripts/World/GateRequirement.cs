using UnityEngine;

/// <summary>What a gate can ask for.</summary>
public enum RequirementKind
{
    CreatureType = 0,
    CreatureTypeCount = 1,
    Flag = 2,
    Item = 3,
    GymsCleared = 4,
    Upgrades = 5,
    AlwaysOpen = 6
}

/// <summary>
/// One condition a <see cref="RequirementGate"/> checks. Plain serializable
/// class (no asset files) so gates are configured entirely in the inspector
/// and cannot break when an asset GUID moves.
/// </summary>
[System.Serializable]
public class Requirement
{
    [Tooltip("What kind of thing the player must have done/brought.")]
    public RequirementKind kind = RequirementKind.CreatureType;

    [Tooltip("Used by CreatureType / CreatureTypeCount.")]
    public CreatureType creatureType = CreatureType.Tide;

    [Tooltip("Used by Flag (the flag name) and Item (the item id).")]
    public string key = "";

    [Tooltip("How many are required (CreatureTypeCount / Item / GymsCleared / Upgrades).")]
    public int amount = 1;

    [Tooltip("Invert the test: open only while the condition is NOT met.")]
    public bool invert = false;

    /// <summary>Is this requirement satisfied right now?</summary>
    public bool IsMet(GameState state)
    {
        bool met = Evaluate(state);
        return invert ? !met : met;
    }

    private bool Evaluate(GameState state)
    {
        if (state == null)
            return false;

        switch (kind)
        {
            case RequirementKind.CreatureType:
                return state.HasCreatureType(creatureType);

            case RequirementKind.CreatureTypeCount:
                return state.CountCreatureType(creatureType) >= Mathf.Max(1, amount);

            case RequirementKind.Flag:
                return state.HasFlag(key);

            case RequirementKind.Item:
                return state.ItemCount(key) >= Mathf.Max(1, amount);

            case RequirementKind.GymsCleared:
                return state.GymsCleared >= Mathf.Max(1, amount);

            case RequirementKind.Upgrades:
                return state.Upgrades >= Mathf.Max(1, amount);

            case RequirementKind.AlwaysOpen:
            default:
                return true;
        }
    }

    /// <summary>Player-facing text, e.g. "a Tide creature".</summary>
    public string Describe()
    {
        switch (kind)
        {
            case RequirementKind.CreatureType:
                return "a " + CreatureTypeUtil.DisplayName(creatureType) + " creature";

            case RequirementKind.CreatureTypeCount:
                return Mathf.Max(1, amount) + " " + CreatureTypeUtil.DisplayName(creatureType) +
                       " creatures";

            case RequirementKind.Flag:
                return string.IsNullOrEmpty(key) ? "progress" : key;

            case RequirementKind.Item:
                return Mathf.Max(1, amount) + " x " + key;

            case RequirementKind.GymsCleared:
                return Mathf.Max(1, amount) + " gym badge(s)";

            case RequirementKind.Upgrades:
                return Mathf.Max(1, amount) + " upgrade(s)";

            default:
                return "nothing";
        }
    }

    /// <summary>Short label for inspectors and debug output.</summary>
    public override string ToString()
    {
        return (invert ? "NOT " : "") + kind + " " + Describe();
    }
}