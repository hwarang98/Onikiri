using System;

namespace Onikiri.Progression
{
    /**
     * @brief 이중 천장(★4 소프트 + ★5 하드)의 상태 분포. **정상해와 과도기가 한 전이를 쓴다.**
     *
     * ## 왜 닫힌 식을 못 쓰는가
     *
     * `GachaCurve.PityShare` 계열은 **천장이 하나**일 때 유도된 식이다. ★5 하드
     * 천장이 붙으면 그 전제가 깨진다 - 하드가 만든 ★5가 소프트 카운터도 함께
     * 0으로 되돌리므로(★5는 ★4 이상이다), 30회 천장이 발동할 기회를 ★5가
     * 가로챈다. 두 과정이 얽히면 곱셈으로 못 풀고 상태를 세어야 한다.
     *
     * ## 왜 정상해를 여정에 쓰면 안 되는가
     *
     * 정상해는 **아주 오래 돌린 뒤의 평균**이다. 이 축의 실제 여정은 그 평균에
     * 한참 못 미치는 구간에서 끝난다:
     *
     *     뽑기      실제 ★5/회      정상해가 말하는 값
     *       1        0.800%          1.449%
     *      30        0.800%          1.449%
     *     100       45.589%          1.449%     <- 29배 차이
     *     200       21.218%          1.449%
     *     400        5.415%          1.449%
     *
     * 100회차에 하드 천장이 몰려 있기 때문이고, 그 메아리가 400회까지 이어진다.
     * 총변동거리가 1e-3 아래로 내려가는 데 **859회**가 걸리는데, 귀오의 4종의
     * 기대가 276회이므로 **여정 전체가 과도기 안에 있다.**
     *
     * 그래서 쓰는 곳을 갈라 둔다:
     *
     *     SolveSteady   장기 확률표 · 확률 정보 팝업 · 경제 보고
     *     Advance       실제 여정 · 세이브 복원 · 무료 10연 · 초기 구간
     *
     * ## 하드 천장을 "끄는" 방법
     *
     * `hardPityPulls <= 0`이 끄는 신호다. **큰 수가 아니다** - int.MaxValue를
     * 넘기면 그 크기의 배열을 만들려 들고, 그것은 끄기가 아니라 사고다.
     * 꺼진 경로는 하드 축의 길이가 1이라 소프트 30칸만 돈다.
     */
    public static class SkillGachaPityModel
    {
        /** 한 번의 뽑기가 내는 기댓값. 회차마다 다르다 - 그것이 이 모델의 요점이다 */
        public struct Rates
        {
            /** 이 회차의 ★4(영웅) 확률 */
            public double UnlockChance;

            /** 이 회차의 ★5(전설) 확률 */
            public double AwakenChance;

            /**
             * @brief 이 회차의 기대 스킬 XP. **수집기 기준이다.**
             *
             * ★4·★5가 해금으로 나가는 동안의 값이라 위 두 등급은 0으로 센다.
             * 재고가 소진되면 미끄러진 XP가 얹히는데(SlideFor), 그 덧셈은
             * 재고를 아는 쪽(SkillGachaSystem·StageSimulation)의 몫이다.
             */
            public double Xp;
        }

        /**
         * @brief (소프트, 하드) 두 카운터의 결합 분포.
         *
         * 두 개의 독립 누산기로 쪼개지 않는 이유가 이 클래스의 존재 이유다 -
         * 쪼개면 "★5가 소프트를 초기화한다"는 사실을 적을 자리가 없어지고,
         * ★4와 ★5의 상관이 사라진다.
         */
        public sealed class State
        {
            private readonly double[] cells;

            /**
             * @brief 전이 결과를 받는 버퍼. **매번 새로 잡지 않는다.**
             *
             * 3,000칸 double이 뽑기 한 번마다 새로 할당되면 시뮬레이션 한 번에
             * 수백 벌이 쌓인다 - EditMode 스위트가 이미 6분이라 그 GC가 그대로
             * 벽시계에 얹힌다. 상태와 수명이 같으므로 여기 두는 것이 맞다.
             */
            private readonly double[] scratch;

            public int SoftSize { get; private set; }
            public int HardSize { get; private set; }

            /** 하드 천장이 켜져 있는가. 꺼져 있으면 하드 축의 길이가 1이다 */
            public bool HardPityEnabled { get; private set; }

            /** 하드 천장이 발동하는 누적 회수. 꺼져 있으면 0 */
            public int HardPityPulls { get; private set; }

