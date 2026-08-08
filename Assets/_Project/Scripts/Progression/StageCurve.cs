using System;
using Onikiri.Core;

namespace Onikiri.Progression
{
    /**
     * @brief 스테이지가 오를 때 요괴 체력과 골드 보상이 자라는 규칙.
     *
     * 이것이 존재하는 이유는 방치형에서 성장이 죽는 방식이 하나이기 때문이다.
     * 요괴 체력이 고정이면 공격력을 올리는 순간 요괴가 한 방에 죽고, 그 뒤로는
     * 공격력을 아무리 더 올려도 수입이 1원도 늘지 않는다. 처치 속도가 공격력이
     * 아니라 요괴 공급에 묶이기 때문이다.
     *
     * 해결은 튜닝이 아니라 구조다. 스테이지마다 체력을 함께 올려 **처치당 타격 수**를
     * 일정 범위로 유지하면, 공격력과 공격속도가 둘 다 끝까지 의미를 갖는다.
     *
     * 골드 성장률을 체력 성장률보다 조금 높게 잡은 것은 의도적이다. 같으면 수입과
     * 강화 비용이 정확히 평형을 이뤄 진행이 멈춘 것처럼 느껴진다.
     */
    public static class StageCurve
    {
        /** 다음 스테이지로 넘어가는 데 필요한 처치 수 */
        public const int KillsPerStage = 10;

        /** 스테이지당 요괴 체력 배수 */
        public const double HealthGrowth = 1.55d;

        /**
         * @brief 스테이지당 골드 보상 배수.
         *
         * 임의로 고른 값이 아니라 다른 세 곡선에서 유도된 값이다. 체력을 따라잡으려면
         * 스테이지마다 공격력 레벨이 이만큼 필요하다:
         *
         *     레벨/스테이지 = ln(HealthGrowth) / ln(공격력 배수)
         *                   = ln(1.55) / ln(1.12) = 3.87
         *
         * 강화 비용은 레벨마다 1.15배이므로, 그만큼 레벨을 올리는 데 드는 비용은
         * 스테이지마다 1.15^3.87 = 1.72배가 된다. 골드가 그보다 느리게 자라면
         * 스테이지가 오를수록 살 수 있는 레벨이 줄어들고, 처치당 타격 수가 서서히
         * 불어나 후반이 늘어진다. 반대로 빠르면 요괴가 한 방에 죽는 상태로 돌아간다.
         *
         * 처음에 1.62로 잡았다가 시뮬레이션에서 50스테이지쯤 뒤 타격 수가 계속 늘어나는
         * 것을 확인하고 고쳤다. StageProgressionTests가 이 관계를 못 박는다.
         */
        public const double GoldGrowth = 1.72d;

        // ---------------------------------------------------------------- 보스

        /**
         * @brief 1스테이지 보스의 체력 배수 (해당 스테이지 잡몹 체력 기준).
         *
         * 이 값의 뜻은 "잡몹 몇 마리 분량"이 아니라 **제한 시간의 압박**이다.
         * 잡몹은 큐로 한 마리씩 들어오므로 처치 속도가 스폰 공급에 묶이지만,
         * 보스는 처음부터 나와 있어서 순수하게 DPS로만 깎인다.
         *
         * 8은 무강화 플레이어가 1스테이지 보스를 아슬아슬하게 잡는 값이다
         * (17.7초 / 때릴 수 있는 24.7초). 첫 보스는 벽이 아니라 튜토리얼이어야 한다.
         */
        /**
         * 8에서 9.72로 올렸다 (17단계). 보스 타이머가 스폰이 아니라 **도달**
         * 시점부터 돌기 시작하면서 때릴 수 있는 시간이 24.7초에서 30초로
         * 늘었기 때문이다 - 30/24.7 = 1.2146 배만큼 여유가 통째로 위로 떴고,
         * 같은 비율을 체력에 곱해 상쇄한다.
         *
         * 램프가 아니라 기본 배수를 쓴 이유는 타이머 변경이 **전 구간에 균일하게**
         * 작용하기 때문이다. 램프는 스테이지마다 곱해져 후반에 치우친다.
         */
        public const double BossHealthMultiplierBase = 10.8d;

