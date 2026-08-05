using System;

namespace Onikiri.Progression
{
    /**
     * @brief 보스 두 유형과 그 공격력.
     *
     * ## 왜 둘로 나누는가
     *
     * 9~10단계의 보스는 매 스테이지 같은 다크 사무라이였고, 등장할 때마다 화면이
     * 어두워지고 이름이 뜨고 5.3초를 걸어 들어왔다. 스테이지 하나가 40초인데
     * 그중 6초가 매번 같은 연출이다. 반복되면 연출은 무게가 아니라 대기 시간이 된다.
     *
     * 그래서 레퍼런스(Slayer Legend)의 구조를 따른다:
     *
     *   일반 스테이지 보스 - 그 스테이지 잡몹의 확대판. 짧게 등장하고 곧바로 싸운다
     *   챕터 보스 (5의 배수) - 다크 사무라이. 전체 연출을 여기에만 남긴다
     *
     * 연출을 5분의 1로 줄이는 것이 아니라, **5분의 1에 몰아준다.** 희소해진
     * 등장이 챕터 보스의 무게를 만든다.
     */
    public static class BossCurve
    {
        /** 몇 스테이지마다 시그니처 보스가 오는가 */
        public const int ChapterEvery = 5;

        public static bool IsChapterBoss(int stage)
        {
            return stage > 0 && stage % ChapterEvery == 0;
        }

        /** 1부터 세는 챕터 번호. 스테이지에서 유도되므로 세이브에 새 필드가 필요 없다 */
        public static int ChapterOf(int stage)
        {
            return stage <= 0 ? 1 : (stage - 1) / ChapterEvery + 1;
        }

        // ---------------------------------------------------------------- 배수

        /**
         * @brief 챕터 보스의 체력 추가 배수.
         *
         * 일반 보스 위에 곱해진다. 챕터의 마지막 관문이므로 그 앞의 넷보다
         * 확실히 무거워야 하지만, 여유 밴드를 벗어나면 벽이 된다. 시뮬레이션에서
         * 챕터 스테이지의 여유가 1.3~2.0에 들어오는 값으로 잡았다.
         */
        public const double ChapterHealthMultiplier = 1.5d;

        /** 챕터 보스의 골드 추가 배수. 위험이 큰 만큼 보상도 크다 */
        public const double ChapterGoldMultiplier = 2d;

        /** 챕터 보스의 공격력 추가 배수 */
        public const double ChapterAttackMultiplier = 1.4d;

        // ---------------------------------------------------------------- 공격력

        /**
         * @brief 1스테이지 일반 보스가 한 번 때릴 때의 피해.
         *
         * 6은 1스테이지 보스가 무강화 플레이어(체력 100)를 죽이지 못하게 하는
         * 값이다. 2초마다 때리므로 때릴 수 있는 시간 안에 12번, 총 72.
         * 첫 보스는 화력으로도 체력으로도 벽이 아니어야 한다.
         */
        public const double BaseAttackDamage = 6d;

        /** 공격 사이의 간격 (초) */
        public const double AttackIntervalSeconds = 2d;

        /**
         * @brief 스테이지마다 공격력에 곱하는 값.
         *
         * **잡몹 체력의 1.55와 같게 두지 않았다.** 그 값을 쓰면 체력 축이 따라갈 수
         * 없기 때문이다. 계산은 이렇다:
         *
         *   체력 곡선은 레벨당 1.10배다. 1.55배를 따라가려면 스테이지마다
         *   ln(1.55)/ln(1.10) = 4.6 레벨이 필요하다.
         *   그런데 골드로 살 수 있는 레벨은 스테이지당 ln(1.72)/ln(1.15) = 3.88개
         *   **전부 합쳐서**다. 체력 하나가 예산 전체를 넘게 먹는다.
         *
         * 1.12는 체력이 예산의 약 3분의 1을 쓰면 따라잡히는 값이다
         * (ln(1.12)/ln(1.10) = 1.19 레벨/스테이지). 나머지 3분의 2가 화력 네 축으로
         * 간다. 생존이 성장의 일부이면서 주인공은 아니게 하는 배분이다.
         *
         * 보스 체력(스테이지마다 1.55 x 램프)과 공격력(1.12)이 다른 속도로 자라는
         * 것이 이상해 보일 수 있지만, 둘이 재는 것이 다르다. 체력은 "얼마나 세게
         * 때려야 하는가", 공격력은 "얼마나 오래 버텨야 하는가"이고, 후자는
         * 제한 시간이 30초로 고정이라 무한정 오를 이유가 없다.
         */
        public const double AttackGrowth = 1.12d;

        public static double AttackDamageForStage(int stage)
        {
            double damage = BaseAttackDamage * Math.Pow(AttackGrowth, Math.Max(0, stage - 1));
            return IsChapterBoss(stage) ? damage * ChapterAttackMultiplier : damage;
        }

        /**
         * @brief 이 스테이지 보스가 제한 시간 동안 낼 수 있는 총 피해.
         *
         * 생존 게이트의 기준이다. 유효체력이 이 값보다 작으면 죽는다.
         */
        public static double TotalDamageOverFight(int stage, double fightSeconds)
        {
            double swings = Math.Floor(fightSeconds / AttackIntervalSeconds);
            return AttackDamageForStage(stage) * swings;
        }
    }
}
