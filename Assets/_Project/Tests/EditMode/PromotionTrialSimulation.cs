using System;

namespace Onikiri.Tests
{
    /**
     * @brief 귀문(승급전) 3연전의 순수 전투 시뮬레이션. 테스트 전용이다.
     *
     * 런타임 `BossFight`에 연결하지 않는다. 이 파일이 존재하는 이유는 9단계의
     * 교훈 그대로다 - 계수를 먼저 지어내고 나중에 화면에서 맞추면, 계산과 게임
     * 중 어느 쪽이 맞는지 판단할 근거가 없어진다. 종료 안전값(`T_enrage`,
     * `T_close`)과 적 계수를 **여기서 유도한 뒤** 3단계가 그 값을 들고
     * `BossFight`에 모드를 얹는다.
     *
     * ## 왜 일반 보스와 다른 자가 필요한가
     *
     * 일반 보스는 30초 제한의 **DPS 시험**이고 `StageSimulation.BossKillSeconds`
     * 한 줄이면 답이 나온다(체력 / DPS). 귀문은 사망 판정이라 **시간에 따라
     * 상태가 변하는** 계산이다 - 피해가 쌓이고 재생이 되돌리고 격노가 그
     * 균형을 뒤집는다. 닫힌 식이 없으므로 적분한다.
     *
     * ## 전환 규칙 - 시계는 멈추지 않고 재생은 계속된다
     *
     * 두 선택지 중 이쪽을 골랐다. 전환에서 시계를 멈추면 "전환을 늘려 폐쇄
     * 시간을 버는" 회피가 생기고, 재생만 멈추면 재생 축이 전환 구간에서만
     * 죽어 일관성이 깨진다. 기존 게임도 보스 접근 시간(`BossWalkInSeconds`
     * 5.3초)이 제한 시간 안에서 흐른다 - 같은 규칙이다.
     *
     * **이 선택은 전투 사양과 PlayMode 검사에 그대로 반영된다.**
     *
     * ## 인덱스 규약 - N과 N+1을 섞지 않는다
     *
     *   currentTier     지금 보유한 경지 (0 = 로닌)
     *   targetTier      currentTier + 1 - 이 시험이 주는 경지
     *   gateNumber      targetTier - 일문이 1, 육문이 6
     *   thirdFoeFrames  GetTier(targetTier) - 3체는 **얻으려는 경지**의 모습이다
     *
     * 세 번째 적이 `targetTier`인 것이 이 시험의 서사다("다음 경지를 이겨야
     * 그 경지가 된다"). `currentTier`를 쓰면 자기 자신과 싸우는 다른 이야기가
     * 되고, 그 혼동은 화면에서만 드러난다.
     */
    public static class PromotionTrialSimulation
    {
        // ---------------------------------------------------------------- 입력

        public struct Player
        {
            /** 치명타 기대값이 포함된 초당 피해 (`StageResult.ExpectedDps`) */
            public double Dps;

            public double MaxHealth;

            /** 초당 회복량. 최대 체력 기준 비율이 아니라 절대값이다 */
            public double RegenPerSecond;
        }

        public struct Foe
        {
            public double Health;
            public double AttackDamage;
            public double AttackInterval;

            /** 등장 후 첫 타격까지의 지연. 접근·예비 동작 몫이다 */
            public double FirstAttackDelay;
        }

        public struct Rules
        {
            /** 적이 쓰러지고 다음 적이 설 때까지. 시계는 흐르고 재생도 계속된다 */
            public double SwapSeconds;

            /** 격노가 시작되는 시각 */
            public double EnrageStartSeconds;

            /** 격노 단계 사이 간격 */
            public double EnrageStepSeconds;

            /** 단계마다 적 공격력에 곱하는 값 */
            public double EnrageAttackStep;

            /** 귀문이 닫히는 시각. 여기 닿으면 실패다 */
            public double CloseSeconds;

            /** 적분 간격 */
            public double TimeStep;
        }

        public enum Outcome
        {
            /** 셋을 다 벴다 */
            Cleared,

            /** 죽었다 */
            Died,

            /** 폐쇄 시각에 닿았다 */
            Closed
        }

        public struct Result
        {
            public Outcome Outcome;

            /** 결판이 난 시각 */
            public double Seconds;

            public int FoesFelled;

            /** 끝났을 때 남은 체력 */
            public double HealthLeft;

