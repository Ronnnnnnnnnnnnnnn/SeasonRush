using UnityEngine;

namespace SeasonRush
{
    public class MusicLoop
    {
        readonly AudioSource[] _sources = new AudioSource[2];
        int _current;
        bool _fading;
        float _loopLength;
        float _crossfade;
        float _volume;

        public MusicLoop(GameObject host)
        {
            for (int i = 0; i < 2; i++)
            {
                var source = host.AddComponent<AudioSource>();
                source.loop = false;
                source.playOnAwake = false;
                source.spatialBlend = 0f;
                source.volume = 0f;
                _sources[i] = source;
            }
        }

        public bool IsPlaying => _sources[0].isPlaying || _sources[1].isPlaying;
        public AudioClip Clip => _sources[_current].clip;

        public void Play(AudioClip clip, float loopSeconds, float crossfadeSeconds)
        {
            Stop();
            if (clip == null) return;
            _loopLength = loopSeconds > 0.5f ? Mathf.Min(loopSeconds, clip.length) : clip.length;
            _crossfade = Mathf.Clamp(crossfadeSeconds, 0.05f, _loopLength * 0.4f);
            _current = 0;
            _sources[0].clip = clip;
            _sources[0].time = 0f;
            _sources[0].volume = _volume;
            _sources[0].Play();
        }

        public void Stop()
        {
            foreach (AudioSource source in _sources) { source.Stop(); source.volume = 0f; }
            _fading = false;
        }

        public void SetVolume(float volume)
        {
            _volume = volume;
            Apply(_fading ? FadeAmount() : 0f);
        }

        public void Tick()
        {
            AudioSource a = _sources[_current];
            if (!a.isPlaying && !_fading) return;

            float start = _loopLength - _crossfade;
            if (!_fading && a.time >= start)
            {
                AudioSource b = _sources[1 - _current];
                b.clip = a.clip;
                b.time = 0f;
                b.volume = 0f;
                b.Play();
                _fading = true;
            }

            if (!_fading) return;

            float k = FadeAmount();
            if (k >= 1f || !a.isPlaying)
            {
                a.Stop();
                a.volume = 0f;
                _current = 1 - _current;
                _fading = false;
                Apply(0f);
                return;
            }
            Apply(k);
        }

        float FadeAmount()
        {
            AudioSource a = _sources[_current];
            return Mathf.Clamp01((a.time - (_loopLength - _crossfade)) / _crossfade);
        }

        void Apply(float k)
        {
            AudioSource a = _sources[_current];
            AudioSource b = _sources[1 - _current];
            if (_fading)
            {
                a.volume = _volume * Mathf.Cos(k * Mathf.PI * 0.5f);
                b.volume = _volume * Mathf.Sin(k * Mathf.PI * 0.5f);
            }
            else
            {
                a.volume = _volume;
            }
        }
    }
}
