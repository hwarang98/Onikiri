using System;

namespace Onikiri.Progression
{
    /**
     * @brief 요도(妖刀)의 티어 곡선, 재료 비용, 해금, 그리고 기대 곡선.
     *
     * ## 이 축은 골드로 사지 않는다 - 그것이 다른 모든 축과 다른 점이다
     *
     * 지금까지의 축은 전부 골드가 속도를 정했다(강화·오의·장비 단련·동료
     * 레벨·심화). 보석은 관문만 열었다. 요도는 셋째 성질이다:
     *
     *   혼(魂)    대요괴를 벤다. **진행으로만 들어온다.** 40스테이지에 넷
     *   파편      정예를 벤다 + 최고 티어 요도의 남는 혼. 합성의 재료
     *   보석      파편을 사는 촉매. **선택이다** - 없어도 축이 멈추지 않는다
     *
     * 그래서 이 축의 속도를 정하는 것은 지갑이 아니라 **한 바퀴**다. 곡선
     * 추종 플레이어와 무과금 플레이어가 같은 속도로 오른다 - 그것이 이 스텝의
     * f2p 바닥 보장이고, 두 사람을 가르는 물건(혼 뽑기)은 다음 스텝의 일이다.
     *
     * ## 왜 보석이 관문이 아닌가 - 실측이 그렇게 시켰다
     *
     * 처음에 티어업 비용에 보석을 넣었다. 하네스로 재보니 **무과금의 보석
     * 잔액이 st50~400 내내 30~190개**다 - 벌이(퀘스트 실수령) 거의 전부가
     * 이미 장비 등급·동료 해금·전직에 배정돼 있다. 거기에 티어업 보석을
     * 얹으면 무과금은 요도를 못 올리는데 **보정(StageCurve.YodoCompensation)은
     * 스테이지의 함수라 그를 구분하지 못한다.** 21단계 골드 축이 겪은 그
     * 사고("축은 없는데 보정만 걸려 순손실")가 심층 밴드 한가운데서 재현되고,
     * 그 구간 f2p 바닥은 이미 1.16까지 얇다.
     *
     * 그래서 보석은 관문에서 내려와 **촉매**가 됐다(ShardPack). 파편이
     * 모자란 순간을 돈으로 건너뛰는 물건이고, 무과금은 그냥 한 바퀴를 더
     * 돌면 된다. "f2p 바닥은 밴드가 보장, 그 위는 과금 가속기"가 곡선 구조로
     * 적힌 자리다.
     */
    public static class YodoCurve
    {
        // ---------------------------------------------------------------- 해금

        /**
         * @brief 요도가 열리는 스테이지. **세계가 한 바퀴 도는 곳이다.**
         *
         * ## 왜 41인가 - 죽은 요괴는 혼을 남기지 않는다
         *
         * 42단계에 라인업이 순환이 됐다(BossRoster.RegionForStage). st41은
         * 네 지역을 다 지나고 지역 1이 다시 서는 첫 칸이고, 그 자리에서만
         * 성립하는 설정이 하나 있다 - **되살아난 요괴**다. 처음 벤 대요괴는
         * 그냥 죽는다. 세계가 한 바퀴 돌아 다시 선 놈을 벨 때, 두 번째 죽음이
         * 혼을 남긴다.
         *
         * 그 설정이 밸런스와 정확히 같은 것을 요구한다. 코리더(st1~30)와
         * 가속 구간(st31~50)은 이 스텝에서 **비트 단위로** 움직이면 안 되는데,
         * 첫 혼이 떨어지는 자리가 st50 피날레라 그 힘은 st51부터 붙는다 -
         * 두 밴드가 구조로 지켜진다(계수가 아니라). 42단계의 심층 램프가
         * st51부터인 것과 같은 경계이고, 그것도 우연이 아니다: 무한 구간은
         * 조율이 끝난 구간의 밖이다.
         *
         * ## 그래서 st41~49는 "보이지만 비어 있는" 열 칸이다
         *
         * 도감이 열리고 네 줄이 전부 잠긴 미리보기다(41단계 규칙: 진입 허용,
         * 액션만 잠금). "○○ 처치 시 해금"이 네 번 적혀 있고, 첫 줄이 st50에
         * 켜진다. 41단계가 잠긴 탭에서 하려던 일("저거 갖고 싶다")을 열 칸의
         * 유예로 실제로 하는 자리다.
         */
        public const int UnlockStage = 41;

