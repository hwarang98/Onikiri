namespace Onikiri.Progression
{
    /**
     * @brief 명중·회피의 판정식 (65단계). **수치 대결(rating)이다.**
     *
     *     성공 확률 = 내 수치 / (내 수치 + 상대 수치)
     *
     * 플레이어 명중 확률은 Chance(명중, 적 회피), 플레이어 회피 확률은
     * Chance(회피, 보스 명중)이다. 같은 식을 양쪽이 쓴다.
     *
     * ## 왜 가산 %p + clamp가 아닌가
     *
     * "명중률 = 90% + 레벨 x 0.5%p, 100%에서 자른다"로 두면 100%에 닿는 순간
     * 그 축은 하드캡과 같아진다 - 64단계가 골드 축에서 지운 바로 그 구조다.
     * 수치 대결은 확률을 (0, 1) 안에 **자연히** 가둔다. 내 수치가 무한히 커지면
     * 확률이 1에 수렴할 뿐 닿지 않고, 그래서 축 값에 상한이 필요 없다.
     *
     * ## 경계
     *
     * 상대 수치가 0 이하면 **정확히 1**이다(나눗셈을 거치지 않는다). 귀문의 적
     * 회피가 0이고(65단계 확정 6), 그 경로는 판정 전과 비트 단위로 같아야 한다 -
     * 0을 더해 나눈 값은 1.0이지만 그 계산을 지나는 것 자체를 피해 둔다.
     * 내 수치가 0 이하이고 상대가 양수면 0이다.
     */
    public static class RatingContest
    {
        public static double Chance(double rating, double opposing)
        {
            if (opposing <= 0d) return 1d;
            if (rating <= 0d) return 0d;
            return rating / (rating + opposing);
        }
    }
}
