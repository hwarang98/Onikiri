using System;
using Onikiri.Core;
using UnityEngine;

namespace Onikiri.Battle
{
    /**
     * @brief 거합 돌진이 지나간 자리에 남는 **얇은 수평 섬광** 한 줄.
     *
     * ## 왜 팩 참격을 쓰지 않는가
     *
     * 29단계에는 일섬도 팩 시트(Slash2)를 썼다. 그런데 그 시트는 **굽은 사선 +
     * 끝의 큰 흰 폭발**이라, 직선 돌진과 두 가지가 어긋났다:
     *
     *   1. 곡선이다. 돌진은 직선인데 이펙트가 휘어 있으면 경로가 두 개로 읽힌다
     *   2. 큰 흰 스파이크 폭발이 맞은 자리를 덮어, 무엇이 몇 대 맞았는지 안 보인다
     *
     * 팩에는 얇은 수평 섬광이 없다. 그래서 이것만 구워 쓴다 - 귀참은 팩 그대로다.
     *
     * ## 캐릭터에 붙어서 자란다
     *
     * 고정된 자리에 통째로 뜨지 않는다. 꼬리는 출발점에 두고 **머리가 사무라이를
     * 따라간다.** 돌진 곡선과 같은 이징을 쓰므로 머리가 칼끝에서 떨어지지 않고,
     * 그래서 "저쪽에 이펙트가 떴다"가 아니라 "지나간 자리가 남았다"로 읽힌다.
     *
     * 29단계의 일섬이 어색했던 가장 큰 이유가 이것이었다 - 사무라이는 왼쪽에
     * 있는데 이펙트만 오른쪽 멀리에 떠 있었다.
     *
     * ## 스케일 타임
     *
     * 히트스톱이 섬광도 함께 얼린다(23단계 규칙).
     */
    [RequireComponent(typeof(SpriteRenderer))]
    public sealed class DashStreak : MonoBehaviour
    {
        [SerializeField] private SpriteRenderer spriteRenderer;

        private float originX;
        private float originY;
        private float sign;
        private float length;
        private float reveal;
        private float hold;
        private float fade;
        private Color tint;
        private Action<DashStreak> finished;
        private float elapsed;
        private bool playing;

        /** 구운 스프라이트의 월드 크기 (32 PPU 기준). 배율 계산의 기준이다 */
        private Vector2 spriteSize;

        private void Awake()
        {
            if (spriteRenderer == null) spriteRenderer = GetComponent<SpriteRenderer>();
            spriteRenderer.sortingOrder = SortingOrders.Vfx;
            spriteRenderer.enabled = false;
        }

        public bool IsPlaying { get { return playing; } }

        public Bounds WorldBounds
        {
            get
            {
                return spriteRenderer != null && spriteRenderer.enabled
                    ? spriteRenderer.bounds
                    : new Bounds(transform.position, Vector3.zero);
            }
        }

        /**
         * @param origin      꼬리가 놓일 자리 (돌진 출발점)
         * @param facingSign  +1이면 오른쪽으로, -1이면 왼쪽으로
         * @param fullLength  다 자랐을 때의 길이 (돌진 거리와 같아야 한다)
         * @param thickness   두께. **얇아야 한다** - 굵으면 다시 덩어리다
         */
        public void Play(Vector3 origin, float facingSign, float fullLength, float thickness,
                         Color color, float revealSeconds, float holdSeconds, float fadeSeconds,
                         Action<DashStreak> onFinished)
        {
            if (spriteRenderer.sprite == null)
            {
                if (onFinished != null) onFinished(this);
                return;
            }

            spriteSize = spriteRenderer.sprite.bounds.size;

            originX = origin.x;
            originY = origin.y;
            sign = facingSign >= 0f ? 1f : -1f;
            length = Mathf.Max(0.01f, fullLength);
            reveal = Mathf.Max(0.0001f, revealSeconds);
            hold = Mathf.Max(0f, holdSeconds);
            fade = Mathf.Max(0.0001f, fadeSeconds);
            tint = color;
            finished = onFinished;

            transform.rotation = Quaternion.identity;
            transform.localScale = new Vector3(0.0001f, thickness / spriteSize.y, 1f);

            spriteRenderer.color = color;
            spriteRenderer.enabled = true;

            elapsed = 0f;
            playing = true;
            Apply();
        }

        private void Update()
        {
            if (!playing) return;

            elapsed += Time.deltaTime;

            if (elapsed >= reveal + hold + fade)
            {
                playing = false;
                spriteRenderer.enabled = false;

                var callback = finished;
                finished = null;
                if (callback != null) callback(this);
                return;
            }

            Apply();
        }

        private void Apply()
        {
            // 돌진 곡선과 **같은 이징**이다 (SkillPerformer.LungeAt의 나가는 구간).
            // 여기만 등속으로 두면 섬광의 머리가 사무라이보다 뒤처져, 칼끝에서
            // 떨어진 선이 따라오는 그림이 된다
            float t = Mathf.Clamp01(elapsed / reveal);
            float grown = length * (1f - (1f - t) * (1f - t));
            if (grown < 0.01f) grown = 0.01f;

            transform.localScale = new Vector3(grown / spriteSize.x, transform.localScale.y, 1f);
            transform.position = new Vector3(originX + sign * grown * 0.5f, originY, 0f);

            float alpha = 1f;
            if (elapsed > reveal + hold)
                alpha = 1f - Mathf.Clamp01((elapsed - reveal - hold) / fade);

            var color = tint;
            color.a = tint.a * alpha;
            spriteRenderer.color = color;

            // 왼쪽으로 벨 때는 좌우를 뒤집는다. 꼬리가 옅고 머리가 진하므로
            // 뒤집지 않으면 옅은 쪽이 앞에 온다
            spriteRenderer.flipX = sign < 0f;
        }
    }
}
