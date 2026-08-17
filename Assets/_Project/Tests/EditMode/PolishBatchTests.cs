using NUnit.Framework;
using Onikiri.Progression;
using Onikiri.UI;
using UnityEngine;

namespace Onikiri.Tests
{
    /**
     * @brief 폴리싱 배치의 순수 로직.
     *
     * UI 항목이 대부분이라 테스트가 잡을 수 있는 것이 적은 배치다. 그래서
     * **화면이 아니라 문장과 판정**만 여기서 잰다 - 설명 문구가 카탈로그의
     * 실제 거동과 어긋나는 것(#14)과, 배수 줄의 상태 기본값(#9)이 그것이다.
     */
    public class PolishBatchTests
    {
        // ---------------------------------------------------------------- #14 설명

        /**
         * 설명이 **카탈로그에서 나오는가**. 손으로 적은 문자열이면 타격 수를
         * 바꾼 날 문장만 옛 값으로 남고, 그 거짓말은 아무것도 안 잡는다
         */
        [Test]
        public void Description_ReadsTheShapeFromTheCatalog()
        {
            for (int i = 0; i < SkillCatalog.Count; i++)
            {
                var spec = SkillCatalog.Skills[i];
                string text = SkillDescription.Of(spec.Id, spec.BaseMultiplier, spec.CooldownSeconds);

                Assert.IsNotEmpty(text, spec.Id + "의 설명이 비어 있다");

                switch (spec.Area)
                {
                    case SkillArea.Pierce:
                        StringAssert.Contains("관통", text, spec.Id + ": 관통이 설명에 없다");
                        break;

                    case SkillArea.Screen:
                        StringAssert.Contains("화면", text, spec.Id + ": 전체 공격이 설명에 없다");
                        break;

                    case SkillArea.Around:
                        StringAssert.Contains("주변", text, spec.Id + ": 전방위가 설명에 없다");
                        break;

                    case SkillArea.Field:
                        StringAssert.Contains("검기", text, spec.Id + ": 장판이 설명에 없다");
                        break;

                    case SkillArea.Captured:
                        StringAssert.Contains("끌어", text, spec.Id + ": 끌어모음이 설명에 없다");
                        break;
                }

                // 다타는 타격 수가 문장에 있어야 한다. 한 번이면 안 적는다.
                // **범위와 무관해졌으므로 switch 밖으로 나온다** - 이제 화면
                // 광역도 다타일 수 있다(귀신난무)
                if (spec.Split == SkillSplit.MultiHit && spec.HitCount > 1)
                    StringAssert.Contains(spec.HitCount + "회", text,
                        spec.Id + ": 타격 수가 설명에 없다");

                // 주기는 언제나 적힌다 - 이 축의 값어치가 배율/쿨다운이라
                // 배율만 적힌 문장은 절반만 참이다(SkillButton 머리 주석)
                StringAssert.Contains(spec.CooldownSeconds.ToString("F1") + "초", text,
                    spec.Id + ": 시전 주기가 설명에 없다");
            }
        }

        /** 배율이 퍼센트로 나오는가. x2.88은 288%다 */
        [Test]
        public void Description_WritesTheMultiplierAsPercent()
        {
            var spec = SkillCatalog.Skills[0];
            string text = SkillDescription.Of(spec.Id, 2.88d, spec.CooldownSeconds);
            StringAssert.Contains("288%", text);
        }

        /** 없는 id는 빈 문자열. 예외가 아니라 - 화면이 죽는 것보다 낫다 */
        [Test]
        public void Description_OfAnUnknownSkill_IsEmpty()
        {
            Assert.IsEmpty(SkillDescription.Of("no_such_skill", 1d, 1d));
        }

        // ---------------------------------------------------------------- #9 배수 줄

        /**
         * 배수의 기본값이 1인가. 0(최대)이나 100으로 시작하면 배수 줄을
         * 한 번도 안 건드린 사람이 첫 탭에서 골드를 통째로 쓴다
         */
        [Test]
        public void BatchMultiplier_DefaultsToOne()
        {
            /**
             * @brief **살아 있는 static을 안 읽는다.** 규칙을 직접 잰다.
             *
             * 전에는 `UpgradeBatchSelector.Current`를 읽었다. 그 값은 씬이 한 번
             * 이라도 돌면 `Awake`가 PlayerPrefs로 덮으므로, **PlayMode를 먼저
             * 돌린 뒤 EditMode를 돌리면** 그 기기의 취향(실제로 100이었다)이
             * 그대로 읽혀 검사가 떨어졌다 - 검사 주석이 적어 둔 "씬 없이 읽힌다"
             * 는 전제가 그때 깨진다.
             *
             * 규칙을 순수 함수(`Resolve`)로 내렸으므로 이제 입력을 주고 답을
             * 본다. 실행 순서도, 이 기기의 PlayerPrefs도 상관이 없다.
             */
            var chips = new[] { 1, 10, 100 };

            // 저장된 것이 없을 때(호출자가 기본값을 넘긴다)
            Assert.AreEqual(1, UpgradeBatchSelector.Resolve(
                UpgradeBatchSelector.DefaultCount, chips),
                "취향이 없는 사람의 배수가 1이 아니다");

            // 옛 세대의 '최대'(0)와 음수는 1로 떨어진다
            Assert.AreEqual(1, UpgradeBatchSelector.Resolve(0, chips),
                "옛 '최대'(0)가 1로 안 떨어진다 - 첫 탭에서 골드를 통째로 쓴다");
            Assert.AreEqual(1, UpgradeBatchSelector.Resolve(-5, chips));

            // 지금 줄에 없는 배수도 1로 떨어진다
            Assert.AreEqual(1, UpgradeBatchSelector.Resolve(50, chips),
                "없는 배수가 고정되면 어떤 칩도 선택으로 안 보인다");

            // 줄이 아직 없는 씬(전투 전용 하네스)에서는 1만 유효하다
            Assert.AreEqual(1, UpgradeBatchSelector.Resolve(100, null));

            // 유효한 취향은 그대로 살아난다 - 기본값으로 뭉개면 안 된다
            Assert.AreEqual(10, UpgradeBatchSelector.Resolve(10, chips));
            Assert.AreEqual(100, UpgradeBatchSelector.Resolve(100, chips));
        }

