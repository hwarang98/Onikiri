using System;
using Onikiri.Core;
using UnityEngine;

namespace Onikiri.Battle
{
    /**
     * @brief 칼이 몸에 닿은 자리에서 터지는 작은 불꽃.
     *
     * 22단계까지 이 자리에는 큰 참격 아크(SlashVfx)가 있었다. 그것이 "이펙트가 칼 궤적과
     * 어긋나 붕 뜬다"의 원인이었다 - **참격이 두 번 그려지고 있었기 때문이다.**
     *
     * 사무라이 팩(FULL_Samurai)의 ATTACK 시트에는 원화가가 칼 궤적에 맞춰 그린 흰 참격이
     * 타격 프레임에 이미 들어 있다. 그것이 정답 궤적이다 - 스프라이트의 일부이므로 칼과
     * 어긋날 수가 없다. 그 위에 별도 팩의 아크를 한 장 더 얹으면, 두 호의 모양도 방향도
     * 다르므로 무엇을 해도 맞지 않는다. 실제로 시트를 재보니 팩 아크는 아래로 긋는 세로
     * 베기(-90도)였고 사무라이의 발도는 오른쪽 위(+24도)였다. 종류가 반대다.
     *
     * 그래서 아크는 스프라이트 하나로 줄이고, 이 컴포넌트는 **아크가 아닌 것**을 맡는다.
     * 스프라이트가 말해주지 못하는 것은 "어디에 맞았는가"다 - 참격은 사무라이에 그려져
     * 있으므로 요괴 쪽에는 아무 일도 일어나지 않는다. 작은 불꽃 하나가 그 자리를 찍는다.
     *
     * 회전은 걸지 않는다. 가시가 사방으로 뻗는 모양이라 돌려도 달라지지 않고, 픽셀 아트를
     * 정수배가 아닌 각도로 돌리면 가시가 뭉개지기만 한다. 방향은 좌우 반전으로만 준다.
     */
    [RequireComponent(typeof(SpriteRenderer))]
    public sealed class ImpactSpark : MonoBehaviour
    {
        [SerializeField] private SpriteRenderer spriteRenderer;
        [SerializeField] private SpriteAnimator animator;

        /**
         * @brief 불꽃 색. **붉은색이다.**
         *
         * 시트는 흰색으로 굽고 색은 여기서 입힌다(꽃잎과 같은 방식).
         *
         * 처음에는 흰기 도는 살구색(#FFE2D0)으로 넣었다가 화면에서 물렸다. 불꽃이
         * 서는 자리를 **사무라이 스프라이트의 흰 참격이 그대로 지나가기 때문이다** -
         * 흰 아크 위에 흰 불꽃을 얹으면 12px짜리가 통째로 묻힌다. 스크린샷으로
         * 확인했다.
         *
         * 붉은색은 그 위에서 읽히고, 초록 팩 원본(실측 hue 74도)과 달리 먹빛 배경 ·
         * 단풍 · 벚꽃과 같은 계열에 있다. 피격 플래시(흰색)와도 갈린다 - 흰 번쩍임은
         * 요괴 몸 전체가 하는 일이고, 이 불꽃은 한 점을 찍는 일이다.
         */
        [Tooltip("불꽃 색. 시트가 흰색이라 여기서 입힌다. 흰색 금지(스프라이트 참격에 묻힌다), " +
                 "초록 금지(먹빛·적·벚꽃 팔레트와 충돌한다)")]
        [SerializeField] private Color tint = new Color32(0xFF, 0x45, 0x3A, 0xFF);

        private Action<ImpactSpark> finished;

        private void Awake()
        {
            if (spriteRenderer == null) spriteRenderer = GetComponent<SpriteRenderer>();
            if (animator == null) animator = GetComponent<SpriteAnimator>();
            spriteRenderer.sortingOrder = SortingOrders.Vfx;
        }

        /**
         * @brief 불꽃을 한 번 터뜨린다.
         *
         * @param mirror 요괴가 왼쪽을 볼 때. 가시가 칼이 들어온 쪽으로 더 길게 뻗도록
         *               그려져 있어서, 뒤집지 않으면 뿌려지는 방향이 반대가 된다
         */
        public void Play(Sprite[] frames, float framesPerSecond, Vector3 position,
                         bool mirror, Action<ImpactSpark> onFinished)
        {
            finished = onFinished;

            transform.position = position;
            transform.localRotation = Quaternion.identity;

            spriteRenderer.flipX = mirror;
            spriteRenderer.color = tint;
            spriteRenderer.sortingOrder = SortingOrders.Vfx;
            spriteRenderer.enabled = true;

            animator.Play(frames, framesPerSecond, false, Complete);
        }

        private void Complete()
        {
            spriteRenderer.enabled = false;
            var callback = finished;
            finished = null;
            if (callback != null) callback(this);
        }
    }
}