            /** 그때까지 오른 격노 단계 수 */
            public int EnrageSteps;
        }

        // ---------------------------------------------------------------- 적분

        /**
         * @brief 3연전을 끝까지 돌린다. **반드시 유한 시간에 끝난다** -
         * 폐쇄 시각이 상한이고, 적분 간격이 양수인 한 루프는 그 안에서 닫힌다.
         */
        public static Result Run(Player player, Foe[] foes, Rules rules)
        {
            if (rules.TimeStep <= 0d) throw new ArgumentException("TimeStep must be positive");
            if (rules.CloseSeconds <= 0d) throw new ArgumentException("CloseSeconds must be positive");

            double time = 0d;
            double health = player.MaxHealth;

            int index = 0;
            double foeHealth = foes.Length > 0 ? foes[0].Health : 0d;
            double swapLeft = 0d;
            double attackTimer = foes.Length > 0 ? foes[0].FirstAttackDelay : 0d;

            int felled = 0;
            double dt = rules.TimeStep;

            while (time < rules.CloseSeconds)
            {
                time += dt;

                // 재생은 전환 중에도 흐른다 (머리 주석의 전환 규칙)
                health = Math.Min(player.MaxHealth, health + player.RegenPerSecond * dt);

                if (swapLeft > 0d)
                {
                    swapLeft -= dt;
                    if (swapLeft <= 0d) attackTimer = foes[index].FirstAttackDelay;
                    continue;
                }

                if (index >= foes.Length) break;

                // 플레이어가 깎는다
                foeHealth -= player.Dps * dt;

                // 적이 때린다. 격노 배수는 **때리는 순간의 시각**으로 잰다
                attackTimer -= dt;
                if (attackTimer <= 0d)
                {
                    attackTimer += foes[index].AttackInterval;
                    health -= foes[index].AttackDamage * EnrageMultiplier(rules, time);
                }

                if (health <= 0d)
                {
                    return new Result
                    {
                        Outcome = Outcome.Died,
                        Seconds = time,
                        FoesFelled = felled,
                        HealthLeft = 0d,
                        EnrageSteps = EnrageSteps(rules, time)
                    };
                }

                if (foeHealth <= 0d)
                {
                    felled++;
                    index++;

                    if (index >= foes.Length)
                    {
                        return new Result
                        {
                            Outcome = Outcome.Cleared,
                            Seconds = time,
                            FoesFelled = felled,
                            HealthLeft = health,
                            EnrageSteps = EnrageSteps(rules, time)
                        };
                    }

                    foeHealth = foes[index].Health;
                    swapLeft = rules.SwapSeconds;
                }
            }

            return new Result
            {
                Outcome = Outcome.Closed,
                Seconds = rules.CloseSeconds,
                FoesFelled = felled,
                HealthLeft = Math.Max(0d, health),
                EnrageSteps = EnrageSteps(rules, rules.CloseSeconds)
            };
        }

        /** 이 시각까지 오른 격노 단계 수 */
        public static int EnrageSteps(Rules rules, double time)
        {
            if (time <= rules.EnrageStartSeconds) return 0;
            if (rules.EnrageStepSeconds <= 0d) return 0;

            return 1 + (int)((time - rules.EnrageStartSeconds) / rules.EnrageStepSeconds);
        }

        /** 이 시각의 적 공격력 배수 */
        public static double EnrageMultiplier(Rules rules, double time)
        {
            return Math.Pow(rules.EnrageAttackStep, EnrageSteps(rules, time));
        }

        // ---------------------------------------------------------------- 여유

        /**
         * @brief `LeadPlayer_ClearsBeforeTheEnrageWindow`가 쓰는 여유 배수.
         *
         * **정의: 격노 시작 시각 ÷ 실제 클리어 시각.** 2 이상이라는 것은
         * "곡선 추종 플레이어가 격노를 볼 때까지 두 배의 시간이 더 걸려야
         * 한다"는 뜻이다.
         *
         * 클리어하지 못한 시도는 여유를 말할 수 없으므로 0을 낸다 - 검사가
         * "클리어했고 여유가 2 이상"을 한 줄로 묻게 하기 위해서다.
         */
        public static double EnrageHeadroom(Rules rules, Result result)
        {
            if (result.Outcome != Outcome.Cleared || result.Seconds <= 0d) return 0d;
            return rules.EnrageStartSeconds / result.Seconds;
        }
    }
}
