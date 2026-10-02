
public static class TypeChart
{
    public static float GetMultiplier(
        CreatureType attacker,
        CreatureType defender)
    {
        if (attacker == defender)
            return 1f;

        switch (attacker)
        {
            case CreatureType.Fire:
                if (defender == CreatureType.Grass)
                    return 2f;

                if (defender == CreatureType.Electric)
                    return 0.5f;
                break;

            case CreatureType.Grass:
                if (defender == CreatureType.Electric)
                    return 2f;

                if (defender == CreatureType.Fire)
                    return 0.5f;
                break;

            case CreatureType.Electric:
                if (defender == CreatureType.Fire)
                    return 2f;

                if (defender == CreatureType.Grass)
                    return 0.5f;
                break;
        }

        return 1f;
    }
}
  