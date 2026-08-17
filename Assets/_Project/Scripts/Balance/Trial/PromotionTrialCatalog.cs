using System;
using Onikiri.Core;

namespace Onikiri.Progression
{
    /**
     * @brief 귀문(승급전)의 **데이터와 순수 도메인 규칙**. 2단계 산출물이다.
     *
     * ## 이 파일이 아직 하지 않는 것
     *
     * 전투를 돌리지 않는다. `BossFight`에 모드를 얹지도, `StageProgress`를
     * 막지도, 세이브를 올리지도 않는다 - 그 셋은 3단계에서 **하나의 원자적
     * 변경**으로 붙는다. 여기 있는 것은 그때 붙일 표와 판정뿐이고, EditMode가
     * 그 표를 지금 검증한다.
     *
     * 순서를 나눈 이유는 9단계의 교훈이다. 계수를 먼저 지어내고 화면에서 맞추면
     * 계산과 게임 중 어느 쪽이 맞는지 판단할 근거가 사라진다. 표를 먼저 굳히고
     * 전투가 그 표를 읽게 하면, 어긋나는 날 어느 쪽이 틀렸는지가 한 줄로 나온다.
     *
     * ## 승급의 값은 0이다
     *
     * 배수도 이름도 기본 외형도 오라도 **귀문 돌파로 무료로 온다.** 보석·골드가
     * 이 시스템에 한 개도 들어오지 않는다는 것이 v1.5 구조의 전부이고,
     * `PromotionGemCost`/`PromotionGoldCost`가 그 사실을 상수로 붙잡는다.
     *
     * ## 인덱스 규약 - N과 N+1을 섞지 않는다
     *
     *   gateNumber   1..6. 일문이 1, 육문이 6
     *   tier         0..6. 문 N을 돌파하면 tier N이 된다
     *   gateStage    그 문이 서 있는 스테이지. 그 **다음** 스테이지로 넘어갈 때 막는다
     *
     * 그래서 "지나온 문의 수"는 `gateStage < 최전선`의 개수다. **등호가 아니다** -
     * st30에 도착한 것은 st30을 클리어한 것이 아니다. 이 한 글자가 마이그레이션
     * 경계 전부를 정한다.
     */
    public static class PromotionTrialCatalog
    {
        // ---------------------------------------------------------------- 게이트

        /**
         * @brief 귀문 여섯이 서 있는 스테이지.
         *
         * 코리더(st1~30) 안에 문이 없다 - 첫 문이 st30 **클리어**이므로 코리더
         * 서른 줄은 문을 한 번도 지나지 않는다. `EvolutionCharacterizationTests`의
         * 비트 동일이 계수가 아니라 구조로 지켜지는 자리다.
         *
         * 간격이 뒤로 갈수록 벌어지는 것(10/10/20/30/50)은 스테이지 시간이
         * 심층에서 짧아지기 때문이다 - 같은 간격이면 마지막 문 셋이 몇 분
         * 안에 연달아 열린다.
         */
        public static readonly int[] GateStages = { 30, 40, 50, 70, 100, 150 };

        public static int GateCount { get { return GateStages.Length; } }

        /**
         * @brief **무한 구간 안에 서 있는 첫 문.** 심층 수렴 보정이 여기서부터 걸린다.
         *
         * 문 여섯 중 둘(st100 · st150)이 심층 구간(`StageCurve.DeepRampStartStage` = 51)
         * 안에 앉아 있다. 그 둘이 주는 배수는 **영구적인 여유 계단**이라, 보정이 없으면
         * st100 -> st200 여유가 x1.38만큼 통째로 뜨고 무한 구간의 수렴이 깨진다
         * (2단계 실측: 드리프트 +50.4%, 기준 35%).
         *
         * 앞의 넷(st30/40/50/70)은 **여기 없다.** 그 문들은 조율 코리더와 가속 구간
         * 안에서 끝나고, 그 구간의 밴드는 계단을 이미 예산에 넣고 잡혀 있다 -
         * 승인된 체감 10/10/12/12%를 그대로 두는 것이 B-1의 요점이다.
         *
         * 값이 5인 것은 게이트 표와 `DeepRampStartStage`가 함께 정한다:
         * `GateStages[4] = 100 > 51`이고 `GateStages[3] = 70 > 51`인데도 넷째를 뺀
         * 이유는 st70이 **여유 계단이 아니라 가속 구간의 연장**으로 잡혀 있었기
         * 때문이다(2단계 실측에서 st70 계단은 드리프트에 안 들어온다 - 드리프트를
         * 재는 창이 st100 -> st200이다). 창이 바뀌면 이 값도 다시 재야 한다.
         *
         * `PromotionDomainTests.DeepGateBoundary_StartsWhereTheDriftWindowDoes`가
         * 그 사실을 잰다.
         */
        public const int FirstDeepGate = 5;

