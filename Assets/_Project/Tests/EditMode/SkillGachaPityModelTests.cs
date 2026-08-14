using NUnit.Framework;
using Onikiri.Progression;

namespace Onikiri.Tests
{
    /**
     * @brief 이중 천장 모델. **정상해와 과도기가 서로 다르다는 것까지 잰다.**
     *
     * 이 파일이 지키는 것은 값 넷이 아니라 **경계 하나**다 - 어디까지 평균으로
     * 말해도 되고 어디부터 상태를 세어야 하는가. 그 경계를 안 지키면
     * "100회차의 ★5 확률"을 1.449%라고 답하게 되는데 실제로는 45.589%다.
     *
     * 값의 출처는 문서(v2.2 §4.2·§4.3)이고, 그 문서의 값은 이 코드와 **독립적으로**
     * 푼 마르코프 해에서 나왔다. 같은 코드를 두 번 돌린 것이 아니라 다른 도구로
     * 같은 답에 닿았다는 뜻이고, 그것이 이 검사가 자기 자신을 검사하지 않는 근거다.
     */
    public class SkillGachaPityModelTests
    {
        /** 소프트 천장. 요도 배너와 공유한다 */
        private const int Soft = 30;

        /** ★5 하드 천장. 스킬 뽑기 전용이다 (5단계에 상수로 승격된다) */
        private const int Hard = 100;

        #region 회귀 안전선 - 하드 천장을 끄면 옛 식이 나와야 한다

        /**
         * @brief 하드 천장을 끄면 **기존 닫힌 식을 소수점까지 재현하는가.**
         *
         * 이 검사가 이 모델의 유일한 외부 기준이다. 새 계산이 옛 계산을 포함하지
         * 않으면 그것은 확장이 아니라 교체이고, 교체라면 47단계가 유도한 천장
         * 보정이 어디로 갔는지 아무도 답할 수 없다.
         *
         * **끄는 방법이 `hardPityPulls <= 0`인 것도 여기서 함께 못 박는다.**
         * int.MaxValue를 넘기는 코드가 생기면 그 크기의 배열을 만들려 들고,
         * 그것은 끄기가 아니라 사고다.
         */
        [Test]
        public void ThePityModel_ReproducesTheClosedFormWithHardPityOff()
        {
            var rates = SkillGachaPityModel.SolveSteady(Soft, 0);

            Assert.AreEqual(GachaCurve.EffectiveRarityChance, rates.UnlockChance, 1e-9d,
                "하드 천장을 껐는데 ★4가 47단계의 닫힌 식과 다르다 - 새 계산이 옛 계산을 "
                + "포함하지 않는다");

            Assert.AreEqual(GachaCurve.EffectiveLegendaryChance, rates.AwakenChance, 1e-9d,
                "하드 천장을 껐는데 ★5가 표 확률과 다르다 - 소프트 천장이 전설을 훔치고 있다");

            Assert.AreEqual(SkillGachaCurve.ExpectedXpPerPull, rates.Xp, 1e-9d,
                "하드 천장을 껐는데 기대 XP가 닫힌 식과 다르다");

            // 꺼진 경로는 하드 축의 길이가 1이다. 30칸만 돈다는 것이 설계다
            var state = new SkillGachaPityModel.State(Soft, 0);
            Assert.IsFalse(state.HardPityEnabled);
            Assert.AreEqual(1, state.HardSize,
                "하드 천장이 꺼졌는데 하드 축이 1보다 크다 - 안 쓰는 상태를 세고 있다");
            Assert.AreEqual(Soft, state.SoftSize);
        }

        #endregion

        #region 정상해 - 문서에 적힌 장기 확률

        /**
         * @brief 정상해가 **문서의 표와 같은가** (v2.2 §4.3).
         *
         * 이 값들이 확률 정보 팝업에 뜨고 경제표의 획득 기간을 만든다. 여기가
         * 움직이면 문서의 표가 통째로 거짓이 되므로, 허용 오차를 1e-6으로 좁게
         * 둔다 - 반올림이 아니라 계산이 바뀐 것만 잡는 폭이다.
         */
        [Test]
        public void ThePityModel_MatchesTheDocumentedSteadyRates()
        {
            var rates = SkillGachaPityModel.SolveSteady(Soft, Hard);

            Assert.AreEqual(0.03899021d, rates.UnlockChance, 1e-6d,
                "★4 실효 확률이 문서(3.899021%)와 다르다");
            Assert.AreEqual(0.01448975d, rates.AwakenChance, 1e-6d,
                "★5 실효 확률이 문서(1.448975%)와 다르다");
            Assert.AreEqual(18.0297d, rates.Xp, 1e-3d,
                "뽑기당 기대 XP가 문서(18.0297)와 다르다");

            Assert.AreEqual(25.6475d, SkillGachaPityModel.PullsPerUnlock(rates), 1e-2d,
                "★4 하나당 기대 회수가 문서(25.6475)와 다르다");
            Assert.AreEqual(69.0143d, SkillGachaPityModel.PullsPerAwaken(rates), 1e-2d,
                "★5 하나당 기대 회수가 문서(69.0143)와 다르다");
        }

