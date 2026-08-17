using System;

namespace Onikiri.Progression
{
    /**
     * @brief 요괴 봉인 뽑기(가챠)의 확률표·비용·천장, 그리고 **가속 상한**.
     *
     * ## 이 뽑기는 혼을 팔지 않는다 - 파편을 판다
     *
     * 44단계가 실측으로 알아낸 것이 이 표의 출발점이다: **무과금은 파편에
     * 막힌다.** 혼은 보스 처치로 충분히 들어오고(한 바퀴에 넷), 과금 창
     * (x1.51)의 실제 병목은 합성 재료인 파편이었다. 그래서 뽑기의 주 상품이
     * 파편이고, 이것은 보석 촉매(YodoCurve.ShardPackGems)의 상위 규모
     * 버전이다 - 새 병목을 만들지 않고 **이미 있는 병목**을 겨눈다.
     *
     * 혼을 직접 팔지 않는 이유는 밸런스가 아니라 게임의 기둥이다. 혼은
     * "요괴를 벤 증거"이고(YodoCurve.UnlockStage - 되살아난 요괴만 혼을
     * 남긴다), 그것을 상점에서 사는 순간 도감의 네 줄이 전투가 아니라 결제
     * 기록이 된다. **첫 봉인은 영원히 보스의 혼이 필요하다.**
     *
     * ## 그런데 파편만 팔면 과금에게 파는 것이 없다
     *
     * 44단계의 과금 곡선(촉매 무제한)은 이미 파편에 안 막힌다 - 혼에 막힌다.
     * 파편만 파는 뽑기는 **무과금의 바닥만 밀어 올리고 과금 천장은 한 톨도
     * 못 민다.** 그것은 수익화가 아니라 무료 배포다.
     *
     * 그래서 낮은 확률로 **혼 정수**가 나온다. 혼을 대신하는 재료이고, 규칙
     * 둘이 그것을 기둥 안에 가둔다:
     *
     *   1. **봉인된 자루에만 들어간다.** 티어 0에는 못 쓴다 - 첫 봉인은 여전히
     *      그 요괴를 벤 사람의 것이다
     *   2. **드랍 일정보다 LeadTiers만큼만 앞설 수 있다** (아래 주석)
     *
     * ## 왜 확률인가 - 대요괴 드랍과 정반대의 자리
     *
     * YodoSpec.DropChance가 넷 다 1인 이유는 "대요괴는 한 바퀴에 한 번만
     * 만나므로 빗나간 한 번이 40스테이지를 지운다"였다. 뽑기는 그 문장의
     * 반대편이다 - **싸게 반복할 수 있는 출처**라서 확률이 긴장이 되고, 같은
     * 이유로 파편 등급이 셋(소·대·잭팟)이다. 한 번의 결과가 진행을 지우지
     * 않는 크기일 때만 변동이 재미가 된다.
     *
     * ## 47단계 - 위의 표가 사다리의 아래 절반이 됐다
     *
     * 46단계의 표는 **파편 셋 + 혼 정수**였고, 그 구성에 구조적인 구멍이
     * 하나 있었다: 보석당 파편이 촉매의 66%라(PullCostGems 주석) **파편만
     * 보면 뽑을 이유가 없었다.** 46단계 보고서가 그것을 "혼 정수가 그
     * 부등호를 뒤집는다"로 넘겼는데, 혼 정수는 리드 상한(LeadTiers)에
     * 묶여 있어 재고가 얇다 - 상한에 닿은 뒤로는 뽑기가 촉매보다 순수하게
     * 나쁜 버튼이 된다.
     *
     * 47단계는 표를 갈아엎지 않고 **등급으로 승격**했다. 위의 넷은 자리도
     * 값도 확률도 거의 그대로 있고, 그 위에 두 칸이 얹혔다:
     *
     *   ★1 일반   파편 6           꽝 방지 바닥
     *   ★2 고급   파편 20 / 70     기존 두 줄. 잭팟이 이 등급의 위쪽이다
     *   ★3 희귀   혼 정수          기존 3%. 티어를 한 바퀴 앞당긴다
     *   ★4 영웅   상위 혼          **신규** - 혼격을 한 칸 올린다
     *   ★5 전설   가챠 전용 妖刀   **신규** - 다섯 번째·여섯 번째 칼
     *
     * ★2가 두 줄인 것은 46단계의 등비 간격(6 / 20 / 70, 약 x3.4)을 지우지
     * 않기 위해서다. 그 간격은 "잭팟이 촉매 세 묶음보다 커야 한 번의 결과가
     * 사건으로 읽힌다"는 판단이었고, 사다리가 생겼다고 그 판단이 틀려지지
     * 않는다 - 등급은 **드라마의 층**이고 결과는 **보상의 층**이라 일대일일
     * 필요가 없다.
     *
     * 그리고 천장이 승격했다. "혼 정수 확정"이 아니라 **"★4 이상 확정"**이다
     * (PityPulls 주석) - 사다리가 생겼으므로 천장이 지키는 것도 사다리의
     * 위쪽이어야 한다.
     */
    public static class GachaCurve
    {
        // ---------------------------------------------------------------- 해금