        /**
         * @brief 스테이지마다 체력 배수에 추가로 곱하는 값.
         *
         * 9단계에서는 배수가 8 고정이었고, 그 결과 곡선을 따라가는 플레이어의
         * 보스 여유가 2.1배에서 5.6배로 발산했다. 5스테이지부터 제한 시간이
         * 아무 일도 하지 않는다는 뜻이다. 10단계에서 치명타 두 축이 들어오면서
         * 그 발산은 20스테이지 기준 82배까지 커졌다.
         *
         * 원인은 단순하다. 잡몹 체력은 스테이지마다 1.55배로 자라는데 플레이어의
         * DPS는 골드(1.72배)로 사는 강화 레벨을 통해 그보다 빨리 자란다. 보스가
         * 잡몹과 같은 비율로만 자라면 그 차이가 그대로 여유가 된다.
         *
         * **고정 배율로는 안 된다.** 처음에 1.225 하나로 맞춰봤더니 1~20 스테이지는
         * 밴드에 들어갔지만 30스테이지에서 여유가 0.88로 떨어져 보스가 벽이 됐다.
         * 플레이어의 DPS 성장률이 일정하지 않기 때문이다 - 초반에는 치명타 두 축이
         * 바닥에서 자라며 빠르게 오르고, 공격속도가 Lv.32에서, 치명타율이 Lv.97에서
         * 상한에 닿으면 성장이 공격력과 치명타 피해 둘로 좁아져 느려진다.
         *
         * 그래서 배율도 같은 모양으로 **감쇠**시킨다. 초반 1.34에서 시작해
         * 후반 1.065로 수렴한다. 후자는 우연한 값이 아니라 관측값이다 - 상한에
         * 닿은 뒤 DPS는 스테이지마다 약 1.72배로 자라고 잡몹 체력은 1.55배로
         * 자라므로, 그 차이 1.72/1.55 = 1.11 근처가 평형이다.
         *
         * 계수는 시뮬레이션으로 찾았다. 1~20 여유의 최대/최소 비가 가장 작아지는
         * 지점이며(1.22배), 동시에 50스테이지까지 여유가 1 아래로 내려가지 않는다.
         */
        /**
         * 26단계에 Final이 1.110 -> 1.130으로 올랐다. 스킬이 **후반으로 갈수록
         * 커지는 축**이기 때문이다 - 세 스킬이 Lv.10/15/20에 차례로 열리고
         * 각자 상한까지 자라므로, DPS에서 스킬이 차지하는 몫이 st9의 2%에서
         * st30의 32%까지 단조 증가한다.
         *
         * 램프는 스테이지마다 곱해지므로 후반에 권한이 크고 초반에는 거의
         * 영향이 없다(st1 x1.000, st11 x1.04, st26 x1.24). 여유가 후반에서만
         * 새어 나갈 때 손댈 곳이 여기라는 것은 16단계에 이미 확인된 성질이고,
         * 이번에도 같은 이유로 같은 손잡이를 썼다.
         */
        /**
         * Start도 1.34 -> 1.36으로 올렸다. **st11 하나 때문이다.**
         *
         * 두 계수는 같은 램프의 양 끝이지만 권한이 반대다. Final은 후반을 누르고
         * (0.93^k가 사라진 뒤 남는 값), Start는 **초·중반을 누른다**(0.93^k가 아직
         * 살아 있는 구간). st11은 그 중간이라 Final로는 x1.04밖에 안 움직이는데
         * Start로는 x1.06이 움직인다. 실측으로 st11 여유가 2.96 -> 2.79가 됐고,
         * 천장 3.00까지 0.04였던 여유가 0.21이 됐다 - 16단계부터 "얇은 경계"로
         * 넘겨온 자리가 이번에 실제로 두꺼워졌다.
         *
         * st1이 Start에 전혀 반응하지 않는 것도 여기서는 이점이다 - 1스테이지는
         * 램프를 한 번도 곱하지 않으므로(StepsFrom(1) = 0), 여유 1.61이 바닥
         * 1.5에 가까운데도 이 조정에 밀리지 않는다.
         */
        public const double BossHealthRampStart = 1.36d;
        public const double BossHealthRampFinal = 1.130d;

        /** 램프가 Start에서 Final로 내려오는 속도. 1에 가까울수록 천천히 */
        public const double BossHealthRampDecay = 0.93d;

