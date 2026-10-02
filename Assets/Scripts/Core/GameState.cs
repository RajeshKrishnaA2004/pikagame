using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Single source of truth for world progress: flags, the party's creature
/// types, inventory items and upgrades.
///
/// Deliberately small. It exists so the world layer (gates, regions, the HUD)
/// can ask "does the player satisfy this?" without depending on the full
/// battle/party systems, which are still to be built. Replace the party and
/// inventory members with the real systems later; the query API stays.
///
/// Persisted through PlayerPrefs so a gate opened in one session stays open.
/// </summary>
public class GameState : MonoBehaviour
{
    public static GameState Instance { get; private set; }

    private const string Prefix = "bt.";

    private readonly HashSet<string> flags = new HashSet<string>(StringComparer.Ordinal);
    private readonly List<CreatureType> partyTypes = new List<CreatureType>();
    private readonly Dictionary<string, int> items = new Dictionary<string, int>(StringComparer.Ordinal);

    [Tooltip("Write state to PlayerPrefs whenever it changes.")]
    public bool persist = true;

    [Tooltip("Starting creature type for a fresh save (mirrors the starter pick).")]
    public CreatureType startingCreature = CreatureType.None;

    public int Upgrades { get; private set; }
    public int GymsCleared { get; private set; }

    /// <summary>Raised whenever a flag is set or cleared.</summary>
    public event Action<string, bool> FlagChanged;
    /// <summary>Raised whenever the party or inventory changes.</summary>
    public event Action RosterChanged;

    public ICollection<string> Flags { get { return flags; } }
    public IList<CreatureType> PartyTypes { get { return partyTypes; } }

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        Load();
    }

    void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    // ------------------------------------------------------------------ flags

    public bool HasFlag(string flag)
    {
        return !string.IsNullOrEmpty(flag) && flags.Contains(flag);
    }

    public void SetFlag(string flag, bool value = true)
    {
        if (string.IsNullOrEmpty(flag))
            return;

        bool changed = value ? flags.Add(flag) : flags.Remove(flag);
        if (!changed)
            return;

        Save();
        if (FlagChanged != null)
            FlagChanged(flag, value);
    }

    public void ClearFlag(string flag)
    {
        SetFlag(flag, false);
    }

    // ----------------------------------------------------------- party / items

    public bool HasCreatureType(CreatureType type)
    {
        return partyTypes.Contains(type);
    }

    public int CountCreatureType(CreatureType type)
    {
        int n = 0;
        for (int i = 0; i < partyTypes.Count; i++)
            if (partyTypes[i] == type)
                n++;
        return n;
    }

    public void AddCreature(CreatureType type)
    {
        if (type == CreatureType.None)
            return;
        partyTypes.Add(type);
        Save();
        if (RosterChanged != null)
            RosterChanged();
    }

    public bool RemoveCreature(CreatureType type)
    {
        bool removed = partyTypes.Remove(type);
        if (removed)
        {
            Save();
            if (RosterChanged != null)
                RosterChanged();
        }
        return removed;
    }

    public int ItemCount(string itemId)
    {
        int n;
        return !string.IsNullOrEmpty(itemId) && items.TryGetValue(itemId, out n) ? n : 0;
    }

    public void AddItem(string itemId, int amount = 1)
    {
        if (string.IsNullOrEmpty(itemId) || amount == 0)
            return;
        int total = ItemCount(itemId) + amount;
        if (total <= 0)
            items.Remove(itemId);
        else
            items[itemId] = total;
        Save();
        if (RosterChanged != null)
            RosterChanged();
    }

    public bool ConsumeItem(string itemId, int amount = 1)
    {
        if (ItemCount(itemId) < amount)
            return false;
        AddItem(itemId, -amount);
        return true;
    }

    // ------------------------------------------------------------ progression

    public void SetUpgrades(int value)
    {
        Upgrades = Mathf.Max(0, value);
        Save();
    }

    public void SetGymsCleared(int value)
    {
        GymsCleared = Mathf.Max(0, value);
        Save();
    }

    // ------------------------------------------------------------ persistence

    public void Save()
    {
        if (!persist)
            return;

        PlayerPrefs.SetString(Prefix + "flags", string.Join("|", ToArray(flags)));

        var types = new List<string>();
        foreach (CreatureType t in partyTypes)
            types.Add(t.ToString());
        PlayerPrefs.SetString(Prefix + "party", string.Join("|", types.ToArray()));

        var kv = new List<string>();
        foreach (KeyValuePair<string, int> pair in items)
            kv.Add(pair.Key + "=" + pair.Value);
        PlayerPrefs.SetString(Prefix + "items", string.Join("|", kv.ToArray()));

        PlayerPrefs.SetInt(Prefix + "upgrades", Upgrades);
        PlayerPrefs.SetInt(Prefix + "gyms", GymsCleared);
        PlayerPrefs.Save();
    }

    public void Load()
    {
        flags.Clear();
        partyTypes.Clear();
        items.Clear();

        foreach (string s in Split(PlayerPrefs.GetString(Prefix + "flags", "")))
            flags.Add(s);

        foreach (string s in Split(PlayerPrefs.GetString(Prefix + "party", "")))
        {
            CreatureType t = CreatureTypeUtil.Parse(s);
            if (t != CreatureType.None)
                partyTypes.Add(t);
        }

        foreach (string s in Split(PlayerPrefs.GetString(Prefix + "items", "")))
        {
            int eq = s.IndexOf('=');
            if (eq <= 0)
                continue;
            int n;
            if (int.TryParse(s.Substring(eq + 1), out n))
                items[s.Substring(0, eq)] = n;
        }

        Upgrades = PlayerPrefs.GetInt(Prefix + "upgrades", 0);
        GymsCleared = PlayerPrefs.GetInt(Prefix + "gyms", 0);

        if (partyTypes.Count == 0 && startingCreature != CreatureType.None)
            partyTypes.Add(startingCreature);
    }

    /// <summary>Wipe all progress (dev helper and "new game").</summary>
    public void ResetAll()
    {
        flags.Clear();
        partyTypes.Clear();
        items.Clear();
        Upgrades = 0;
        GymsCleared = 0;

        if (startingCreature != CreatureType.None)
            partyTypes.Add(startingCreature);

        Save();
    }

    private static string[] ToArray(ICollection<string> set)
    {
        var a = new string[set.Count];
        set.CopyTo(a, 0);
        return a;
    }

    private static string[] Split(string s)
    {
        if (string.IsNullOrEmpty(s))
            return new string[0];
        return s.Split(new[] { '|' }, StringSplitOptions.RemoveEmptyEntries);
    }
}