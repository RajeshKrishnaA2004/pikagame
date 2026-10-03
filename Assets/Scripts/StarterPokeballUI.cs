using UnityEngine;
using UnityEngine.UI;

public class StarterPokeballUI : MonoBehaviour
{
    [Header("References")]
    public CreaturePartyManager partyManager;
    public Image pokeballImage;

    [Header("Pokéball Damage Sprites")]
    public Sprite stability5Sprite;
    public Sprite stability4Sprite;
    public Sprite stability3Sprite;
    public Sprite stability2Sprite;
    public Sprite stability1Sprite;

    private int lastStability = -1;

    void Start()
    {
        if (pokeballImage == null)
            pokeballImage = GetComponent<Image>();

        UpdatePokeball();
    }

    void Update()
    {
        if (partyManager == null)
            return;

        int currentStability =
            partyManager.GetStarterStability();

        if (currentStability != lastStability)
        {
            UpdatePokeball();
        }
    }

    void UpdatePokeball()
    {
        if (partyManager == null)
        {
            Debug.LogError(
                "StarterPokeballUI: Party Manager is not assigned!"
            );
            return;
        }

        if (pokeballImage == null)
        {
            Debug.LogError(
                "StarterPokeballUI: Pokéball Image is not assigned!"
            );
            return;
        }

        int stability =
            partyManager.GetStarterStability();

        lastStability = stability;

        if (stability <= 0)
        {
            pokeballImage.enabled = false;
            return;
        }

        pokeballImage.enabled = true;

        switch (stability)
        {
            case 5:
                pokeballImage.sprite =
                    stability5Sprite;
                break;

            case 4:
                pokeballImage.sprite =
                    stability4Sprite;
                break;

            case 3:
                pokeballImage.sprite =
                    stability3Sprite;
                break;

            case 2:
                pokeballImage.sprite =
                    stability2Sprite;
                break;

            case 1:
                pokeballImage.sprite =
                    stability1Sprite;
                break;
        }

        pokeballImage.preserveAspect = true;
    }
}