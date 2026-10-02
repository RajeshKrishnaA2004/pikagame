
using UnityEngine;

[CreateAssetMenu(fileName = "NewCreature",
    menuName = "Creatures/Creature")]
public class CreatureData : ScriptableObject
{
    public string creatureName;
    public CreatureType type;
    public Sprite battleSprite;

    public int maxHP = 100;
    public int typeAttackDamage = 20;
    public int normalAttackDamage = 10;
}
  