        /**
         * @brief 이 문이 심층 수렴 보정을 받는가.
         *
         * 판정을 여러 파일에 복제하지 않기 위해 여기 하나만 둔다 - `>=` 비교가
         * 두 벌이 되는 순간 경계가 갈리고, 그 갈림은 st100/st101에서만 나타난다.
         */
        public static bool IsDeepGate(int gateNumber)
        {
            return gateNumber >= FirstDeepGate && gateNumber <= GateCount;
        }

        /**
         * @brief 게이트별 **총 체력 배수** M. 지역 보스 체력에 곱한 값이 적 셋의 합이다.
         *
         * ## 값이 아니라 규칙이 설계다
         *
         *   M(gate) = (45초 - 전환 2회 x 2초) / 보석 하한 플레이어의 그 게이트 보스 처치 시간
         *           = 41초 / 그 시간
         *
         * 앵커가 하한 플레이어인 이유는 귀문이 진행을 막기 때문이다. 곡선 추종에
         * 앵커하면 하한 플레이어가 사문~육문에서 죽고, 그러면 문은 시험이 아니라
         * 벽이 된다. 곡선 추종 쪽은 소프트캡이 잡는다(`SoftCapExponent`).
         *
         * 고정 배수가 아닌 이유는 보스 여유가 심층에서 발산하기 때문이다 -
         * 하나로 두면 뒷문이 앞문보다 압도적으로 쉬워져 사다리의 마지막이 가장
         * 쉬운 시험이 된다.
         *
         * **곡선이 움직이면 이 표를 같은 규칙으로 다시 굽는다.**
         * `PromotionTrialTests.HealthMultiple_TracksTheFortyFiveSecondAnchor`가
         * 실측과 대조하므로, 어긋나면 밴드가 아니라 거기서 먼저 걸린다.
         *
         * ## 2단계에 실제로 다시 구웠다 - 그 이유가 이 주석의 핵심이다
         *
         * v1.5가 승인받은 표는 `1.74 / 1.68 / 1.49 / 1.91 / 4.75 / 5.36`이었다.
         * 그 값들은 **지수 0.42의 세계에서** 잰 것인데, 같은 승인이 지수를
         * 0.00으로 내렸다(A-1). 두 결정이 같은 표를 두 방향으로 당긴다:
         *
         *   보정항 제거      게이트 보스 체력이 st50+에서 x1.354만큼 가벼워진다
         *   무료 게이트 티어  하한 플레이어가 1티어가 아니라 2~5티어로 문을 만난다
         *
         * 둘 다 하한 플레이어의 보스 처치를 **빠르게** 만들므로, 45초를 유지하려면
         * M이 커져야 한다. 옛 표를 그대로 쓰면 하한이 육문을 23.6초에 끝내고
         * 밴드 하한(35초)이 통째로 무너진다 - 값을 고집하면 규칙이 깨진다.
         *
         *   문   v1.5 (e=0.42)   2단계 재유도 (e=0.00)
         *   1    1.74            1.7376
         *   2    1.68            1.9528
         *   3    1.49            2.1813
         *   4    1.91            3.1164
         *   5    4.75            8.6655
         *   6    5.36            11.2413
         *
         * 일문이 거의 안 움직인 것이 이 표가 옳다는 증거다 - st30에는 지나온
         * 문이 없어서 두 변경 중 어느 쪽도 닿지 않는다.
         *
         * ## 2.1단계에 **육문 하나만** 다시 구웠다
         *
         * 심층 수렴 보정(B-1)이 st151부터 보스를 무겁게 하는데, **육문은 st150에
         * 서 있다.** 그 자리의 티어는 5이므로 문5의 몫(x1.15^0.55 = x1.0799)이
         * 걸리고, 보스가 그만큼 무거워졌으니 45초를 유지하려면 M이 그만큼
         * 내려가야 한다: 11.2413 / 1.0799 = **10.4095**.
         *
         * 나머지 다섯은 비트 단위로 그대로다. 오문이 st100에 서 있고 그 자리의
         * 티어가 4라 보정이 정확히 1이기 때문이다 - "클리어해야 걸린다"는 경계
         * 규칙이 M 표에서 이렇게 보인다.
         */
        public static readonly double[] TotalHealthMultiple =
            { 1.7376d, 1.9528d, 2.1813d, 3.1164d, 8.6655d, 10.4095d };