        /**
         * @brief 뽑기가 열리는 스테이지. **요도와 같은 칸이다.**
         *
         * 값을 여기 적지 않고 끌어오는 이유는 이 뽑기가 파는 것이 전부 요도의
         * 재료이기 때문이다 - 파편도 혼 정수도 요도가 없으면 쓸 데가 없고,
         * 두 값이 갈리면 "뽑았는데 넣을 칼이 없다"가 된다.
         *
         * 밸런스에도 같은 것을 요구한다. 조율 코리더(st1~30)와 가속 구간
         * (31~50)은 이 스텝에서도 **비트 단위로** 움직이면 안 되는데, 뽑기가
         * 요도 해금과 같은 칸에서 열리면 그 불변이 계수가 아니라 **구조로**
         * 지켜진다 - 44·45단계가 같은 자리에서 같은 방법을 썼다.
         */
        public static int UnlockStage { get { return YodoCurve.UnlockStage; } }

        public static bool IsUnlockedAt(int stage)
        {
            return stage >= UnlockStage;
        }

        // ---------------------------------------------------------------- 비용

        /**
         * @brief 단연 한 번의 보석 값.
         *
         * 촉매(보석 30 -> 파편 20)보다 싸다. 그런데 **보석당 파편은 더 적다**
         * (0.46 대 0.67) - 그것이 두 버튼이 같이 사는 방법이다:
         *
         *   촉매   확정. 지금 파편 N개가 모자랄 때 정확히 그만큼을 산다
         *   뽑기   변동. 기대값은 낮지만 잭팟과 혼 정수가 붙어 있다
         *
         * 뽑기의 기대 파편이 촉매보다 높으면 촉매가 죽고, 낮은데 혼 정수까지
         * 없으면 뽑기가 죽는다. 44단계가 촉매를 살리려고 ShardsPerElite를
         * 25에서 14로 내린 것과 같은 종류의 조정이고, 이번에는 **가격이 아니라
         * 상품 구성**이 그 일을 한다.
         */
        public const int PullCostGems = 25;

        /** 10연에 묶이는 뽑기 수 */
        public const int TenPullCount = 10;

        /**
         * @brief 10연의 보석 값. 단연 열 번의 90%.
         *
         * 할인이 있는 이유는 10연이 **천장을 향해 가는 단위**이기 때문이다
         * (PityPulls = 30 = 10연 셋). 할인이 없으면 10연은 손가락을 아끼는
         * 버튼일 뿐이고, 그러면 "세 번이면 확정"이라는 리듬이 화면에서 사라진다.
         */
        public const int TenPullCostGems = PullCostGems * TenPullCount * 9 / 10;

        // ---------------------------------------------------------------- 확률표

        /**
         * @brief 희귀도 등급. **드라마의 층이다 - 보상의 층은 Outcome이다.**
         *
         * 둘이 일대일이 아닌 이유는 ★2에 두 줄(파편 20 / 70)이 있기
         * 때문이고, 그 이유는 파일 머리 주석에 있다. 등급이 하는 일은
         * 화면에서 **색과 연출을 정하는 것**이고, 결과가 하는 일은 무엇이
         * 지갑에 들어오는가를 정하는 것이다.
         */
        public enum Grade
        {
            Common,     // ★1 일반
            Uncommon,   // ★2 고급
            Rare,       // ★3 희귀
            Epic,       // ★4 영웅
            Legendary   // ★5 전설
        }

        public const int GradeCount = 5;

        /** 화면에 뜨는 등급 이름. 확률표와 결과 판이 같은 배열에서 나온다 */
        public static readonly string[] GradeNames = { "일반", "고급", "희귀", "영웅", "전설" };

        /**
         * @brief 등급을 별로 적는다. "★★★☆☆"
         *
         * 별 두 글자(U+2605 / U+2606)는 Galmuri11에 이미 있다. 등급을 색
         * 하나로만 말하면 색약에게 사다리가 통째로 사라지고, 그 판단은
         * 이 프로젝트가 게이지에서 이미 한 번 했다(38b - 그라데이션 금지).
         */
        /**
         * @brief 배너 아래 진행 줄. **빌더와 런타임이 같은 함수를 지난다.**
         *
         * 46단계에는 두 곳에 따로 적혀 있었고 우연히 같은 말이었다(둘 다
         * "혼 정수 확정까지"). 47단계에 천장이 지키는 것이 ★4로 승격하면서
         * 그 우연이 끝났다 - 한쪽만 고치면 씬을 연 순간과 첫 갱신 사이에
         * 문장이 바뀌고, 그 증상은 "확률표가 잠깐 다른 말을 한다"다.
         *
         * 런타임에 두는 이유는 빌더가 런타임을 참조할 수 있고 그 반대는
         * 아니기 때문이다(YodoSprites가 런타임 어셈블리에 사는 것과 같은
         * 규칙).
         */
        public static string PityText(int left, int total)
        {
            return GradeNames[(int)Grade.Epic] + " 이상 확정까지 " + left
                 + "회  ·  누적 " + total + "회";
        }

