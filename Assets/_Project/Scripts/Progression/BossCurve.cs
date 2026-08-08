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
        /**
         * @brief 보스 등급 세 단계.
         *
         * 13단계에서 둘에서 셋으로 늘렸다. 다크 사무라이가 5의 배수마다 나오던
         * 것을 **지역 피날레 전용**으로 옮기면서, 5스테이지 관문과 10스테이지
         * 피날레가 서로 다른 무게를 가져야 했기 때문이다. 둘이 같은 배수를 쓰면
         * 피날레가 그냥 또 하나의 챕터 보스가 된다.
         */
        public enum Tier
        {
            /** 그 스테이지 잡몹의 확대판. 짧게 등장하고 곧바로 싸운다 */
            Normal,

            /** 지역 중간의 관문. 엘리트 잡몹 */
            Chapter,

            /** 지역의 마지막. 전용 아트와 전체 연출 */
            Finale
        }

        /** 몇 스테이지마다 챕터 관문이 오는가 */
        public const int ChapterEvery = 5;

        /**
         * @brief 지역 하나의 길이. 마지막 스테이지가 피날레다.
         *
         * 진짜 출처는 `Region_1.asset`이고 여기는 그 사본이다. 시뮬레이션과 곡선이
         * 순수 함수라 애셋을 읽을 수 없기 때문인데, 사본이 있으면 언젠가 갈린다.
         * `BossRosterTests`가 둘이 같은지 검사한다 - 11단계의
         * ChapterHealthMultiplier처럼 선언만 남고 실제와 어긋나는 상태를 막는다.
         */
        public const int RegionLength = 10;

        public static Tier TierOf(int stage)
        {
            if (stage <= 0) return Tier.Normal;
            if (stage % RegionLength == 0) return Tier.Finale;
            if (stage % ChapterEvery == 0) return Tier.Chapter;
            return Tier.Normal;
        }

        /**
         * @brief 확대판이 아닌 보스인가.
         *
         * 챕터와 피날레 둘 다 참이다. 이 값이 가르는 것은 "추가 배수를 받는가"와
         * "잡몹 확대판이 아닌가"이고, 그 둘은 등급이 셋이 된 뒤에도 같이 움직인다.
         * 연출의 크기는 이것이 아니라 UsesFullIntro가 정한다.
         */
        public static bool IsChapterBoss(int stage)
        {
            return TierOf(stage) != Tier.Normal;
        }

        /**
         * @brief 전체 등장 연출(암전 + 이름 + 5.3초 워크인)을 쓰는가.
         *
         * **피날레만이다.** 12단계까지는 5의 배수마다 이 연출이 돌았는데, 그러면
         * 10스테이지 지역에서 두 번 나오고 두 번째가 특별할 이유가 없어진다.
         * 연출을 줄이는 것이 아니라 한 곳에 몰아준다.
         */
        public static bool UsesFullIntro(int stage)
        {
            return TierOf(stage) == Tier.Finale;
        }

        /** 1부터 세는 챕터 번호. 스테이지에서 유도되므로 세이브에 새 필드가 필요 없다 */
        public static int ChapterOf(int stage)
        {
            return stage <= 0 ? 1 : (stage - 1) / ChapterEvery + 1;
        }

        /**
         * @brief 1부터 세는 지역 번호.
         *
         * 챕터와 같은 이유로 **스테이지에서 유도한다.** 세이브에 지역 필드를
         * 따로 두면 진행(stage)과 표시(region)가 어긋날 수 있는 상태가 생기고,
         * 그 둘이 갈리면 어느 쪽이 맞는지 판단할 근거가 없다. 유도하면 갈릴 수
         * 없다 - 세이브는 v5 그대로다.
         */
        public static int RegionOf(int stage)
        {
            return stage <= 0 ? 1 : (stage - 1) / RegionLength + 1;
        }

        /** 지역 안에서 몇 번째 스테이지인가. 1..RegionLength */
        public static int StageInRegion(int stage)
        {
            return stage <= 0 ? 1 : (stage - 1) % RegionLength + 1;
        }

        // ---------------------------------------------------------------- 배수

        /**
         * @brief 챕터 보스의 체력 추가 배수.
         *
         * 일반 보스 위에 곱해진다. 챕터의 마지막 관문이므로 그 앞의 넷보다
         * 확실히 무거워야 하지만, 여유 밴드를 벗어나면 벽이 된다. 시뮬레이션에서
         * 챕터 스테이지의 여유가 1.3~2.0에 들어오는 값으로 잡았다.
         *
         * 1.5에서 1.7로 올렸다. 회복 축을 최대 체력 비례로 바꾸자 골드가 그쪽으로
         * 흘러 화력 성장이 조금 느려졌고, 그만큼 보스 처치가 길어져 챕터 여유가
         * 2.15까지 올라갔다(일반 스테이지 밴드와 겹친다). 계수 하나가 다른 축의
         * 곡선에 묶여 있다는 뜻이고, 새 축을 건드릴 때마다 여기를 다시 봐야 한다.
         */
        public const double ChapterHealthMultiplier = 1.25d;

        /** 챕터 보스의 골드 추가 배수. 위험이 큰 만큼 보상도 크다 */
        public const double ChapterGoldMultiplier = 2d;

        /** 챕터 보스의 공격력 추가 배수 */
        public const double ChapterAttackMultiplier = 1.4d;

        /**
         * @brief 피날레 배수. 챕터를 **대체한다** (곱하지 않는다).
         *
         * 곱하면 지역이 길어질수록 피날레가 지수로 무거워진다. 피날레는 챕터보다
         * 무거워야 하는 것이지 챕터의 몇 배여야 하는 것이 아니다.
         *
         * 값은 시뮬레이션에서 피날레 여유가 1.15~1.7에 들어오게 잡았다. 챕터
         * 밴드(1.3~2.0)보다 아래에 있어야 "지역의 마지막이 가장 빡빡하다"가
         * 수치로도 성립한다.
         *
         * 20단계에서 1.5에서 1.7로 올렸다. 골드 획득 축이 들어오면서 **챕터가
         * 피날레보다 빡빡해졌기** 때문이다 - 챕터 최악 1.34, 피날레 최악 1.45로
         * 순서가 뒤집혔다. 위 ChapterHealthMultiplier 주석이 "계수 하나가 다른
         * 축의 곡선에 묶여 있고 새 축을 건드릴 때마다 여기를 다시 봐야 한다"고
         * 적어둔 자리이고, 실제로 그렇게 됐다.
         *
         * 뒤집힘의 원인은 두 등급이 골드 축의 투자 구간을 다르게 겪는 데 있다.
         * 첫 챕터(st5)는 축을 사느라 화력이 밀린 한가운데이고, 첫 피날레(st10)는
         * 축이 이미 상한에 닿아 회수가 끝난 뒤다.
         */
        public const double FinaleHealthMultiplier = 1.7d;
        public const double FinaleGoldMultiplier = 2.0d;
        public const double FinaleAttackMultiplier = 1.6d;

        /** 이 스테이지 보스가 받는 체력 추가 배수 */
        public static double HealthMultiplierFor(int stage)
        {
            switch (TierOf(stage))
            {
                case Tier.Finale: return FinaleHealthMultiplier;
                case Tier.Chapter: return ChapterHealthMultiplier;
                default: return 1d;
            }
        }

        public static double GoldMultiplierFor(int stage)
        {
            switch (TierOf(stage))
            {
                case Tier.Finale: return FinaleGoldMultiplier;
                case Tier.Chapter: return ChapterGoldMultiplier;
                default: return 1d;
            }
        }

        public static double AttackMultiplierFor(int stage)
        {
            switch (TierOf(stage))
            {
                case Tier.Finale: return FinaleAttackMultiplier;
                case Tier.Chapter: return ChapterAttackMultiplier;
                default: return 1d;
            }
        }

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
            return damage * AttackMultiplierFor(stage);
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