        // ---------------------------------------------------------------- 시간 규칙

        /** 적이 쓰러지고 다음 적이 설 때까지. **시계는 흐르고 재생도 계속된다** */
        public const double SwapSeconds = 2d;

        /**
         * @brief 격노가 시작되는 시각(초).
         *
         * 하한 플레이어의 앵커(45초)의 두 배다. 의도한 플레이어가 격노를 볼 일이
         * 없고, 못 넘는 플레이어에게만 종료를 강제한다.
         */
        public const double EnrageSeconds = 90d;

        /** 격노 단계 사이 간격(초) */
        public const double EnrageIntervalSeconds = 10d;

        /**
         * @brief 단계마다 적 공격력에 곱하는 값.
         *
         * 1.3은 **완만한 압박**이다. 재생 임계가 칼날이라(보스 DPS / 초당 재생 =
         * 0.88~1.22) 큰 배수는 격노 첫 단계에서 즉사를 만든다 - 그러면 격노는
         * 압박이 아니라 시각이 정해진 처형이고, 플레이어가 읽을 수 있는 정보가
         * 없다. 1.3은 임계를 조금씩 넘겨 몇 단계에 걸쳐 밀어낸다.
         *
         * 폐쇄(`CloseSeconds`)가 상한이므로 어떤 배수에서도 유한 시간에 끝난다 -
         * 격노는 종료를 **보장**하는 장치가 아니라 종료를 **앞당기는** 장치다.
         */
        public const double EnrageMultiplierPerStep = 1.3d;

        /**
         * @brief 귀문이 닫히는 시각(초). 여기 닿으면 실패다 - 종료 보장의 상한.
         *
         * ## 5.0단계에 180에서 150으로 내렸다
         *
         * 실측된 **모든** 시도가 132초 안에 끝난다 - 실제 플레이어의 가장 긴 시도가
         * 0.35x 화력의 98.6초(사망)이고, 통과하는 가장 느린 케이스가 0.70x의 62.6초다.
         * 132초는 지어낸 정지 시도(DPS 1/100 + 최대 재생)의 문1 값이고, 150은 그것까지
         * 그대로 담는 가장 작은 값이다.
         *
         * 그래서 이 변경은 **결과를 한 칸도 바꾸지 않는다** - 통과·실패·사망 시각이
         * 세 후보(180/150/120)에서 완전히 같다는 것을 `TrialClockPolicyTests`가 잰다.
         * 바뀌는 것은 벽시계뿐이다: 히트스톱 비율 0.70에서 화면의 「180초」는 실시간
         * 4분 17초였고 「150초」는 3분 34초다.
         *
         * 120도 계약을 지키지만 격노 단계가 넷만 남는다(150은 일곱). 실측 정지 시도가
         * 격노 1~5단계에서 죽으므로 그 여유를 남겨 두는 쪽을 골랐다.
         *
         * **시계는 `Time.deltaTime`(scaled)이다.** unscaled로 바꾸지 않은 이유는
         * 조작할 수 없는 시간(히트스톱)을 제한 시간에서 빼는 구조가 되고, 빼는 양이
         * **타격 수에 비례**하기 때문이다 - 다단·연격 빌드가 같은 초당 피해로도 시간을
         * 더 잃는다. 그것은 `TrialPowerScore` 머리 주석이 타격당 캡을 금지한 이유와
         * 같은 종류의 결함이다.
         */
        public const double CloseSeconds = 150d;