        public static bool IsUnlockedAt(int stage)
        {
            return stage >= UnlockStage;
        }

        /**
         * @brief 세계 한 바퀴의 길이. **로스터가 정한다.**
         *
         * 지역 수 x 지역 길이다. 혼 종류가 곧 지역 수이므로
         * (YodoCatalog 머리 주석) 카탈로그에서 유도할 수 있고, 그러면 다섯째
         * 지역이 생기는 날 이 값이 저절로 따라온다. 로스터 애셋과 갈리면
         * YodoTests.CycleLength_MatchesTheRoster가 잡는다.
         */
        public static int CycleLength { get { return YodoCatalog.Count * BossCurve.RegionLength; } }

        /**
         * @brief 이 스테이지의 피날레가 남기는 혼의 번호. 피날레가 아니면 -1.
         *
         * 순환 안의 위치로 접는다 - BossRoster.RegionForStage와 같은 산수이고,
         * 같아야 한다. 저쪽이 어느 보스를 세울지 정하고 이쪽은 그 보스가 어떤
         * 혼을 남기는지 정하므로, 둘이 갈리면 등롱을 베고 흑야의 혼이 나온다.
         *
         * **런타임은 이 함수를 쓰지 않는다.** 실제 드랍은 보스 애셋이 들고 있는
         * soulId로 간다(YodoSystem.ReportBossDefeated) - 애셋이 단일 출처라는
         * 13단계 규칙 그대로다. 여기 있는 것은 **닫힌 식이 필요한 쪽**, 즉
         * 시뮬레이션의 기대 곡선과 보스 체력 보정을 위한 사본이고, 둘이 같은지는
         * YodoTests.SoulSchedule_MatchesTheRoster가 애셋을 읽어 대조한다.
         */
        public static int SoulIndexDroppedAt(int stage)
        {
            if (!IsUnlockedAt(stage)) return -1;
            if (BossCurve.TierOf(stage) != BossCurve.Tier.Finale) return -1;

            int inCycle = (stage - 1) % CycleLength;   // 0..39
            return inCycle / BossCurve.RegionLength;   // 0..3
        }

        /** 이 스테이지의 챕터 보스(정예)가 파편을 남기는가 */
        public static bool DropsShardsAt(int stage)
        {
            return IsUnlockedAt(stage) && BossCurve.TierOf(stage) == BossCurve.Tier.Chapter;
        }

        // ---------------------------------------------------------------- 값

        /**
         * @brief 요도 한 자루의 최고 티어.
         *
         * ## 상한이 구조적 요구다 - 그런데 이유가 다른 축과 다르다
         *
         * 장비·동료·골드 획득 축은 **골드로 사기 때문에** 상한이 필요했다
         * (수입이 지수로 자라므로 상한이 없으면 DPS가 제곱으로 자란다).
         * 요도는 골드로 사지 않으므로 그 산수가 없다 - 혼이 한 바퀴에 하나씩만
         * 들어오니 티어는 스테이지에 **선형**이고, 배수는 스테이지의 지수가
         * 아니라 `TierStep^(stage/40)`이다.
         *
         * 그래도 상한을 두는 이유는 42단계가 심층 램프로 배운 것이다:
         * **스테이지당 x1.008의 발산도 st200에서 8.4배가 된다.** 여기서
         * 상한을 빼면 한 바퀴마다 네 자루가 각자 x1.035, 즉 40스테이지에
         * x1.148 - 심층 밴드의 수렴이 통째로 무너진다. 상한 10은 열 바퀴,
         * 곧 st440 언저리에서 닫히고 그 뒤 밴드는 다시 수평이다.
         */
        public const int MaxTier = 10;

