using System;

namespace Onikiri.Progression
{
    /**
     * @brief 전설 妖刀의 값. **사본 수 하나가 이 축의 전부다.**
     *
     * ## 세 곳에 붙되, 영체만은 다르게 붙는다
     *
     *   공격력   PowerFactor  - 요도 티어 배수와 같은 자리(곱)
     *   상성     FactorForSkill - 45단계 곡선과 같은 모양, 자기 상수
     *   영체     SpiritFactor - **소환되지 않는다. 남의 소환을 키운다**
     *
     * 앞의 둘은 45단계 프레임을 글자 그대로 재사용한다. 세 번째만 다른
     * 이유는 아트가 아니라 설정이다.
     *
     * ## 왜 전설 요도의 영체는 나오지 않는가
     *
     * 영체는 **봉인한 혼이 잠깐 나오는 것**이다(YodoSpiritCurve 머리 주석).
     * 전설 요도에는 봉인한 혼이 없다 - 요괴를 벤 적이 없는 칼이고, 그것이
     * 이 풀이 보스 혼 넷과 갈린 이유 그 자체다(LegendaryYodoSpec 머리
     * 주석). 여기에 영체를 붙이면 "벤 적 없는 요괴가 전장에 선다"가 되고,
     * 44단계부터의 기둥("혼은 요괴를 벤 증거")이 화면에서 무너진다.
     *
     * 대신 **이미 서 있는 영체를 키운다**. 요도가 혼의 격을 끌어올린다는
     * 것은 혼격(YodoRarityCurve)이 이미 하는 말이고, 전설 요도는 그것을
     * 자루 하나가 아니라 로테이션 전체에 한다. 새 아트 0장이라는 결과는
     * 부수적이지만, 그 선택을 정당화하는 것은 아트 예산이 아니라 이
     * 문단이다.
     *
     * ## 사본 = 돌파. 상한을 넘으면 파편이 된다
     *
     * 44단계의 넘침 규칙(YodoCurve.ShardsPerOverflowSoul - "버려지는 드랍
     * 0")을 이 축도 잇는다. 상한 사본에 또 같은 칼이 나오면 파편 뭉치가
     * 되고, 그 크기가 이 게임에서 가장 큰 파편 한 덩어리다 - ★5가 중복
     * 이라고 해서 ★1보다 못한 결과가 되면 그것은 확률이 아니라 사고다.
     */
    public static class LegendaryYodoCurve
    {
        // ---------------------------------------------------------------- 칸

        /**
         * @brief 한 자루가 쌓을 수 있는 사본 수. **1이 획득, 그 위가 돌파다.**
         *
         * 4인 이유는 뽑기 수명이다. 전설 확률이 {GachaCurve} 표의 마지막
         * 줄이라 두 자루 x 4사본은 여덟 번의 ★5이고, 그것은 기대 뽑기
         * 수로 천 회가 넘는다 - 상점이 이 축 하나로 오래 버틴다.
         *
         * 위로도 묶여 있다. 사본이 무한이면 보석 무제한 플레이어의 요도
         * 축이 다시 발산하고, 42단계가 심층 램프에서 배운 것("스테이지당
         * x1.008도 st200에서 8.4배가 된다")이 재현된다.
         */
        public const int MaxCopies = 4;

        /** 돌파 단계 (0~3). 사본 하나는 획득이지 돌파가 아니다 */
        public static int BreakthroughOf(int copies)
        {
            if (copies < 1) return 0;
            int c = copies > MaxCopies ? MaxCopies : copies;
            return c - 1;
        }

        // ---------------------------------------------------------------- 공격력

        /**
         * @brief 획득(사본 1)의 공격력 배수.
         *
         * 요도 봉인(YodoCurve.SealStep 1.05)보다 크다. 봉인은 한 바퀴를
         * 돌면 반드시 오는 사건이고 이쪽은 200회에 한 번이라, 같은 크기면
         * 화면에서 "★5인데 봉인만도 못하다"가 된다.
         */
        public const double GrantStep = 1.12d;