        public static string StarsFor(Grade grade)
        {
            int filled = (int)grade + 1;
            var text = new System.Text.StringBuilder(GradeCount);
            for (int i = 0; i < GradeCount; i++) text.Append(i < filled ? '★' : '☆');
            return text.ToString();
        }

        /**
         * @brief 한 번의 뽑기가 내는 결과.
         *
         * 여섯 중 하나이고 **꽝이 없다.** 모든 뽑기가 최소한 파편 소를 준다 -
         * 방치형에서 아무것도 안 주는 결과는 긴장이 아니라 짜증이고, 이 뽑기가
         * 파는 것이 "언젠가"가 아니라 "지금 파편"이기 때문이다.
         *
         * **순서가 확률의 내림차순이고, 그것이 계약이다.** Roll이 누적으로
         * 훑으므로 순서를 바꾸면 표가 통째로 어긋난다. 46단계의 넷이 앞에
         * 그대로 있고 뒤에 둘이 붙은 것도 그래서다 - 기존 세 등급의 경계값이
         * 한 비트도 안 움직인다.
         */
        public enum Outcome
        {
            ShardSmall,
            ShardLarge,
            ShardJackpot,
            SoulEssence,
            SoulRarity,
            LegendaryBlade
        }

        /**
         * @brief 결과 -> 등급. 화면의 색·별·연출이 이 표를 지난다.
         *
         * 파편 대와 잭팟이 같은 ★2인 것이 이 표의 유일한 다대일이다
         * (머리 주석).
         */
        public static readonly Grade[] GradeOf =
        {
            Grade.Common,      // 파편 6
            Grade.Uncommon,    // 파편 20
            Grade.Uncommon,    // 파편 70 (잭팟 - ★2의 위쪽)
            Grade.Rare,        // 혼 정수
            Grade.Epic,        // 상위 혼
            Grade.Legendary    // 전설 妖刀
        };

        /**
         * @brief 확률표. **공개한다** - 화면에 그대로 뜬다(ShopPanel).
         *
         * 합이 정확히 1이어야 하고, GachaTests.Table_SumsToOne이 잰다.
         *
         * ## 46단계에서 무엇이 움직였는가 - 2.9%p뿐이다
         *
         *     ★1 파편 소   74.0 -> 71.6   (-2.4)
         *     ★2 파편 대   18.0 -> 17.5   (-0.5)
         *     ★2 잭팟       5.0 ->  5.0   (그대로)
         *     ★3 혼 정수    3.0 ->  3.0   (그대로)
         *     ★4 상위 혼      -  ->  2.1   (신규)
         *     ★5 전설         -  ->  0.8   (신규)
         *
         * **★3을 안 건드린 것이 이 표의 중심이다.** 혼 정수는 티어를
         * 앞당기고, 티어는 심층 밴드가 재는 값이다 - 그 확률을 움직이면
         * 46단계의 재기준(19.2/13.8/11.0)이 이 스텝의 신규 축과 뒤섞여
         * "무엇이 천장을 밀었는가"를 나눠 잴 수 없게 된다. 새 몫은 전부
         * ★1·★2에서 나왔다.
         *
         * ## ★4와 ★5의 비 - 합을 고정하고 안에서 나눴다
         *
         * 둘의 합(2.9%)이 천장이 지키는 값이므로(EpicOrBetterChance) 그
         * **합을 먼저 정하고 안에서 나눴다.** 처음에 2.4 / 0.5로 뒀다가
         * 하네스가 잡았다 - 계약 구간(st51~200) 안에서 보석 무제한
         * 플레이어의 뽑기 수가 113회라 전설 기대가 0.57자루이고, 그러면
         * ★5가 그 구간에서 **한 번도 안 나오는 쪽이 더 흔하다.** 죽은
         * 버튼 검사가 +0.28%를 냈다.
         *
         * 2.1 / 0.8로 옮기면 기대가 0.90자루가 되고 천장의 산수는 **한
         * 비트도 안 바뀐다**(합이 그대로다). 사다리의 위쪽 두 칸을 어떻게
         * 나눌지는 천장과 독립인 결정이라는 사실이 이 조정의 근거다.
         *
         * ## ★5가 0.8%인 이유 - 125회에 한 번
         *
         * 10연 열두 번 반이다. 일일 무료만 도는 무과금에게는 넉 달이고,
         * 보석 무제한 플레이어에게도 계약 구간 안에 한 자루 남짓이다
         * (재고가 뽑기 수를 묶는다 - LeadTiers·YodoRarityCurve.CapAt).
         * **한 스텝에 두 자루를 다 모으지 못하는 것이 의도다** - 사양이
         * "소수 바운드"라고 적은 자리이고, 다 모이는 순간 이 축의 재고가
         * 끝난다.
         */
        public static readonly double[] Chances = { 0.716d, 0.175d, 0.05d, 0.03d, 0.021d, 0.008d };

