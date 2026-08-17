using System;
using Onikiri.Core;
using UnityEngine;

namespace Onikiri.Battle
{
    /**
     * @brief 돌진이 지나간 자리에 남는 사무라이 잔상 한 장.
     *
     * ## 왜 필요한가
     *
     * 일섬은 0.09초 만에 1.25u를 튀어나간다. 그 속도에서 캐릭터는 **두 위치
     * 사이를 순간이동한 것처럼** 보이고, 화면에서는 "돌진"이 아니라 "끊겼다"로
     * 읽힌다. 사이를 메우는 것이 잔상이다.
     *
     * 새 아트가 필요 없다. 지금 재생 중인 사무라이 스프라이트를 그대로 복사해
     * 반투명으로 뒤에 남긴다 - 팩에 없는 것을 만들지 않는다는 규칙 그대로다.
     *
     * ## 사무라이보다 뒤에 그린다
     *
     * 참격(Vfx=100)보다 아래, 플레이어(50)보다 아래다. 잔상이 본체 앞에 오면
     * 지금 어느 것이 진짜인지 알 수 없고, 그 순간 연출이 아니라 버그로 보인다.
     */
    [RequireComponent(typeof(SpriteRenderer))]
    public sealed class Afterimage : MonoBehaviour
    {
        [SerializeField] private SpriteRenderer spriteRenderer;

        [Tooltip("남아 있는 시간. 돌진 자체가 0.09초라 그보다 조금 길어야 " +
                 "지나간 자리가 보인다")]
        [SerializeField] private float lifetime = 0.16f;

        private Action<Afterimage> finished;
        private float elapsed;
        private Color tint;
        private bool playing;

        private void Awake()
        {
            if (spriteRenderer == null) spriteRenderer = GetComponent<SpriteRenderer>();

            // 플레이어보다 한 칸 뒤. 위 주석 참고
            spriteRenderer.sortingOrder = SortingOrders.Player - 1;
            spriteRenderer.enabled = false;
        }

        /**
         * @param sortingOrder 그릴 층. **부르는 쪽이 정한다.**
         *
         * 45b까지는 여기서 `Player - 1`로 못 박고 있었다. 잔상을 남기는 것이
         * 사무라이 하나뿐이었기 때문인데, 영체(Spirit = Player - 2)가 같은
         * 잔상을 쓰면서 그 상수가 틀린 답이 됐다 - 49는 영체(48)보다 **앞**이라
         * 잔상이 본체를 덮는다. 층은 남기는 쪽이 자기 층에서 계산해야 한다.
         */
        public void Play(Sprite sprite, Vector3 position, bool flip, Color color,
                         int sortingOrder, Action<Afterimage> onFinished)
        {
            finished = onFinished;
            tint = color;

            transform.position = position;
            spriteRenderer.sprite = sprite;
            spriteRenderer.flipX = flip;
            spriteRenderer.color = color;
            spriteRenderer.sortingOrder = sortingOrder;
            spriteRenderer.enabled = true;

            elapsed = 0f;
            playing = true;
        }

        /** 스케일 타임. 히트스톱이 잔상도 함께 얼린다(23단계 규칙) */
        private void Update()
        {
            if (!playing) return;

            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / Mathf.Max(0.0001f, lifetime));

            var color = tint;
            color.a = tint.a * (1f - t);
            spriteRenderer.color = color;

            if (t < 1f)
            {
                return;
            }

            playing = false;
            spriteRenderer.enabled = false;

            var callback = finished;
            finished = null;
            if (callback != null) callback(this);
        }
    }
}
