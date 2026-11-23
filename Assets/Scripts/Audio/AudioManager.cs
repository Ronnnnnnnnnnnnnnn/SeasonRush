using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace SeasonRush
{
    public class AudioManager : MonoBehaviour
    {
        public static AudioManager Instance { get; private set; }

        [Tooltip("Empty = Assets/Resources/Sounds.asset")]
        public SoundSettings settings;

        AudioSource _timed, _result;
        bool _resultPlayed;
        MusicLoop _music, _battleMusic;
        AudioClip _battleClip;
        float _battleLevel;
        float _timedElapsed, _timedLimit;
        bool _countdownDone;
        int _endSeasonDoneFor = -1;
        int _lastSeason = -1;
        AudioSource[] _voices;
        int _nextVoice;
        readonly Dictionary<AudioClip, float> _onsets = new Dictionary<AudioClip, float>();
        float _musicLevel;
        int _lastSlash = -1;
        MatchManager _subscribedTo;
        float _nextScan;
        readonly HashSet<int> _wired = new HashSet<int>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => Instance = null;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (FindFirstObjectByType<AudioManager>() != null) return;
            new GameObject("AudioManager").AddComponent<AudioManager>();
        }

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            if (settings == null) settings = SoundSettings.Current;

            _music = new MusicLoop(gameObject);
            _battleMusic = new MusicLoop(gameObject);

            _timed = gameObject.AddComponent<AudioSource>();
            _timed.playOnAwake = false;
            _timed.spatialBlend = 0f;

            _result = gameObject.AddComponent<AudioSource>();
            _result.playOnAwake = false;
            _result.spatialBlend = 0f;

            _voices = new AudioSource[6];
            for (int i = 0; i < _voices.Length; i++)
            {
                _voices[i] = gameObject.AddComponent<AudioSource>();
                _voices[i].playOnAwake = false;
                _voices[i].spatialBlend = 0f;
            }
            Prepare(settings);

            if (GameConfig.TestMode && !GameConfig.HasFlag("-sound")) AudioListener.volume = 0f;
            Log($"ready: music={(settings != null && settings.menuMusic != null ? settings.menuMusic.name : "NONE")}, slash clips={(settings != null && settings.slashClips != null ? settings.slashClips.Length : 0)}, hurt={(settings != null && settings.hurtClip != null ? settings.hurtClip.name : "NONE")}");
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
            if (_subscribedTo != null) _subscribedTo.SeasonChanged -= OnSeasonChanged;
        }

        void Prepare(SoundSettings sounds)
        {
            if (sounds == null) return;
            OnsetOf(sounds.seasonChangeClip);
            OnsetOf(sounds.buttonClickClip);
            OnsetOf(sounds.hurtClip);
            OnsetOf(sounds.deathClip);
            OnsetOf(sounds.countdownClip);
            OnsetOf(sounds.endSeasonClip);
            OnsetOf(sounds.winClip);
            OnsetOf(sounds.loseClip);
            OnsetOf(sounds.drawClip);
            if (sounds.slashClips != null) foreach (AudioClip clip in sounds.slashClips) OnsetOf(clip);
        }

        float OnsetOf(AudioClip clip)
        {
            if (clip == null) return 0f;
            if (_onsets.TryGetValue(clip, out float known)) return known;

            float onset = 0f;
            try
            {
                if (clip.loadType != AudioClipLoadType.Streaming)
                {
                    var data = new float[clip.samples * clip.channels];
                    if (clip.GetData(data, 0))
                    {
                        float peak = 0f;
                        for (int i = 0; i < data.Length; i++) peak = Mathf.Max(peak, Mathf.Abs(data[i]));
                        float threshold = Mathf.Max(0.01f, peak * 0.06f);
                        for (int i = 0; i < data.Length; i++)
                        {
                            if (Mathf.Abs(data[i]) < threshold) continue;
                            onset = Mathf.Max(0f, i / clip.channels / (float)clip.frequency - 0.01f);
                            break;
                        }
                        onset = Mathf.Min(onset, Mathf.Max(0f, clip.length - 0.1f));
                    }
                }
            }
            catch (System.Exception) { onset = 0f; }

            _onsets[clip] = onset;
            Log($"{clip.name}: the sound starts {onset:0.000}s into the file, that part is skipped");
            return onset;
        }

        void PlayEffect(AudioClip clip, float pitch)
        {
            if (clip == null || settings == null || _voices == null) return;
            AudioSource voice = _voices[_nextVoice];
            _nextVoice = (_nextVoice + 1) % _voices.Length;
            voice.Stop();
            voice.clip = clip;
            voice.pitch = pitch;
            voice.volume = settings.sfxVolume;
            voice.time = OnsetOf(clip);
            voice.Play();
        }

        void StartTimed(AudioClip clip, float seconds, string what)
        {
            if (clip == null || settings == null) return;
            _timed.Stop();
            _timed.clip = clip;
            _timed.pitch = 1f;
            _timed.volume = settings.sfxVolume;
            _timed.time = OnsetOf(clip);
            _timed.Play();
            _timedElapsed = 0f;
            _timedLimit = seconds;
            Log(what);
        }

        void StopTimed()
        {
            if (!_timed.isPlaying && _timedLimit <= 0f) return;
            _timed.Stop();
            _timedLimit = 0f;
        }

        void UpdateTimedSounds(MatchManager mm)
        {
            bool paused = GameHud.Paused;
            if (paused) { if (_timed.isPlaying) _timed.Pause(); }
            else _timed.UnPause();

            if (_timedLimit > 0f && !paused)
            {
                _timedElapsed += Time.unscaledDeltaTime;
                if (_timedElapsed >= _timedLimit) { StopTimed(); Log("timed sound cut"); }
            }

            if (mm == null)
            {
                _countdownDone = false;
                _endSeasonDoneFor = -1;
                _lastSeason = -1;
                StopTimed();
                return;
            }

            MatchPhase phase = mm.Phase.Value;
            int season = mm.SeasonIndex.Value;

            if (phase == MatchPhase.Countdown)
            {
                if (!_countdownDone)
                {
                    _countdownDone = true;
                    _endSeasonDoneFor = -1;
                    StartTimed(settings.countdownClip, settings.countdownSeconds, "countdown 3-2-1");
                }
            }
            else
            {
                _countdownDone = false;
            }

            if (phase != MatchPhase.Playing)
            {
                if (_timedLimit == 0f || phase == MatchPhase.Finished || phase == MatchPhase.Lobby) { if (phase != MatchPhase.Countdown) StopTimed(); }
                _lastSeason = -1;
                return;
            }

            if (season != _lastSeason)
            {
                _lastSeason = season;
                StopTimed();
            }

            float remaining = mm.TimeRemaining;
            if (_endSeasonDoneFor != season && remaining > 0.05f && remaining <= settings.endSeasonSeconds)
            {
                _endSeasonDoneFor = season;
                StartTimed(settings.endSeasonClip, remaining + 0.3f, "end of season countdown");
            }
        }

        void UpdateResultSound(MatchManager mm)
        {
            bool finished = mm != null && mm.Phase.Value == MatchPhase.Finished;
            if (!finished)
            {
                _resultPlayed = false;
                if (_result.isPlaying) _result.Stop();
                return;
            }
            if (_resultPlayed) return;
            _resultPlayed = true;

            AudioClip clip = ResultClip(mm);
            if (clip == null) return;
            _result.Stop();
            _result.clip = clip;
            _result.volume = settings.resultVolume;
            _result.time = OnsetOf(clip);
            _result.Play();
            Log("result: " + clip.name);
        }

        AudioClip ResultClip(MatchManager mm)
        {
            if (PlayerStats.All.Count < 2) return null;
            int mySlot = PlayerStats.Local != null ? PlayerStats.Local.Slot.Value : -1;
            int surrendered = mm.SurrenderSlot.Value;
            if (surrendered >= 0) return surrendered == mySlot ? settings.loseClip : settings.winClip;
            int winner = mm.WinnerSlot.Value;
            if (winner < 0) return settings.drawClip;
            return winner == mySlot ? settings.winClip : settings.loseClip;
        }

        void UpdateBattleMusic(MatchManager mm)
        {
            AudioClip wanted = null;
            bool playing = mm != null && mm.Phase.Value == MatchPhase.Playing;
            if (playing && settings.seasonMusic != null)
            {
                int season = Mathf.Clamp(mm.SeasonIndex.Value, 0, settings.seasonMusic.Length - 1);
                wanted = settings.seasonMusic[season];
            }

            float fade = settings.fadeSeconds > 0.01f ? Time.unscaledDeltaTime / settings.fadeSeconds : 1f;
            float duck = _timed.isPlaying && _timedLimit > 0f ? settings.duckedMusicLevel : 1f;
            float target = wanted != null && wanted == _battleClip ? settings.seasonMusicVolume * duck : 0f;
            _battleLevel = Mathf.MoveTowards(_battleLevel, target, Mathf.Max(fade * settings.seasonMusicVolume, 0.0001f));
            _battleMusic.SetVolume(_battleLevel);
            _battleMusic.Tick();

            if (wanted != _battleClip && _battleLevel <= 0.001f)
            {
                _battleMusic.Stop();
                _battleClip = wanted;
                if (wanted != null)
                {
                    _battleMusic.Play(wanted, 0f, settings.loopCrossfadeSeconds);
                    Log("battle music: " + wanted.name);
                }
            }
            else if (wanted == null && _battleClip == null && _battleMusic.IsPlaying && _battleLevel <= 0.001f)
            {
                _battleMusic.Stop();
            }
        }

        void OnSeasonChanged(int season) => PlayOneShot(settings != null ? settings.seasonChangeClip : null, "season change");

        void WireButtons()
        {
            if (Time.unscaledTime < _nextScan) return;
            _nextScan = Time.unscaledTime + 1f;
            foreach (Button b in FindObjectsByType<Button>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (_wired.Add(b.GetInstanceID())) b.onClick.AddListener(PlayClick);
            foreach (Toggle t in FindObjectsByType<Toggle>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (_wired.Add(t.GetInstanceID())) t.onValueChanged.AddListener(_ => PlayClick());
        }

        void PlayClick() => PlayOneShot(settings != null ? settings.buttonClickClip : null, "click");

        void PlayOneShot(AudioClip clip, string what)
        {
            if (clip == null || settings == null) return;
            PlayEffect(clip, 1f);
            Log(what);
        }

        static bool BattleRunning
        {
            get
            {
                MatchManager mm = MatchManager.Instance;
                return mm != null && (mm.Phase.Value == MatchPhase.Countdown || mm.Phase.Value == MatchPhase.Playing);
            }
        }

        void Update()
        {
            WireButtons();
            MatchManager current = MatchManager.Instance;
            if (current != _subscribedTo)
            {
                if (_subscribedTo != null) _subscribedTo.SeasonChanged -= OnSeasonChanged;
                _subscribedTo = current;
                if (current != null) current.SeasonChanged += OnSeasonChanged;
            }

            if (settings == null) return;
            UpdateTimedSounds(current);
            UpdateResultSound(current);
            UpdateBattleMusic(current);
            if (settings.menuMusic == null) return;

            bool battle = BattleRunning;
            float target = battle || _result.isPlaying ? 0f : settings.musicVolume;
            float step = settings.fadeSeconds > 0.01f ? settings.musicVolume * Time.unscaledDeltaTime / settings.fadeSeconds : 1f;
            _musicLevel = Mathf.MoveTowards(_musicLevel, target, Mathf.Max(step, 0.0001f));
            _music.SetVolume(_musicLevel);
            _music.Tick();

            if (!battle && !_music.IsPlaying)
            {
                _music.Play(settings.menuMusic, settings.menuLoopSeconds, settings.loopCrossfadeSeconds);
                Log("music starts");
            }
            else if (battle && _music.IsPlaying && _musicLevel <= 0.001f)
            {
                _music.Stop();
                Log("music stops (battle)");
            }
        }

        public static void PlaySlash()
        {
            AudioManager m = Instance;
            if (m == null || m.settings == null || m.settings.slashClips == null || m.settings.slashClips.Length == 0) return;
            AudioClip[] clips = m.settings.slashClips;
            int pick = clips.Length > 1 ? Random.Range(0, clips.Length) : 0;
            if (clips.Length > 1 && pick == m._lastSlash) pick = (pick + 1) % clips.Length;
            m._lastSlash = pick;
            if (clips[pick] == null) return;
            m.PlayEffect(clips[pick], 1f + Random.Range(-m.settings.pitchVariation, m.settings.pitchVariation));
            Log("slash");
        }

        public static void PlayDeath()
        {
            AudioManager m = Instance;
            if (m == null || m.settings == null || m.settings.deathClip == null) return;
            m.PlayEffect(m.settings.deathClip, 1f);
            Log("death");
        }

        public static void PlayHurt()
        {
            AudioManager m = Instance;
            if (m == null || m.settings == null || m.settings.hurtClip == null) return;
            m.PlayEffect(m.settings.hurtClip, 1f);
            Log("hurt");
        }

        static void Log(string what)
        {
            if (GameConfig.TestMode) Debug.Log("[SOUND] " + what);
        }
    }
}