        /**
         * @brief 하드 천장이 **★4를 깎고 ★5를 올리는** 방향인가.
         *
         * 부호만 재는 검사다. 위 검사가 값을 잠그는 동안 이쪽은 **왜 그 값인지**를
         * 잠근다 - ★5가 소프트 카운터를 초기화하므로 30회 천장의 발동 기회를
         * 가로채고, 그래서 ★4가 내려간다. 그 인과가 뒤집히면 값이 맞아도
         * 모델이 다른 물건이다.
         */
        [Test]
        public void TheHardPity_TradesEpicForLegendary()
        {
            var without = SkillGachaPityModel.SolveSteady(Soft, 0);
            var with = SkillGachaPityModel.SolveSteady(Soft, Hard);

            Assert.Less(with.UnlockChance, without.UnlockChance,
                "하드 천장을 켰는데 ★4가 안 줄었다 - ★5가 소프트 카운터를 초기화하지 않고 있다");

            Assert.Greater(with.AwakenChance, without.AwakenChance,
                "하드 천장을 켰는데 ★5가 안 늘었다 - 천장이 아무것도 안 하고 있다");

            // 합계는 오히려 는다. 하드가 주는 ★5가 ★4에서 뺏는 것보다 크기 때문이다
            Assert.Greater(with.UnlockChance + with.AwakenChance,
                           without.UnlockChance + without.AwakenChance,
                "★4 이상 합계가 안 늘었다");
        }

        #endregion

        #region 과도기 - 정상해를 여기 쓰면 안 된다는 증거

        /**
         * @brief 초기 구간이 정상해와 **다르다는 것을 못 박는다.**
         *
         * 이 검사의 목적이 특이하다 - 보통 검사는 "같은가"를 묻는데 이쪽은
         * **"다른가"**를 묻는다. 그 이유는 v2.1이 실제로 저지른 실수가
         * "정상해를 여정에 넣기"였고, 그 실수는 값이 그럴듯해서 안 잡히기
         * 때문이다. 숫자가 있어야 잡힌다.
         *
         *     1회차   XP 18.4960   = TableXpPerPull (천장이 아직 아무것도 안 눌렀다)
         *    30회차   ★4 43.4597%  (소프트 천장이 그 회차에 몰려 있다)
         *   100회차   ★5 45.5886%  (하드 천장이 그 회차에 몰려 있다 - 정상해의 31배)
         */
        [Test]
        public void TheTransient_DiffersFromSteadyInTheEarlyPulls()
        {
            var state = new SkillGachaPityModel.State(Soft, Hard);

            var first = SkillGachaPityModel.Advance(state);
            Assert.AreEqual(SkillGachaCurve.TableXpPerPull, first.Xp, 1e-9d,
                "첫 뽑기의 기대 XP가 표 그대로가 아니다 - 천장은 아직 아무것도 안 눌렀다");
            Assert.AreEqual(GachaCurve.EffectiveLegendaryChance, first.AwakenChance, 1e-9d,
                "첫 뽑기의 ★5가 표 확률이 아니다");

            var atThirty = default(SkillGachaPityModel.Rates);
            for (int pull = 2; pull <= Soft; pull++) atThirty = SkillGachaPityModel.Advance(state);

            Assert.AreEqual(0.4345965d, atThirty.UnlockChance, 1e-5d,
                "30회차의 ★4가 43.4597%가 아니다 - 소프트 천장이 그 회차에 안 몰려 있다");

            var atHundred = default(SkillGachaPityModel.Rates);
            for (int pull = Soft + 1; pull <= Hard; pull++)
                atHundred = SkillGachaPityModel.Advance(state);

            Assert.AreEqual(0.4558857d, atHundred.AwakenChance, 1e-5d,
                "100회차의 ★5가 45.5886%가 아니다 - 하드 천장이 그 회차에 안 몰려 있다");

            // 그리고 그것이 정상해와 얼마나 다른지를 숫자로 남긴다
            var steady = SkillGachaPityModel.SolveSteady(Soft, Hard);
            Assert.Greater(atHundred.AwakenChance, steady.AwakenChance * 20d, string.Format(
                "100회차의 ★5({0:P3})가 정상해({1:P3})의 스무 배도 안 된다 - 정상해를 "
                + "여정에 써도 된다는 뜻이 되고, 그러면 이 모델을 나눈 이유가 사라진다",
                atHundred.AwakenChance, steady.AwakenChance));
        }

