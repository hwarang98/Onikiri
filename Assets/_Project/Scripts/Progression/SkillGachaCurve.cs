using System;

namespace Onikiri.Progression
{
    /**
     * @brief 오의 뽑기(스킬 가챠)의 확률표·값·상한. **47단계의 사다리를 그대로 빌린다.**
     *
     * ## 이 뽑기는 파워를 팔지 않는다 - 폭과 시간을 판다
     *
     * 49단계가 슬롯을 만들면서 이 스텝의 자리를 미리 비워 뒀다. 그 스텝의
     * 주장은 하나였다 - **밴드가 보는 것은 풀 크기가 아니라 자리 수**이므로
     * 풀이 여덟이 되든 스물이 되든 DPS가 안 자란다(SkillCurve.BaseSlots).
     * 이 스텝은 그 주장 위에 서 있고, 그래서 팔 수 있는 것이 정확히 둘로
     * 정해진다:
     *
     *   **폭**    가챠 몫으로 예약된 오의 둘(혈폭·혈조)을 연다
     *   **시간**  장착한 오의를 **상한까지** 더 빨리 민다 (스킬 XP)
     *
     * 그리고 팔 수 없는 것이 하나 있다: **상한 그 자체**다. 스킬 XP는 레벨을
     * 밀지만 `SkillCurve.CeilingRatio`를 한 톨도 안 건드린다 - 그것이 이 스텝의
     * 유일한 안전선이고, 45단계가 못 박은 오의 몫 계약(49.0% / 한계 50%)이
     * 그 상한 위에서만 성립하기 때문이다. 뽑기가 상한을 밀면 그 계약이 그
     * 자리에서 깨진다.
     *
     * 44단계의 촉매가 "힘이 아니라 시간을 판다"고 적은 자리와 같은 문장이고,
     * 47단계의 요도 파편이 티어 상한을 **앞당길 뿐 넘지 못하는** 것과 정확히
     * 같은 구조다.
     *
     * ## 왜 표를 새로 안 짓는가 - 사다리는 하나여야 한다
     *
     * 확률·천장·비용·일일 무료가 전부 `GachaCurve`에서 온다. 값을 옮겨 적지
     * 않고 **가리키는** 이유는 두 가지다.
     *
     * **하나. 화면이 같은 것을 두 번 가르치지 않는다.** 상점의 두 배너가
     * 다른 확률·다른 천장·다른 가격을 쓰면 플레이어는 사다리를 두 번 배워야
     * 하고, 47단계가 등급 색과 별로 세운 눈금이 배너마다 다른 뜻이 된다.
     *
     * **둘. 천장의 산수가 하나다.** 47단계는 "천장이 표를 누른다"는 보정을
     * 닫힌 식으로 유도했고(GachaCurve.PityShare), 그 유도는 확률표가 같으면
     * 그대로 성립한다. 표를 따로 두면 그 식을 두 번 유도해야 하고, 두 번
     * 유도한 것은 언젠가 갈린다.
     *
     * 표가 갈리는 날은 두 뽑기가 **다른 물건**이 되는 날이다. 그날에는 이
     * 파일이 자기 배열을 갖게 되고, 그 전까지는 안 갖는 편이 정직하다 -
     * `SkillGachaTests.Ladder_IsTheSameLadder`가 그 계약을 지킨다.
     *
     * ## 결과 여섯 - 자리는 같고 상품만 갈린다
     *
     *     ★1 일반   스킬 XP 6      꽝 방지 바닥 (요도 파편 6과 같은 자리)
     *     ★2 고급   스킬 XP 20     기존 두 줄
     *     ★2 고급   스킬 XP 70     잭팟
     *     ★3 희귀   스킬 XP 240    한 오의를 서너 칸 민다
     *     ★4 영웅   **오의 해금**   혈폭 -> 혈조
     *     ★5 전설   **오의 개안**   장착 오의 하나를 즉시 상한까지
     *
     * ★2가 두 줄인 다대일도 47단계 그대로다. 등급은 드라마의 층이고 결과는
     * 보상의 층이라 일대일일 필요가 없다(GachaCurve.Grade 주석).
     *
     * ## ★5가 "즉시 상한"인 이유 - 파워가 아닌 가장 큰 상품
     *
     * 전설 자리에 새 오의나 새 배수를 두면 그 순간 이 뽑기가 파워를 판다.
     * 그래서 가장 큰 상품을 **가장 큰 가속**으로 뒀다 - 개안은 열한 칸을
     * 한 번에 건너뛰지만 **열두 번째 칸은 만들지 않는다.** 도달하는 곳이
     * 진행으로 도달하는 곳과 같은 자리이고, 그것이 이 축의 천장이 안
     * 움직이는 이유다.
     *
     * 47단계의 ★5(전설 요도)와 갈리는 점이 하나 있다. 저쪽은 별개 풀이라
     * **내려갈 자리가 없어** 넘치면 곧바로 파편 뭉치였는데, 이쪽은 사다리
     * 안에 살아서 ★5 -> ★4 -> ★3으로 **두 칸 미끄러진다**(SlideFor).
     */
    public static class SkillGachaCurve
    {
        // ---------------------------------------------------------------- 해금