        /** 파편 등급의 값. 위쪽 세 등급 자리는 0이다 - 파편을 주지 않는다 */
        public static readonly int[] ShardsOf = { 6, 20, 70, 0, 0, 0 };

        public static int OutcomeCount { get { return Chances.Length; } }

        public static Grade GradeFor(Outcome outcome)
        {
            int index = (int)outcome;
            if (index < 0 || index >= GradeOf.Length) return Grade.Common;
            return GradeOf[index];
        }

        /** 이 결과가 요도의 상태를 바꾸는가. 파편은 지갑만 채운다 */
        public static bool IsBladeReward(Outcome outcome)
        {
            return outcome == Outcome.SoulEssence
                || outcome == Outcome.SoulRarity
                || outcome == Outcome.LegendaryBlade;
        }

        /**
         * @brief 0~1 난수를 결과로 옮긴다. **천장은 여기 없다.**
         *
         * 천장(PityPulls)은 카운터가 필요하므로 뽑는 쪽(GachaSystem)이
         * 들고 있고, 이 함수는 순수한 표다 - 시뮬레이션이 기댓값을 쓰고
         * 실제가 굴리는 두 경로가 **같은 표**를 지나야 한다는 44단계 규칙
         * (YodoSpec.DropChance)의 연장이다.
         */
        public static Outcome Roll(double value)
        {
            double cumulative = 0d;
            for (int i = 0; i < Chances.Length; i++)
            {
                cumulative += Chances[i];
                if (value < cumulative) return (Outcome)i;
            }
            return Outcome.ShardSmall;
        }

        public static int ShardsFor(Outcome outcome)
        {
            int index = (int)outcome;
            if (index < 0 || index >= ShardsOf.Length) return 0;
            return ShardsOf[index];
        }

        /** 표에 적힌 혼 정수 확률. 천장을 빼고 본 값이다 */
        public static double EssenceChance { get { return Chances[(int)Outcome.SoulEssence]; } }

        /** 표에 적힌 상위 혼(★4) 확률 */
        public static double RarityChance { get { return Chances[(int)Outcome.SoulRarity]; } }

        /** 표에 적힌 전설(★5) 확률 */
        public static double LegendaryChance { get { return Chances[(int)Outcome.LegendaryBlade]; } }

        /**
         * @brief ★4 이상이 나올 확률. **천장이 지키는 것이 이 값이다.**
         *
         * 46단계의 천장은 ★3(혼 정수)을 지켰다. 사다리가 생겼으므로 천장도
         * 승격했다 - 아래 PityPulls 주석.
         */
        public static double EpicOrBetterChance { get { return RarityChance + LegendaryChance; } }

        // ---------------------------------------------------------------- 천장

        /**
         * @brief 소프트 천장. 이 횟수 안에 **★4 이상**이 반드시 나온다.
         *
         * ## 무엇을 막는가 - 46단계에서 한 칸 올라갔다
         *
         * 파편은 매번 나오므로 이 게임의 뽑기에 꽝은 없다. 46단계에서 천장이
         * 막은 것은 "혼 정수(★3)를 한 번도 못 보는 것"이었다. 사다리가
         * 생긴 지금 그 자리는 **★4 이상**이다 - 천장이 지키는 것은 언제나
         * "이 뽑기의 진짜 상품"이어야 하고, ★3은 이제 사다리의 한가운데이지
         * 꼭대기가 아니다.
         *
         * 승격에는 대가가 없다. 30회 안에 ★4+를 보장하면 ★3도 자연 확률로
         * 계속 나오므로(3.0%, 34회에 하나) 46단계의 플레이어가 잃는 것이
         * 없다 - 오히려 같은 30회에 위쪽이 하나 더 붙는다.
         *
         * ## 30을 안 옮긴 이유, 그리고 기대 대기가 그대로인 이유
         *
         * 30은 10연 셋이다. 화면의 진행 표시가 "10연 세 번이면 확정"으로
         * 읽히는 것이 요점이고, 그 리듬이 10연 할인(TenPullCostGems)과 같은
         * 단위 위에 선다 - 사다리가 생겼다고 그 리듬을 옮길 이유는 없다.
         *
         * 그리고 표를 그렇게 잡았다. ★4+ 합이 2.9%라 천장을 접은 실제 대기가
         * **20.2회**이고, 46단계의 ★3 대기(20.0회)와 사실상 같다. 천장이
         * 지키는 대상만 한 칸 올라가고 **손에 잡히는 리듬은 그대로**인 것이
         * 이 표의 설계다.
         *
         * ## f2p 바닥이 신성해서 두는 장치이기도 하다
         *
         * 일일 무료 뽑기는 하루 한 번이므로(FreePullsPerDay) 무과금에게 30회는
         * 한 달이다. 천장이 없으면 그 한 달이 확률에 따라 두 달도 되고, 그
         * 편차는 무과금에게 "이 시스템은 나를 위한 것이 아니다"로 읽힌다.
         * 극단적 streak를 자르는 것이 곧 바닥을 지키는 일이다.
         */
        public const int PityPulls = 30;