        /**
         * @brief 이 스테이지 보스의 체력 배수.
         *
         * 닫힌 식이 없어 곱을 직접 돈다. 스테이지 수만큼의 반복이고 호출은
         * 보스 스폰과 시뮬레이션뿐이라 비용은 문제가 되지 않는다.
         */
        public static double BossHealthMultiplier(int stage)
        {
            double multiplier = BossHealthMultiplierBase;
            int steps = StepsFrom(stage);

            for (int k = 0; k < steps; k++)
            {
                double ramp = BossHealthRampFinal
                            + (BossHealthRampStart - BossHealthRampFinal) * Math.Pow(BossHealthRampDecay, k);
                multiplier *= ramp;
            }

            return multiplier;
        }

        /**
         * @brief 보스 골드 = 해당 스테이지 잡몹 골드 x 이 값. **체력 배수와 같다.**
         *
         * ## 웃돈이 있었고, 그것이 st11 부채였다
         *
         * 9단계에는 체력 배수가 8이고 이 값이 12였다. 1.5배의 웃돈이 "30초 제한과
         * 실패 위험을 감수하는 대가"라는 것이 그때의 근거였다.
         *
         * 17단계에 타이머가 스폰이 아니라 도달 시점부터 도는 것으로 바뀌면서
         * 체력 배수가 8 -> 10.8로 올랐는데, **이 값은 12에 그대로 남았다.** 웃돈이
         * 1.5배에서 1.11배로 조용히 줄었고, 그때부터 이 상수는 아무 설계도
         * 표현하지 않는 잔여값이었다.
         *
         * 그 잔여 웃돈이 남긴 것이 16~20단계 보고서가 "얇은 경계"로 적어둔 부채다.
         * 보스 골드는 **다음 스테이지 시작 직전에 한 번에 들어와 곧바로 화력으로
         * 바뀌므로**, 피날레(st10) 다음인 st11에 여유 스파이크를 만든다. 실측으로
         * 그 한 스테이지가 천장 3.00에 0.02까지 붙어 있었고, 그래서 20단계가 골드
         * 축의 액티브 이득을 포기해야 했다 - 보정 지수를 낮추면 st11이 먼저 터졌다.
         *
         * ## 왜 웃돈을 없애는 쪽인가
         *
         * 웃돈의 원래 근거가 "감수할 이유"인데, 9단계 이후 보스는 **선택이 아니라
         * 스테이지 관문**이다(BossGate). 안 싸우면 다음 스테이지가 없으므로 도전할
         * 이유를 골드로 살 필요가 없다. 등급 보상 차등은 남아 있고
         * (BossCurve.ChapterGoldMultiplier / FinaleClearGoldMultiplier), 그쪽은
         * "일반보다 챕터가 후하다"라는 살아 있는 설계다.
         *
         * 그래서 잔여값을 지우고 체력 배수와 같은 10.8로 맞춘다. 이제 이 상수의
         * 뜻은 "보스는 잡몹 10.8마리 몫"으로 한 줄이 되고, st11이 천장에서
         * 떨어져 골드 축 보정 지수를 낮출 자리가 생긴다(GoldAxisMarginExponent).
         */
        public const double BossGoldMultiplier = BossHealthMultiplierBase;

        /** 보스전 제한 시간 (초). 초과하면 스테이지 실패 - 패널티는 없다 */
        public const float BossTimeLimitSeconds = 30f;

        public static BigDouble BossHealth(BigDouble stageMobHealth, int stage)
        {
            return stageMobHealth * BigDouble.FromDouble(BossHealthMultiplier(stage));
        }

        public static BigDouble BossGold(BigDouble stageMobGold)
        {
            return stageMobGold * BigDouble.FromDouble(BossGoldMultiplier);
        }