        /** 적의 공격 간격(초). 지역 보스와 같다 */
        public const double FoeAttackIntervalSeconds = 2d;

        /** 등장 후 첫 타격까지의 지연(초). 게임의 보스 접근 시간과 같은 자리다 */
        public const double FirstAttackDelaySeconds = 2d;

        // ---------------------------------------------------------------- 체력 배분

        /**
         * @brief 적 셋의 체력 배분. **3체가 절반이라 "마지막이 본체"가 성립한다.**
         *
         * 3체를 더 세게 때리게 만들지 않은 이유는 재생 임계가 칼날이기
         * 때문이다(`EnrageMultiplierPerStep` 주석). 차별화는 공격이 아니라
         * 체력과 시간에 둔다 - 3체의 정체성은 총 체력의 절반과 외형과 서사이지
         * 다른 공격 리듬이 아니다.
         */
        public static readonly double[] FoeHealthShare = { 0.25d, 0.25d, 0.50d };

        public static int FoeCount { get { return FoeHealthShare.Length; } }

        /**
         * @brief 문의 한자 이름. 화면에서 "일문"이 "1번 문"보다 읽힌다.
         *
         * **여기 하나다.** 4단계에서 가이드 카드가 같은 이름을 쓰게 되면서
         * TrialHud 안의 private 표를 이쪽으로 올렸다 - 두 벌로 두면 그 둘이
         * 갈리는 날 상단 띠와 우측 카드가 다른 문을 가리킨다.
         */
        public static string GateName(int gateNumber)
        {
            switch (gateNumber)
            {
                case 1: return "일문";
                case 2: return "이문";
                case 3: return "삼문";
                case 4: return "사문";
                case 5: return "오문";
                case 6: return "육문";
                default: return "귀문";
            }
        }

        // ---------------------------------------------------------------- 화면 문구

        /**
         * @brief 귀문이 화면에 적는 문구 전부. **여기 하나다.**
         *
         * ## 왜 상수로 올렸나
         *
         * 4단계 실기에서 「시간이 다 **□**다」와 「무료 재도전 가**□**」이 떴다.
         * `능`(U+B2A5)·`됐`(U+B410) 두 글자가 폰트 아틀라스에 없었기 때문이다.
         *
         * 문자셋 빌더는 `UIStrings.txt`·프리팹·씬·데이터 애셋을 훑는데,
         * **런타임에 C#이 만드는 문자열은 그 넷 어디에도 없다.** 그래서 새 문구를
         * 코드에 적고 `UIStrings.txt`에 옮겨 적는 것을 잊으면 □가 뜬다 -
         * `FontCharsetBuilder` 주석이 정확히 그것을 예고했다("손으로 옮겨 적는
         * 단계가 있는 한 그 단계는 언젠가 빠진다").
         *
         * 문구를 여기 모으면 `All`을 훑는 검사 하나가 그 단계를 대신 지킨다
         * (`PromotionDomainTests`). 잊어도 검사가 먼저 걸린다.
         */
        public const string VictoryTitle = "돌파";
        public const string FailureTitle = "귀문 실패";
        public const string ClosedReason = "시간이 다 됐다";
        public const string DeathReason = "쓰러졌다";
        public const string FailureConsolation = "잃은 것은 없다 · 무료 재도전 가능";
        public const string AppearanceChanged = "외형이 바뀌었다";
        public const string BreakthroughJoin = " 돌파 · ";
        public const string AttackPrefix = "공격 x";
        public const string HealthPrefix = "체력 x";
        public const string Arrow = " -> x";

        public const string TransitionLabel = "다음 적";
        public const string EnragePrefix = "격노 ";
        public const string EnrageSuffix = "단계";

