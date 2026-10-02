/// <summary>
/// Creature types. These follow the design document (Borrowed Time), which
/// uses Flame / Tide / Leaf / Stone / Spark rather than the classic
/// Fire / Water / Grass set.
/// </summary>
public enum CreatureType
{
    None = 0,
    Flame = 1,
    Tide = 2,
    Leaf = 3,
    Stone = 4,
    Spark = 5
}

public static class CreatureTypeUtil
{
    public static string DisplayName(CreatureType t)
    {
        switch (t)
        {
            case CreatureType.Flame: return "Flame";
            case CreatureType.Tide: return "Tide";
            case CreatureType.Leaf: return "Leaf";
            case CreatureType.Stone: return "Stone";
            case CreatureType.Spark: return "Spark";
            default: return "None";
        }
    }

    /// <summary>Parse a type name (case/space insensitive).</summary>
    public static CreatureType Parse(string s)
    {
        if (string.IsNullOrEmpty(s))
            return CreatureType.None;
        switch (s.Trim().ToLowerInvariant())
        {
            case "flame": case "fire": return CreatureType.Flame;
            case "tide": case "water": return CreatureType.Tide;
            case "leaf": case "grass": return CreatureType.Leaf;
            case "stone": case "rock": case "ground": return CreatureType.Stone;
            case "spark": case "electric": return CreatureType.Spark;
            default: return CreatureType.None;
        }
    }
}