        /**
         * @brief 천장까지 몇 번 남았는가. counter는 마지막 ★4+ 뒤의 뽑기 수.
         */
        public static int PullsUntilPity(int counter)
        {
            int left = PityPulls - counter;
            return left < 0 ? 0 : left;
        }

        /**
         * @brief 천장을 포함한 **★4 이상 하나당 실제 뽑기 수**.
         *
         * 표의 확률(2.9%)만 보면 34.5회지만 30회에서 잘리므로 실제는 그보다
         * 짧다.
         *
         *   E[뽑기] = Σ(k=1..N-1) k·p·q^(k-1) + N·q^(N-1)
         *
         * 닫힌 식으로 접으면 (1 - q^N) / p 이다(기하분포의 절단 기댓값).
         */
        public static double ExpectedPullsPerEpic
        {
            get
            {
                double p = EpicOrBetterChance;
                if (p <= 0d) return PityPulls;
                double q = 1d - p;
                return (1d - Math.Pow(q, PityPulls)) / p;
            }
        }

        /**
         * @brief 천장이 표를 눌러 만드는 **실효 확률**들.
         *
         * ## 왜 표의 값을 그대로 못 쓰는가
         *
         * 천장이 터지는 뽑기는 그 회차의 굴림을 **덮어쓴다**. 30회째에
         * 파편 소가 나왔어도 결과는 ★4이므로, 아래 등급들은 천장이 터지는
         * 만큼(1/E) 실제로는 덜 나온다. 46단계에는 이 보정이 없었다 -
         * 천장이 ★3을 줬고 ★3이 표의 마지막 줄이라, 덮어쓰이는 것이
         * 파편뿐이고 그 차이가 밸런스에 안 닿았다.
         *
         * 지금은 ★3이 사다리 한가운데라 **덮어쓰이는 쪽**이다. 보정 없이
         * 표의 3%를 쓰면 시뮬레이션이 요도 티어를 실제보다 2% 빨리 올리고,
         * 그 2%가 심층 밴드에서 보정과 실제의 차로 남는다.
         *
         * ## 닫힌 식
         *
         * 갱신 주기(cycle)를 "★4+가 나올 때까지"로 잡으면 주기의 길이가
         * E = ExpectedPullsPerEpic이고, 주기마다:
         *
         *   ★4+          정확히 1회      -> 회당 1/E
         *   ★5 전설      c5·E 회         -> 회당 **정확히 c5** (아래 항등식)
         *   ★4 영웅      나머지          -> 회당 1/E - c5
         *   ★3 이하 i    (E-1)·c_i/q 회  -> 회당 (1 - 1/E)·c_i/q
         *
         * 전설 몫이 표의 값과 정확히 같은 것은 우연이 아니다. 천장이
         * 덮어쓰는 것은 "★4+가 아닌 굴림"뿐이라 전설은 한 번도 안 잡아먹히고,
         * 그 사실이 (1-q^(N-1))/p + q^(N-1) = (1-q^N)/p 라는 항등식으로
         * 떨어진다. **천장은 전설을 훔치지 않는다** - 검사가 그것을 못 박는다
         * (GachaTests.Pity_NeverStealsALegendary).
         */
        public static double PityShare { get { return 1d / ExpectedPullsPerEpic; } }

        /** 천장에 눌린 뒤의 회당 ★3 확률. 시뮬레이션과 화면이 이것을 쓴다 */
        public static double EffectiveEssenceChance
        {
            get { return SuppressedChance(EssenceChance); }
        }

        /** 회당 ★4(영웅) 확률. 천장이 밀어 올린 값이라 표보다 크다 */
        public static double EffectiveRarityChance
        {
            get
            {
                double value = PityShare - LegendaryChance;
                return value < 0d ? 0d : value;
            }
        }

        /** 회당 ★5 확률. **표와 정확히 같다** - 위 주석의 항등식 */
        public static double EffectiveLegendaryChance { get { return LegendaryChance; } }

        /** ★3 이하 한 줄이 천장에 눌린 뒤의 회당 확률 */
        private static double SuppressedChance(double raw)
        {
            double q = 1d - EpicOrBetterChance;
            if (q <= 0d) return 0d;
            return (1d - PityShare) * raw / q;
        }