        /** 가이드 카드 (4단계 §3) */
        public const string EntranceTag = "귀문";
        public const string ChallengeSuffix = " 도전";
        public const string RetrySuffix = " 재도전";

        /** 경지 표 (4단계 §5) */
        public const string GateInProgress = "귀문 도전 중";
        public const string GateOpenNow = "귀문 열림 · 지금 도전";
        public const string GateRequirementPrefix = "귀문 st";
        public const string GateRequirementSuffix = " 돌파";

        /**
         * @brief 위 문구 전부 + 문 이름 여섯. 글리프 검사가 이것을 훑는다.
         *
         * 새 문구를 더하면 **여기에도 더한다.** 안 더하면 검사가 그 문구를
         * 안 보므로, 이 배열이 곧 "화면에 뜨는 글자의 목록"이라는 계약이다.
         */
        public static string[] AllUiStrings()
        {
            var names = new string[FirstDeepGate + 1 + GateStages.Length];
            int i = 0;
            for (int gate = 1; gate <= GateStages.Length; gate++) names[i++] = GateName(gate);
            names[i++] = GateName(0);

            var fixedText = new[]
            {
                SoftCapNotice,
                VictoryTitle, FailureTitle, ClosedReason, DeathReason,
                FailureConsolation, AppearanceChanged, BreakthroughJoin,
                AttackPrefix, HealthPrefix, Arrow,
                TransitionLabel, EnragePrefix, EnrageSuffix,
                EntranceTag, ChallengeSuffix, RetrySuffix,
                GateInProgress, GateOpenNow, GateRequirementPrefix, GateRequirementSuffix
            };

            var all = new string[i + fixedText.Length];
            System.Array.Copy(names, all, i);
            System.Array.Copy(fixedText, 0, all, i, fixedText.Length);
            return all;
        }

        // ---------------------------------------------------------------- 소프트캡

        /**
         * @brief 소프트캡 지수 - **후보 확정값이다. 최종 고정값이 아니다.**
         *
         * 하한 앵커만으로는 곡선 추종이 15초에 끝나 시험이 형식이 된다. 캡이
         * 그것을 푼다 - 캡 없이 두 플레이어를 밴드에 넣는 것은 산수로 불가능하다
         * (격차 x3.61 > 밴드 비 1.571).
         *
         * 0.45의 근거는 EditMode 실측(곡선 추종 27~43초 / 중간 35~44 / 하한 45)
         * 이고, **PlayMode 실측 전이다.** 3단계가 실제 피해 파이프라인에 붙인 뒤
         * 다시 재고, 그때 고정값이 된다.
         */
        public const double SoftCapExponent = 0.45d;

        // ---------------------------------------------------------------- 값

        /**
         * @brief 승급의 보석 값. **0이다.**
         *
         * 상수를 지우지 않고 0으로 두는 이유는 계약을 코드에 남기기 위해서다 -
         * 유료 항이 다시 들어오는 날 `PromotionTrialTests.PromotionCostsNothing`이
         * 먼저 걸리고, 그때 밴드·초기 경제를 전부 다시 재야 한다는 사실이
         * 그 실패 메시지에 적혀 있다.
         */
        public const int PromotionGemCost = 0;

        /** 승급의 골드 값. **0이다.** 이유는 위와 같다 */
        public const double PromotionGoldCost = 0d;

        // ---------------------------------------------------------------- 문구

        /**
         * @brief 진입 화면에 **한 번만** 뜨는 귀문 전용 규칙 안내.
         *
         * "기준 화력을 넘는 몫은 절반쯤만 실린다"를 폐기했다 - 수학적으로
         * 부정확하고(절반이 아니라 ^0.45), 플레이어에게 손해를 직접 강조한다.
         *
         * 정확한 수식과 k 값은 일반 화면에 띄우지 않는다. 2단계에서는 문구를
         * 여기 두기만 하고 씬·UI에 연결하지 않는다 - 글리프 존재만
         * `PromotionStringsFitAndHaveGlyphs`가 확인한다.
         */
        public const string SoftCapNotice = "귀문에서는 지나친 화력이 완만하게 조정됩니다.";

        // ---------------------------------------------------------------- 변환

