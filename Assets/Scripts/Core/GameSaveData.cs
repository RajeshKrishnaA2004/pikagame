using System;
using System.Collections.Generic;

/// <summary>
/// One saved playthrough. Plain serialisable data - no UnityEngine.Object
/// references, so it survives JsonUtility cleanly.
/// </summary>
[Serializable]
public class GameSaveData
{
    /// <summary>Id of the creature the player chose in the Lab, or "" if undecided.</summary>
    public string starterId = "";

    /// <summary>Ids of every party member, in order. Party[0] is the lead.</summary>
    public List<string> party = new List<string>();

    /// <summary>True once the post-gym Ember reward has been handed out.</summary>
    public bool lastEmberGranted = false;

    /// <summary>Stat points still to spend. New games start with 5.</summary>
    public int upgradesLeft = 5;

    /// <summary>0 = Chapter 1 (the route). Advances as chapters are finished.</summary>
    public int chapterIndex = 0;

    /// <summary>Guard against a corrupt or truncated file.</summary>
    public bool IsSane()
    {
        if (upgradesLeft < 0 || chapterIndex < 0)
            return false;
        if (party == null)
            return false;
        for (int i = 0; i < party.Count; i++)
        {
            if (string.IsNullOrEmpty(party[i]))
                return false;
        }
        // A starter must also be in the party, otherwise the save is inconsistent.
        return string.IsNullOrEmpty(starterId) || party.Contains(starterId);
    }
}
