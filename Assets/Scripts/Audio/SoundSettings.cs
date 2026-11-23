using UnityEngine;

namespace SeasonRush
{
    [CreateAssetMenu(menuName = "Season Rush/Sound Settings", fileName = "Sounds")]
    public class SoundSettings : ScriptableObject
    {
        [Header("Music (plays in the menu and the lobby, stops when the battle starts)")]
        public AudioClip menuMusic;
        [Range(0f, 1f)] public float musicVolume = 0.5f;
        [Tooltip("Seconds the music takes to fade out when the battle starts / fade in when it is over.")]
        public float fadeSeconds = 0.8f;
        [Tooltip("Only this many seconds from the start of the menu music are played, then it starts again. 0 = the whole file.")]
        public float menuLoopSeconds = 16f;
        [Tooltip("Seconds the end of a music loop blends into its start so the loop has no gap or click.")]
        public float loopCrossfadeSeconds = 1.2f;

        [Header("Battle music (one track per season: Spring, Fall, Winter; fades when the season changes)")]
        public AudioClip[] seasonMusic = new AudioClip[3];
        [Range(0f, 1f)] public float seasonMusicVolume = 0.45f;

        [Header("Countdown sounds")]
        [Tooltip("Starts with the 3-2-1 countdown. Only its first seconds are played (see below), the rest of the file is not used.")]
        public AudioClip countdownClip;
        [Tooltip("How many seconds of the countdown file are played.")]
        public float countdownSeconds = 3f;
        [Tooltip("Starts when 10 seconds of a season are left and stops when the season ends.")]
        public AudioClip endSeasonClip;
        public float endSeasonSeconds = 10f;
        [Range(0f, 1f)] [Tooltip("How quiet the battle music gets while the countdown sounds play.")]
        public float duckedMusicLevel = 0.35f;

        [Header("Interface and match sounds")]
        [Tooltip("Plays for both players each time a new season starts.")]
        public AudioClip seasonChangeClip;
        [Tooltip("Plays whenever a button of the menu / HUD is pressed.")]
        public AudioClip buttonClickClip;

        [Header("Your warrior (only you hear these, not your rival's)")]
        [Tooltip("One of these is picked each time you swing.")]
        public AudioClip[] slashClips;
        public AudioClip hurtClip;
        public AudioClip deathClip;

        [Header("Match result (played once for you when the match is over)")]
        public AudioClip winClip;
        public AudioClip loseClip;
        public AudioClip drawClip;
        [Range(0f, 1f)] public float resultVolume = 1f;
        [Range(0f, 1f)] public float sfxVolume = 1f;
        [Tooltip("The swing sound changes pitch a little each time so it does not get boring.")]
        [Range(0f, 0.3f)] public float pitchVariation = 0.06f;

        static SoundSettings _loaded;
        static bool _tried;

        public static SoundSettings Current
        {
            get
            {
                if (!_tried) { _tried = true; _loaded = Resources.Load<SoundSettings>("Sounds"); }
                return _loaded;
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            _loaded = null;
            _tried = false;
        }
    }
}