        /**
         * @brief 실효 ★3 하나당 뽑기 수. 시뮬레이션의 정수 주기다.
         *
         * 46단계에는 이 이름이 "천장이 접힌 ★3 대기"였고 지금은 "천장이
         * **누른** ★3 대기"다 - 천장이 ★4로 옮겨 갔으므로 ★3에는 더 이상
         * 보장이 없고, 대신 눌림만 남는다.
         */
        public static double ExpectedPullsPerEssence
        {
            get
            {
                double p = EffectiveEssenceChance;
                return p > 0d ? 1d / p : double.PositiveInfinity;
            }
        }

        /** 실효 ★4 하나당 뽑기 수 */
        public static double ExpectedPullsPerRarity
        {
            get
            {
                double p = EffectiveRarityChance;
                return p > 0d ? 1d / p : double.PositiveInfinity;
            }
        }

        /** 실효 ★5 하나당 뽑기 수 */
        public static double ExpectedPullsPerLegendary
        {
            get
            {
                double p = EffectiveLegendaryChance;
                return p > 0d ? 1d / p : double.PositiveInfinity;
            }
        }

        /** 표에 적힌 그대로의 기대 파편. 천장 보정 전이다 - 보고와 검사가 쓴다 */
        public static double TableShardsPerPull
        {
            get
            {
                double total = 0d;
                for (int i = 0; i < Chances.Length; i++) total += Chances[i] * ShardsOf[i];
                return total;
            }
        }

        /**
         * @brief 한 번의 뽑기가 내는 기대 파편. **천장에 눌린 값이다.**
         *
         * 위쪽 세 등급이 넘쳐 파편이 되는 몫은 여기 없다 - 그것은 요도의
         * 상태에 달린 값이라 표가 알 수 없다.
         */
        public static double ExpectedShardsPerPull
        {
            get { return SuppressedChance(TableShardsPerPull); }
        }

        // ---------------------------------------------------------------- 가속 상한

        /**
         * @brief 혼 정수가 드랍 일정보다 앞설 수 있는 티어 수. **한 바퀴다.**
         *
         * ## 이 상수가 이 스텝의 밸런스 전부다
         *
         * 45단계 보고서가 남긴 경고가 이 자리였다 - "가챠가 요도를 더 밀어
         * 올리면 그 스텝이 다시 재기준해야 한다. 이번 재기준이 가챠 몫을 미리
         * 열어 둔 것은 아니다."
         *
         * 상한이 없으면 보석 무제한 플레이어는 st51에 네 자루를 통째로 상한
         * 티어로 올린다. 그러면 두 가지가 동시에 죽는다: 심층 밴드의 천장이
         * 세 배로 뚫리고, **뽑기가 팔 것이 400스테이지 동안 사라진다.**
         * 상한이 곧 재고다.
         *
         * ## 왜 하필 1인가 - 하네스가 셋을 재고 골랐다
         *
         * 티어를 L만큼 앞세웠을 때 요도 축이 DPS에 곱하는 값(티어 x 세트 x
         * 상성 x 영체)의 비를 st100~440에서 쟀다:
         *
         *   L=1   x1.27   (심층 천장 13.82 -> 17.6)
         *   L=2   x1.61   (            -> 22.2)
         *   L=3   x2.03   (            -> 28.1)
         *
         * L=2·3은 밴드를 한 번에 +60%·+100% 밀어야 한다. 45단계의 재기준이
         * +14%였고, 그 크기의 이동이 두어 번 더 쌓이면 심층 밴드는 어떤 것도
         * 막지 않는 숫자가 된다 - 천장은 "과금이 얼마나 앞서도 되는가"의
         * 기록이지 사후 승인 도장이 아니다.
         *
         * L=1에는 그 위에 성질이 하나 더 있다. **비가 st100 이후 평평하다**
         * (1.2966 -> 1.2659). 과금 곡선이 무과금 곡선을 정확히 한 바퀴
         * 평행이동한 모양이라 밴드의 수렴·바닥·코리더가 전부 그대로 상속되고,
         * 재기준이 등급별 판단이 아니라 **한 번의 이동**이 된다.
         *
         * ## 그리고 그 값에 말이 붙는다
         *
         * 리드 1 = 소울 하나 = 정확히 한 바퀴(40스테이지)다. 44단계가
         * "이 축의 속도를 정하는 것은 지갑이 아니라 한 바퀴"라고 적었고,
         * 뽑기는 그 문장을 부정하지 않는다 - **한 바퀴를 앞당길 뿐이다.**
         * 파는 것은 힘이 아니라 시간이라는 44단계의 촉매 규칙이 여기서도
         * 그대로 성립한다.
         */
        public const int LeadTiers = 1;

