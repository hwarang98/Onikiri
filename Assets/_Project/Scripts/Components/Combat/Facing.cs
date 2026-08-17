namespace Onikiri.Battle
{
    /**
     * @brief 그려진 방향을 화면 방향으로 바꾸는 규칙. **두 줄뿐이고, 그것이 요점이다.**
     *
     * ## 왜 함수로 뽑았는가 - 같은 실수를 세 번 했다
     *
     * 이 게임의 전투는 한 줄로 서 있다. 아군은 왼쪽에서 오른쪽을 보고, 적은
     * 오른쪽에서 왼쪽을 본다. 그런데 스프라이트 팩마다 그려진 방향이 달라서
     * (`artFacesLeft`), 화면 방향은 언제나 그 데이터에서 **유도**해야 한다.
     *
     * 유도식이 두 개다:
     *
     *     아군  flipX = artFacesLeft       (왼쪽으로 그려진 팩만 뒤집는다)
     *     적    flipX = !artFacesLeft      (오른쪽으로 그려진 팩만 뒤집는다)
     *
     * 둘의 차이가 느낌표 하나다. 그 느낌표를 세 번 틀렸다 - 21단계에 처형인
     * 팩이 플레이어에게 등을 돌렸고(팩이 왼쪽을 보고 그려져 있었다), 동료
     * 스텝에 늑대가 같은 자리에서 걸렸고, 45단계에 영체가 요괴에게 등을
     * 돌렸다. 마지막 것이 특히 고약했던 이유는 **아트의 출처와 진영이
     * 갈렸기** 때문이다 - 대요괴의 스프라이트를 아군이 빌려 쓰는 첫 자리라,
     * 아트를 따라가면 적의 규칙을 쓰게 된다.
     *
     * 그래서 값이 아니라 **이름**을 고르게 만든다. `!` 하나를 빠뜨리는 것은
     * 리뷰에서 안 보이지만, `Facing.Enemy`라고 적힌 아군 컴포넌트는 보인다.
     *
     * 새 전투체를 만들 때 물어야 하는 것은 하나다: **이것은 어느 편에서
     * 싸우는가.** 그리는 팩이 무엇인지는 답과 무관하다.
     */
    public static class Facing
    {
        /**
         * @brief 아군(로닌·동료·영체)의 flipX. **오른쪽을 본다.**
         *
         * @param artFacesLeft 원본 아트가 왼쪽을 보고 그려졌는가
         */
        public static bool Ally(bool artFacesLeft)
        {
            return artFacesLeft;
        }

        /**
         * @brief 적(잡몹·보스)의 flipX. **왼쪽의 플레이어를 본다.**
         *
         * @param artFacesLeft 원본 아트가 왼쪽을 보고 그려졌는가
         */
        public static bool Enemy(bool artFacesLeft)
        {
            return !artFacesLeft;
        }
    }
}