        /**
         * @brief 1스테이지 기준 잡몹 평균에서 이 스테이지 보스의 체력/보상을 낸다.
         *
         * BossFight가 스폰할 때 부르는 함수이고, StageSimulation이 난이도를 잴 때도
         * 같은 것을 부른다. 두 곳이 각자 곱셈을 하고 있으면 언젠가 한쪽만 고쳐지고,
         * 그때 "계산상으로는 통과하는데 실제로는 실패하는" 상태가 만들어진다.
         */
        /**
         * @brief 골드 획득 축(20단계)이 만든 여유를 보스가 따라가는 보정 배수.
         *
         * ## 왜 필요한가
         *
         * 골드가 늘면 강화를 더 사고, 그만큼 보스 여유가 오른다. 실측 지수는
         * **여유 = 골드배수^0.81**이다(시뮬레이션에서 골드 x1.25가 여유 x1.197).
         *
         * 이 보정이 없으면 축 하나가 전 등급의 밴드를 1.2배씩 밀어 올린다.
         * 그런데 17단계 시점에 일반 밴드 상단이 이미 2.98/3.00까지 차 있어서
         * (그것이 보고서가 적어둔 "얇은 경계"다), 어떤 크기의 골드 축도 천장을
         * 뚫는다. 밴드를 지키면서 넣을 수 있는 최대 축이 x1.10인데, 그 크기는
         * 화면에서 존재감이 없다.
         *
         * ## 왜 램프가 아니라 보정항인가
         *
         * 램프(BossHealthRamp*)는 스테이지마다 **누적**된다. 골드 축은 상한이
         * 있어서 이득이 어느 지점에서 멈추는데, 누적되는 손잡이로 상쇄하면
         * 축이 멈춘 뒤에도 보스만 계속 무거워진다.
         *
         * 이 보정은 축과 같은 모양으로 자라고 **같은 곳에서 멈춘다.** 상쇄해야
         * 할 것과 같은 곡선을 쓰는 것이 요점이다.
         *
         * ## 왜 완전히 상쇄하지 않는가 (0.81 -> 0.55)
         *
         * 실측 지수 0.81을 그대로 쓰면 이득이 **정확히 0이 된다.** 축에 쓴 골드가
         * 화력으로 바뀌는 만큼 보스도 무거워지므로, 남는 것은 그 골드를 다른 축에
         * 썼다면 얻었을 몫뿐이다 - 즉 순손실이다. 20단계 실측에서 30스테이지까지
         * 총 시간이 기준선과 같았고(-0.1%) 초반은 오히려 +3.9% 느렸다.
         *
         * 회수 시간 자로는 "회수된다"고 나오는데 실제로는 손해인 상태이고, 그것은
         * 8단계 공격속도보다 나쁘다 - 그때는 안 사면 그만이었지만 여기서는 지표가
         * 사라고 말한다.
         *
         * **20단계는 그것을 알면서도 0.81을 유지했다.** 지수를 낮추면 st11이
         * 천장을 넘었기 때문이다. st11은 피날레(st10) 직후라 보스 보상 골드가
         * 한꺼번에 화력으로 바뀌는 자리이고, 거기에 보스 골드 웃돈(x12 대 체력
         * x10.8)이 얹혀 있었다. 그것이 16~20단계가 "얇은 경계"로 넘겨온 부채다.
         *
         * ## 26단계에 부채를 먼저 갚고 지수를 낮췄다
         *
         * 순서가 중요하다. 웃돈을 지우자(BossGoldMultiplier 12 -> 10.8) st11의
         * 여유가 천장에서 떨어졌고, 그제야 지수를 낮출 자리가 생겼다. 0.55는
         * 상쇄를 3분의 2쯤만 한다 - 남는 여유 상승분이 골드축 상한 x1.25 기준
         * 1.25^(0.81-0.55) = x1.06이고, 그만큼이 이 축의 **액티브 이득**이다.
         *
         * 더 낮추지 않은 이유는 st11이 다시 천장에 닿기 때문이다. 지수는 이제
         * 밴드 여유가 허락하는 만큼 낮춘 값이지, 이득을 0으로 만드는 값이 아니다.
         *
         * ## 그래서 이 축의 이득은 무엇이 되는가
         *
         * 셋이다. **액티브 진행 속도**(이번에 되살아난 것), **방치 보상**(보스
         * 체력과 무관해서 처음부터 온전했다), 그리고 26단계에 생긴 **스킬 싱크** -
         * 골드가 스킬 레벨을 거쳐 DPS가 되는 통로가 하나 더 늘었다.
         */
        public const double GoldAxisMarginExponent = 0.55d;

        public static double GoldAxisCompensation(int stage)
        {
            return Math.Pow(GoldGainCurve.ExpectedAtStage(stage), GoldAxisMarginExponent);
        }