        /**
         * @brief 리드가 한 칸 더 늘어나는 주기 (바퀴 수). **계약 구간 밖에서만 자란다.**
         *
         * ## 왜 자라야 하는가 - 상한이 곧 재고이기 때문이다
         *
         * 리드가 1로 고정이면 뽑기의 수요가 **네 번**으로 끝난다. 자루마다
         * 정수 하나로 한 바퀴를 앞서고 나면, 그다음 바퀴부터는 자연 드랍이
         * 그 리드를 스스로 유지한다(티어도 +1, 일정도 +1). 하네스가 그것을
         * 그대로 보여줬다 - 뽑기 수가 st100에 79.9로 평평해지고 그 뒤
         * 400스테이지 동안 상점에 팔 것이 없다.
         *
         * ## 왜 하필 5인가 - 계약 구간이 정했다
         *
         * 5바퀴째 혼이 들어오는 것은 st220 언저리다. 심층 **계약 구간**은
         * st51~200이므로(StageSimulationTests.DeepZoneTo) 그 안에서는 리드가
         * 영원히 1이고, 이번 재기준은 **한 번의 이동**으로 끝난다.
         *
         * 4로 두면 계약 구간 끝(st200, 네 바퀴)에서 리드가 2가 되어 천장이
         * 17.66에서 20.65로 한 번 더 뛴다. 그 3할을 계약 구간 안에서 받으면
         * 재기준이 두 겹이 되고, 45단계가 "재기준이 등급별 판단이 아니라 한
         * 번의 이동"이라고 적은 성질이 깨진다.
         *
         * 계약 밖(st201~)에서 천장이 자라는 것은 **아직 어떤 밴드도 지키지
         * 않는 구간의 사실**이라 그대로 적어 둔다 - 45단계가 계약 밖 오의 몫
         * 0.57을 그렇게 처리했고, 밴드를 st500까지 늘리는 스텝이 이 값도
         * 함께 봐야 한다.
         */
        public const int LeadGrowthCycles = 5;

        /**
         * @brief 지금까지 dropped바퀴를 돈 자루의 리드 상한.
         *
         * 바퀴 수로 세는 이유는 이 축의 시계가 스테이지가 아니라 **한 바퀴**
         * 이기 때문이다(YodoCurve 머리 주석). 스테이지로 나누면 세계 순환의
         * 길이가 바뀌는 날 둘이 갈린다.
         */
        public static int LeadTiersAt(int soulsDropped)
        {
            if (LeadGrowthCycles <= 0) return LeadTiers;
            return LeadTiers + soulsDropped / LeadGrowthCycles;
        }

        /**
         * @brief soulIndex번 혼이 stage까지 **떨어졌을** 개수.
         *
         * YodoCurve.SoulsBeforeStage는 "그 스테이지를 싸우는 시점"이라 아직
         * 그 스테이지의 보스를 안 벤 상태다. 여기서 필요한 것은 최전선까지
         * 실제로 받은 수이므로 한 칸 뒤를 본다.
         */
        public static int SoulsDroppedThrough(int soulIndex, int stage)
        {
            return YodoCurve.SoulsBeforeStage(soulIndex, stage + 1);
        }

        /**
         * @brief 이 자루가 혼 정수로 도달할 수 있는 **혼 총량**의 상한.
         *
         * 총량이지 티어가 아니다 - 손에 든 혼도 언젠가 티어가 되므로, 티어만
         * 세면 혼을 쟁여 두는 것으로 상한을 통과할 수 있다. 그러면 상한이
         * 아니라 지연일 뿐이다.
         */
        public static int EssenceSoulCap(int soulIndex, int frontierStage)
        {
            int dropped = SoulsDroppedThrough(soulIndex, frontierStage);
            return dropped + LeadTiersAt(dropped);
        }

        /**
         * @brief 이 자루가 지금 혼 정수를 받을 수 있는가.
         *
         * @param tier   그 자루의 티어 (0이면 미봉인 - 받을 수 없다)
         * @param souls  아직 안 쓴 혼
         */
        public static bool AcceptsEssence(int soulIndex, int frontierStage, int tier, long souls)
        {
            if (tier < 1) return false;                       // 첫 봉인은 보스의 것이다
            if (tier >= YodoCurve.MaxTier) return false;      // 올릴 칸이 없다
            return tier + souls < EssenceSoulCap(soulIndex, frontierStage);
        }

        /**
         * @brief 혼 정수가 갈 자루. 없으면 -1.
         *
         * ## 가장 낮은 티어로 간다 - 타겟팅이 없다
         *
         * 두 가지를 동시에 한다. 하나는 수익화의 선이다 - "원하는 혼을 지정해
         * 뽑는다"는 현금 확정팩의 몫이고(다음 스텝), 보석 뽑기가 그것을 하면
         * 그 상품이 미리 소진된다.
         *
         * 다른 하나는 45단계가 만든 트레이드오프를 지키는 일이다. 상성은
         * 몰아주기를 보상하고 영체는 고르기를 보상하는데(YodoSpiritCurve
         * 머리 주석), 뽑기가 한 자루를 지목할 수 있으면 과금은 언제나
         * 몰아주기를 산다. **뽑기는 넓히고 파편은 몬다** - 두 재화가 서로
         * 다른 빌드를 가리키는 것이 이 스텝이 그 선택에 더한 것이다.
         */
        public static int EssenceTargetFor(int frontierStage, int[] tiers, long[] souls)
        {
            if (tiers == null) return -1;

            int best = -1;
            int bestTier = int.MaxValue;
            int count = Math.Min(YodoCatalog.Count, tiers.Length);

            for (int i = 0; i < count; i++)
            {
                long held = souls != null && i < souls.Length ? souls[i] : 0L;
                if (!AcceptsEssence(i, frontierStage, tiers[i], held)) continue;

                // 같은 티어면 앞의 자루가 이긴다 - 카탈로그 순서가 곧 획득
                // 순서이고(YodoCatalog 머리 주석), 먼저 만난 요괴가 먼저
                // 자라는 것이 도감의 줄 순서와 같은 말을 한다
                if (tiers[i] >= bestTier) continue;

                best = i;
                bestTier = tiers[i];
            }

            return best;
        }

