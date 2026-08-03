using UnityEngine;

namespace Onikiri.Battle
{
    /// <summary>
    /// Impact and kill sounds.
    ///
    /// The hard part is not playing a sound, it is playing thousands of them without the
    /// game turning into noise. An idle game ends up at ten-plus attacks per second, and
    /// the naive version - one AudioSource, one clip, every hit - becomes a buzzing drone
    /// within a minute. Four guards, all of them needed:
    ///
    ///   clip variation    several takes, chosen at random
    ///   pitch variation   a few percent per play, so repeats never phase-lock
    ///   voice cap         a fixed ring of sources; the fifth overlapping hit steals the
    ///                     oldest voice instead of layering into a wall of sound
    ///   minimum interval  hits closer together than this are dropped, because past about
    ///                     25/sec they stop being individually audible anyway
    ///
    /// Kills get their own bank, pitched lower and louder, so a death always reads
    /// differently from the stream of ordinary hits.
    ///
    /// AudioSource playback ignores Time.timeScale, so sounds continue through a hitstop -
    /// which is what makes the freeze read as impact rather than a dropped frame.
    ///
    /// With no clips assigned every method is a silent no-op, so combat can call these
    /// unconditionally while the sound files are still being sourced.
    /// </summary>
    public sealed class HitAudio : MonoBehaviour
    {
        [Header("Clips")]
        [Tooltip("Impact sounds. One is picked at random per hit.")]
        [SerializeField] private AudioClip[] hitClips;

        [Tooltip("Kill sounds - lower and heavier than a hit.")]
        [SerializeField] private AudioClip[] killClips;

        [Header("Voices")]
        [Tooltip("Concurrent voices. Beyond this the oldest is reused.")]
        [SerializeField] private int voices = 4;

        [Header("Fatigue guards")]
        [Tooltip("Minimum gap between hit sounds. Hits closer than this are dropped.")]
        [SerializeField] private float minHitInterval = 0.04f;

        [Tooltip("Minimum gap between kill sounds.")]
        [SerializeField] private float minKillInterval = 0.02f;

        [Tooltip("Random pitch range for hits.")]
        [SerializeField] private Vector2 hitPitchRange = new Vector2(0.94f, 1.06f);

        [Tooltip("Kill pitch sits below 1 so a death lands heavier.")]
        [SerializeField] private Vector2 killPitchRange = new Vector2(0.78f, 0.88f);

        [Range(0f, 1f)] [SerializeField] private float hitVolume = 0.6f;
        [Range(0f, 1f)] [SerializeField] private float killVolume = 0.85f;

        private AudioSource[] sources;
        private int nextVoice;
        private float lastHitTime;
        private float lastKillTime;

        public bool HasHitClips { get { return hitClips != null && hitClips.Length > 0; } }
        public bool HasKillClips { get { return killClips != null && killClips.Length > 0; } }
        public int HitClipCount { get { return hitClips != null ? hitClips.Length : 0; } }
        public int KillClipCount { get { return killClips != null ? killClips.Length : 0; } }

        /// <summary>Plays dropped by the interval guard. Handy when tuning the throttle.</summary>
        public int ThrottledCount { get; private set; }

        private void Awake()
        {
            voices = Mathf.Max(1, voices);
            sources = new AudioSource[voices];

            // Built once, never per hit: AddComponent on every impact would allocate
            // exactly where the game is busiest.
            for (int i = 0; i < voices; i++)
            {
                var source = gameObject.AddComponent<AudioSource>();
                source.playOnAwake = false;
                source.loop = false;
                source.spatialBlend = 0f;   // 2D: a side-view idle game has no listener depth
                sources[i] = source;
            }

            // Negative so the first hit of a session is never throttled.
            lastHitTime = -999f;
            lastKillTime = -999f;
        }

        public void PlayHit()
        {
            if (!HasHitClips) return;

            // Unscaled time: the throttle has to keep working while a hitstop pins
            // timeScale at zero, or every hit landing during a freeze would pass at once.
            if (Time.unscaledTime - lastHitTime < minHitInterval)
            {
                ThrottledCount++;
                return;
            }
            lastHitTime = Time.unscaledTime;

            Play(hitClips, hitPitchRange, hitVolume);
        }

        public void PlayKill()
        {
            if (Time.unscaledTime - lastKillTime < minKillInterval)
            {
                ThrottledCount++;
                return;
            }

            // Until a dedicated kill bank exists, reuse a hit pitched well down so a death
            // still reads as heavier than a normal blow.
            var bank = HasKillClips ? killClips : hitClips;
            if (bank == null || bank.Length == 0) return;

            lastKillTime = Time.unscaledTime;
            Play(bank, killPitchRange, killVolume);
        }

        private void Play(AudioClip[] bank, Vector2 pitchRange, float volume)
        {
            if (sources == null) return;

            var source = sources[nextVoice];
            nextVoice = (nextVoice + 1) % sources.Length;

            source.clip = bank[Random.Range(0, bank.Length)];
            source.pitch = Random.Range(pitchRange.x, pitchRange.y);
            source.volume = volume;
            source.Play();
        }
    }
}
