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
         * @brief 무한 구간(st51+)의 램프 하한 (42단계).
         *
         * Final(1.130)은 "상한에 닿은 뒤 DPS는 스테이지마다 약 1.72배, 잡몹
         * 체력은 1.55배, 그 차이 1.11 근처가 평형"이라는 관측에서 온 값인데,
         * 콘텐츠 끝(st50)을 지나 계속 돌려보니 그 평형이 틀렸다 - 실측으로
         * 여유가 스테이지당 x1.008씩 단조 발산한다(st200 천장 8.4배). 캐릭터
         * 레벨(경험치가 뒤로 갈수록 유리한 37단계 구조)의 스탯 증폭이 Final을
         * 잡을 때 계산에 없던 성장 축이기 때문이다.
         *
         * 그래서 st51부터 램프가 이 값 아래로 내려가지 않는다. 값은 하네스
         * 실측으로 잡았다 - 발산분 x1.008을 상쇄해 f2p 바닥과 천장이 각자
         * 수평선에 수렴하는 지점이다.
         *
         * **st50까지는 비트 단위로 이전과 같다.** 코리더(1~30)와 가속 구간
         * (31~50)의 밴드 앵커를 전부 그대로 두는 것이 이 게이트의 이유다 -
         * 무한은 후반 구간이고, 조율이 끝난 구간을 다시 여는 비용은 이
         * 스텝이 감당할 크기가 아니다.
         */
        public const double BossHealthRampDeep = 1.138d;

        /** 심층 램프가 시작되는 스테이지. 이 앞은 기존 곡선 그대로다 */
        public const int DeepRampStartStage = 51;

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

                // 무한 구간(42단계). k번째 곱은 st(k+1) -> st(k+2)로 가는
                // 걸음이므로, st51에 도착하는 걸음부터 = k >= DeepRampStartStage - 2
                if (k >= DeepRampStartStage - 2 && ramp < BossHealthRampDeep)
                    ramp = BossHealthRampDeep;

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
        /**
         * 43단계에 0.55 -> 0.52. 미세화가 st1~10의 지출 결을 바꿔 골드 축의
         * 10스테이지 이득이 -0.13초로 뒤집혔다(죽은 버튼 검사에 걸림). 실측
         * 스윙이 지수 0.01당 약 0.17초라 세 눈금 낮춰 이득을 +0.2초대로
         * 되살린다 - st11 천장 여유는 42단계에 이미 두꺼워졌고(0.21), 이
         * 완화가 미는 폭은 그 1/10이다.
         */
        public const double GoldAxisMarginExponent = 0.52d;

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

        /**
         * @brief 전직(진화)이 만든 여유를 보스가 따라가는 보정 배수. **밴드 재유도.**
         *
         * ## 구조는 장비와 같다 - 보정항이고, 축과 같은 곡선을 쓴다
         *
         * 전직은 Lv.30(곡선 추종 기준 st37)에 아예 없다가 거기서부터 생긴다.
         * 램프는 스테이지 1부터 누적 곱해지므로 해금 전 구간까지 함께 무거워지고,
         * 그 구간에는 상쇄할 이득이 없다 - 장비가 램프 대신 보정항을 쓴 것과
         * 같은 이유다(EquipmentMarginExponent 주석).
         *
         * ## 지수의 뜻은 장비와 다르다 - 여기가 밴드 재유도의 손잡이다
         *
         * 장비 지수(0.70)는 "밴드 안에 축을 밀어 넣는" 값이었다. 32단계 말미의
         * 실측이 말하듯 그 밴드에는 이제 자리가 없다(피날레 비 1.478 중 1.22
         * 사용). 그래서 이번에는 축을 줄이는 대신 **밴드를 갈랐다**:
         *
         *   조율 코리더 (st1~30)   기존 밴드 그대로. 전직이 구조적으로 없어서
         *                          이 보정도 1.0이다 (기대 티어 0)
         *   가속 구간 (st31~50)    바닥과 천장이 서로 다른 플레이어를 잰다.
         *                          바닥 = 무과금(보석 하한, 1티어) 클리어 보장.
         *                          천장 = 과금(무제한, 6티어) 가속 곡선의 상한
         *
         * 지수 0.42는 그 두 끝에서 유도된 값이다:
         *
         *   무과금 바닥   보정이 최종 x2.09^0.42 = x1.36까지 걸리는 동안 무과금은
         *                x1.10(1티어)뿐이다. st50 피날레 실측 1.43 x 1.10 / 1.36
         *                = 1.16 - "노력하면 깬다"의 바닥(>= 1.10) 위에 선다
         *   과금 가속     남는 몫 x2.09^0.58 = x1.54가 여섯 티어의 실제 가속이다.
         *                지수 1.0이면 이득이 0이 되어 20단계 골드 축의 함정
         *                ("지표는 사라는데 실제로는 손해")이 재현된다
         *
         * 무전직 비교군(전직을 한 번도 안 사는 플레이어)은 st50에서 여유가 1.0
         * 언저리로 내려온다. 콘텐츠의 끝에서 전직이 사실상 필수가 되는 것은
         * 이 설계의 의도다 - 사다리의 마지막이 벽이어야 그 앞의 보석이 값을
         * 가진다. 단 무과금도 1티어는 사다리에 오르므로(보석 60, f2p 여유분)
         * 바닥 보장과 모순되지 않는다.
         */
        public const double EvolutionMarginExponent = 0.42d;

        public static double EvolutionCompensation(int stage)
        {
            return Math.Pow(EvolutionCurve.ExpectedPowerAtStage(stage), EvolutionMarginExponent);
        }

        /**
         * @brief 펫(동료)이 만든 여유를 보스가 따라가는 보정 배수. **예약 몫에 착지한다.**
         *
         * ## 구조는 장비·전직과 같다 - 보정항이고, 축과 같은 곡선을 쓴다
         *
         * 펫은 st31에 아예 없다가 거기서부터 생긴다(PetCurve.UnlockStage).
         * 램프는 스테이지 1부터 누적 곱해지므로 해금 전 구간까지 함께
         * 무거워지고, 그 구간에는 상쇄할 이득이 없다 - 21단계 골드 축의 사고를
         * 막는 같은 구조를 네 번째로 쓴다.
         *
         * ## 지수는 33단계가 예약해 둔 몫에서 유도된 값이다
         *
         * 가속 구간 천장은 펫 몫 x1.25를 **미리 포함해서** 잡혀 있고
         * (StageSimulationTests.PetReserveMultiplier), 그때의 가정이 "명목
         * x1.5 안팎, 보정이 3분의 2쯤 상쇄"였다. 0.45가 정확히 그 산수다:
         *
         *   보정   x1.5^0.45 = x1.20 만큼 보스가 무거워진다
         *   실가속 x1.5^0.55 = x1.25 - 예약된 자리 그대로
         *
         * 지수 1.0이면 이득이 0이 되어 20단계 골드 축의 함정("지표는 사라는데
         * 실제로는 손해")이 재현된다. 남는 x1.25가 이 축의 액티브 이득이고,
         * 밴드를 재유도하지 않아도 되는 이유가 이 예약이다.
         */
        public const double PetMarginExponent = 0.45d;

        public static double PetCompensation(int stage)
        {
            return Math.Pow(PetCurve.ExpectedMultiplierAtStage(stage), PetMarginExponent);
        }

        /**
         * @brief 발도 개방(43단계)이 만든 여유를 보스가 따라가는 보정 배수.
         *
         * ## 구조는 장비·전직·펫과 같다 - 보정항이고, 축과 같은 곡선을 쓴다
         *
         * 개방(치명타 60% -> 100% + 초월·연격)은 st59 언저리(벽 통과 실측)에
         * 아예 없다가 거기서부터 생긴다. 램프로 상쇄하면 벽 앞 구간까지 함께
         * 무거워진다 - 21단계 골드 축의 사고를 막는 같은 구조를 다섯 번째로
         * 쓴다.
         *
         * ## 지수의 산수 - 착지분에만 걸린다
         *
         * 무보정 착지(비무력화/무력화 여유 비, 드립 제거 후)가 x4.65다.
         * 지수 0.62로 보정이 x4.65^0.62 = x2.59까지 걸리고, 남는
         * x4.65^0.38 = **x1.79가 개방의 액티브 이득**이다 - 콘텐츠의 끝을
         * 지나 온 플레이어에게 주는 마지막 대도약답게 네 기둥(골드 x1.06 /
         * 장비 x1.10 / 동료 x1.25 / 전직 x1.54)을 전부 넘는 가장 큰 축이다.
         * 지수 1.0이면 이득이 0이 되어 20단계 골드 축의 함정("지표는
         * 사라는데 실제로는 손해")이 재현된다.
         *
         * 초월의 드립(착지 뒤 x1.0117/스테이지)은 지수 없이 **전량** 흡수한다
         * (MasteryCompensation 본문 주석) - 무한 밴드의 수렴이 그 조건이다.
         */
        public const double MasteryMarginExponent = 0.62d;

        public static double MasteryCompensation(int stage)
        {
            // 두 부분이다. 착지분(치명타 재평가 + 연격 상한 + 초월 초기 칸)은
            // 지수 < 1로 눌러 액티브 이득을 남기고, 착지 뒤의 초월 드립은
            // **전량** 흡수한다 - 드립까지 지수로 남기면 여유가 다시 발산한다
            // (42단계 심층 램프가 잡은 것과 같은 병이 재발한다)
            return Math.Pow(TranscendCurve.ExpectedLandingAtStage(stage), MasteryMarginExponent)
                 * TranscendCurve.ExpectedDripAtStage(stage);
        }

        /**
         * @brief 요도(44단계)가 만든 여유를 보스가 따라가는 보정 배수. **일곱 번째 항.**
         *
         * ## 구조는 앞의 다섯 보정과 같다 - 보정항이고, 축과 같은 곡선을 쓴다
         *
         * 요도는 st50 피날레에 첫 혼이 떨어지기 전까지 아예 없다. 램프는
         * 스테이지 1부터 누적 곱해지므로 해금 전 구간까지 함께 무거워지고, 그
         * 구간에는 상쇄할 이득이 없다 - 21단계 골드 축의 사고를 막는 같은
         * 구조를 여섯 번째로 쓴다(다섯째가 발도 개방, 여섯째가 방향이 반대인
         * CostRaiseRelief다).
         *
         * **st50까지 정확히 1이다.** YodoCurve.ExpectedMultiplierAtStage가
         * 그 구간에서 1을 내므로 계수가 아니라 구조가 그것을 지킨다 - 조율
         * 코리더(1~30)와 가속 구간(31~50)이 이 스텝에서 비트 단위로 불변인
         * 근거가 여기다.
         *
         * ## 지수의 산수 - 이 축은 두 계단과 한 드립이다
         *
         * 앞의 축들과 모양이 다르다. 요도는 매끈하게 자라지 않는다:
         *
         *   st50~80  네 자루가 차례로 봉인된다. 세트 보너스가 함께 오르고
         *            st80에 오니키리(x1.25)가 완성된다 - **큰 계단 넷**
         *   st80~440 한 바퀴(40스테이지)마다 네 자루가 한 티어씩 - **드립**
         *   그 뒤     상한(MaxTier). 배수가 멈추고 밴드는 다시 수평이 된다
         *
         * 드립까지 지수로 남기면 심층 밴드가 40스테이지마다 x1.148씩 발산한다 -
         * 42단계가 심층 램프로 잡은 그 병이다. 그래서 MasteryCompensation과
         * 같은 처방을 쓸 수도 있었지만(착지분만 지수, 드립은 전량 흡수) 여기서는
         * 나누지 않았다. **드립이 유한하기 때문이다** - 상한이 열 바퀴에서
         * 닫히므로 발산이 아니라 유한한 계단 열 개이고, 그 총합을 지수 하나로
         * 눌러도 수렴이 깨지지 않는다. 실측이 그것을 확인했다(심층 f2p 바닥과
         * 천장이 st400까지 밴드 안).
         *
         * 지수는 실측으로 잡는다. 1.0이면 이득이 정확히 0이 되어 20단계 골드
         * 축의 함정("지표는 사라는데 실제로는 손해")이 재현되고, 너무 낮으면
         * 심층 천장을 뚫는다.
         */
        public const double YodoMarginExponent = 0.70d;

        /**
         * @brief 44단계의 몫 - **요도 티어 배수(공격력)**만 따라간다.
         *
         * 45단계에 상성·영체가 붙으면서 요도 보정이 둘로 갈렸다. 나눈 이유는
         * 밴드 재현이다: 44단계의 세계를 다시 만들려면 새 두 축의 보정만
         * 걷어내야 하는데(Policy.NeutralizeYodoPower), 곱이 하나면 걷어낼
         * 수가 없다. 43단계가 NeutralizeMastery로 42단계를 재현한 것과 같은
         * 자리이고, 그때는 보정 전체를 걷어내면 됐지만 여기는 축이 **같은
         * 요도 위에 층으로** 쌓여서 한 겹만 벗겨야 한다.
         *
         * 값은 44단계와 비트 단위로 같다 - 같은 함수, 같은 지수다.
         */
        public static double YodoBladeCompensation(int stage)
        {
            return Math.Pow(YodoCurve.ExpectedMultiplierAtStage(stage), YodoMarginExponent);
        }

        /**
         * @brief 45단계의 몫 - **상성과 영체**를 따라간다.
         *
         * ## 왜 지수를 따로 두는가
         *
         * 두 축은 티어 배수와 같은 티어를 읽지만 곡선의 모양이 다르다. 티어
         * 배수는 st80에 계단 넷(봉인·세트)을 몰아 놓고 그 뒤가 드립인데,
         * 상성·영체는 **드립 쪽에 무게가 있다**(영체 티어 스텝 1.15). 한
         * 지수로 누르면 앞 구간과 뒤 구간 중 한쪽이 반드시 어긋난다.
         *
         * 값은 실측으로 잡는다. 1.0이면 이 스텝의 두 축이 통째로 죽은
         * 버튼이 되고(20단계 골드 축의 함정), 낮으면 심층 천장을 뚫는다 -
         * 44단계가 남긴 여유가 7% / 9% / 4%뿐이라 그 폭이 좁다.
         *
         * **st50까지 정확히 1이다.** 봉인된 요도가 없으면 상성 배수도 1이고
         * 영체 배율도 0이라(YodoSpiritCurve.MultiplierAtTier) 구조가 지킨다 -
         * 44단계가 티어 배수에서 얻은 불변을 그대로 물려받는다.
         */
        public const double YodoPowerMarginExponent = 0.62d;

        public static double YodoPowerCompensation(int stage)
        {
            return Math.Pow(YodoCurve.ExpectedPowerFactorAtStage(stage), YodoPowerMarginExponent);
        }

        /** 요도 축 전체의 보정. 두 겹의 곱이다 */
        public static double YodoCompensation(int stage)
        {
            return YodoBladeCompensation(stage) * YodoPowerCompensation(stage);
        }

        /**
         * @brief 강화 비용 상향(E-3 수정)이 덜어낸 화력을 보스가 따라 내려오는 **완화 배수.**
         *
         * ## 여섯 번째 항이고, 처음으로 나누는 항이다
         *
         * 골드·장비·전직·펫·개방 보정은 축이 플레이어를 **세게** 만들어서 보스를
         * 무겁게 했다. 이번에는 반대다 - 강화 비용이 전 축 x3.5(UpgradeCost.RaiseScale)로
         * 오르면서 곡선 추종 플레이어의 화력이 내려갔고, 코리더가 통째로 가라앉았다
         * (하네스 실측: st30 이후 여유가 정확히 1/3.6로 - 비용 탄력이 1이다).
         * 램프로 되돌리면 안 되는 이유도 다섯 보정과 같다 - 결손은 st1~10에
         * 걸쳐 **차오르는 과도 구간** 뒤 평평해지는데, 램프는 스테이지마다 누적
         * 곱해져 그 모양을 낼 수 없다.
         *
         * ## 마디는 전부 하네스 실측이다
         *
         * 결손 곡선(기준 여유 / 상향 여유)을 st1~200에서 재고, 그 위에 세 제약을
         * 얹어 마디를 골랐다:
         *
         *   코리더 밴드    st1~30 여유가 등급별 밴드 안 (기본·f2p 두 세계 모두)
         *   무강화 게이트   완화는 무강화 플레이어의 보스도 가볍게 하므로, st2가
         *                 뚫리면 게이트 앵커(막히는 스테이지 = 2)가 무너진다.
         *                 st2 마디 1.60은 무강화 여유 0.94 / 코리더 1.54를 동시에
         *                 만족하는 좁은 창([1.56, 1.68])의 가운데다 - 이 창이
         *                 RaiseScale의 상한을 정했다(4.05에서 창이 닫힌다)
         *   초반은 바닥 쪽  st1~5는 기준 여유(1.87~1.97)가 아니라 밴드 바닥
         *                 언저리(1.5~1.8)로만 되돌린다. 상향의 목적이 초반
         *                 병목이므로, 초반 코리더는 밴드 안에서 의도적으로 맵다
         *
         * st30 이후는 평평한 3.60(결손의 수렴값)이고, 심층 밴드(f2p 바닥/천장/
         * 수렴)는 그 값으로 전부 통과한다 - 무한 구간의 수렴 구조(심층 램프)를
         * 건드리지 않는 것이 상수 완화의 요점이다. RaiseScale을 다시 움직이면
         * 이 마디들도 반드시 재실측이다.
         */
        private static readonly int[] CostRaiseStages =
            { 1, 2, 3, 4, 5, 8, 10, 11, 15, 20, 25, 30 };
        private static readonly double[] CostRaiseReliefs =
            { 1.28d, 1.60d, 2.60d, 3.30d, 4.30d, 4.90d, 4.43d, 4.10d, 3.89d, 4.08d, 3.85d, 3.60d };

        public static double CostRaiseRelief(int stage)
        {
            var stages = CostRaiseStages;
            var reliefs = CostRaiseReliefs;

            if (stage <= stages[0]) return reliefs[0];
            if (stage >= stages[stages.Length - 1]) return reliefs[reliefs.Length - 1];

            for (int i = 1; i < stages.Length; i++)
            {
                if (stage > stages[i]) continue;

                // 여유는 곱으로 움직이므로 마디 사이는 기하 보간이 맞다 -
                // TranscendCurve.ExpectedLandingAtStage와 같은 규칙
                double into = (stage - stages[i - 1]) / (double)(stages[i] - stages[i - 1]);
                return reliefs[i - 1] * Math.Pow(reliefs[i] / reliefs[i - 1], into);
            }

            return reliefs[reliefs.Length - 1];
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

            // 33단계의 전직도 같은 자리, 같은 이유다
            health *= BigDouble.FromDouble(EvolutionCompensation(stage));

            // 펫(동료)도 같은 자리다. 네 보정이 곱해지는 것이 맞다 - 네 축이
            // 각자 독립으로 여유를 밀어 올리므로, 상쇄도 각자여야 한다
            health *= BigDouble.FromDouble(PetCompensation(stage));

            // 발도 개방(43단계)도 같은 자리, 같은 이유다 - 다섯 번째 보정항
            health *= BigDouble.FromDouble(MasteryCompensation(stage));

            // 요도(44단계)도 같은 자리, 같은 이유다 - 일곱 번째 항.
            // st50까지는 정확히 1이라 조율 구간의 계산이 비트 단위로 같다
            health *= BigDouble.FromDouble(YodoCompensation(stage));

            // 강화 비용 상향(E-3 수정)의 완화. 곱이 아니라 나눗셈인 이유는
            // 위 다섯과 방향이 반대이기 때문이다 - 축이 세진 것이 아니라
            // 비용이 무거워졌다. CostRaiseRelief 주석 참고
            health /= BigDouble.FromDouble(CostRaiseRelief(stage));

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

        // ------------------------------------------------- E-3 후속: 온보딩 잡몹 완화

        /**
         * @brief 온보딩(st1~5) **잡몹 전용** 체력 완화. 보스는 건드리지 않는다.
         *
         * ## 왜 잡몹만인가
         *
         * E-3 수정(비용 x3.5)이 온보딩을 169초에서 241초로 늘렸다. 분해하면
         * 초과 72초 중 65초가 잡몹 파밍이고 보스는 7초뿐이다 - 보스 쪽은
         * 완화 곡선(CostRaiseRelief)이 이미 밴드로 되돌렸기 때문이다. 남은
         * 몫은 잡몹 체력에 있고, 여기만 내리면 되는 이유가 하나 더 있다:
         * **잡몹 처치 속도는 골드 총량과 무관하다.** 스테이지당 10마리 고정에
         * 마리당 골드가 그대로라, 빨리 잡아도 지갑은 같은 지점에서 같은
         * 금액이다. 그래서 구매 궤적·보스 여유·st6+ 밴드가 전부 비트 불변이고
         * 움직이는 것은 벽시계뿐이다 - 하네스로 재확인했다.
         *
         * 무강화 게이트(st2)도 안전하다. 게이트는 보스 체력이 정하고
         * (BossHealthForStage), 이 완화는 그 경로를 지나지 않는다.
         *
         * ## 왜 기하 감쇠인가 - "6+ 자연 합류"
         *
         * st1 나눗값 OnboardingMobRelief에서 st6의 1까지 기하로 감쇠한다.
         * 그러면 온보딩 구간의 실효 체력 성장이 스테이지당
         * 1.55 x R^(1/5) = 약 x2.1로 **균일**해지고, st6에서 원곡선 값에
         * 정확히 닿는다 - 초입만 낮게 시작해 더 가파르게 올라와 합류하는
         * 곡선이지, 경계에서 체력이 뛰는 계단이 아니다.
         *
         * 크기(x5)는 실측이다: 온보딩 총 시간이 241초에서 기준(169초) 부근으로
         * 돌아오는 지점. 상한도 있다 - 초입을 너무 내리면 무강화 한 방에
         * 잡몹이 전부 죽어(타격 수 1) 공격력 강화가 잡몹에서 안 보인다.
         * 그 몫은 보스가 담당하므로 온보딩에서는 허용했다.
         */
        public const double OnboardingMobRelief = 5d;

        /** 이 스테이지부터 원곡선 그대로다. 코리더(6~30) 비트 불변의 경계 */
        public const int OnboardingReliefEndStage = 6;

        /** st1~5의 나눗값. st6부터 정확히 1이다 */
        public static double MobHealthRelief(int stage)
        {
            if (stage >= OnboardingReliefEndStage) return 1d;
            if (stage < 1) stage = 1;

            return Math.Pow(OnboardingMobRelief,
                (OnboardingReliefEndStage - stage)
                / (double)(OnboardingReliefEndStage - 1));
        }

        /**
         * @brief 이 스테이지 잡몹의 실제 체력. 스포너와 시뮬레이션이 함께 부른다.
         *
         * 완화가 걸린 구간(st1~5)만 정수로 반올림한다(최소 1) - 강화 비용의
         * 정수 규칙(UpgradeCost)과 같은 문법이다. st6+는 반올림조차 하지
         * 않는다 - 여기서 값을 만지면 "코리더 비트 불변"이 거짓말이 된다.
         */
        public static BigDouble MobHealth(BigDouble baseHealth, int stage)
        {
            var raw = baseHealth * HealthMultiplier(stage);

            double relief = MobHealthRelief(stage);
            if (relief <= 1d) return raw;

            double relieved = Math.Round(raw.ToDouble() / relief, MidpointRounding.AwayFromZero);
            return BigDouble.FromDouble(Math.Max(1d, relieved));
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