        /**
         * @brief 이 배너가 서는 스테이지. **요도 뽑기와 같은 칸이다.**
         *
         * 값을 여기 적지 않고 끌어오는 이유가 46·47단계와 같다 - 배너가 상점
         * 안에 서므로 상점이 열리는 칸이 곧 이 배너가 열리는 칸이고, 두 값이
         * 갈리면 "화면은 들어가지는데 배너 하나만 비어 있다"가 된다.
         *
         * 밸런스에도 같은 것을 요구한다. 조율 코리더(st1~30)와 가속 구간
         * (st31~50)은 이 스텝에서도 **비트 단위로** 움직이면 안 되는데,
         * 게이트가 st41이면 그 불변이 계수가 아니라 **구조로** 지켜진다 -
         * 44·45·46·47단계가 전부 같은 자리에서 같은 방법을 썼다.
         */
        public static int UnlockStage { get { return GachaCurve.UnlockStage; } }

        public static bool IsUnlockedAt(int stage)
        {
            return stage >= UnlockStage;
        }

        // ------------------------------------------------------- 빌린 것들 (47단계)

        /** 단연 한 번의 보석 값. 요도 뽑기와 같다 - 머리 주석의 "사다리는 하나" */
        public static int PullCostGems { get { return GachaCurve.PullCostGems; } }

        public static int TenPullCount { get { return GachaCurve.TenPullCount; } }
        public static int TenPullCostGems { get { return GachaCurve.TenPullCostGems; } }

        /** 소프트 천장. 이 횟수 안에 ★4 이상이 반드시 나온다 */
        public static int PityPulls { get { return GachaCurve.PityPulls; } }

        public static int PullsUntilPity(int counter) { return GachaCurve.PullsUntilPity(counter); }

        /**
         * @brief 하루에 주어지는 무료 뽑기 수. **f2p가 이 축에 닿는 경로다.**
         *
         * 46단계가 요도 뽑기에서 세운 판단이 여기서 한 겹 더 무거워진다.
         * 무과금의 보석은 44단계 실측대로 장비 등급·동료 해금·전직에 이미 다
         * 배정돼 있는데, 이제 그 보석을 노리는 배너가 **둘**이다 - 나눠 쓰면
         * 무과금 보석이 한 번 더 쪼개지고, 그것은 44단계가 지킨 코어 진행을
         * 두 배로 흔든다.
         *
         * 그래서 이 배너의 f2p 경로는 보석이 아니라 **일일 무료**다. 하루
         * 한 번이면 한 달에 천장 하나이고, 천장이 ★4(해금)를 보장하므로
         * **무과금은 한 달에 오의 하나씩 두 달이면 둘 다 연다.** 보석은
         * 그 위에 얹는 가속이지 경로가 아니다.
         *
         * 별도의 "스킬 토큰"을 두지 않은 것도 같은 판단이다. 재화를 하나 더
         * 만들면 그 재화의 faucet·화면·세이브 칸·밴드 모델이 함께 생기는데,
         * 일일 무료가 이미 그 일을 하고 있고 재화가 하나 늘면 무과금이 관리할
         * 것이 늘 뿐 얻는 것이 같다.
         */
        public static int FreePullsPerDay { get { return GachaCurve.FreePullsPerDay; } }