        /**
         * @brief 기존 세이브의 천장 상태에서 **이어서** 전진하는가.
         *
         * 정상해로는 절대 못 하는 일이다. 소프트 29 · 하드 99를 쌓아 둔
         * 플레이어의 다음 한 번은 **★5가 확정**인데, 평균은 그 사람과 방금
         * 받은 사람을 같게 본다.
         *
         * v20 마이그레이션이 하드 카운터를 0에서 시작시키는 것과 별개로,
         * 소프트 카운터는 v19에서 그대로 넘어온다(skillGachaPity) - 그 복원
         * 경로가 이 함수다.
         */
        [Test]
        public void TheTransient_ResumesFromSavedCounters()
        {
            var state = new SkillGachaPityModel.State(Soft, Hard);
            state.ResetToSavedCounters(Soft - 1, Hard - 1);

            var next = SkillGachaPityModel.Advance(state);

            Assert.AreEqual(1d, next.AwakenChance, 1e-12d,
                "하드 천장 직전에서 복원했는데 다음 한 번이 ★5 확정이 아니다");
            Assert.AreEqual(0d, next.UnlockChance, 1e-12d,
                "하드 천장이 지급한 회차에 ★4도 함께 나왔다 - 한 회차에 결과는 하나다");
            Assert.AreEqual(0d, next.Xp, 1e-12d, "★5 확정 회차에 XP가 들어왔다");

            // 소프트 직전이지만 하드는 아직 먼 상태 - 이쪽은 ★4가 확정이다
            state.ResetToSavedCounters(Soft - 1, 0);
            var soft = SkillGachaPityModel.Advance(state);

            Assert.AreEqual(GachaCurve.EffectiveLegendaryChance, soft.AwakenChance, 1e-12d,
                "소프트 천장 회차의 ★5가 표 확률이 아니다 - 소프트가 전설을 훔치고 있다");
            Assert.AreEqual(1d - soft.AwakenChance, soft.UnlockChance, 1e-12d,
                "소프트 천장 회차인데 ★5가 아닌 나머지가 전부 ★4가 아니다");
        }

        #endregion

        #region 아직 안 붙였다 - 5단계에 이어진다

        /**
         * @brief 이 모델은 **아직 아무도 안 쓴다.** 그 사실을 여기 적어 둔다.
         *
         * `SkillGachaCurve`의 네 파생값(ExpectedXpPerPull · EffectiveUnlockChance ·
         * EffectiveAwakenChance · ExpectedPullsPerUnlock)은 여전히 47단계의 닫힌
         * 식을 가리키고, `StageSimulation`도 그것을 읽는다.
         *
         * **일부러 안 바꿨다.** 하드 천장은 아직 `SkillGachaSystem`에 없으므로,
         * 지금 파생값만 이중 천장 값으로 갈아끼우면 시뮬레이션이 **일어나지 않는
         * 일**을 계산하게 된다 - 화면에서는 100회를 채워도 ★5가 안 나오는데
         * 밴드는 나온다고 가정하는 상태다.
         *
         * 갈아끼우는 것은 5단계(하드 천장 구현)와 **같은 커밋**이어야 하고,
         * 이 검사가 그때까지 그 사실을 지킨다. 5단계에서 이 검사를 지우는 것이
         * 곧 "이제 붙었다"의 표시다.
         */
        [Test]
        public void ThePityModel_IsNotWiredIntoTheCurveYet()
        {
            Assert.AreEqual(GachaCurve.EffectiveRarityChance, SkillGachaCurve.EffectiveUnlockChance,
                1e-12d, "곡선이 이미 이중 천장을 읽고 있다 - 하드 천장 구현과 같은 커밋이어야 한다");

            Assert.AreEqual(GachaCurve.EffectiveLegendaryChance, SkillGachaCurve.EffectiveAwakenChance,
                1e-12d, "곡선의 ★5가 이미 갈렸다");
        }

        #endregion
    }
}