        /** 티어 `tier`(1..6)를 주는 문의 스테이지. 범위 밖이면 -1 */
        public static int GateStageOfTier(int tier)
        {
            if (tier < 1 || tier > GateCount) return -1;
            return GateStages[tier - 1];
        }

        /** 이 스테이지에 서 있는 문의 번호(1..6). 문이 없으면 0 */
        public static int GateNumberAtStage(int stage)
        {
            for (int g = 0; g < GateStages.Length; g++)
                if (GateStages[g] == stage) return g + 1;
            return 0;
        }

        public static bool IsGateStage(int stage)
        {
            return GateNumberAtStage(stage) > 0;
        }

        /**
         * @brief 이 최전선까지 **지나온 문의 수** = 그 플레이어가 가져야 할 티어.
         *
         * `gateStage < frontier`다. **등호가 아니다** - 최전선이 30이라는 것은
         * "st30에 도착했다"이지 "st30을 클리어했다"가 아니다. st30을 클리어하면
         * 문이 열리고, 그 문을 넘으면 최전선이 31이 된다.
         *
         * 경계: 29->0 / 30->0 / 31->1 / 40->1 / 41->2 / 50->2 / 51->3 /
         *       70->3 / 71->4 / 100->4 / 101->5 / 150->5 / 151->6
         */
        public static int TierAtFrontier(int frontier)
        {
            int tier = 0;
            for (int g = 0; g < GateStages.Length; g++)
                if (GateStages[g] < frontier) tier = g + 1;
            return tier;
        }

        /**
         * @brief 이 스테이지를 클리어한 뒤 다음으로 넘어가려면 넘어야 할 문. 없으면 0.
         *
         * **3단계가 `StageProgress.AdvanceStage`에서 부를 자리다.** 2단계에서는
         * 아무도 부르지 않는다 - 전투가 없는데 판정만 붙이면 그 순간 진행이
         * 영구히 막힌다.
         */
        public static int RequiredGateAfterClearing(int clearedStage, int currentTier)
        {
            int gate = GateNumberAtStage(clearedStage);
            if (gate <= 0) return 0;
            return currentTier >= gate ? 0 : gate;
        }

        /**
         * @brief 귀문 돌파 시의 **무료 티어 상승.**
         *
         * `Math.Max`인 이유는 세 가지다 - ① 이미 가진 것을 뺏지 않는다
         * ② 재도전이 티어를 되돌리지 않는다 ③ Firebase의 `max` 병합과 같은
         * 규칙이라 클라이언트와 서버가 다른 산수를 하지 않는다.
         *
         * 배수·이름·기본 외형·기본 오라는 전부 이 하나에서 유도된다
         * (`EvolutionCurve` / `EvolutionCatalog`). 저장할 값이 이것뿐인 이유다.
         */
        public static int TierAfterTrialVictory(int currentTier, int gateNumber)
        {
            if (gateNumber < 1 || gateNumber > GateCount) return currentTier;
            return Math.Max(currentTier, gateNumber);
        }

        /** 이 티어가 다음으로 만날 문의 스테이지. 다 넘었으면 -1 */
        public static int NextGateStage(int tier)
        {
            return tier >= GateCount ? -1 : GateStageOfTier(tier + 1);
        }