        /**
         * @brief 봉인(티어 1)의 배수. 요도가 태어나는 순간의 크기다.
         *
         * 티어 한 칸(TierStep)보다 큰 것이 중요하다. 봉인은 재료를 모아 한 칸
         * 올리는 일이 아니라 **없던 칼이 생기는 일**이고, 그 차이가 숫자로도
         * 있어야 도감의 첫 줄이 켜지는 순간이 티어업 아홉 번과 구분된다.
         */
        public const double SealStep = 1.05d;

        /** 티어 한 칸의 배수. 아홉 번 곱해진다 */
        public const double TierStep = 1.035d;

        /**
         * @brief 이 티어의 배수. **티어 0(미봉인)은 정확히 1이다.**
         *
         * 1로 떨어지는 것이 중요하다 - 봉인 전의 요도가 0을 곱하면 공격력이
         * 통째로 사라지고, 그 상태가 곧 st1~49의 모든 플레이어다.
         * EquipmentSystem.MultiplierFor가 슬롯 없이 1을 내는 것과 같은 규칙이다.
         *
         * 상한 위의 티어는 자른다. 상한이 내려간 업데이트에서 세이브의 티어를
         * 깎지 않는 것이 이 프로젝트의 규칙이고(EquipmentCurve.ValueAt),
         * 값에서 자르면 상한이 다시 오를 때 잠든 티어가 깨어난다.
         */
        public static double TierValue(int tier)
        {
            if (tier < 1) return 1d;
            int t = tier > MaxTier ? MaxTier : tier;
            return SealStep * Math.Pow(TierStep, t - 1);
        }

        /** 한 자루가 상한까지 갔을 때의 배수. 보고와 테스트가 쓴다 */
        public static double BladeCeiling { get { return TierValue(MaxTier); } }

        // ---------------------------------------------------------------- 세트

        /**
         * @brief 도감 세트 보너스. 봉인한 요도 수(0~4)로 읽는다.
         *
         * ## 왜 마지막 칸만 큰가
         *
         * 앞의 셋(x1.02 / x1.05 / x1.09)은 한 자루 봉인의 배수(x1.05)와
         * 비슷한 크기다 - 도감이 채워지는 것 자체를 보상하되, 그 보상이
         * 요도보다 커지면 칼이 아니라 수집이 주인공이 된다.
         *
         * 넷째 칸(x1.25)만 다르다. 그 자리가 **오니키리 완성**이고, 이
         * 스텝 전체가 향하는 곳이다(YodoCatalog.OnikiriName). 앞의 셋과 같은
         * 간격으로 두면 도감의 마지막 줄이 "네 번째 요도"가 되어버린다 -
         * 완성은 진척의 연장이 아니라 다른 사건이어야 한다.
         *
         * 이 크기가 밴드에서 감당된다는 것은 실측이다(StageSimulationTests의
         * 심층 밴드) - st80에 한 번 오는 계단이고, 보정이 그 계단을 따라간다.
         */
        private static readonly double[] SetBonuses = { 1d, 1.02d, 1.05d, 1.09d, 1.25d };

        public static double SetBonusAt(int sealedCount)
        {
            if (sealedCount < 0) return 1d;
            if (sealedCount >= SetBonuses.Length) return SetBonuses[SetBonuses.Length - 1];
            return SetBonuses[sealedCount];
        }

        /** 넷을 다 봉인했는가. 화면과 세트 보너스가 같은 판정을 쓴다 */
        public static bool IsComplete(int sealedCount)
        {
            return sealedCount >= YodoCatalog.Count;
        }

        // ------------------------------------------------ 47단계: 공격력 한 곳