        // -------------------------------------------------- 47단계: ★4·★5의 갈 곳

        /**
         * @brief 상위 혼(★4)이 갈 자루. 없으면 -1.
         *
         * 판정은 YodoRarityCurve가 하고 여기서는 이름만 빌려준다 - 뽑기 쪽
         * 호출부(GachaSystem·StageSimulation)가 상한의 출처를 한 군데서만
         * 찾게 하려는 것이고, 혼 정수가 EssenceTargetFor를 지나는 것과 같은
         * 모양이다.
         */
        public static int RarityTargetFor(int frontierStage, int[] tiers, int[] rarities)
        {
            return YodoRarityCurve.TargetFor(frontierStage, tiers, rarities);
        }

        /** 전설(★5)이 갈 자루. 없으면 -1 */
        public static int LegendaryTargetFor(int[] copies)
        {
            return LegendaryYodoCurve.TargetFor(copies);
        }

        /**
         * @brief 갈 곳 없는 상위 혼이 바뀌는 파편 수. **넘침 규칙의 사다리.**
         *
         * 44단계가 "버려지는 드랍 0"을 만들며 세운 규칙(ShardsPerOverflowSoul
         * 40)의 한 칸 위다. 등급이 위면 넘쳐도 위여야 한다 - ★4가 넘쳐서
         * ★3의 넘침과 같은 값을 내면 사다리가 그 자리에서 평평해진다.
         *
         * ★4는 넘치기 전에 **한 번 더 미끄러진다**: 혼격이 꽉 찼으면 먼저
         * ★3(혼 정수)으로 시도하고, 그것도 막혔을 때만 이 값이 된다
         * (GachaSystem.GrantRarity). 미끄러지는 순서가 사다리의 역순이라
         * 화면의 문구도 그대로 읽힌다("상위 혼 → 혼 정수").
         */
        public static int ShardsPerOverflowRarity
        {
            get { return YodoCurve.ShardsPerOverflowSoul * 2; }
        }

        // ---------------------------------------------------------------- 일일 무료

        /**
         * @brief 하루에 주어지는 무료 뽑기 수. **f2p의 뽑기 접근권이다.**
         *
         * ## 왜 무료 뽑기가 필요한가 - 잠식을 막는 유일한 장치
         *
         * 44단계 실측: 무과금의 보석 잔액은 st50~400 내내 30~190개이고 그
         * 거의 전부가 장비 등급·동료 해금·전직에 배정돼 있다. 뽑기가 그
         * 보석을 가져가면 무과금의 **코어 진행**이 무너진다 - 21단계 골드
         * 축이 겪은 "축은 없는데 보정만 걸려 순손실"이 이번에는 뽑기 쪽에서
         * 재현된다.
         *
         * 그래서 이 게임의 무과금은 **뽑기에 보석을 쓰지 않는 것이 정답**이고
         * (화면이 그것을 말리지는 않는다 - 선택은 플레이어의 것이다), 대신
         * 무료 뽑기로 트리클을 받는다. 하루 한 번은 한 달에 30회, 곧 천장
         * (PityPulls) 하나다.
         *
         * 시뮬레이션에는 이 트리클이 들어가지 않는다 - 거기에는 달력이 없다
         * (44단계가 일일 퀘스트 보석을 뺀 것과 같은 이유). 그래서 보고되는
         * f2p 바닥은 **여전히 하한**이고, 무료 뽑기는 그 위로만 얹힌다.
         */
        public const int FreePullsPerDay = 1;

        /**
         * @brief 무료 뽑기의 리셋 경계. **일일 퀘스트와 같은 KST 04:00.**
         *
         * 값을 여기 적지 않고 QuestSystem에서 끌어오는 이유는 하루의 경계가
         * 게임에 하나여야 하기 때문이다. 두 곳에 적으면 언젠가 갈리고, 그
         * 증상은 "퀘스트는 리셋됐는데 무료 뽑기는 아직"이다 - 플레이어에게는
         * 버그가 아니라 사기로 읽힌다.
         */
        public static DateTime DayOf(DateTime utc)
        {
            return QuestSystem.QuestDayOf(utc);
        }
    }
}