        public static DateTime DayOf(DateTime utc) { return GachaCurve.DayOf(utc); }

        /** 확률표. **요도 뽑기와 같은 배열이다** - 머리 주석 참고 */
        public static double[] Chances { get { return GachaCurve.Chances; } }

        public static int OutcomeCount { get { return GachaCurve.OutcomeCount; } }

        // ---------------------------------------------------------------- 결과

        /**
         * @brief 한 번의 뽑기가 내는 결과. **자리는 요도 표와 하나씩 맞물린다.**
         *
         * 같은 인덱스가 같은 등급이어야 `GachaCurve.GradeOf`를 그대로 쓸 수
         * 있고, 그래야 확률표 UI가 두 배너에서 같은 코드를 지난다. 순서가
         * 확률의 내림차순인 것도 그 표의 계약 그대로다.
         */
        public enum Outcome
        {
            XpSmall,      // ★1  스킬 XP 6
            XpLarge,      // ★2  스킬 XP 20
            XpJackpot,    // ★2  스킬 XP 70
            XpSurge,      // ★3  스킬 XP 240
            SkillUnlock,  // ★4  오의 해금
            Awakening     // ★5  오의 개안 (즉시 상한)
        }

        /**
         * @brief 결과가 주는 스킬 XP. 위쪽 두 등급은 0이다 - XP를 주지 않는다.
         *
         * **아래 네 줄이 요도 파편과 같은 수인 것은 우연이 아니다.** 46단계가
         * 등비 간격(6 / 20 / 70, 약 x3.4)을 defend하며 "잭팟이 촉매 세
         * 묶음보다 커야 한 번의 결과가 사건으로 읽힌다"고 적었고, 그 판단은
         * 재화가 파편에서 XP로 바뀌었다고 틀려지지 않는다 - 사다리의 모양은
         * 재화가 아니라 **한 번의 결과가 사건인가**가 정한다.
         *
         * ★3만 값을 새로 골랐다(240). 요도 쪽 ★3은 파편이 아니라 혼 정수라
         * 빌릴 수가 없고, 이쪽에서는 "한 오의를 서너 칸 민다"가 그 자리의
         * 뜻이다 - 상한 근처 한 칸이 166 XP이므로(XpToNextLevel) 240은
         * 초반이면 예닐곱 칸, 끝물이면 한 칸 반이다.
         */
        public static readonly int[] XpOf = { 6, 20, 70, 240, 0, 0 };

        public static GachaCurve.Grade GradeFor(Outcome outcome)
        {
            int index = (int)outcome;
            if (index < 0 || index >= GachaCurve.GradeOf.Length) return GachaCurve.Grade.Common;
            return GachaCurve.GradeOf[index];
        }

        public static int XpFor(Outcome outcome)
        {
            int index = (int)outcome;
            if (index < 0 || index >= XpOf.Length) return 0;
            return XpOf[index];
        }

        /** 0~1 난수를 결과로 옮긴다. **천장은 여기 없다** - GachaCurve.Roll과 같은 규칙 */
        public static Outcome Roll(double value)
        {
            return (Outcome)(int)GachaCurve.Roll(value);
        }

        /**
         * @brief 이 결과가 막혔을 때 **한 칸 아래**의 결과.
         *
         * 47단계의 ★4가 "혼격 -> 혼 정수 -> 파편"으로 미끄러진 것과 같은
         * 규칙이고 같은 이유다 - 천장이 준 결과가 아무것도 아니면 30회를
         * 채운 대가가 사라지고, 그것은 보장이 아니다.
         *
         * 다른 점은 ★5도 미끄러진다는 것이다. 저쪽의 전설은 별개 풀이라
         * 사다리에서 내려올 자리가 없었지만(GachaSystem.GrantLegendary),
         * 개안은 사다리 안에 살아서 **해금으로, 그다음 XP로** 내려간다.
         * 두 칸을 다 내려가도 XP가 남으므로 이 뽑기에는 파편으로 떨어질
         * 바닥이 필요 없다 - 44단계의 "버려지는 드랍 0"이 사다리 안에서
         * 닫힌다.
         */
        public static Outcome SlideFor(Outcome outcome)
        {
            switch (outcome)
            {
                case Outcome.Awakening:   return Outcome.SkillUnlock;
                case Outcome.SkillUnlock: return Outcome.XpSurge;
                default:                  return outcome;
            }
        }