        /**
         * @brief 요도 축이 **공격력에** 곱하는 총 배수. 곱의 순서가 여기 있다.
         *
         *     티어 x 세트 x 혼격 x 전설
         *
         * 47단계에 항이 둘 늘면서 이 곱을 부르는 곳이 셋이 됐다 -
         * YodoSystem(실제), StageSimulation.Levels(시뮬), 그리고 이 파일의
         * 기대 곡선. 세 곳이 각자 곱하면 언젠가 순서나 항 하나가 갈리고,
         * 그 증상은 "화면의 배수와 실제 데미지가 다르다"다 - 26단계가
         * SkillSystem.MultiplierOf 하나로 못 박은 규칙을 이 축에도 둔다.
         *
         * **세트 보너스는 봉인한 보스 요도만 센다.** 전설을 두 자루 다
         * 모아도 오니키리는 완성되지 않는다 - 세로축과 가로축이 갈린
         * 자리이고, 그것이 LegendaryYodoSpec 머리 주석의 기둥이다.
         *
         * @param rarities 혼격. null이면 전부 0 (무과금 - 44·45단계와 동일)
         * @param legends  전설 사본. null이면 미보유
         */
        public static double AttackFactorFor(int[] tiers, int[] rarities = null,
                                             int[] legends = null)
        {
            if (tiers == null) return 1d;

            double product = 1d;
            int sealedCount = 0;
            int count = Math.Min(YodoCatalog.Count, tiers.Length);

            for (int i = 0; i < count; i++)
            {
                product *= TierValue(tiers[i]);
                if (tiers[i] >= 1) sealedCount++;
            }

            return product * SetBonusAt(sealedCount)
                 * YodoRarityCurve.AttackFactor(tiers, rarities)
                 * LegendaryYodoCurve.PowerFactor(legends);
        }

        // ---------------------------------------------------------------- 비용

        /**
         * @brief 티어업에 드는 혼의 수. **언제나 하나다.**
         *
         * 상수 함수를 함수로 두는 이유는 이것이 이 축의 속도 그 자체이기
         * 때문이다. 티어 = 그 혼을 몇 번 얻었는가이고, 혼은 한 바퀴에 하나
         * 들어온다 - 그래서 티어는 바퀴 수와 같고, 기대 곡선
         * (ExpectedTierAtStage)이 닫힌 식으로 적힌다.
         *
         * 여기에 티어별 값을 넣는 순간 그 등식이 깨지고 기대 곡선이
         * 시뮬레이션 결과를 참조해야 한다 - 보스 체력이 그것을 읽으므로
         * 순환이 된다(EquipmentCurve.ExpectedLevelAtStage가 적어둔 제약).
         */
        public const int SoulsPerTier = 1;

        /**
         * @brief 티어업의 파편 값. **선형이다** - 이 프로젝트에서 처음이다.
         *
         * 다른 모든 비용 곡선은 지수다(x1.15 ~ x1.9). 지수인 이유는 언제나
         * 같았다: **수입이 스테이지마다 지수로 자라니 비용도 그래야 한다.**
         *
         * 파편은 그 성질이 없다. 정예 하나가 남기는 파편은 st45나 st445나
         * 같은 수이고(진행 보상이지 골드가 아니다), 그래서 수입이 스테이지에
         * **선형**이다. 비용을 지수로 두면 서너 티어 만에 수입이 따라잡을 수
         * 없어지고, 그 시점부터 파편은 영원히 모자란 채로 남는다 - 상한이
         * 아니라 **벽**이다.
         *
         * 선형이면 티어 t의 값이 Base + Step x (t-1)이고, 누적은 t의 제곱에
         * 비례한다. 수입은 t에 선형(한 바퀴에 정해진 양)이므로 결국 어딘가에서
         * 만난다 - 그 지점이 상한(MaxTier)보다 뒤에 오도록 ShardsPerElite를
         * 잡았고, 하네스가 무과금 경로에서 실측한다.
         */
        public const int ShardCostBase = 8;
        public const int ShardCostStep = 6;

        /** tier -> tier+1 에 드는 파편. 상한 티어면 0 */
        public static int ShardCostAtTier(int tier)
        {
            if (tier < 1 || tier >= MaxTier) return 0;
            return ShardCostBase + ShardCostStep * (tier - 1);
        }

