using System;

namespace Onikiri.Progression
{
    /**
     * @brief 혼격(魂格). **같은 요괴의 혼에도 격이 있다.**
     *
     * ## 무엇이 새로 생겼는가 - 자루마다 두 번째 숫자
     *
     * 44단계까지 요도 한 자루의 상태는 **티어 하나**였다. 티어는 그 요괴를
     * 몇 번 벴는가이고, 그래서 한 바퀴에 한 칸씩만 오른다 - 지갑이 아니라
     * 시계가 정하는 값이다(YodoCurve 머리 주석).
     *
     * 혼격은 그 옆에 서는 두 번째 숫자다. 같은 등롱의 혼이라도 **더 짙은
     * 것**이 있고, 짙은 혼을 먹인 등롱도는 같은 티어에서 더 세다. 티어가
     * "몇 번 벴는가"라면 혼격은 "무엇을 먹였는가"다.
     *
     * ## 왜 이것이 뽑기의 상품인가
     *
     * 46단계의 뽑기가 판 것은 **혼 정수**(★3) 하나뿐이었고, 그것은 티어를
     * 앞당기는 물건이라 리드 상한이 곧 재고였다. 상한에 닿으면 팔 것이
     * 사라진다. 혼격은 그 위에 **두 번째 재고**를 얹는다 - 티어가 상한에
     * 닿아도 혼격은 아직 남아 있고, 혼격이 차도 티어가 다시 열린다.
     *
     * 그리고 이것이 46단계 보고서가 남긴 숙제의 답이다. 뽑기의 보석당
     * 파편은 촉매의 66%라 **파편만 보면 뽑을 이유가 없었다** - ★4가
     * 그 부등호를 뒤집는 자리이고, 뒤집는 방법이 파편을 더 주는 것이
     * 아니라 파편으로 살 수 없는 것을 주는 것이라야 촉매도 함께 산다.
     *
     * ## 상한이 **바퀴**에서 나온다 - 그것이 이 축의 브레이크다
     *
     * 혼 정수(★3)의 상한은 드랍 일정에서 나왔다(GachaCurve.LeadTiers -
     * 한 바퀴만 앞선다). 혼격에는 드랍 일정이 없다. 자연 출처가 아예 없고
     * 뽑기에서만 나오므로, 상한을 밖에서 주지 않으면 보석 무제한 플레이어는
     * st51에 넷을 통째로 상한 혼격까지 올린다.
     *
     * 그래서 같은 시계를 쓴다: **혼격 상한도 바퀴가 정한다**(CapAt -
     * 계약 구간 안에서 한 칸, 다섯 바퀴마다 한 칸 더). 첫 설계는 티어로
     * 잠그는 것이었고 설정으로는 그쪽이 나았는데, 실측이 뒤집었다 -
     * RarityGrowthCycles 주석에 그 실측이 있다.
     *
     * 이 선택이 세 가지를 동시에 한다:
     *
     *   1. 과금 곡선이 계약 구간(st51~200) **안에서 발산하지 않는다** -
     *      상한이 그 구간 내내 상수이므로 재기준이 한 번의 이동으로 끝난다
     *   2. 46단계의 리드 상한과 **같은 모양**이라 두 상한을 한 문장으로
     *      설명할 수 있다 - "이 축의 시계는 언제나 한 바퀴다"
     *   3. 계약 밖에서 자라므로 상점의 재고가 안 끝난다(46단계
     *      LeadGrowthCycles가 푼 것과 같은 문제)
     *
     * ## 곱연산이고, 그 자루의 셋 모두에 곱한다
     *
     * 티어 배수(공격력)·상성(오의)·영체(초당환산) 셋 다 그 자루의 것이므로
     * 셋 다 오른다 - "혼이 짙다"가 한 군데만 세지면 그것은 혼이 아니라
     * 스탯 하나다. 45단계가 티어 하나를 세 곳에서 읽게 만든 구조를 그대로
     * 물려받는 자리이고, 그래서 이 파일에 새 저울이 없다.
     */
    public static class YodoRarityCurve
    {
        // ---------------------------------------------------------------- 칸

        /**
         * @brief 혼격의 최고 칸. **0은 격이 없는 보통 혼이다.**
         *
         * 0이 실제 상태라는 것이 요점이다 - 보스가 떨어뜨리는 혼은 언제나
         * 혼격 0이고, 그것이 무과금이 평생 서 있는 자리다. 값이 정확히 1로
         * 떨어져야 f2p 바닥이 이 축과 무관해진다(ValueAt 주석).
         */
        public const int MaxRarity = 4;

        /**
         * @brief 계약 구간 안에서 허용되는 혼격. **한 칸이다.**
         *
         * 46단계의 LeadTiers와 같은 자리, 같은 값, 같은 이유다.
         */
        public const int BaseCap = 1;