        // ---------------------------------------------------------------- 실효 확률

        /**
         * @brief 천장이 아래 등급을 누른 뒤의 회당 확률. **47단계의 닫힌 식 그대로.**
         *
         * 유도는 `GachaCurve.PityShare` 주석에 있고, 확률표가 같으므로 여기서
         * 다시 유도할 것이 없다. 이 함수가 있는 이유는 그 유도의 **private
         * 한 줄**(SuppressedChance)을 밖에서 다시 적지 않기 위해서다.
         */
        public static double Suppressed(double raw)
        {
            double q = 1d - GachaCurve.EpicOrBetterChance;
            if (q <= 0d) return 0d;
            return (1d - GachaCurve.PityShare) * raw / q;
        }

        /** 표에 적힌 그대로의 기대 XP. 천장 보정 전이다 */
        public static double TableXpPerPull
        {
            get
            {
                double total = 0d;
                for (int i = 0; i < Chances.Length && i < XpOf.Length; i++)
                    total += Chances[i] * XpOf[i];
                return total;
            }
        }

        /** 한 번의 뽑기가 내는 기대 XP. **천장에 눌린 값이다** */
        public static double ExpectedXpPerPull { get { return Suppressed(TableXpPerPull); } }

        /** 회당 ★4(해금) 확률. 천장이 밀어 올린 값이라 표보다 크다 */
        public static double EffectiveUnlockChance { get { return GachaCurve.EffectiveRarityChance; } }

        /** 회당 ★5(개안) 확률. **표와 정확히 같다** - 천장은 전설을 훔치지 않는다 */
        public static double EffectiveAwakenChance { get { return GachaCurve.EffectiveLegendaryChance; } }

        /** ★4 이상 하나당 실제 뽑기 수. 천장을 접은 값 */
        public static double ExpectedPullsPerUnlock { get { return GachaCurve.ExpectedPullsPerEpic; } }

        // ---------------------------------------------------------------- 스킬 XP

        /**
         * @brief 레벨 1 -> 2 에 드는 스킬 XP.
         *
         * 크기를 고른 방식이 골드와 반대다. 골드 비용은 **해금 시점의 수입
         * 규모**에 맞춰야 뜻이 생기지만(SkillSpec.BaseCost - 절대 액수는
         * 아무 뜻이 없다), XP는 뽑기에서만 나오는 재화라 기준이 하나뿐이다:
         * **몇 번 뽑아야 한 오의가 상한에 닿는가.**
         *
         * 한 오의를 상한까지 미는 데 682 XP이고(TotalXpToCap) 한 번의 뽑기가
         * 기대 18.1 XP를 내므로(ExpectedXpPerPull) **약 38회**다. 천장이
         * 30회이므로 "천장 한 바퀴에 오의 하나가 거의 상한"이 되고, 그 리듬이
         * 47단계가 10연 셋으로 잡아 둔 리듬 위에 그대로 선다.
         *
         * 더 싸게 두면 첫 10연에 오의 하나가 상한에 닿아 남은 구간이 전부
         * 잉여가 된다 - 21단계 골드 축이 "해금 즉시 상한"으로 겪은 자리이고,
         * SkillCurve.CostPerRate가 같은 이유로 같은 조정을 했다.
         */
        public const double XpBase = 12d;

        /**
         * @brief 레벨당 XP 증가율. **골드 비용(1.45)보다 완만하다.**
         *
         * 두 재화가 같은 계단을 오를 이유가 없다. 골드 비용이 가파른 것은
         * 이 축이 **여섯 축과 경쟁**하기 때문이고(SkillCurve.CostGrowth -
         * 상한 있는 축은 반드시 빨리 팔린다), 그 경쟁이 XP에는 없다 - XP는
         * 오의 말고 쓸 곳이 없다.
         *
         * 1.45로 두면 1.45^11 = 60이라 마지막 한 칸이 첫 칸의 예순 배가 되고,
         * 뽑기의 ★1(XP 6)이 후반에 **소수점 아래**가 된다. 꽝 방지 바닥이
         * 사실상 꽝이 되는 것이라 그것은 사다리를 아래에서부터 무너뜨린다.
         * 1.30이면 마지막 칸이 첫 칸의 열넷 배이고, ★1 하나가 그 칸의
         * 3.6%라 여전히 게이지가 움직인다.
         */
        public const double XpGrowth = 1.30d;