        /** 1티어에서 이 티어까지 든 파편 총합. 보고와 테스트가 쓴다 */
        public static int TotalShardsThroughTier(int tier)
        {
            int total = 0;
            int cap = tier > MaxTier ? MaxTier : tier;
            for (int t = 1; t < cap; t++) total += ShardCostAtTier(t);
            return total;
        }

        // ---------------------------------------------------------------- 파편 수입

        /**
         * @brief 정예(챕터 보스) 하나가 남기는 파편.
         *
         * ## 이 숫자가 무과금과 과금을 가른다
         *
         * 혼은 진행이 주므로 무과금과 과금이 **같은 속도로** 받는다. 그래서
         * 이 축에서 둘을 가르는 것은 파편뿐이고, 그 사실이 값을 정했다.
         *
         * 한 바퀴(40스테이지)의 수입은 4 x 이 값이다. 그 바퀴의 티어업 넷은
         * 4 x ShardCostAtTier(t)이고 t와 함께 선형으로 자란다 - 수입은 평평한데
         * 비용이 자라므로 **어느 바퀴부터는 반드시 모자란다.** 16이면 그 지점이
         * 일곱째 바퀴(티어 7, st~320) 언저리다:
         *
         *   과금(촉매 있음)  혼에만 막힌다. 열째 바퀴에 상한 - st~440
         *   무과금(촉매 없음) 파편에 막힌다. 상한은 st~580
         *
         * 그 사이 300스테이지가 이 스텝의 과금 창이고, 크기는 티어 셋 차이
         * (x1.51)다. **상한이 있으므로 창은 닫힌다** - 벌어진 채로 발산하지
         * 않는 것이 이 축을 심층 밴드에 넣을 수 있는 이유다.
         *
         * 45단계에 그 창이 넓어졌다. 상성과 영체가 **같은 티어를 읽으므로**
         * 티어 셋 차이가 이제 세 축에서 동시에 벌어진다(st200 실측 기준
         * 파워 축에서만 x1.10 추가). 창의 크기가 커진 것이지 성질이 바뀐
         * 것은 아니다 - 상한이 같은 자리에서 닫으므로 여전히 유한하고,
         * 그 대가는 심층 천장 재기준으로 치렀다(StageSimulationTests의
         * DeepCeiling 주석).
         *
         * 25로 두고 한 번 재봤다. 무과금도 열째 바퀴에 상한에 닿아 **촉매
         * 버튼이 죽었다**(하네스 실측: 보석 묶음 이득 0.0%). 파편이 남으면
         * 파는 것이 없다.
         */
        public const int ShardsPerElite = 14;

        /**
         * @brief 최고 티어 요도에게 들어온 혼이 바뀌는 파편 수.
         *
         * **버려지는 드랍을 0으로 만드는 유일한 경로다.** 티어가 남아 있는
         * 동안 혼은 그 자리에서 쓰이므로 중복이라는 상태 자체가 없고
         * (SoulsPerTier = 1), 혼이 갈 곳을 잃는 것은 그 요도가 상한에 닿은
         * 뒤뿐이다. 그때부터 대요괴는 파편 공장이 된다.
         *
         * 정예 하나(25)보다 크게 둔다. 대요괴는 한 바퀴에 한 번뿐이고
         * 정예는 네 번이다 - 희소한 쪽이 작으면 화면에서 "대요괴를 잡았는데
         * 정예만도 못하다"가 된다.
         */
        public const int ShardsPerOverflowSoul = 40;

        // ---------------------------------------------------------------- 촉매

        /**
         * @brief 보석으로 사는 파편 한 묶음. **이 스텝의 새 보석 소비처다.**
         *
         * 관문이 아니라 촉매인 이유는 이 파일 머리 주석에 있다. 값은 무과금이
         * 쳐다보지 않을 만큼, 그러나 보석이 남는 플레이어에게는 한 바퀴를
         * 앞당길 만큼으로 잡았다:
         *
         *   한 바퀴의 파편 수입   4 x 25 = 100
         *   묶음 하나            보석 30 -> 파편 20
         *   한 바퀴를 통째로 앞당기려면  보석 150
         *
         * 무과금의 st50~400 보석 잔액이 30~190이므로(하네스 실측) 그에게
         * 이 버튼은 "가끔 한 묶음"이다. 다음 스텝의 현금 보석이 이 버튼을
         * 통해 요도로 흐른다 - 파는 것은 힘이 아니라 **시간**이고, 그 구분이
         * 이 게임의 과금 지향이 밴드를 깨지 않는 이유다.
         */
        public const int ShardPackGems = 30;
        public const int ShardPackShards = 20;

