using System;
using UnityEngine;

namespace Onikiri.Battle
{
    /**
     * @brief 풀링된 꽃잎 하나. 참격 방향으로 튀어나가 회전하며 사라진다.
     *
     * ParticleSystem을 쓰지 않는다. 여기서 필요한 것은 타격마다 6~10개, 1~2px짜리
     * 조각이고 그 규모에서 파티클 시스템은 얻는 것보다 잃는 것이 많다. 픽셀 격자에
     * 스냅되지 않아 1px 조각이 프레임마다 다른 모양으로 리샘플되고, 히트스톱이
     * Time.timeScale로 걸릴 때 시뮬레이션과 나머지 연출의 정지 시점이 어긋난다.
     *
     * SpriteRenderer 하나에 Update 하나면 픽셀 퍼펙트 카메라의 스냅을 그대로 받고,
     * 스케일 타임으로 돌아 히트스톱이 꽃잎까지 함께 얼린다. 참격·숫자·정지가 한
     * 순간으로 읽히는 것의 절반은 이 동시성이다.
     */
    [RequireComponent(typeof(SpriteRenderer))]
    public sealed class SakuraPetal : MonoBehaviour
    {
        [SerializeField] private SpriteRenderer spriteRenderer;

        [Tooltip("아래로 당기는 힘. 꽃잎이 직선으로 날아가지 않고 흩날리게 한다")]
        [SerializeField] private float gravity = 4.5f;

        [Tooltip("공기 저항. 튀어나간 속도가 죽으면서 체공이 길어진다")]
        [SerializeField] private float drag = 2.2f;

        [Tooltip("수명 중 완전 불투명으로 버티는 비율. 뒤쪽만 페이드한다")]
        [Range(0f, 1f)]
        [SerializeField] private float holdFraction = 0.45f;

        private Action<SakuraPetal> finished;
        private Vector2 velocity;
        private float spin;
        private float lifetime;
        private float elapsed;
        private Color baseColor;

        private void Awake()
        {
            if (spriteRenderer == null) spriteRenderer = GetComponent<SpriteRenderer>();
            spriteRenderer.sortingOrder = Onikiri.Core.SortingOrders.Vfx;
        }

        public void Play(Sprite sprite, Vector3 position, Vector2 initialVelocity,
                         float spinDegreesPerSecond, float life, Color color,
                         Action<SakuraPetal> onFinished)
        {
            finished = onFinished;

            spriteRenderer.sprite = sprite;
            spriteRenderer.enabled = true;
            spriteRenderer.sortingOrder = Onikiri.Core.SortingOrders.Vfx;

            transform.position = position;
            transform.localRotation = Quaternion.Euler(0f, 0f, UnityEngine.Random.Range(0f, 360f));

            velocity = initialVelocity;
            spin = spinDegreesPerSecond;
            lifetime = Mathf.Max(0.01f, life);
            elapsed = 0f;

            baseColor = color;
            spriteRenderer.color = color;
        }

        private void Update()
        {
            elapsed += Time.deltaTime;

            if (elapsed >= lifetime)
            {
                spriteRenderer.enabled = false;
                var callback = finished;
                finished = null;
                if (callback != null) callback(this);
                return;
            }

            velocity.y -= gravity * Time.deltaTime;
            velocity -= velocity * (drag * Time.deltaTime);

            transform.position += new Vector3(velocity.x, velocity.y, 0f) * Time.deltaTime;
            transform.Rotate(0f, 0f, spin * Time.deltaTime);

            float t = elapsed / lifetime;
            var color = baseColor;
            color.a = t <= holdFraction ? baseColor.a : baseColor.a * (1f - Mathf.InverseLerp(holdFraction, 1f, t));
            spriteRenderer.color = color;
        }
    }
}