            public State(int softPityPulls, int hardPityPulls)
            {
                SoftSize = Math.Max(1, softPityPulls);
                HardPityEnabled = hardPityPulls > 0;
                HardPityPulls = HardPityEnabled ? hardPityPulls : 0;
                HardSize = HardPityEnabled ? hardPityPulls : 1;

                cells = new double[SoftSize * HardSize];
                scratch = new double[cells.Length];
                ResetToNewSave();
            }

            /** 새 세이브. 두 카운터가 0이다 */
            public void ResetToNewSave()
            {
                ResetToSavedCounters(0, 0);
            }

            /**
             * @brief 기존 세이브의 천장 상태에서 이어 간다.
             *
             * **정상해로는 못 하는 일이 이것이다.** 29회를 쌓아 둔 플레이어와
             * 방금 받은 플레이어는 다음 한 번이 전혀 다른데, 평균은 그 둘을
             * 같게 본다.
             */
            public void ResetToSavedCounters(int soft, int hard)
            {
                Array.Clear(cells, 0, cells.Length);

                int s = Clamp(soft, 0, SoftSize - 1);
                int h = Clamp(hard, 0, HardSize - 1);
                cells[Index(s, h)] = 1d;
            }

            internal int Index(int soft, int hard) { return soft * HardSize + hard; }

            internal double this[int soft, int hard]
            {
                get { return cells[Index(soft, hard)]; }
                set { cells[Index(soft, hard)] = value; }
            }

            internal double[] Cells { get { return cells; } }

            /** 비운 전이 버퍼. Advance가 채워서 cells에 되돌린다 */
            internal double[] TakeScratch()
            {
                Array.Clear(scratch, 0, scratch.Length);
                return scratch;
            }

            /**
             * @brief 두 분포의 총변동거리. 정상해 수렴 판정이 쓴다.
             *
             * 절반을 곱하는 것이 총변동거리의 정의다. 곱하지 않은 L1을 쓰면
             * 같은 허용 오차가 두 배로 느슨해진다.
             */
            internal static double Distance(double[] a, double[] b)
            {
                double sum = 0d;
                for (int i = 0; i < a.Length; i++) sum += Math.Abs(a[i] - b[i]);
                return sum * 0.5d;
            }

            private static int Clamp(int value, int low, int high)
            {
                return value < low ? low : (value > high ? high : value);
            }
        }

        // ---------------------------------------------------------------- 표에서 읽는 값

        /**
         * @brief ★3 이하의 확률 합. 소프트 천장이 눌러 승격시키는 몫이다.
         *
         * 값을 여기 적지 않고 표에서 세는 이유는 47단계가 확률표를 한 곳에 둔
         * 이유 그대로다 - 표가 바뀌면 이 모델이 저절로 따라와야 하고, 옮겨
         * 적으면 따라오지 않는다.
         */
        private static double LowChance
        {
            get
            {
                double total = 0d;
                for (int i = 0; i < GachaCurve.Chances.Length; i++)
                    if (GachaCurve.GradeOf[i] < GachaCurve.Grade.Epic) total += GachaCurve.Chances[i];
                return total;
            }
        }

        private static double EpicChance
        {
            get
            {
                double total = 0d;
                for (int i = 0; i < GachaCurve.Chances.Length; i++)
                    if (GachaCurve.GradeOf[i] == GachaCurve.Grade.Epic) total += GachaCurve.Chances[i];
                return total;
            }
        }

        private static double LegendaryChance
        {
            get
            {
                double total = 0d;
                for (int i = 0; i < GachaCurve.Chances.Length; i++)
                    if (GachaCurve.GradeOf[i] == GachaCurve.Grade.Legendary)
                        total += GachaCurve.Chances[i];
                return total;
            }
        }

        /** ★3 이하가 내는 기대 XP. 천장이 아무것도 안 누른 회차의 값이다 */
        private static double LowXp
        {
            get
            {
                double total = 0d;
                int count = Math.Min(GachaCurve.Chances.Length, SkillGachaCurve.XpOf.Length);
                for (int i = 0; i < count; i++)
                    if (GachaCurve.GradeOf[i] < GachaCurve.Grade.Epic)
                        total += GachaCurve.Chances[i] * SkillGachaCurve.XpOf[i];
                return total;
            }
        }

        // ---------------------------------------------------------------- 과도기