        // ---------------------------------------------------------------- 기대 곡선

        /**
         * @brief i번 혼이 처음 떨어지는 스테이지. 그 뒤 CycleLength마다 한 번이다.
         *
         * 해금(st41) 뒤 첫 피날레가 st50이고, i번 지역의 피날레는 거기서
         * 10 x i 뒤다.
         */
        public static int FirstDropStage(int soulIndex)
        {
            return UnlockStage + (BossCurve.RegionLength - 1) + soulIndex * BossCurve.RegionLength;
        }

        /**
         * @brief **stage를 싸우는 시점에** 손에 들어와 있는 i번 혼의 누적 개수.
         *
         * ## 경계가 하나 있고, 한 번 물렸다
         *
         * 혼은 보스를 **벤 뒤에** 떨어진다. 그러므로 st50에 떨어진 혼은
         * st50의 보스 여유에 아무 영향이 없고 st51부터 힘을 낸다. 처음에
         * `stage`까지를 세었더니 st50의 f2p 여유가 1.08에서 1.04로 내려가
         * **가속 구간의 마지막 칸이 무너졌다**(하네스가 잡았다). 조율 구간
         * 비트 불변이 이 한 칸에 걸려 있다.
         *
         * 그래서 `stage - 1`까지를 센다. 게임 쪽도 같은 순서다
         * (BossFight.OnEnemyKilled - 클리어 처리와 같은 자리에서 드랍한다).
         */
        public static int SoulsBeforeStage(int soulIndex, int stage)
        {
            if (soulIndex < 0 || soulIndex >= YodoCatalog.Count) return 0;

            int last = stage - 1;
            int first = FirstDropStage(soulIndex);
            if (last < first) return 0;

            return (last - first) / CycleLength + 1;
        }

        /** stage를 싸우는 시점까지 정예에게서 받은 파편 총량 */
        public static int ShardsEarnedBeforeStage(int stage)
        {
            int last = stage - 1;
            if (last < UnlockStage) return 0;

            // 정예 = 5의 배수인데 10의 배수가 아닌 칸. 해금 앞은 빼야 하므로
            // [1..last]에서 세고 [1..UnlockStage-1]에서 센 것을 뺀다
            int before = UnlockStage - 1;
            int elites = (last / BossCurve.ChapterEvery - last / BossCurve.RegionLength)
                       - (before / BossCurve.ChapterEvery - before / BossCurve.RegionLength);
            return elites * ShardsPerElite;
        }

