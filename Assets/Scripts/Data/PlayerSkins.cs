using UnityEngine;

namespace SeasonRush
{
    /// <summary>
    /// The pictures of the two warriors: slot 0 is player 1 (blue, the host), slot 1 is player 2 (red).
    /// The asset lives at Assets/Resources/PlayerSkins.asset and is filled in by the editor tool from
    /// Assets/2D Asset/Players/player1.png and player2.png (see docs/Character_Sprites.md).
    /// A slot without a picture keeps the old placeholder warrior, so the game never breaks without art.
    /// </summary>
    [CreateAssetMenu(menuName = "Season Rush/Player Skins", fileName = "PlayerSkins")]
    public class PlayerSkins : ScriptableObject
    {
        [Tooltip("Element 0 = player 1 (blue, host), element 1 = player 2 (red). The picture should have its pivot at the feet.")]
        public Sprite[] slotSprites = new Sprite[2];

        [Tooltip("Tick when the pictures show the character looking to the LEFT (the sword is on the left). The game then mirrors the " +
                 "picture while the player walks to the right. Untick it if you draw the character looking to the right.")]
        public bool artFacesLeft = true;

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

        /// <summary>The picture for a player slot, or null when none is set (then the placeholder is used).</summary>
        public static Sprite For(int slot)
        {
            PlayerSkins skins = Instance;
            if (skins == null || skins.slotSprites == null || slot < 0 || slot >= skins.slotSprites.Length) return null;
            return skins.slotSprites[slot];
        }

        /// <summary>Which way the pictures look when they are not mirrored (see <see cref="artFacesLeft"/>).</summary>
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