        /** 돌파 한 칸. 세 번 곱해진다 */
        public const double BreakStep = 1.07d;

        /**
         * @brief 이 사본 수의 공격력 배수. **미보유는 정확히 1이다.**
         *
         * 1로 떨어지는 것이 f2p 안전선이다 - 전설 요도의 자연 출처가 없으므로
         * 무과금의 기대 곡선과 보정이 44·45단계 그대로 남는다.
         */
        public static double PowerAt(int copies)
        {
            if (copies < 1) return 1d;
            return GrantStep * Math.Pow(BreakStep, BreakthroughOf(copies));
        }

        /** 두 자루가 공격력에 곱하는 총 배수 */
        public static double PowerFactor(int[] copies)
        {
            if (copies == null) return 1d;

            double product = 1d;
            int count = Math.Min(LegendaryYodoCatalog.Count, copies.Length);
            for (int i = 0; i < count; i++) product *= PowerAt(copies[i]);
            return product;
        }

        // ---------------------------------------------------------------- 상성

        /**
         * @brief 한 오의를 전담하는 전설(천수도)의 획득 배수.
         *
         * 45단계의 전담 봉인(AffinitySealStep 1.13)보다 작다. 전설은 티어가
         * 없어 아홉 칸을 오르지 않으므로 시작값이 곧 대부분이고, 같은 값을
         * 두면 획득 한 번이 요도 한 자루의 절반을 통째로 준다.
         */
        public const double DedicatedGrantStep = 1.11d;
        public const double DedicatedBreakStep = 1.07d;

        /** 전 오의를 미는 전설(백면도). 셋에 걸리므로 얕다 - 45단계 흑야와 같은 규칙 */
        public const double BroadGrantStep = 1.045d;
        public const double BroadBreakStep = 1.028d;

        /**
         * @brief "연참" 또는 "전 오의". 도감이 쓴다.
         *
         * YodoRow.AffinitySkillName과 같은 규칙이고 같은 이유다 - 빈 id가
         * 전 오의라는 사실은 카탈로그가 정하고, 그것을 화면 문구로 옮기는
         * 곳은 하나여야 한다.
         */
        public static string SkillName(int index)
        {
            if (index < 0 || index >= LegendaryYodoCatalog.Count) return "오의";

            string id = LegendaryYodoCatalog.Blades[index].AffinitySkillId;
            if (string.IsNullOrEmpty(id)) return "전 오의";

            int skill = SkillCatalog.IndexOf(id);
            return skill >= 0 ? SkillCatalog.Skills[skill].DisplayName : "오의";
        }

        /** 이 칼이 전 오의를 미는가. 카탈로그의 빈 id가 그 뜻이다 */
        public static bool IsBroad(int index)
        {
            if (index < 0 || index >= LegendaryYodoCatalog.Count) return false;
            return string.IsNullOrEmpty(LegendaryYodoCatalog.Blades[index].AffinitySkillId);
        }

        /** 이 칼이 자기 오의에 곱하는 배수. 미보유면 정확히 1이다 */
        public static double AffinityAt(int index, int copies)
        {
            if (copies < 1) return 1d;

            bool broad = IsBroad(index);
            double grant = broad ? BroadGrantStep : DedicatedGrantStep;
            double step = broad ? BroadBreakStep : DedicatedBreakStep;
            return grant * Math.Pow(step, BreakthroughOf(copies));
        }

        /**
         * @brief skillIndex번 오의가 전설에게서 받는 총 상성 배수.
         *
         * 보스 요도의 상성과 **곱해진다**(YodoAffinityCurve.FactorForSkill이
         * 두 값을 함께 낸다). 더하지 않는 이유는 45단계와 같다 - 한 화면에서
         * 합과 곱이 섞이면 어느 쪽이 큰지를 플레이어가 셀 수 없다.
         */
        public static double AffinityForSkill(int skillIndex, int[] copies)
        {
            if (copies == null) return 1d;
            if (skillIndex < 0 || skillIndex >= SkillCatalog.Count) return 1d;

            string skillId = SkillCatalog.Skills[skillIndex].Id;

            double factor = 1d;
            int count = Math.Min(LegendaryYodoCatalog.Count, copies.Length);
            for (int i = 0; i < count; i++)
            {
                if (copies[i] < 1) continue;

                string target = LegendaryYodoCatalog.Blades[i].AffinitySkillId;
                if (!string.IsNullOrEmpty(target) && target != skillId) continue;

                factor *= AffinityAt(i, copies[i]);
            }
            return factor;
        }

