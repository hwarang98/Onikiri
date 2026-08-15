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
        public static int UnlockStage { get { return ShopCurve.UnlockStage; } }

        public static bool IsUnlockedAt(int stage)
        {
            return stage >= UnlockStage;
        }

        /**
         * @brief **보석으로 뽑을 수 있게 되는 칸.** 배너가 서는 칸과 다르다.
         *
         * ## 왜 갈라야 했는가 - 실측이 강제했다
         *
         * 처음에는 하나였다. 배너가 st14에 서면 거기서부터 사고 팔면 된다고
         * 봤는데, 시뮬레이션이 그 세계를 거절했다:
         *
         *     무과금이 퀘스트 보석을 전부 이 배너에 쓰면
         *     st52에서 보스 여유가 1.343 -> 바닥(1.40)을 뚫는다
         *
         * 47단계부터 이 축의 계약은 "**보석을 여기 쓰면 느려지지만 막히지는
         * 않는다**"였다(SpendingFreeToPlayGemsHere_CostsProgressButNeverBreaksTheFloor).
         * 느려지는 것은 선택이고 막히는 것은 함정이다. 상점을 스물일곱 칸
         * 앞당기면서 그 차이가 무너졌다 - 코리더(st1~30)는 보석 여유가 가장
         * 얇은 구간이라, 거기서 빠져나간 보석은 나중에 못 메운다.
         *
         * ## 그래서 배너와 지갑을 따로 연다
         *
         *     st14  배너가 선다. **무료 10연 + 일일 무료**가 열린다
         *     st41  **보석 구매**가 열린다 (요도 배너와 같은 칸)
         *
         * 재설계가 원한 것은 "폭을 일찍 판다"와 "무과금이 일찍 닿는다" 둘이고,
         * 그 둘은 무료 경로만으로 성립한다 - 애초에 이 축의 f2p 경로가 일일
         * 무료라고 적어 둔 자리다(FreePullsPerDay 주석). 보석 구매는 가속이지
         * 경로가 아니므로, 가속만 st41에 남겨도 설계가 안 무너진다.
         *
         * 그리고 그 st41은 요도 배너가 서는 칸이다 - 두 배너의 지갑이 같은
         * 칸에서 동시에 열리므로, 44단계가 잰 보석 배정표가 한 번만 흔들린다.
         */
        public static int PullUnlockStage { get { return GachaCurve.UnlockStage; } }

        /** 보석으로 뽑을 수 있는가. 배너가 보이는 것과 다른 질문이다 */
        public static bool CanBuyAt(int stage)
        {
            return stage >= PullUnlockStage;
        }

        // ------------------------------------------------------- 15종 재설계: 전용 규칙

        /**
         * @brief ★5 하드 천장. **이 배너만의 규칙이다.**
         *
         * ## 왜 필요한가
         *
         * 47단계의 소프트 천장(30회)은 "★4 **이상**"을 보장한다. ★5는 그 안에
         * 얹혀 있을 뿐 따로 보장되지 않아서, 표 확률 0.8%면 기대 125회이고
         * 상한이 없다. 400회를 돌아도 안 나올 수 있다는 뜻이다.
         *
         * ★5가 XP 가속(개안)이던 시절에는 그것이 견딜 만했다. 안 나와도
         * 진행으로 도달하는 곳이 같았기 때문이다. 이제 ★5가 **귀오의 넷의
         * 유일한 출처**가 되면서 상한이 없는 것이 곧 "영영 못 볼 수도 있다"가
         * 된다 - 그것은 수집 목표가 아니라 도박이다.
         *
         * ## 왜 100인가
         *
         * 이 값으로 자르면 ★5 간격의 기대가 69.01회가 되고(SkillGachaPityModel),
         * 귀오의 넷이 기대 276회 · **최악 400회**로 닫힌다. 일일 무료만 도는
         * 무과금에게 기대 266일 · 최악 390일이다 - 장기 목표로는 길지만
         * **끝이 있는** 수다.
         *
         * 소프트 천장(30)의 세 배가 조금 넘는 것도 값이다. 두 게이지가 화면에
         * 나란히 서는데 배수가 정수에 가까우면 "★4 세 번쯤에 ★5 한 번"이라는
         * 감각이 생긴다.
         */
        public const int AwakenPityPulls = 100;

        public static int PullsUntilAwakenPity(int counter)
        {
            int left = AwakenPityPulls - counter;
            return left < 0 ? 0 : left;
        }

        /**
         * @brief 배너의 천장 줄. **두 약속을 한 줄에 나란히 적는다.**
         *
         * 요도 배너는 천장이 하나라 `GachaCurve.PityText` 하나면 됐다. 이쪽은
         * 둘이고, **하나만 적으면 안 적은 쪽이 없는 규칙이 된다** - ★5 게이지를
         * 감추면 "100회 안에 반드시"라는 이 재설계의 가장 큰 약속이 화면 어디에도
         * 없다.
         *
         * 누적 횟수를 뺀 자리에 ★5를 넣었다. 셋을 다 적으면 칸을 넘치는데
         * (빌드의 CheckLine이 잡는다), 셋 중 버릴 것을 고르면 누적이다 -
         * 그것은 지나온 기록이고 나머지 둘은 **앞으로의 약속**이다.
         */
        public static string PityText(int softLeft, int hardLeft)
        {
            return GachaCurve.GradeNames[(int)GachaCurve.Grade.Epic] + " 확정 " + softLeft
                 + "회  ·  " + GachaCurve.GradeNames[(int)GachaCurve.Grade.Legendary]
                 + " 확정 " + hardLeft + "회";
        }

        /**
         * @brief 온보딩 무료 뽑기 수. st14에 한 번만 열린다.
         *
         * 10연인 것은 `GachaCurve.TenPullCount`와 같은 수이지만 **빌려 쓰지
         * 않는다.** 저쪽은 상품(225젬짜리 묶음)의 크기이고 이쪽은 선물의
         * 크기다 - 상품 가격을 조정하는 날 선물까지 따라 움직이면 안 된다.
         */
        public const int IntroPullCount = 10;

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

        /**
         * @brief `from`에서 **아래로 걸어** `to`에 닿는가.
         *
         * ## 이 함수가 없어서 게임이 멈췄다
         *
         * 결과 팝업이 미끄러진 경로를 적으려고 `Rolled`에서 `Outcome`까지
         * `SlideFor`로 걸었다. 사다리는 **아래로만** 가는데(개안 -> 해금 ->
         * XP, 그 아래는 자기 자신), **천장은 결과를 위로 덮는다** -
         * `SkillGachaSystem.RollOnce`가 낮게 굴린 회차를 개안으로 바꾼다.
         *
         * 그러면 `Rolled`(낮음)에서 `Outcome`(높음)에 영영 못 닿고, 바닥
         * 칸이 자기 자신을 돌려주므로 루프가 안 끝난다. 소프트 천장이
         * 서른 회마다 서니 십연을 몇 번만 돌려도 걸린다.
         *
         * 걷기 전에 물어봐야 하는 것은 "미끄러졌는가"이고, 그 답을 아는
         * 것은 **사다리 자신**이다. 화면이 걸음 수를 세는 방식으로 다시
         * 짜면 같은 실수가 다른 화면에서 또 난다.
         *
         * 같은 칸이면 참이다 - 걷지 않고 도착한 것도 도착이고, 호출부가
         * 그 경우를 따로 안 적어도 되게 한다.
         */
        public static bool SlidesTo(Outcome from, Outcome to)
        {
            var at = from;

            // 사다리 길이는 셋(개안·해금·XP)이라 넷이면 넉넉하다. 상수를
            // 두는 이유는 SlideFor가 바닥에서 자기 자신을 돌려주기 때문이다 -
            // 종료를 그 성질에만 맡기면 사다리가 길어진 날 여기가 다시 위험해진다
            for (int guard = 0; guard < 4; guard++)
            {
                if (at == to) return true;

                var next = SlideFor(at);
                if (next == at) return false;   // 바닥이다. 더 내려갈 곳이 없다

                at = next;
            }
            return false;
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

        /**
         * @brief 이중 천장의 **장기 평균**. 정적 생성자에서 한 번만 푼다.
         *
         * ## 왜 닫힌 식을 못 쓰게 됐는가
         *
         * 47단계의 식은 천장이 하나일 때 유도됐다. ★5 하드 천장이 붙으면
         * 그 전제가 깨진다 - 하드가 만든 ★5가 **소프트 카운터도 함께** 0으로
         * 되돌리므로(★5는 ★4 이상이다) 30회 천장이 발동할 기회를 ★5가
         * 가로챈다. 두 과정이 얽히면 곱셈으로 못 풀고 상태를 세어야 한다.
         *
         * 그 결과 ★4가 4.145% -> 3.899%로 내려가고 ★5가 0.800% -> 1.449%로
         * 오른다. 합계는 4.945% -> 5.348%로 늘어난다.
         *
         * ## ⚠ 이 값은 **장기 보고 전용**이다
         *
         * 실제 여정의 초반은 이 평균과 전혀 다르다 - 100회차의 ★5는 45.589%다
         * (하드 천장이 그 회차에 몰려 있다). 총변동거리가 1e-3 아래로 내려가는
         * 데 859회가 걸리는데 귀오의 넷의 기대가 276회이므로, **여정 전체가
         * 과도기 안에 있다.**
         *
         * 여정을 재는 쪽(StageSimulation)은 이 값이 아니라
         * `SkillGachaPityModel.Advance`로 상태를 전진시켜야 한다.
         */
        private static readonly SkillGachaPityModel.Rates SteadyRates =
            SkillGachaPityModel.SolveSteady(PityPulls, AwakenPityPulls);

        /** 한 번의 뽑기가 내는 기대 XP (장기 평균). 수집기 기준이라 ★4·★5는 0이다 */
        public static double ExpectedXpPerPull { get { return SteadyRates.Xp; } }

        /** 회당 ★4(표준 해금) 확률. 소프트 천장이 밀어 올리고 하드 천장이 조금 깎았다 */
        public static double EffectiveUnlockChance { get { return SteadyRates.UnlockChance; } }

        /** 회당 ★5(귀오의) 확률. **표(0.8%)의 두 배 가까이다** - 하드 천장의 몫 */
        public static double EffectiveAwakenChance { get { return SteadyRates.AwakenChance; } }

        /** ★4 하나당 실제 뽑기 수 (장기 평균) */
        public static double ExpectedPullsPerUnlock
        {
            get { return SkillGachaPityModel.PullsPerUnlock(SteadyRates); }
        }

        /** ★5 하나당 실제 뽑기 수 (장기 평균). 귀오의 넷이면 이 값의 네 배다 */
        public static double ExpectedPullsPerAwaken
        {
            get { return SkillGachaPityModel.PullsPerAwaken(SteadyRates); }
        }

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
        /**
         * @brief ★4가 여는 **표준 해금 풀** (5종). 머리가 혈조다.
         *
         * ## 왜 혈조가 머리인가
         *
         * 온보딩 무료 10연이 표준 해금을 못 뽑았을 때 주는 보상이 이 배열의
         * **첫 미보유**다(SkillGachaSystem.ClaimIntro). 자연 뽑기가 여는 것도
         * 같은 함수를 지난다 - 즉 두 경로가 한 데이터를 읽으므로 "다음 해금"
         * 문구와 실제 결과가 갈릴 수가 없다.
         *
         * 그 머리에 혈조를 두는 이유는 **첫 오의의 체감**이다. 쿨 2.5초로
         * 열다섯 중 가장 짧아서 해금 즉시 전투 리듬이 눈에 띄게 빨라진다.
         * 혈폭(9.5초)을 앞에 두면 첫 오의가 "가끔 큰 거 한 방"이라 안 읽힌다.
         *
         * 47단계의 순서(혈폭 -> 혈조)를 뒤집는 것이지만 잃는 것이 없다 -
         * 그때는 둘뿐이라 순서가 곧 전부였고, 지금은 다섯 중 앞의 둘이다.
         */
        public static readonly string[] StandardUnlockOrder =
        {
            SkillCatalog.BloodWhipId,    // 혈조    2.5초 - 첫 오의의 체감이 가장 크다
            SkillCatalog.BloodBurstId,   // 혈폭
            SkillCatalog.DeepThrustId,   // 심격
            SkillCatalog.MoonArcId,      // 회월참
            SkillCatalog.SwordFieldId    // 검진
        };

        /**
         * @brief ★5가 여는 **귀오의 해금 풀** (4종).
         *
         * ## 왜 배열이 둘이어야 하는가
         *
         * 아홉을 한 배열에 넣고 싶어진다 - `UnlockTargetFor` 하나로 끝나기
         * 때문이다. 그런데 그 함수는 앞에서부터 첫 미보유를 돌려주므로,
         * 한 배열이면 **★5가 ★4의 재고를 먼저 가져간다.** 운 좋게 전설을
         * 뽑은 플레이어가 받는 것이 귀오의가 아니라 혈조가 된다.
         *
         * 재고 표시도 갈려야 한다. "★4는 소진, ★5는 남음"이 정상 상태이고
         * 화면이 그 둘을 따로 말한다 - 한 배열이면 그 문장을 쓸 수가 없다.
         *
         * 순서를 고정하는 이유는 44단계 요도와 같다. 무작위면 "무엇을 뽑으려
         * 하는지"를 화면이 말할 수 없고, 셋째를 먼저 받은 플레이어와 아닌
         * 플레이어가 다른 세계에 살게 된다.
         */
        public static readonly string[] OniSecretUnlockOrder =
        {
            SkillCatalog.OniDanceId,     // 귀신난무
            SkillCatalog.AbyssPullId,    // 나락인력
            SkillCatalog.DecapitateId,   // 참수
            SkillCatalog.OniAdventId     // 귀왕강림 - 수집의 마지막 칸
        };

        /** 이 오의가 뽑기로만 열리는가. 카탈로그의 플래그를 이름으로 빌려준다 */
        public static bool IsGachaGated(int index)
        {
            return index >= 0 && index < SkillCatalog.Count
                && SkillCatalog.Skills[index].GachaGated;
        }

        /**
         * @brief 배열에서 첫 미보유를 찾는다. 두 풀이 같은 규칙을 지난다.
         *
         * @param owned 카탈로그 인덱스 비트마스크. 비트가 서 있으면 보유
         */
        private static int TargetIn(string[] order, int owned)
        {
            for (int i = 0; i < order.Length; i++)
            {
                int index = SkillCatalog.IndexOf(order[i]);
                if (index < 0) continue;
                if ((owned & (1 << index)) != 0) continue;
                return index;
            }
            return -1;
        }

        /** ★4가 다음에 열 표준 오의. 다 열렸으면 -1 */
        public static int StandardTargetFor(int owned)
        {
            return TargetIn(StandardUnlockOrder, owned);
        }

        /** ★5가 다음에 열 귀오의. 다 열렸으면 -1 */
        public static int OniSecretTargetFor(int owned)
        {
            return TargetIn(OniSecretUnlockOrder, owned);
        }

        /**
         * @brief 표준 다섯을 전부 열었는가. **배너를 닫는 조건이 아니다.**
         *
         * 표준이 소진돼도 귀오의가 남아 있으면 배너는 열려 있고, 둘 다
         * 소진돼도 장착 오의가 미상한이면 여전히 열려 있다
         * (SkillSystem.HasStock). 이 함수가 답하는 것은 "★4가 무엇을
         * 주는가"뿐이다 - 소진 뒤에는 XP로 미끄러진다.
         */
        public static bool StandardSoldOut(int owned)
        {
            return StandardTargetFor(owned) < 0;
        }

        public static bool OniSecretSoldOut(int owned)
        {
            return OniSecretTargetFor(owned) < 0;
        }

        /** 뽑기 몫 아홉을 전부 열었는가 */
        public static bool AllUnlocked(int owned)
        {
            return StandardSoldOut(owned) && OniSecretSoldOut(owned);
        }
    }
}
