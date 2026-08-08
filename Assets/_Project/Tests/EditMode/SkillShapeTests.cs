using NUnit.Framework;
using Onikiri.Progression;
using UnityEngine;

namespace Onikiri.Tests
{
    /**
     * @brief 오의 거동(다타·관통·광역)이 **밸런스를 바꾸지 않는지**.
     *
     * 27단계는 데미지를 어떻게 뿌리고 어떻게 보여주는가만 바꿨다. 배율·쿨다운·
     * 상한은 26단계 값 그대로이고, 그래서 보스 여유 밴드와 StageSimulation을
     * 손대지 않았다.
     *
     * 그 주장이 성립하는 근거는 하나다 - **보스는 언제나 단일 대상이다.** 다타는
     * 총량을 나눠 같은 대상에게 주고, 관통·광역은 대상이 하나면 한 번 준다.
     * 그러므로 어느 거동이든 보스에게 들어가는 총량이 같다.
     *
     * 여기서 검사하는 것은 그 등식이다. 등식이 깨지면 밴드 테스트가 깨지기
     * **전에** 여기서 걸려야 한다 - 밴드는 30스테이지를 돌려야 나오는 결과이고,
     * 이쪽은 산수라서 원인이 곧바로 읽힌다.
     */
    public class SkillShapeTests
    {
        /**
         * @brief 다타의 합이 **정확히** 총 배율이다.
         *
         * 부동소수점 여유를 주지 않는다(delta 0). 배율을 셋으로 나눠 셋을 더하면
         * 원래 값과 미세하게 어긋나는데, 그 어긋남을 허용하면 "총 데미지 불변"이
         * 검사 가능한 성질이 아니게 된다 - 오차인지 설계 변경인지 구분되지 않는다.
         *
         * 그래서 마지막 타격이 나머지를 받는다(SkillCatalog.HitDamageShare).
         */
        [Test]
        public void MultiHitShares_SumExactlyToTheTotal()
        {
            for (int s = 0; s < SkillCatalog.Count; s++)
            {
                var spec = SkillCatalog.Skills[s];
                if (spec.Shape != SkillShape.MultiHit) continue;

                // 레벨별 배율 전 구간에서 확인한다. 상한 근처의 잘린 배율에서도
                // 합이 맞아야 한다
                for (int level = 1; level <= SkillCurve.MaxLevel; level++)
                {
                    double total = SkillCurve.CappedMultiplierAtLevel(spec.BaseMultiplier, level);

                    double sum = 0d;
                    int hits = SkillCatalog.HitsPerCast(s);
                    for (int h = 0; h < hits; h++) sum += SkillCatalog.HitDamageShare(s, h, total);

                    Assert.AreEqual(total, sum, 0d, string.Format(
                        "'{0}' Lv.{1}: {2}회로 나눈 합이 {3:R}인데 총 배율은 {4:R}이다. "
                        + "마지막 타격이 나머지를 받고 있지 않다 - 총 데미지 불변이 깨진다",
                        spec.DisplayName, level, hits, sum, total));
                }
            }
        }

        /**
         * @brief 관통·광역은 **대상마다 총 배율**이다. 나누지 않는다.
         *
         * 나누면 요괴가 많을 때 한 마리당 피해가 줄어드는데, 그러면 보스전(대상
         * 하나)과 잡몹전의 배율이 달라진다. 밸런스는 보스 기준으로 잡혀 있으므로
         * 보스에게 들어가는 값이 총 배율이어야 하고, 그것이 이 거동의 정의다.
         */
        [Test]
        public void PierceAndScreen_DealTheFullMultiplierPerTarget()
        {
            for (int s = 0; s < SkillCatalog.Count; s++)
            {
                var spec = SkillCatalog.Skills[s];
                if (spec.Shape == SkillShape.MultiHit) continue;

                Assert.AreEqual(1, SkillCatalog.HitsPerCast(s), string.Format(
                    "'{0}'은 {1}인데 시전당 타격이 {2}회다", spec.DisplayName, spec.Shape,
                    SkillCatalog.HitsPerCast(s)));

                double total = SkillCurve.CappedMultiplierAtLevel(spec.BaseMultiplier, 7);
                Assert.AreEqual(total, SkillCatalog.HitDamageShare(s, 0, total), 0d,
                    "'" + spec.DisplayName + "'이 배율을 나누고 있다");
            }
        }

        /**
         * @brief 단일 대상에게 들어가는 총량이 거동과 무관하다. **밴드 불변의 근거다.**
         *
         * 보스전은 대상이 하나이므로 이 등식이 곧 "보스 여유 밴드가 바뀌지 않는다"다.
         * StageSimulation은 스킬을 배율/쿨다운으로만 보고 거동을 모르는데, 그것이
         * 틀리지 않은 이유가 여기 있다.
         */
        [Test]
        public void AgainstOneTarget_TotalPerCastIsTheMultiplier()
        {
            for (int s = 0; s < SkillCatalog.Count; s++)
            {
                var spec = SkillCatalog.Skills[s];

                for (int level = 1; level <= SkillCurve.MaxLevel; level++)
                {
                    double total = SkillCurve.CappedMultiplierAtLevel(spec.BaseMultiplier, level);

                    double delivered = 0d;
                    int hits = SkillCatalog.HitsPerCast(s);
                    for (int h = 0; h < hits; h++) delivered += SkillCatalog.HitDamageShare(s, h, total);

                    Assert.AreEqual(total, delivered, 0d, string.Format(
                        "'{0}' Lv.{1}: 단일 대상에게 {2:R}이 들어간다 (배율 {3:R}). "
                        + "보스 여유 밴드가 27단계에 움직인다 - 시뮬레이션도 함께 고쳐야 한다",
                        spec.DisplayName, level, delivered, total));
                }
            }
        }