        /**
         * @brief **일반 보스를 잡았고 귀문이 아직 안 끝난 상태인가** (D-4).
         *
         * ## 신규 세이브 필드 없이 유도한다
         *
         * 정상 상태에서는 `bossKillCount == maxStageReached - 1`이다. 스테이지를
         * 올린 횟수가 곧 넘은 보스의 수이기 때문이다:
         *
         * ```
         * st30 진입 전     stage 30 · max 30 · bossKillCount 29     29 == 30-1  정상
         * 게이트 보스 처치  stage 30 · max 30 · bossKillCount 30     30 == stage 대기
         * 귀문 승리        stage 31 · max 31 · bossKillCount 30     30 == 31-1  정상
         * ```
         *
         * 게이트 보스만 **`bossKillCount`를 올리고 스테이지를 안 올리므로**, 그
         * 한 칸의 어긋남이 "보스는 벴는데 문을 아직 못 넘었다"를 뜻한다.
         * 앱이 그 사이에 죽어도 저장된 값에서 그대로 읽힌다.
         *
         * ## 조건 다섯을 **전부** 만족해야 한다
         *
         *   1. `stage == maxStageReached`   최전선이다 (재선택으로 되돌아간 것이 아니다)
         *   2. 그 스테이지에 문이 있다
         *   3. `tier < gate`                아직 그 문의 경지가 없다
         *   4. `kills == KillsPerStage`     보스가 열려 있던 상태다
         *   5. `bossKillCount == stage`     보스를 벴다 (정상이면 stage-1이다)
         *
         * 하나라도 어긋나면 **0을 낸다.** 손상되거나 테스트 패널로 점프한
         * 세이브에서 문이 저절로 열리지 않게 하는 것이 그 엄격함의 이유이고,
         * 그때는 일반 보스를 다시 잡는 안전한 경로(D-1)로 떨어진다.
         *
         * **이 함수는 문을 열어 줄 뿐 통과시키지 않는다.** 경지는 귀문을 이겨야
         * 오르므로, 설령 오탐이 나도 공짜로 얻는 것은 없다.
         */
        public static int PendingGate(int stage, int maxStageReached,
                                      int killsThisStage, int bossKillCount, int tier)
        {
            if (stage != maxStageReached) return 0;
            if (killsThisStage != StageCurve.KillsPerStage) return 0;
            if (bossKillCount != stage) return 0;

            int gate = GateNumberAtStage(stage);
            if (gate <= 0) return 0;

            return tier >= gate ? 0 : gate;
        }

        // ---------------------------------------------------------------- 격노

        /** 이 시각까지 오른 격노 단계 수 */
        public static int EnrageStepsAt(double seconds)
        {
            if (seconds <= EnrageSeconds) return 0;
            return 1 + (int)((seconds - EnrageSeconds) / EnrageIntervalSeconds);
        }

        /** 이 시각의 적 공격력 배수 */
        public static double EnrageMultiplierAt(double seconds)
        {
            return Math.Pow(EnrageMultiplierPerStep, EnrageStepsAt(seconds));
        }

        // ---------------------------------------------------------------- 체력

        /**
         * @brief 이 문의 **총 체력**. 지역 보스 체력 x M이다.
         *
         * `StageCurve.BossHealthForStage`를 지나는 것이 요점이다 - 여기서 곱셈을
         * 따로 하면 "계산상으로는 통과하는데 실제로는 실패하는" 상태가 만들어진다
         * (그 함수 주석의 규칙 그대로).
         */
        public static BigDouble TotalTrialHealth(BigDouble averageMobHealth, int gateNumber)
        {
            if (gateNumber < 1 || gateNumber > GateCount) return BigDouble.Zero;

            var bossHealth = StageCurve.BossHealthForStage(averageMobHealth, GateStages[gateNumber - 1]);
            return bossHealth * BigDouble.FromDouble(TotalHealthMultiple[gateNumber - 1]);
        }

        /** 이 문의 `foeIndex`번째(0부터) 적의 체력 */
        public static BigDouble FoeHealth(BigDouble averageMobHealth, int gateNumber, int foeIndex)
        {
            if (foeIndex < 0 || foeIndex >= FoeCount) return BigDouble.Zero;

            return TotalTrialHealth(averageMobHealth, gateNumber)
                 * BigDouble.FromDouble(FoeHealthShare[foeIndex]);
        }

        /**
         * @brief 이 문의 **기준 화력** - 소프트캡의 `ref`.
         *
         * 총 체력 / 기준 시간이다. 별도 곡선 상수를 두지 않는 것이 요점 -
         * 적 체력이 바뀌면 기준도 같이 움직이므로 두 값이 어긋날 수 없다.
         */
        public static double ReferencePowerForGate(BigDouble averageMobHealth, int gateNumber)
        {
            return TrialPowerScore.ReferencePower(
                TotalTrialHealth(averageMobHealth, gateNumber).ToDouble());
        }
    }
}