        // ---------------------------------------------------------------- 영체

        /**
         * @brief 전설 하나가 **로테이션 전체의** 영체 배율에 곱하는 값.
         *
         * 소환되는 것이 아니라 소환을 키운다(머리 주석). 로테이션의 평균에
         * 곱해지므로 자루를 고르게 올린 플레이어에게 더 크게 돌아간다 -
         * 45단계의 영체가 이미 그 방향을 보상하고 있고, 전설은 그 축을
         * 부정하지 않는다.
         */
        public const double SpiritGrantStep = 1.05d;
        public const double SpiritBreakStep = 1.03d;

        public static double SpiritAt(int copies)
        {
            if (copies < 1) return 1d;
            return SpiritGrantStep * Math.Pow(SpiritBreakStep, BreakthroughOf(copies));
        }

        /** 두 자루가 영체 초당환산에 곱하는 총 배수. 미보유면 1 */
        public static double SpiritFactor(int[] copies)
        {
            if (copies == null) return 1d;

            double product = 1d;
            int count = Math.Min(LegendaryYodoCatalog.Count, copies.Length);
            for (int i = 0; i < count; i++) product *= SpiritAt(copies[i]);
            return product;
        }

        // ---------------------------------------------------------------- 재고

        /** 이 칼이 사본을 더 받을 수 있는가 */
        public static bool Accepts(int copies)
        {
            return copies < MaxCopies;
        }

        /**
         * @brief 전설이 갈 자루. 없으면 -1.
         *
         * **가장 적은 사본으로 간다.** 혼 정수(가장 낮은 티어)·상위 혼(가장
         * 낮은 혼격)과 같은 규칙이다 - 뽑기는 넓히고 파편은 몬다(46단계).
         * 미보유가 있으면 언제나 그쪽이 먼저이므로 두 자루를 다 모으기 전에는
         * 돌파가 시작되지 않고, 그래서 "새 칼"이라는 사건이 앞에 온다.
         */
        public static int TargetFor(int[] copies)
        {
            int best = -1;
            int bestCopies = int.MaxValue;

            for (int i = 0; i < LegendaryYodoCatalog.Count; i++)
            {
                int held = copies != null && i < copies.Length ? copies[i] : 0;
                if (!Accepts(held)) continue;
                if (held >= bestCopies) continue;

                best = i;
                bestCopies = held;
            }

            return best;
        }

        /**
         * @brief 갈 곳 없는 전설이 바뀌는 파편 수. **이 게임의 가장 큰 한 덩어리다.**
         *
         * 잭팟(파편 70)의 네 배쯤이고 촉매 열두 묶음이다. 44단계의 넘침
         * 규칙(ShardsPerOverflowSoul 40)이 대요괴 한 마리 값이었던 것처럼,
         * 이 값은 **200회에 한 번**의 값이어야 한다.
         */
        public const int ShardsPerOverflow = 240;

        /** 보유한 자루 수. 도감 머리글과 테스트가 쓴다 */
        public static int OwnedCount(int[] copies)
        {
            if (copies == null) return 0;

            int owned = 0;
            int count = Math.Min(LegendaryYodoCatalog.Count, copies.Length);
            for (int i = 0; i < count; i++) if (copies[i] >= 1) owned++;
            return owned;
        }

        /** 넷 다 상한 사본일 때의 공격력 배수. 보고와 밴드 검사가 쓴다 */
        public static double Ceiling
        {
            get
            {
                double product = 1d;
                for (int i = 0; i < LegendaryYodoCatalog.Count; i++) product *= PowerAt(MaxCopies);
                return product;
            }
        }
    }
}
