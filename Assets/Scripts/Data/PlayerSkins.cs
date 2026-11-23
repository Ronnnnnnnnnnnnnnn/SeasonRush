using UnityEngine;

namespace SeasonRush
{
    [CreateAssetMenu(menuName = "Season Rush/Player Skins", fileName = "PlayerSkins")]
    public class PlayerSkins : ScriptableObject
    {
        [Tooltip("Element 0 = player 1 (blue, host), element 1 = player 2 (red). The picture should have its pivot at the feet.")]
        public Sprite[] slotSprites = new Sprite[2];

        [Tooltip("Tick when the pictures show the character looking to the LEFT (the sword is on the left). The game then mirrors the " +
                 "picture while the player walks to the right. Untick it if you draw the character looking to the right.")]
        public bool artFacesLeft = true;

        [Tooltip("Optional: a whole prefab per player (picture + Animator with idle / walk / attack). Element 0 = Player 1.prefab, element 1 = Player 2.prefab. " +
                 "When set it is used instead of the single picture above. The Animator needs a Bool 'IsMoving' and a Trigger 'Attack'.")]
        public GameObject[] slotPrefabs = new GameObject[2];

        static PlayerSkins _loaded;
        static bool _triedLoading;

        static PlayerSkins Instance
        {
            get
            {
                if (!_triedLoading)
                {
                    _triedLoading = true;
                    _loaded = Resources.Load<PlayerSkins>("PlayerSkins");
                }
                return _loaded;
            }
        }

        public static Sprite For(int slot)
        {
            PlayerSkins skins = Instance;
            if (skins == null || skins.slotSprites == null || slot < 0 || slot >= skins.slotSprites.Length) return null;
            return skins.slotSprites[slot];
        }

        public static GameObject PrefabFor(int slot)
        {
            PlayerSkins skins = Instance;
            if (skins == null || skins.slotPrefabs == null || slot < 0 || slot >= skins.slotPrefabs.Length) return null;
            return skins.slotPrefabs[slot];
        }

        public static bool ArtFacesLeft
        {
            get { PlayerSkins skins = Instance; return skins == null || skins.artFacesLeft; }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            _loaded = null;
            _triedLoading = false;
        }
    }
}
