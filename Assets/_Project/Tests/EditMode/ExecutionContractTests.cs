using NUnit.Framework;
using Onikiri.Progression;

namespace Onikiri.Tests
{
    /**
     * @brief 참수의 **처형이 저울을 안 건드린다**는 계약 (§6.5).
     *
     * ## 왜 이 파일이 따로 있는가
     *
     * 즉사는 "남은 체력과 무관하게 처치"라 초당 환산 기여로 잴 수가 없다.
     * 그 순간 이 오의만 저울 밖에 서고 `ExpansionRate` 계약이 깨진다 -
     * 다른 열넷은 전부 `배율 / 쿨타임`으로 비교되는데 참수만 "가끔 무한대"가
     * 되기 때문이다.
     *
     * 그래서 즉사를 뺐다. 남은 것은 **연출뿐**이고, 그 사실을 값으로
     * 못 박는 것이 여기다. 나중에 누군가 "처형인데 더 아파야 하지 않나"로
     * 손대면 이 파일이 먼저 붉어진다.
     *
     * ## `ExecutionThreshold`는 지금 **저장된 계약값**이다
     *
     * 설계 문서가 이 값을 `SkillSpec`에 두라고 한 이유를 그대로 적는다 -
     * "계약 테스트가 읽는다". 전투 코드는 이 값을 아직 안 읽는다.
     * 처형은 구운 VFX 클립 안에 들어 있어 조건 없이 재생되고, 조건부
     * 연출(빈사·보스 제외)은 아직 안 붙었다. **그 사실을 숨기지 않는다** -
     * 아래 마지막 검사가 "안 읽힌다"를 명시적으로 기록한다.
     */
    public class ExecutionContractTests
    {
        private static int Decapitate
        {
            get { return SkillCatalog.IndexOf(SkillCatalog.DecapitateId); }
        }

        /**
         * @brief **총 피해가 처형 여부와 무관하다** (§12.3의 테스트 16).
         *
         * 참수는 `Single · Once`라 타격이 하나다. 그 하나가 총 배율 전부를
         * 받는다는 것이 "처형이 피해를 안 바꾼다"의 실제 내용이다 - 처형
         * 분기가 배율을 만지려면 여기를 지나야 하기 때문이다.
         *
         * 여러 배율로 재는 이유는 상수 하나로는 "언제나 그 값"과 "우연히
         * 그 값"을 구별 못 하기 때문이다.
         */
        [Test]
        public void TheExecution_DoesNotChangeDamage()
        {
            int index = Decapitate;
            Assert.GreaterOrEqual(index, 0, "참수가 카탈로그에 없다");

            var spec = SkillCatalog.Skills[index];
            Assert.AreEqual(SkillSpecial.Execution, spec.Special,
                "참수의 Special이 Execution이 아니다");

            Assert.AreEqual(1, SkillCatalog.HitsPerCast(index),
                "참수는 단타여야 한다 - 다타가 되면 처형 판정이 타격마다 서고, "
                + "그 순간 '한 번의 처형'이라는 말이 뜻을 잃는다");

            double[] multipliers = { 0d, 1d, 2.88d, 12.5d, 1e9d };
            foreach (var total in multipliers)
            {
                double dealt = SkillCatalog.HitDamageShare(index, 0, total);
                Assert.AreEqual(total, dealt, 1e-12d,
                    "참수의 타격이 총 배율과 다르다 (배율 " + total + "). "
                    + "처형이 피해를 만지기 시작하면 이 오의만 초당 환산 저울 "
                    + "밖에 서고 ExpansionRate 계약이 깨진다");
            }
        }

        /** 문턱값은 §12.1의 22가 정한 **0.30**이다 */
        [Test]
        public void TheExecutionThreshold_IsThirtyPercent()
        {
            var spec = SkillCatalog.Skills[Decapitate];

            Assert.AreEqual(0.30d, spec.ExecutionThreshold, 1e-12d,
                "참수의 빈사 문턱이 0.30이 아니다");

            Assert.Greater(spec.ExecutionThreshold, 0d,
                "문턱이 0 이하면 처형이 영영 안 뜨거나 언제나 뜬다");
            Assert.Less(spec.ExecutionThreshold, 1d,
                "문턱이 1 이상이면 모든 타격이 처형이라 연출이 뜻을 잃는다");
        }

        /**
         * @brief 처형은 **참수 하나뿐**이다.
         *
         * 둘 이상이 되면 "처형"이 그 오의의 정체가 아니라 등급의 성질이
         * 된다. 지금 규칙은 전자다.
         */
        [Test]
        public void TheExecution_BelongsToExactlyOneSkill()
        {
            int found = 0;
            for (int i = 0; i < SkillCatalog.Count; i++)
                if (SkillCatalog.Skills[i].Special == SkillSpecial.Execution) found++;

            Assert.AreEqual(1, found, "처형을 가진 오의가 하나가 아니다");
        }

        /**
         * @brief 처형이 아닌 열넷은 문턱을 **안 들고 있어야 한다.**
         *
         * 값이 남아 있으면 나중에 `Special`만 바꿔도 조건이 따라붙어,
         * 아무도 의도하지 않은 오의가 처형이 된다.
         */
        [Test]
        public void TheOtherSkills_CarryNoExecutionThreshold()
        {
            for (int i = 0; i < SkillCatalog.Count; i++)
            {
                var spec = SkillCatalog.Skills[i];
                if (spec.Special == SkillSpecial.Execution) continue;

                Assert.AreEqual(0d, spec.ExecutionThreshold, 1e-12d,
                    "'" + spec.DisplayName + "' 이 처형도 아닌데 문턱을 들고 있다");
            }
        }
    }
}
