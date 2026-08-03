using UnityEngine;

namespace Onikiri.Battle
{
    /// <summary>
    /// Plays a random impact sound per hit.
    ///
    /// Uses a small ring of AudioSources rather than one, so rapid hits overlap instead of
    /// cutting each other off - at late-game attack speeds a single source would chop every
    /// sound to nothing. Pitch is randomised slightly because the same sample replayed at
    /// identical pitch several times a second turns into a machine-gun rattle.
    ///
    /// AudioSource playback ignores Time.timeScale, so hits still sound during a hitstop,
    /// which is what sells the freeze.
    ///
    /// With no clips assigned this is a silent no-op: the combat code can call it
    /// unconditionally while the sound files are still being sourced.
    /// </summary>
    public sealed class HitAudio : MonoBehaviour
    {
        [Tooltip("Impact sounds. One is picked at random per hit. Empty is silent.")]
        [SerializeField] private AudioClip[] clips;

        [Tooltip("Concurrent voices. Needs to cover the fastest expected attack rate.")]
        [SerializeField] private int voices = 4;

        [Range(0f, 1f)]
        [SerializeField] private float volume = 0.7f;

        [Tooltip("Random pitch range, to stop repeated hits sounding mechanical.")]
        [SerializeField] private Vector2 pitchRange = new Vector2(0.92f, 1.08f);

        private AudioSource[] sources;
        private int nextVoice;

        public bool HasClips { get { return clips != null && clips.Length > 0; } }
        public int ClipCount { get { return clips != null ? clips.Length : 0; } }

        private void Awake()
        {
            voices = Mathf.Max(1, voices);
            sources = new AudioSource[voices];

            for (int i = 0; i < voices; i++)
            {
                var source = gameObject.AddComponent<AudioSource>();
                source.playOnAwake = false;
                source.loop = false;
                source.spatialBlend = 0f;   // 2D: a side-view idle game has no listener depth
                sources[i] = source;
            }
        }

        public void PlayHit()
        {
            if (!HasClips || sources == null) return;

            var source = sources[nextVoice];
            nextVoice = (nextVoice + 1) % sources.Length;

            source.clip = clips[Random.Range(0, clips.Length)];
            source.pitch = Random.Range(pitchRange.x, pitchRange.y);
            source.volume = volume;
            source.Play();
        }
    }
}