        /**
         * @brief 혼격 상한이 한 칸 자라는 주기 (바퀴 수). **5다 - 리드와 같다.**
         *
         * ## 처음에 티어로 잠갔다가 실측이 뒤집었다
         *
         * 첫 설계는 "혼격 상한 = 티어 / 2"였다("얕게 벼린 칼에 짙은 혼을
         * 먹이면 칼이 먼저 부러진다"). 설정으로는 그쪽이 낫고 상한도 잘
         * 걸리는데, **하네스가 다른 것을 잡았다** - 티어가 심층 계약 구간
         * 안에서 3에서 5로 자라므로 혼격도 그 안에서 1에서 2로 자라고,
         * 그러면 과금 곡선이 계약 구간 **안에서 발산**한다.
         *
         * 수렴 검사(StageSimulationTests.DeepZone_MarginConverges - st100과
         * st200의 여유가 35% 안)가 1.335로 문턱 1.35에 4%까지 붙었다.
         * 통과하긴 하지만 그것은 통과가 아니라 우연이다.
         *
         * ## 그래서 시계를 티어에서 **바퀴**로 옮겼다
         *
         * 46단계가 리드 상한에서 이미 푼 문제이고 답도 같다: 계약 구간
         * (st51~200) 안에서 상한이 **상수**여야 재기준이 한 번의 이동으로
         * 끝난다. 다섯째 혼이 st220 언저리이므로 주기를 5로 두면 계약 구간
         * 안에서 혼격 상한은 영원히 1이고, 계약 밖에서만 자란다.
         *
         * 두 상수가 같은 값(5)인 것은 우연이 아니라 같은 사실 때문이다 -
         * 이 축의 시계는 언제나 **한 바퀴**다(YodoCurve 머리 주석). 갈라
         * 두는 이유는 둘이 다른 것을 재기 때문이고(저쪽은 티어, 이쪽은
         * 격), 밴드를 st500까지 늘리는 스텝이 둘을 함께 봐야 한다.
         */
        public const int RarityGrowthCycles = 5;

        /**
         * @brief 혼격 한 칸의 배수. **세 곳에 각각 곱해진다.**
         *
         * ## 왜 티어 한 칸(1.035)보다 큰가
         *
         * 계약 구간 안에서 이 축이 **한 칸밖에 안 오르기 때문**이다(위
         * RarityGrowthCycles). 티어는 그 구간에서 다섯 칸을 오르므로 한
         * 칸이 작아도 되지만, 혼격은 한 칸이 곧 전부다 - 작게 두면 400
         * 스테이지 동안 화면에서 아무 일도 안 일어난다.
         *
         * 곱해지는 자리가 셋(공격력·상성·영체)이고 자루가 넷이라, 계약
         * 구간의 실제 크기는 x{RarityStep}^4 x (상성·영체 몫)이다. 1.05면
         * 공격력에서만 x1.216이고 46단계의 리드 한 칸(x1.27)보다 조금
         * 작다 - 새 축 하나가 앞 스텝의 축 하나보다 커지지 않는 자리다.
         *
         * 값은 실측이 정했다(하네스 - 심층 천장 재기준이 46단계의 +28%보다
         * 작게 끝나는 가장 큰 값). 46단계가 리드 L을 셋 재고 1을 고른 것과
         * 같은 방법이다.
         */
        public const double RarityStep = 1.05d;

        /**
         * @brief 이 혼격의 배수. **0이면 정확히 1이다.**
         *
         * 1로 떨어지는 것이 이 스텝의 f2p 안전선이다 - 무과금은 혼격을 한
         * 칸도 못 올리므로(자연 출처가 없다) 기대 곡선과 보정이 44·45단계
         * 그대로 남고, 바닥이 비트 단위로 보존된다. 44단계의 TierValue,
         * 45단계의 AffinityCurve.ValueAt이 같은 규칙 위에 섰다.
         *
         * 상한 위는 자른다. 값에서 자르고 저장된 격은 안 자르는 규칙
         * (YodoCurve.TierValue)과 같다.
         */
        public static double ValueAt(int rarity)
        {
            if (rarity < 1) return 1d;
            int r = rarity > MaxRarity ? MaxRarity : rarity;
            return Math.Pow(RarityStep, r);
        }

        /** 한 자루가 상한 혼격에서 얻는 배수. 보고와 테스트가 쓴다 */
        public static double Ceiling { get { return ValueAt(MaxRarity); } }

        // ---------------------------------------------------------------- 상한