        /**
         * @brief `level`에서 다음 레벨로 가는 데 드는 XP. 상한에서는 0이다.
         *
         * 정수로 올림하는 이유는 화면 때문이다 - 게이지 옆에 "12 / 16"이
         * 떠야 하는데 소수가 섞이면 그 줄이 두 자리씩 흔들린다. 올림(내림이
         * 아니라)인 것은 0이 되는 칸을 만들지 않기 위해서다.
         */
        public static long XpToNextLevel(int level)
        {
            if (level < 1) level = 1;
            if (level >= SkillCurve.MaxLevel) return 0L;
            return (long)Math.Ceiling(XpBase * Math.Pow(XpGrowth, level - 1));
        }

        /** 한 오의를 Lv.1에서 상한까지 미는 총 XP */
        public static long TotalXpToCap
        {
            get
            {
                long total = 0L;
                for (int level = 1; level < SkillCurve.MaxLevel; level++)
                    total += XpToNextLevel(level);
                return total;
            }
        }

        /** `level`에서 상한까지 남은 XP. 개안이 건너뛰는 양이기도 하다 */
        public static long XpFromLevelToCap(int level)
        {
            long total = 0L;
            for (int l = Math.Max(1, level); l < SkillCurve.MaxLevel; l++)
                total += XpToNextLevel(l);
            return total;
        }

        // ---------------------------------------------------------------- 해금 몫

        /**
         * @brief 가챠 몫 오의의 **획득 순서**. 표 순서 그대로다.
         *
         * 44단계 요도가 "카탈로그 순서가 곧 획득 순서"로 정한 규칙 그대로다
         * (GachaCurve.EssenceTargetFor). 무작위로 고르지 않는 이유는 두
         * 가지다 - 무작위면 "무엇을 뽑으려 하는지"를 화면이 말할 수 없고
         * (47단계가 전설 줄에 미보유 이름을 적은 것과 같은 판단), 둘째 오의를
         * 먼저 받은 플레이어와 아닌 플레이어가 다른 세계에 살게 된다.
         *
         * ★4는 두 번만 값을 갖는다. 그 뒤로는 사다리를 미끄러져 ★3이 되고
         * (SlideFor), 그것이 이 축의 **재고가 유한하다**는 사실이 표에 적힌
         * 자리다 - 47단계가 혼격 상한을 바퀴로 늘려 재고를 만든 것과 반대
         * 방향의 선택이고, 그 대가는 아래 IsSoldOut이 화면에 그대로 적는다.
         */
        public static readonly string[] UnlockOrder =
        {
            SkillCatalog.BloodBurstId,
            SkillCatalog.BloodWhipId
        };

        /** 이 오의가 뽑기로만 열리는가. 카탈로그의 플래그를 이름으로 빌려준다 */
        public static bool IsGachaGated(int index)
        {
            return index >= 0 && index < SkillCatalog.Count
                && SkillCatalog.Skills[index].GachaGated;
        }

        /**
         * @brief 다음에 열릴 가챠 몫 오의. 둘 다 열려 있으면 -1.
         *
         * @param owned 카탈로그 인덱스 비트마스크. 비트가 서 있으면 보유
         */
        public static int UnlockTargetFor(int owned)
        {
            for (int i = 0; i < UnlockOrder.Length; i++)
            {
                int index = SkillCatalog.IndexOf(UnlockOrder[i]);
                if (index < 0) continue;
                if ((owned & (1 << index)) != 0) continue;
                return index;
            }
            return -1;
        }

        /** 가챠 몫 오의를 전부 열었는가 */
        public static bool AllUnlocked(int owned)
        {
            return UnlockTargetFor(owned) < 0;
        }
    }
}