        /**
         * @brief 장비(32단계)가 만든 여유를 보스가 따라가는 보정 배수.
         *
         * ## 왜 램프가 아니라 보정항인가
         *
         * 26단계에 스킬이 들어왔을 때는 램프(BossHealthRampFinal)를 올려 잡았다.
         * 스킬이 **해금 뒤 끝까지 단조 증가**하는 축이라 램프의 모양과 맞았기
         * 때문이다.
         *
         * 장비는 다르다. st11에 아예 없다가 거기서부터 생긴다. 램프는 스테이지
         * 1부터 누적 곱해지므로 st11 이전까지 함께 무거워지고, 그 구간에는
         * 상쇄할 이득이 없다 - 21단계에 골드 축 보정을 해금 전에도 걸어두고
         * "축은 안 사는데 보정만 걸려 순손실"이었던 것과 정확히 같은 사고다.
         *
         * 보정항은 축과 같은 곡선을 쓰고 같은 곳에서 시작해 같은 곳에서 멈춘다.
         * 골드 획득 축이 같은 이유로 같은 구조를 쓴다.
         *
         * ## 왜 완전히 상쇄하지 않는가
         *
         * 지수 1.0이면 이 축의 이득이 정확히 0이 된다 - 장비에 쓴 골드가 화력이
         * 되는 만큼 보스도 무거워지므로, 남는 것은 그 골드를 다른 축에 썼다면
         * 얻었을 몫뿐이고 그것은 순손실이다. 20단계 골드 축이 그 상태였고,
         * 그때 얻은 교훈이 "지표는 사라고 말하는데 실제로는 손해"였다.
         *
         * 실측 지수는 아래 EquipmentMarginExponent 주석에 적어둔다. 그보다
         * 낮게 잡은 만큼이 이 pillar의 **액티브 이득**이다.
         */
        /**
         * 실측: 장비를 편입하기 전과 후를 나란히 돌려 (여유 비) / (장비 배수)의
         * 로그 비로 잰 값이 **0.92**였다. 1에 가까운 이유는 장비가 골드 획득
         * 축과 달리 **곧바로 DPS**이기 때문이다 - 골드 축은 수입을 늘려 간접적으로
         * 화력이 되므로 0.81이었지만, 무기 배수는 공격력에 그대로 곱해진다.
         *
         * 0.78로 잡았다. 남는 이득이 x2.0 기준 2.0^(0.92-0.78) = **x1.10**이고,
         * 그것이 장비 pillar의 액티브 진행 이득이다. 더 낮추지 못한 이유는 st11과
         * st20이다 - 0.70에서 st11이 천장 3.00을 넘었다(3.06).
         */
        public const double EquipmentMarginExponent = 0.70d;

        public static double EquipmentCompensation(int stage)
        {
            return Math.Pow(EquipmentCurve.ExpectedPowerAtStage(stage), EquipmentMarginExponent);
        }

        public static BigDouble BossHealthForStage(BigDouble averageMobHealth, int stage)
        {
            var health = BossHealth(averageMobHealth * HealthMultiplier(stage), stage);

            // 골드 축이 만든 여유를 보스가 따라간다. 곱하는 자리가 여기인 이유는
            // 시뮬레이션과 BossFight가 둘 다 이 함수를 지나기 때문이다 - 한쪽에만
            // 넣으면 "계산상으로는 통과하는데 실제로는 실패하는" 상태가 만들어진다
            health *= BigDouble.FromDouble(GoldAxisCompensation(stage));

            // 32단계의 장비도 같은 자리다. 두 보정이 곱해지는 것이 맞다 - 두
            // 축이 각자 독립으로 여유를 밀어 올리므로, 상쇄도 각자여야 한다
            health *= BigDouble.FromDouble(EquipmentCompensation(stage));

            // 등급별 추가 배수. **이 한 줄이 빠져 있었다.**
            //
            // BossCurve에 상수를 선언하고 "1보다 큰가"만 검사하는 테스트를 뒀는데,
            // 그 값이 실제로 쓰이는지는 아무도 확인하지 않았다. 치명타 축에서
            // 같은 함정을 막으려고 CritAxes_FeedTheDpsFormula 를 만들어 놓고
            // 여기서는 그러지 않았다. 상수의 존재는 연결의 증거가 아니다.
            //
            // 13단계에서 등급이 셋(일반/챕터/피날레)이 되면서 분기가 아니라
            // 조회로 바뀌었다. 등급이 하나 더 늘어도 이쪽은 고칠 것이 없다
            return health * BigDouble.FromDouble(BossCurve.HealthMultiplierFor(stage));
        }

        public static BigDouble BossGoldForStage(BigDouble averageMobGold, int stage)
        {
            var gold = BossGold(averageMobGold * GoldMultiplier(stage));
            return gold * BigDouble.FromDouble(BossCurve.GoldMultiplierFor(stage));
        }