        /**
         * @brief 분포를 뽑기 **한 번**만큼 전진시키고 그 회차의 기댓값을 돌려준다.
         *
         * 전이 순서가 `SkillGachaSystem.RollOnce`와 **같아야 한다** - 자연 판정 ->
         * ★5 하드 -> ★4 소프트 -> 카운터 갱신. 두 곳이 갈리면 시뮬레이션이 재는
         * 것과 플레이어가 받는 것이 달라지고, 그 차이는 화면에도 로그에도 안 남는다.
         */
        public static Rates Advance(State state)
        {
            var rates = default(Rates);
            if (state == null) return rates;

            double low = LowChance, epic = EpicChance, legendary = LegendaryChance, lowXp = LowXp;

            var next = state.TakeScratch();

            for (int s = 0; s < state.SoftSize; s++)
            {
                for (int h = 0; h < state.HardSize; h++)
                {
                    double mass = state[s, h];
                    if (mass <= 0d) continue;

                    // ★5 하드 천장. 자연 결과가 무엇이든 이 회차는 ★5다 -
                    // 자연 ★4가 덮어써지는 것까지 포함한다(전체의 0.0137%)
                    if (state.HardPityEnabled && h + 1 >= state.HardPityPulls)
                    {
                        rates.AwakenChance += mass;
                        next[state.Index(0, 0)] += mass;
                        continue;
                    }

                    int nextHard = state.HardPityEnabled ? h + 1 : 0;

                    // 자연 ★5. **소프트도 함께 0으로** - ★5는 ★4 이상이다
                    rates.AwakenChance += mass * legendary;
                    next[state.Index(0, 0)] += mass * legendary;

                    if (s + 1 >= state.SoftSize)
                    {
                        // ★4 소프트 천장. ★3 이하가 전부 ★4로 승격된다
                        rates.UnlockChance += mass * (low + epic);
                        next[state.Index(0, nextHard)] += mass * (low + epic);
                    }
                    else
                    {
                        rates.UnlockChance += mass * epic;
                        next[state.Index(0, nextHard)] += mass * epic;

                        rates.Xp += mass * lowXp;
                        next[state.Index(s + 1, nextHard)] += mass * low;
                    }
                }
            }

            Array.Copy(next, state.Cells, next.Length);
            return rates;
        }

        // ---------------------------------------------------------------- 정상해

        /** 정상해 수렴 한계. double의 정밀도(1e-16) 위에서 가장 촘촘한 자리다 */
        public const double DefaultTolerance = 1e-15d;

        /**
         * @brief 반복 상한. 실측 수렴이 4,430회라 네 배 남짓 여유를 둔다.
         *
         * 상한을 두는 이유는 무한 루프 방지가 아니라 **에디터가 멈추지 않게**
         * 하기 위해서다. 확률표가 이상해지면 수렴이 안 할 수 있고, 그때
         * 정적 생성자에서 도는 이 계산이 Unity를 통째로 잡는다.
         */
        public const int DefaultMaxIterations = 20000;

        /**
         * @brief 아주 오래 돌린 뒤의 회당 평균. **장기 보고 전용이다.**
         *
         * 여정 검증에 쓰면 초기 구간이 통째로 틀린다 - 머리 주석의 표 참고.
         *
         * @param softPityPulls ★4 소프트 천장 회수 (30)
         * @param hardPityPulls ★5 하드 천장 회수. **0 이하면 하드 천장 없음**
         */
        public static Rates SolveSteady(int softPityPulls, int hardPityPulls)
        {
            return SolveSteady(softPityPulls, hardPityPulls, DefaultTolerance, DefaultMaxIterations);
        }

        public static Rates SolveSteady(int softPityPulls, int hardPityPulls,
                                        double tolerance, int maxIterations)
        {
            var state = new State(softPityPulls, hardPityPulls);

            var previous = new double[state.Cells.Length];
            for (int i = 0; i < maxIterations; i++)
            {
                Array.Copy(state.Cells, previous, previous.Length);
                Advance(state);

                if (State.Distance(state.Cells, previous) < tolerance) break;
            }

            // 수렴한 분포에서 **한 번 더** 전진시켜 그 회차의 기댓값을 읽는다.
            // 분포가 이미 고정점이므로 이 한 번이 곧 장기 평균이다
            return Advance(state);
        }

        /** ★4 하나에 드는 기대 뽑기 수. 확률의 역수다 */
        public static double PullsPerUnlock(Rates rates)
        {
            return rates.UnlockChance > 0d ? 1d / rates.UnlockChance : double.PositiveInfinity;
        }

        public static double PullsPerAwaken(Rates rates)
        {
            return rates.AwakenChance > 0d ? 1d / rates.AwakenChance : double.PositiveInfinity;
        }
    }
}