        /**
         * @brief 곡선 추종 플레이어의 요도 상태. **촉매 없는 무과금 경로다.**
         *
         * ## 왜 무과금 쪽을 기대 곡선으로 삼는가
         *
         * 앞의 축들(장비 등급·동료 해금·전직)은 기대 곡선이 **보석 무제한**
         * 플레이어였다. 그쪽이 여유의 위쪽 끝이고, 밴드가 천장을 검사해야
         * 했기 때문이다.
         *
         * 요도는 반대다. 보정(StageCurve.YodoCompensation)이 따라가는 곡선이
         * 곧 **아무도 그 아래로 떨어지지 않아야 하는 선**인데, 무과금은
         * 파편(보석 촉매 없이 정예 드랍만)에 막혀 과금보다 늦다. 기대 곡선을
         * 과금 쪽에 두면 무과금은 없는 이득을 상쇄당한다 - 21단계 골드 축이
         * 겪은 그 사고이고, 심층 f2p 바닥은 이미 1.16까지 얇다.
         *
         * 그래서 여기서만 규칙이 뒤집힌다: **기대 = 바닥.** 과금이 얻는
         * 것은 그 위로 삐져나온 몫이고, 그것이 정확히 "f2p 바닥은 밴드가
         * 보장, 그 위는 과금 가속기"다.
         *
         * ## 왜 닫힌 식이 아니라 되감기인가
         *
         * 파편은 네 자루가 **하나의 지갑을 나눠 쓰므로** 티어가 서로 얽힌다.
         * 닫힌 식으로 적을 수 없고, 적으려 하면 실제와 갈린다. 대신 이
         * 함수는 시뮬레이션 결과를 참조하지 않는다 - 드랍 일정만 읽고 되감으므로
         * 보스 체력이 불러도 순환이 생기지 않는다(EquipmentCurve.
         * ExpectedLevelAtStage가 요구한 제약은 "닫힌 식"이 아니라 "순환 금지"였다).
         *
         * 시뮬레이션의 벼림 규칙(StageSimulation.TryForgeYodo)과 여기가 갈리면
         * YodoTests.ExpectedCurve_TracksTheSimulation이 잡는다.
         */
        public static void ExpectedStateAtStage(int stage, int[] tiers)
        {
            if (tiers == null) return;
            for (int i = 0; i < tiers.Length; i++) tiers[i] = 0;

            int count = YodoCatalog.Count;
            if (count > tiers.Length) count = tiers.Length;

            var souls = new int[count];
            int shards = 0;

            for (int s = UnlockStage; s < stage; s++)
            {
                if (DropsShardsAt(s)) shards += ShardsPerElite;

                int dropped = SoulIndexDroppedAt(s);
                if (dropped >= 0 && dropped < count) souls[dropped]++;

                for (int i = 0; i < count; i++)
                {
                    while (souls[i] >= SoulsPerTier)
                    {
                        if (tiers[i] >= MaxTier)
                        {
                            shards += souls[i] * ShardsPerOverflowSoul;
                            souls[i] = 0;
                            break;
                        }

                        int cost = tiers[i] == 0 ? 0 : ShardCostAtTier(tiers[i]);
                        if (cost > shards) break;

                        shards -= cost;
                        souls[i] -= SoulsPerTier;
                        tiers[i]++;
                    }
                }
            }
        }

        /** i번 요도의 기대 티어. 되감기 한 번에 하나만 꺼내는 편의 함수 */
        public static int ExpectedTierAtStage(int soulIndex, int stage)
        {
            if (soulIndex < 0 || soulIndex >= YodoCatalog.Count) return 0;

            var tiers = new int[YodoCatalog.Count];
            ExpectedStateAtStage(stage, tiers);
            return tiers[soulIndex];
        }

        /**
         * @brief 그 스테이지의 기대 DPS 배수. **해금 전에는 정확히 1이다.**
         *
         * 네 자루의 티어 배수 곱 x 세트 보너스. StageCurve.YodoCompensation이
         * 이것을 따라간다.
         *
         * 1로 떨어지는 구간이 st1~50 전부라는 것이 이 스텝의 안전선이다 -
         * 조율 코리더와 가속 구간에는 이 축도 보정도 **존재하지 않는다**.
         * 계수가 아니라 구조가 지키는 불변이고,
         * YodoTests.Yodo_IsAbsentFromTheTunedBands가 못 박는다.
         *
         * 보스 체력이 스폰마다 부르는 값이라 되감기를 스테이지마다 다시 돌면
         * 아깝다. 마지막 답을 하나만 기억해 둔다 - 같은 스테이지를 연달아
         * 묻는 것이 이 함수의 실제 사용 패턴이다(시뮬레이션의 한 줄,
         * BossFight의 한 스폰).
         */
        private static int cachedStage = -1;
        private static double cachedMultiplier = 1d;