        /**
         * @brief 스테이지 클리어 보너스 골드. 그 스테이지 잡몹 골드의 배수.
         *
         * ## 왜 작게 잡는가
         *
         * 16단계에서 정확히 이 종류의 사고가 있었다. 피날레 골드를 x3으로 두자
         * 그 골드가 즉시 화력으로 바뀌어 **다음 스테이지 보스 여유가 천장을
         * 뚫었다**(st11이 3.48까지). 결국 골드를 낮추고 그만큼을 경험치로 옮겼다.
         *
         * 클리어 보너스도 같은 위험이다. 그래서 "축하 숫자"로 보이되 밸런스는
         * 흔들지 않는 크기로 잡는다 - 보스 처치 골드(잡몹 골드 x12)의
         * 4분의 1과 2분의 1이다. 화면에는 큰 숫자가 뜨지만 실제로는 반 스테이지
         * 분량의 파밍에 해당한다.
         *
         * 축하의 크기가 더 필요하면 골드가 아니라 경험치나 보석으로 얹는 것이
         * 맞다. 골드만이 즉시 DPS로 환산된다.
         */
        public const double ClearGoldMultiplier = 2d;

        /**
         * 지역 피날레.
         *
         * 처음에 x6으로 잡았다가 x3으로 내렸다. 시뮬레이션에서 **피날레 직후
         * 스테이지(st11)의 여유가 3.39까지** 올라갔기 때문이다 - 16단계에서
         * 피날레 골드로 겪은 것과 정확히 같은 사고를 클리어 보너스로 한 번 더
         * 낸 셈이다.
         *
         * 축하의 크기가 더 필요하면 골드가 아니라 경험치나 보석으로 얹는다.
         *
         * ## 26단계에 x2로 내려봤다가 되돌렸다
         *
         * st11이 천장을 0.01 넘겼을 때 여기가 남은 웃돈이라 손댔는데, 실측이
         * **3.01 -> 3.03으로 오히려 올랐다.** 구매 순서가 한 칸 달라지며 생긴
         * 노이즈 수준이고, 이 상수가 st11에 갖는 실제 권한이 그만큼 작다.
         *
         * 크기를 보면 당연하다. 피날레 보스 골드가 잡몹 골드의 10.8 x 2.0 = 21.6배인데
         * 클리어 보너스는 3배다 - 그 구간 수입의 1%도 되지 않는다. **x6에서 x3으로
         * 내렸을 때 효과가 있었던 것은 그때 이 값이 6이라 보스 골드의 4분의 1을
         * 차지했기 때문이지, 이 자리가 st11의 손잡이여서가 아니었다.**
         *
         * 효과 없는 변경으로 축하를 깎지 않는다. st11은 램프 기울기로 잡았다
         * (BossHealthRampStart).
         */
        public const double FinaleClearGoldMultiplier = 3d;

        public static BigDouble ClearGoldForStage(BigDouble averageMobGold, int stage)
        {
            double multiplier = BossCurve.TierOf(stage) == BossCurve.Tier.Finale
                ? FinaleClearGoldMultiplier
                : ClearGoldMultiplier;

            return averageMobGold * GoldMultiplier(stage) * BigDouble.FromDouble(multiplier);
        }

        /** stage(1부터)의 체력 배수. 1스테이지는 1배 */
        public static BigDouble HealthMultiplier(int stage)
        {
            return BigDouble.Pow(BigDouble.FromDouble(HealthGrowth), StepsFrom(stage));
        }

        /** stage(1부터)의 골드 배수. 1스테이지는 1배 */
        public static BigDouble GoldMultiplier(int stage)
        {
            return BigDouble.Pow(BigDouble.FromDouble(GoldGrowth), StepsFrom(stage));
        }

        private static int StepsFrom(int stage)
        {
            return stage > 1 ? stage - 1 : 0;
        }

        /**
         * @brief 이 체력을 이 데미지로 처치하는 데 필요한 타격 수.
         *
         * 밸런싱의 핵심 지표다. 이 값이 1로 내려앉으면 공격력 강화가 무의미해지고,
         * 너무 커지면 한 마리 잡는 데 지루해진다.
         */
        public static int HitsToKill(BigDouble health, BigDouble damage)
        {
            if (damage <= BigDouble.Zero) return int.MaxValue;

            BigDouble hits = health / damage;
            double value = hits.ToDouble();
            if (double.IsInfinity(value) || value > int.MaxValue) return int.MaxValue;

            int rounded = (int)value;
            // 나머지가 남으면 한 대 더 때려야 한다
            return BigDouble.FromDouble(rounded) < hits ? rounded + 1 : (rounded < 1 ? 1 : rounded);
        }
    }
}
