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
                if (spec.Split != SkillSplit.MultiHit) continue;

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
                if (spec.Split == SkillSplit.MultiHit) continue;

                Assert.AreEqual(1, SkillCatalog.HitsPerCast(s), string.Format(
                    "'{0}'은 {1}인데 시전당 타격이 {2}회다", spec.DisplayName, spec.Area,
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
        /**
         * ## 49단계에 재서술했다 - "표 순서"에서 "짝 비교"로
         *
         * 27단계에는 오의가 셋이고 무게 등급도 셋이라 "표 순서 = 배율 순서 =
         * 무게 순서"가 한 줄로 성립했다. 여덟이 되면 그 셋이 갈라진다:
         *
         *   표 순서    기존 셋의 인덱스를 지켜야 한다(세이브·시뮬 칸이 물려 있다)
         *              -> 신규는 뒤에 붙으므로 배율 순서가 아니다
         *   무게 등급  히트스톱·셰이크·숫자 크기의 **세 단계**다
         *              -> 여덟에 여덟 등급을 주면 그것은 등급이 아니라 값이다
         *
         * 남는 참말은 하나다: **더 센 오의가 더 가볍게 느껴지면 안 된다.**
         * 그것을 짝 비교로 적으면 표 순서에도 등급 수에도 매이지 않는다.
         *
         * 배율이 무게의 자인 이유는 여덟이 같은 초당 기여를 나눠 갖기 때문이다 -
         * 배율이 크다는 것은 곧 쿨다운이 길다는 것이고, 한 번에 큰 것이 온다.
         */
        [Test]
        public void Weights_NeverContradictTheMultiplierOrder()
        {
            for (int a = 0; a < SkillCatalog.Count; a++)
            {
                for (int b = 0; b < SkillCatalog.Count; b++)
                {
                    var heavier = SkillCatalog.Skills[a];
                    var lighter = SkillCatalog.Skills[b];
                    if (heavier.BaseMultiplier <= lighter.BaseMultiplier) continue;

                    Assert.GreaterOrEqual(heavier.Weight, lighter.Weight, string.Format(
                        "'{0}'(배율 x{1})의 무게가 {2}인데 더 약한 '{3}'(배율 x{4})은 {5}다 - "
                        + "더 센 오의가 더 가볍게 느껴진다",
                        heavier.DisplayName, heavier.BaseMultiplier, heavier.Weight,
                        lighter.DisplayName, lighter.BaseMultiplier, lighter.Weight));
                }
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
         * @brief 오의끼리는 **색이 갈리거나 그림이 갈리거나** 해야 한다.
         *
         * ## 49단계에 전제가 바뀌었다
         *
         * 27단계의 전제는 "색이 유일한 구분자"였다. 그때는 셋이 **같은 아크
         * 그림**을 색만 바꿔 썼기 때문이고, 그래서 세 색이 서로 0.25 이상
         * 떨어져야 했다.
         *
         * 여덟이 되면서 그 전제가 둘 다 깨진다. 신규 다섯은 혈(血) 한 계열을
         * 나눠 쓰므로 여덟 색을 0.25씩 벌릴 수가 없고(RGB 정육면체에
         * 스물여덟 쌍이 들어가지 않는다), 대신 **각자 다른 그림**을 쓴다 -
         * 퍼지는 링, 터지는 구름, 감기는 소용돌이, 솟는 파도, 휘는 채찍.
         *
         * 그래서 자를 둘로 나눈다:
         *
         *   같은 그림을 쓰는 두 오의  색이 유일한 구분자다 -> 0.25
         *   다른 그림을 쓰는 두 오의  그림이 이미 가른다   -> 0.08 (같은 색 금지)
         *
         * 데미지 팔레트와의 거리는 **여덟 다 0.25 그대로**다(위 검사). 숫자는
         * 그림이 없어서 색이 여전히 유일한 구분자이기 때문이다.
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

                    bool sameArt = first.VfxId == second.VfxId;
                    float limit = sameArt ? MinimumColorDistance : MinimumFamilyDistance;

                    float distance = Distance(Rgba(first.SlashRgba), Rgba(second.SlashRgba));

                    Assert.Greater(distance, limit, string.Format(
                        "'{0}'과 '{1}'의 색이 너무 가깝다 (거리 {2:F3}, 한계 {3:F2}). {4}",
                        first.DisplayName, second.DisplayName, distance, limit,
                        sameArt ? "그림도 같아서 화면에서 한 사건으로 읽힌다"
                                : "같은 계열이라도 같은 색을 두 뜻에 쓰면 안 된다"));
                }
            }
        }

        /**
         * @brief 그림이 오의마다 유일한가. **위 검사의 두 번째 자가 성립하는 근거다.**
         *
         * 색 한계를 0.08까지 내린 유일한 이유가 "그림이 다르다"이므로, 그림이
         * 겹치는 순간 그 완화가 근거를 잃는다. 그때 위 검사가 자동으로 0.25로
         * 올라가지만(sameArt), 그것은 색으로 못 가르는 두 오의를 만들어 놓고
         * 뒤늦게 막는 것이다 - 굽는 조각이 하나 모자랄 때 같은 클립을 두 번
         * 가리키고 싶어지는 유혹이 실재한다.
         *
         * 기존 셋은 VfxId가 비어 있다(팩 참격과 클립 자체의 궤적을 쓴다).
         * 빈 것끼리는 세지 않는다 - 그 셋은 27단계가 이미 색으로 갈라 뒀고,
         * 그 거리(최소 0.36)가 위 검사에서 그대로 확인된다.
         */
        [Test]
        public void EverySkillEffect_BelongsToExactlyOneSkill()
        {
            for (int a = 0; a < SkillCatalog.Count; a++)
            {
                string id = SkillCatalog.Skills[a].VfxId;
                if (string.IsNullOrEmpty(id)) continue;

                for (int b = a + 1; b < SkillCatalog.Count; b++)
                {
                    Assert.AreNotEqual(id, SkillCatalog.Skills[b].VfxId, string.Format(
                        "'{0}'과 '{1}'이 같은 이펙트 '{2}'를 쓴다 - 두 오의가 화면에서 "
                        + "한 사건으로 읽힌다", SkillCatalog.Skills[a].DisplayName,
                        SkillCatalog.Skills[b].DisplayName, id));
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

        /**
         * @brief 그림이 다른 두 오의 사이의 최소 거리 (49단계).
         *
         * "같은 색을 두 뜻에 쓰지 않는다"만 남긴 값이다. 실측으로 여덟 오의의
         * 최소 쌍이 0.107(낙혈 #B02060 대 혈조 #96285E)이라 그 아래에 둔다 -
         * 이 자는 계열 안의 단계를 강제하는 것이 아니라 **두 오의가 같은
         * 색으로 굳는 것**을 막는다.
         */
        const float MinimumFamilyDistance = 0.08f;

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