        public static double ExpectedMultiplierAtStage(int stage)
        {
            if (stage < UnlockStage) return 1d;
            if (stage == cachedStage) return cachedMultiplier;

            var tiers = new int[YodoCatalog.Count];
            ExpectedStateAtStage(stage, tiers);

            // 혼격도 전설도 넘기지 않는다. **기대 곡선은 무과금의 것**이고
            // (이 파일 ExpectedStateAtStage 주석) 무과금에게는 둘 다 자연
            // 출처가 없다 - 47단계가 이 함수를 한 비트도 안 움직이는 이유이자
            // f2p 바닥이 구조로 지켜지는 자리다
            cachedStage = stage;
            cachedMultiplier = AttackFactorFor(tiers);
            return cachedMultiplier;
        }

        // ------------------------------------------------ 45단계: 상성·영체

        /**
         * @brief 상성과 영체가 **DPS에** 곱하는 기대 배수. 해금 전에는 1이다.
         *
         * ## 왜 티어 배수와 나눠 두는가
         *
         * ExpectedMultiplierAtStage는 **공격력 배수**다 - 화면의 요도 카드가
         * 적는 값이고, 시뮬레이션의 YodoMultiplier가 대조하는 값이다. 상성과
         * 영체는 공격력이 아니라 괄호 안(오의 초당환산)을 건드리므로 단위가
         * 다르고, 한 함수에 접으면 그 대조가 무엇을 재는지 알 수 없어진다.
         *
         * 나눠 두는 두 번째 이유는 밴드 재현이다. 44단계의 세계를 다시
         * 만들려면 **새 두 축의 보정만** 걷어내야 하는데, 곱이 하나면
         * 걷어낼 수가 없다(StageCurve.YodoPowerCompensation).
         *
         * ## 곱의 순서가 정해져 있다
         *
         * 상성 먼저, 영체 나중이다. 둘이 같은 괄호 안에서 더해지므로 영체의
         * 몫은 상성이 이미 부풀린 분모 위에서 재야 하고, 그래야 두 배수의
         * 곱이 전체와 정확히 같다(YodoSpiritCurve.DpsFactorAfterAffinity).
         */
        public static double ExpectedPowerFactorAtStage(int stage)
        {
            if (stage < UnlockStage) return 1d;
            if (stage == cachedPowerStage) return cachedPowerFactor;

            var tiers = new int[YodoCatalog.Count];
            ExpectedStateAtStage(stage, tiers);

            cachedPowerStage = stage;
            cachedPowerFactor = YodoAffinityCurve.DpsFactor(tiers)
                              * YodoSpiritCurve.DpsFactorAfterAffinity(tiers);
            return cachedPowerFactor;
        }

        private static int cachedPowerStage = -1;
        private static double cachedPowerFactor = 1d;

        /**
         * @brief 요도 축 전체가 기대 DPS에 곱하는 배수 (티어 x 세트 x 상성 x 영체).
         *
         * 보고와 밴드 검사가 "이 축이 지금 얼마인가"를 물을 때의 답이다.
         * 보정은 이것을 통째로 따라가지 않고 두 조각으로 나눠 따라간다 -
         * 이유는 위 주석과 StageCurve.YodoPowerCompensation에 있다.
         */
        public static double ExpectedCombatFactorAtStage(int stage)
        {
            return ExpectedMultiplierAtStage(stage) * ExpectedPowerFactorAtStage(stage);
        }

        /** 이 스테이지에 봉인돼 있을 요도 수 (0~4). 보고와 테스트가 쓴다 */
        public static int ExpectedSealedAtStage(int stage)
        {
            var tiers = new int[YodoCatalog.Count];
            ExpectedStateAtStage(stage, tiers);

            int sealedCount = 0;
            for (int i = 0; i < tiers.Length; i++) if (tiers[i] >= 1) sealedCount++;
            return sealedCount;
        }

        /** 넷 다 상한일 때의 총 배수. 보고와 밴드 검사가 쓴다 */
        public static double Ceiling
        {
            get
            {
                double product = 1d;
                for (int i = 0; i < YodoCatalog.Count; i++) product *= BladeCeiling;
                return product * SetBonusAt(YodoCatalog.Count);
            }
        }
    }
}
