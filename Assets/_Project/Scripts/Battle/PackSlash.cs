using System;
using Onikiri.Core;
using UnityEngine;

namespace Onikiri.Battle
{
    /**
     * @brief 팩 참격 애니 한 번. `Assets/ThirdParty/VFX/Slashes/`의 시트를 그대로 재생한다.
     *
     * ## 왜 절차적 메시를 버렸는가
     *
     * 28단계에는 정점을 직접 찍는 메시 트레일(`SlashTrail`)을 썼다. 픽셀이 안
     * 뭉개진다는 점은 사실이었지만 **전제가 틀렸다** - "화면을 가로지르는 초대형
     * 참격"을 맞추려고 만든 것이었고, 그 크기 자체가 문제였다. 스프라이트를
     * 8배로 늘리든 같은 크기의 메시를 그리든 화면에는 똑같이 **덩어리**가 뜬다.
     * 뭉개짐을 고쳐도 덩어리는 그대로였다.
     *
     * 그래서 크기를 줄이고 팩으로 돌아온다. 팩 시트는 128x128 열 장짜리
     * **실제 베는 모션**이다 - 정적인 한 장을 키운 것이 아니라 칼이 지나가는
     * 열 프레임이라, 크기가 작아도 "벴다"로 읽힌다. 그리고 이 게임과 같은
     * 픽셀 아트(32 PPU)라 톤이 맞는다.
     *
     * ## 크기 규칙
     *
     * 스케일 1.0이 곧 **원본 픽셀 크기**다 - 팩이 32 PPU라 게임 아트와 같은
     * 격자에 얹힌다. 그래서 1.0에서는 아트 픽셀 하나가 화면 4px이고, 2.0이면
     * 8px, 2.5면 10px이다. 그 위로 올라가면 다시 27단계의 네모가 된다.
     * **정수 배율만 쓴다** - 1.5 같은 값은 픽셀마다 크기가 들쭉날쭉해져
     * 점 필터링에서 격자가 흔들린다.
     *
     * ## 반전
     *
     * 23단계 규칙대로 부르는 쪽이 정한다. 좌우 반전은 세로축 거울이므로
     * **회전각의 부호도 함께 뒤집어야** 한다 - 뒤집지 않으면 왼쪽을 벨 때
     * 참격이 위아래가 바뀐 채 나간다.
     *
     * ## 스케일 타임
     *
     * 히트스톱이 참격도 함께 얼린다(23단계 규칙). 정지 중에 참격만 계속
     * 흐르면 "멈췄는데 이펙트는 지나간다"가 된다.
     */
    [RequireComponent(typeof(SpriteRenderer))]
    public sealed class PackSlash : MonoBehaviour
    {
        [SerializeField] private SpriteRenderer spriteRenderer;

        private Sprite[] frames;
        private float frameRate;
        private Action<PackSlash> finished;
        private float elapsed;
        private bool playing;

        private void Awake()
        {
            if (spriteRenderer == null) spriteRenderer = GetComponent<SpriteRenderer>();
            spriteRenderer.sortingOrder = SortingOrders.Vfx;
            spriteRenderer.enabled = false;
        }

        public bool IsPlaying { get { return playing; } }

        /** 지금 그려진 참격의 월드 경계. 진단용 - 화면을 얼마나 덮는지 잰다 */
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
         * @param clip      팩 시트에서 잘라낸 프레임들 (보통 열 장)
         * @param angle     회전각 (도). 팩 원본 방향을 화면 방향으로 돌린다
         * @param scale     원본 픽셀 대비 배수. 정수만 쓴다. 위 주석 참고
         * @param flip      왼쪽을 벨 때 true
         */
        public void Play(Sprite[] clip, float fps, Vector3 position, float angle,
                         float scale, bool flip, Action<PackSlash> onFinished)
        {
            if (clip == null || clip.Length == 0)
            {
                if (onFinished != null) onFinished(this);
                return;
            }

            frames = clip;
            frameRate = fps > 0f ? fps : 24f;
            finished = onFinished;

            transform.position = position;
            // 반전은 각도의 부호도 함께 뒤집는다. 위 주석 참고
            transform.rotation = Quaternion.Euler(0f, 0f, flip ? -angle : angle);
            transform.localScale = Vector3.one * scale;

            spriteRenderer.sprite = frames[0];
            spriteRenderer.flipX = flip;
            spriteRenderer.color = Color.white;
            spriteRenderer.sortingOrder = SortingOrders.Vfx;
            spriteRenderer.enabled = true;

            elapsed = 0f;
            playing = true;
        }

        private void Update()
        {
            if (!playing) return;

            elapsed += Time.deltaTime;

            int index = Mathf.FloorToInt(elapsed * frameRate);
            if (index < frames.Length)
            {
                spriteRenderer.sprite = frames[index];
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