        // ---------------------------------------------------------------- 성장 탭 배수

        /**
         * @brief 남은 포인트가 최소 `points`인 캐릭터.
         *
         * 포인트를 직접 넣는 문은 없다 - 남은 양은 **레벨에서 계산되는 값**이고
         * (CharacterLevel.UnspentPoints), 그것이 세 값이 어긋나지 않게 하는
         * 설계다. 그래서 세이브 복원 경로로 레벨을 올려 받는다.
         */
        private static CharacterLevel LevelWith(int points)
        {
            var go = new GameObject("~TestLevel");
            var character = go.AddComponent<CharacterLevel>();

            int level = 1;
            while (StatPointCurve.TotalPointsAtLevel(level) < points && level < 10000) level++;

            character.Restore(level, Onikiri.Core.BigDouble.Zero, 0, 0);
            return character;
        }

        /** 스탯 포인트 배수도 **한 점씩 찍는 것과 같아야 한다** */
        [Test]
        public void SpendingPointsInBulk_MatchesOneByOne()
        {
            var bulk = LevelWith(20);
            int spent = bulk.TrySpendPoints(CharacterLevel.AttackAmpId, 10);

            var singles = LevelWith(20);
            for (int i = 0; i < 10; i++) singles.TrySpendPoint(CharacterLevel.AttackAmpId);

            Assert.AreEqual(10, spent);
            Assert.AreEqual(singles.PointsIn(CharacterLevel.AttackAmpId),
                            bulk.PointsIn(CharacterLevel.AttackAmpId));
            Assert.AreEqual(singles.UnspentPoints, bulk.UnspentPoints);

            Object.DestroyImmediate(bulk.gameObject);
            Object.DestroyImmediate(singles.gameObject);
        }

        /** "최대"가 남은 포인트를 정확히 다 쓰는가 - 한 점도 남기지 않고, 넘기지도 않고 */
        [Test]
        public void SpendableInto_MatchesWhatSpendingActuallyTakes()
        {
            var character = LevelWith(7);
            int available = character.UnspentPoints;

            int counted = character.SpendableInto(CharacterLevel.AttackAmpId, 0);
            int spent = character.TrySpendPoints(CharacterLevel.AttackAmpId, counted);

            Assert.AreEqual(available, counted, "남은 포인트를 다 세지 않았다");
            Assert.AreEqual(counted, spent, "센 수와 쓴 수가 다르다");
            Assert.AreEqual(0, character.UnspentPoints);

            Object.DestroyImmediate(character.gameObject);
        }

        /** 축 상한을 배수가 넘지 못한다 */
        [Test]
        public void PointBatch_StopsAtTheAxisCap()
        {
            var character = LevelWith(StatPointCurve.MaxPoints + 50);

            int spent = character.TrySpendPoints(CharacterLevel.AttackAmpId, int.MaxValue);

            Assert.AreEqual(StatPointCurve.MaxPoints, character.PointsIn(CharacterLevel.AttackAmpId));
            Assert.AreEqual(StatPointCurve.MaxPoints, spent);
            Assert.AreEqual(0, character.SpendableInto(CharacterLevel.AttackAmpId, 0),
                "상한에 닿은 축이 아직 찍을 게 있다고 한다");

            Object.DestroyImmediate(character.gameObject);
        }

        /** 포인트가 없으면 아무 일도 없어야 한다 */
        [Test]
        public void PointBatch_WithNoPoints_DoesNothing()
        {
            var character = LevelWith(0);

            Assert.AreEqual(0, character.SpendableInto(CharacterLevel.AttackAmpId, 0));
            Assert.AreEqual(0, character.TrySpendPoints(CharacterLevel.AttackAmpId, 100));
            Assert.AreEqual(0, character.PointsIn(CharacterLevel.AttackAmpId));

            Object.DestroyImmediate(character.gameObject);
        }
    }
}