        /**
         * @brief soulIndex번 자루가 frontierStage에서 허용받는 혼격 상한.
         *
         * 바퀴 수로 센다 - GachaCurve.EssenceSoulCap과 **같은 모양이고
         * 같은 함수를 지난다**(YodoCurve.SoulsBeforeStage). 두 상한이 다른
         * 시계를 쓰면 "티어는 열렸는데 격은 안 열린다"가 스테이지마다
         * 다르게 나타나고, 그것은 상한이 아니라 잡음이다.
         */
        public static int CapAt(int soulIndex, int frontierStage)
        {
            // 한 칸 뒤를 보는 것은 "떨어진 개수"를 세기 때문이다.
            // GachaCurve.SoulsDroppedThrough와 같은 산수, 같은 이유
            int dropped = YodoCurve.SoulsBeforeStage(soulIndex, frontierStage + 1);

            int cap = RarityGrowthCycles > 0
                ? BaseCap + dropped / RarityGrowthCycles
                : BaseCap;
            return cap > MaxRarity ? MaxRarity : cap;
        }

        /**
         * @brief 이 자루가 지금 상위 혼(★4)을 받을 수 있는가.
         *
         * 미봉인은 못 받는다 - 44단계의 "첫 봉인은 그 요괴를 벤 사람의 것"이
         * 여기서도 그대로다. 혼격만 올려 미봉인 칼을 세게 만들 수 있으면
         * 도감의 첫 줄이 결제로 켜진다.
         */
        public static bool Accepts(int soulIndex, int frontierStage, int tier, int rarity)
        {
            if (tier < 1) return false;
            return rarity < CapAt(soulIndex, frontierStage);
        }

        /**
         * @brief 상위 혼이 갈 자루. 없으면 -1.
         *
         * **가장 낮은 혼격으로 간다** - 46단계의 혼 정수가 가장 낮은 티어로
         * 간 것과 같은 규칙이고 같은 이유다(GachaCurve.EssenceTargetFor):
         * 타겟팅은 현금 확정팩의 몫이고, 뽑기가 한 자루를 지목할 수 있으면
         * 45단계가 만든 트레이드오프(상성은 몰아주기 / 영체는 고르기)에서
         * 과금은 언제나 몰아주기를 산다.
         *
         * 같은 혼격이면 앞의 자루가 이긴다 - 카탈로그 순서가 곧 획득
         * 순서다(YodoCatalog 머리 주석).
         */
        public static int TargetFor(int frontierStage, int[] tiers, int[] rarities)
        {
            if (tiers == null) return -1;

            int best = -1;
            int bestRarity = int.MaxValue;
            int count = Math.Min(YodoCatalog.Count, tiers.Length);

            for (int i = 0; i < count; i++)
            {
                int rarity = At(rarities, i);
                if (!Accepts(i, frontierStage, tiers[i], rarity)) continue;
                if (rarity >= bestRarity) continue;

                best = i;
                bestRarity = rarity;
            }

            return best;
        }

        // ---------------------------------------------------------------- 읽기

        /** i번 자루의 혼격. 배열이 짧거나 없으면 0 */
        public static int At(int[] rarities, int index)
        {
            if (rarities == null || index < 0 || index >= rarities.Length) return 0;
            int value = rarities[index];
            return value < 0 ? 0 : value;
        }

        /**
         * @brief 네 자루의 혼격이 **공격력에** 곱하는 배수.
         *
         * 미봉인 자루는 세지 않는다. 티어 0에 혼격이 남아 있는 경우는
         * 세이브가 손상됐거나 상한이 내려간 업데이트뿐인데, 그 자루의 티어
         * 배수가 이미 1이므로 혼격만 살아 있으면 "없는 칼이 공격력을 준다"가
         * 된다.
         */
        public static double AttackFactor(int[] tiers, int[] rarities)
        {
            if (tiers == null) return 1d;

            double product = 1d;
            int count = Math.Min(YodoCatalog.Count, tiers.Length);
            for (int i = 0; i < count; i++)
            {
                if (tiers[i] < 1) continue;
                product *= ValueAt(At(rarities, i));
            }
            return product;
        }

        // ---------------------------------------------------------------- 표시

        /**
         * @brief 혼격을 별로 적는다. "★★☆☆"
         *
         * 새 스프라이트가 없다는 것이 이 축의 아트 예산 전부다(사양 R-2).
         * 별 두 글자(U+2605 / U+2606)는 Galmuri11에 이미 있고, 차셋에
         * 더하는 것으로 끝난다 - 44단계의 요도 아이콘이 기존 시트의 색
         * 변형으로 선 것과 같은 판단이다.
         *
         * 채운 별이 0개면 빈 문자열이다. 혼격 0은 **격이 없는 것**이지
         * "별 넷 중 0"이 아니라서, 도감의 줄에 ☆☆☆☆가 서 있으면 무과금의
         * 모든 자루가 미완성으로 읽힌다.
         */
        public static string Stars(int rarity)
        {
            if (rarity < 1) return string.Empty;

            int filled = rarity > MaxRarity ? MaxRarity : rarity;
            var text = new System.Text.StringBuilder(MaxRarity);
            for (int i = 0; i < MaxRarity; i++) text.Append(i < filled ? '★' : '☆');
            return text.ToString();
        }
    }
}