        /**
         * @brief 무게 등급이 배율 순서와 같다.
         *
         * 화면에서 가장 무겁게 느껴지는 오의가 실제로 가장 센 오의여야 한다.
         * 히트스톱과 셰이크는 숫자보다 강한 신호라, 어긋나면 숫자가 아니라 무게가
         * 믿긴다.
         */
        [Test]
        public void Weights_FollowTheMultiplierOrder()
        {
            for (int i = 1; i < SkillCatalog.Count; i++)
            {
                var previous = SkillCatalog.Skills[i - 1];
                var current = SkillCatalog.Skills[i];

                Assert.Greater(current.BaseMultiplier, previous.BaseMultiplier,
                    "표의 순서가 배율 순서와 다르다 - 아래 무게 검사의 전제가 깨진다");

                Assert.Greater(current.Weight, previous.Weight, string.Format(
                    "'{0}'(배율 x{1})의 무게가 {2}인데 '{3}'(배율 x{4})은 {5}다 - "
                    + "더 센 오의가 더 가볍게 느껴진다",
                    current.DisplayName, current.BaseMultiplier, current.Weight,
                    previous.DisplayName, previous.BaseMultiplier, previous.Weight));
            }
        }

        /**
         * @brief 오의 색이 **데미지 팔레트와 갈린다.**
         *
         * 27단계에 이 검사가 없어서 한 번 물렸다. 무기 등급 톤(흰/적/금)을 그대로
         * 잡았더니:
         *
         * ```
         * 연참 #FFF4E4  vs  평타 #FFF4D6   거의 같다
         * 귀참 #FFD34D  vs  치명타 #FFD34D  완전히 같다
         * ```
         *
         * 치명타율 60% 구간에서는 화면의 큰 숫자 대부분이 금색이라, 귀참 숫자가
         * 치명타와 구분되지 않았다. 색으로 오의를 가르기로 했는데 그 색이 이미
         * 쓰이고 있으면 설계가 성립하지 않는다.
         *
         * 팔레트를 **컴포넌트에서 읽는다.** 상수를 여기 복사해두면 그 순간부터
         * 이 테스트는 자기 자신을 검사하게 된다 - 애셋에서 필드 평균을 읽는
         * StageSimulationTests와 같은 규칙이다.
         */
        [Test]
        public void SkillColors_AreDistinctFromTheDamagePalette()
        {
            var go = new GameObject("~TestDamageNumbers");
            var spawner = go.AddComponent<Onikiri.UI.DamageNumberSpawner>();
            var so = new UnityEditor.SerializedObject(spawner);

            var palette = new[]
            {
                new { Name = "평타", Color = so.FindProperty("normalColor").colorValue },
                new { Name = "치명타", Color = so.FindProperty("critColor").colorValue },
                new { Name = "처치", Color = so.FindProperty("killColor").colorValue },
            };

            foreach (var skill in SkillCatalog.Skills)
            {
                var tint = Rgba(skill.SlashRgba);

                foreach (var entry in palette)
                {
                    float distance = Distance(tint, entry.Color);

                    Assert.Greater(distance, MinimumColorDistance, string.Format(
                        "'{0}' {1} 가 {2} 숫자 색 {3} 과 너무 가깝다 (거리 {4:F3}). "
                        + "색으로 오의를 가르기로 했는데 그 색이 이미 쓰이고 있으면 "
                        + "화면에서 구분되지 않는다",
                        skill.DisplayName, Hex(tint), entry.Name, Hex(entry.Color), distance));
                }
            }

            Object.DestroyImmediate(go);
        }

        /**
         * @brief 세 오의끼리도 갈려야 한다.
         *
         * 색이 유일한 구분자인 신호가 둘 있다(아크·숫자). 오의 사이가 가까우면
         * 그 둘이 아무것도 가리키지 않는다.
         */
        [Test]
        public void SkillColors_AreDistinctFromEachOther()
        {
            for (int a = 0; a < SkillCatalog.Count; a++)
            {
                for (int b = a + 1; b < SkillCatalog.Count; b++)
                {
                    var first = SkillCatalog.Skills[a];
                    var second = SkillCatalog.Skills[b];

                    float distance = Distance(Rgba(first.SlashRgba), Rgba(second.SlashRgba));

                    Assert.Greater(distance, MinimumColorDistance, string.Format(
                        "'{0}'과 '{1}'의 색이 너무 가깝다 (거리 {2:F3})",
                        first.DisplayName, second.DisplayName, distance));
                }
            }
        }

        /**
         * @brief 두 색이 화면에서 갈리는 최소 거리.
         *
         * RGB 유클리드 거리다(0~1 정규화, 최대 sqrt(3) = 1.73). 0.25는 실측으로
         * 잡았다 - 물렸던 조합이 연참/평타 0.05, 귀참/치명타 0.00이었고, 고친
         * 조합이 0.29 이상이다. 사람의 색 지각은 균일하지 않아서 이 자가 완벽하지는
         * 않지만, **같은 색을 두 뜻에 쓰는 것**을 잡는 데는 충분하다.
         */
        const float MinimumColorDistance = 0.25f;

        static Color Rgba(uint value)
        {
            return new Color32(
                (byte)((value >> 24) & 0xFF),
                (byte)((value >> 16) & 0xFF),
                (byte)((value >> 8) & 0xFF),
                (byte)(value & 0xFF));
        }

        static float Distance(Color a, Color b)
        {
            float dr = a.r - b.r, dg = a.g - b.g, db = a.b - b.b;
            return Mathf.Sqrt(dr * dr + dg * dg + db * db);
        }

        static string Hex(Color c)
        {
            return "#" + ColorUtility.ToHtmlStringRGB(c);
        }
    }
}
