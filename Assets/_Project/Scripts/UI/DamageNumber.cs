using System;
using TMPro;
using UnityEngine;

namespace Onikiri.UI
{
    /// <summary>
    /// One pooled damage popup: appears at the hit, arcs upward and fades.
    ///
    /// Lives on the overlay canvas rather than in world space so the glyphs stay at an
    /// exact multiple of the font's design size. A world-space label would scale with the
    /// camera and land the pixel grid off-screen-pixel, undoing the raster atlas.
    ///
    /// Driven by scaled time so a hitstop freezes the popup along with everything else.
    /// </summary>
    public sealed class DamageNumber : MonoBehaviour
    {
        [SerializeField] private TMP_Text label;
        [Tooltip("Dark copy drawn one step behind the label. A bitmap font cannot use TMP's " +
                 "SDF outline, and without this the number vanishes against the white slash.")]
        [SerializeField] private TMP_Text shadow;
        [SerializeField] private RectTransform rect;

        [Header("Motion")]
        [SerializeField] private float lifetime = 0.75f;
        [Tooltip("Initial upward speed, in canvas units per second.")]
        [SerializeField] private float riseSpeed = 320f;
        [Tooltip("Downward pull, so the popup arcs instead of sliding.")]
        [SerializeField] private float gravity = 520f;
        [Tooltip("Random horizontal spread so stacked hits do not overlap exactly.")]
        [SerializeField] private float horizontalSpread = 60f;
        [Tooltip("Fraction of life spent at full opacity before fading.")]
        [Range(0f, 1f)]
        [SerializeField] private float holdFraction = 0.35f;

        private Action<DamageNumber> finished;
        private Vector2 velocity;
        private Vector2 position;
        private float elapsed;
        private Color baseColor;

        private void Awake()
        {
            if (rect == null) rect = (RectTransform)transform;
            if (label == null) label = GetComponent<TMP_Text>();
        }

        public void Play(string text, Vector2 anchoredPosition, Color color, Action<DamageNumber> onFinished)
        {
            finished = onFinished;

            label.text = text;
            baseColor = color;
            label.color = color;

            if (shadow != null)
            {
                shadow.text = text;
                shadow.color = new Color(0f, 0f, 0f, 0.75f);
            }

            position = anchoredPosition;
            rect.anchoredPosition = position;

            velocity = new Vector2(UnityEngine.Random.Range(-horizontalSpread, horizontalSpread), riseSpeed);
            elapsed = 0f;

            gameObject.SetActive(true);
        }

        private void Update()
        {
            elapsed += Time.deltaTime;

            if (elapsed >= lifetime)
            {
                var callback = finished;
                finished = null;
                if (callback != null) callback(this);
                return;
            }

            velocity.y -= gravity * Time.deltaTime;
            position += velocity * Time.deltaTime;
            rect.anchoredPosition = position;

            float t = elapsed / lifetime;
            float fade = t <= holdFraction ? 1f : 1f - Mathf.InverseLerp(holdFraction, 1f, t);

            var color = baseColor;
            color.a = fade;
            label.color = color;

            if (shadow != null) shadow.color = new Color(0f, 0f, 0f, 0.75f * fade);
        }
    }
}
