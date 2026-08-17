using NUnit.Framework;
using Onikiri.Progression;
using UnityEditor;
using UnityEngine;

namespace Onikiri.Tests
{
    /**
     * @brief 온보딩 강조가 **한 번만, 올바른 순서로** 뜨는가. 7단계가 지는 빚이다.
     *
     * 이 파일이 있는 이유는 강조 조건이 파생값이면 조용히 틀리기 때문이다.
     * "보유 && 미장착"은 읽으면 맞는 말인데 실기에서는 되살아난다 - 몇 시간
     * 뒤 혈조를 빼는 순간 다 끝난 온보딩이 다시 화면을 잡는다. 그 실수는
     * 화면을 보고 있어야만 보이므로, 여기서 상태로 잡는다.
     *
     * 순서도 마찬가지다. Lv.1 혈조를 그냥 끼우면 기여가 36% 줄어드는데
     * (SkillIntroGuide 주석의 실측), 그것은 화면에 "손해"라고 안 적힌다.
     * 강화가 교체보다 먼저 온다는 것을 값으로 못 박는다.
     */
    public class SkillIntroGuideTests
    {
        /**
         * @brief 실기와 같은 오의 표를 가진 SkillSystem 하나.
         *
         * 카탈로그에서 옮겨 적는다 - 빌더(SkillPanelBuilder)가 씬에 적는
         * 것과 같은 출처여야, 여기서 통과한 판정이 실기에서도 같은 값을 본다.
         */
        private static SkillSystem BuildSkills(int characterLevel, int frontierStage)
        {
            var go = new GameObject("~IntroGuideSkills");

            var character = go.AddComponent<CharacterLevel>();
            character.Restore(characterLevel, Onikiri.Core.BigDouble.Zero, 0, 0);

            // 자리 수는 캐릭터 레벨이 정하는데(SkillCurve.SlotsFor), SkillSystem은
            // 그것을 **싱글턴**으로 읽는다(CharacterLevel.Instance). 에디트
            // 모드에서는 Awake가 안 돌아 그 자리가 비고, 그러면 레벨이 1로
            // 떨어져 자리가 0개가 된다 - 검사가 아니라 하네스가 무너진다.
            // 에디터가 안 부르는 것을 여기서 부른다
            Invoke(character, "Awake");

            var stage = go.AddComponent<StageProgress>();
            stage.SetProgress(frontierStage, 0, 0, frontierStage);

            var system = go.AddComponent<SkillSystem>();

            var so = new SerializedObject(system);
            so.FindProperty("stage").objectReferenceValue = stage;

            var slots = so.FindProperty("slots");
            slots.arraySize = SkillCatalog.Count;
            for (int i = 0; i < SkillCatalog.Count; i++)
            {
                var spec = SkillCatalog.Skills[i];
                var element = slots.GetArrayElementAtIndex(i);
                element.FindPropertyRelative("id").stringValue = spec.Id;
                element.FindPropertyRelative("displayName").stringValue = spec.DisplayName;
                element.FindPropertyRelative("level").intValue = 1;
                element.FindPropertyRelative("baseMultiplier").doubleValue = spec.BaseMultiplier;
                element.FindPropertyRelative("cooldownSeconds").floatValue = (float)spec.CooldownSeconds;
                element.FindPropertyRelative("unlockLevel").intValue = spec.UnlockLevel;
                element.FindPropertyRelative("unlockStage").intValue = spec.UnlockStage;
                element.FindPropertyRelative("gachaGated").boolValue = spec.GachaGated;
            }

            var equipped = so.FindProperty("equipped");
            equipped.arraySize = SkillCurve.BaseSlots + 1;
            for (int s = 0; s < equipped.arraySize; s++)
                equipped.GetArrayElementAtIndex(s).intValue = -1;

            so.ApplyModifiedPropertiesWithoutUndo();
            return system;
        }

        private static void Invoke(MonoBehaviour target, string method)
        {
            var m = target.GetType().GetMethod(method,
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            Assert.IsNotNull(m, target.GetType().Name + "." + method + " 가 없다");
            m.Invoke(target, null);
        }

        private static void Destroy(SkillSystem system)
        {
            if (system == null) return;

            // 싱글턴을 남기면 다음 검사가 남의 레벨을 본다. OnDestroy가
            // 지우기는 하는데, 안 도는 경우가 있어 명시적으로도 한 번 더 본다
            var character = system.GetComponent<CharacterLevel>();
            if (character != null) Invoke(character, "OnDestroy");

            Object.DestroyImmediate(system.gameObject);
        }

        private static int Intro { get { return SkillCatalog.IndexOf(SkillCatalog.BloodWhipId); } }
        private static int Chain { get { return SkillCatalog.IndexOf(SkillCatalog.ChainSlashId); } }

        /**
         * @brief 레벨을 그대로 세운다. 골드를 태우지 않는다.
         *
         * 강화 경로(TryUpgrade)로 올리면 지갑·비용 곡선이 이 검사에 섞인다.
         * 여기서 묻는 것은 **어느 레벨에서 조언이 바뀌는가** 하나뿐이라,
         * 세이브 복원과 같은 문으로 값만 놓는다.
         */
        private static void SetLevel(SkillSystem system, int index, int level)
        {
            system.RestoreLevels(new[] { SkillCatalog.Skills[index].Id },
                                 new[] { level }, true);
        }

        private static int LevelOf(SkillSystem system, int index)
        {
            var slot = system.GetSlot(index);
            return slot != null ? slot.level : 1;
        }

        /** 슬롯 하나에 하나를 끼우고 나머지는 비운다 */
        private static void EquipOnly(SkillSystem system, int slot, int index)
        {
            for (int s = 0; s < system.SlotCapacity; s++)
                if (system.EquippedAt(s) >= 0) system.Equip(s, -1);

            Assert.IsTrue(system.Equip(slot, index), "장착이 안 된다 - 자리나 해금이 막혔다");
        }

        // ------------------------------------------------------------ 1회성

        /**
         * @brief 강조는 **한 번 끼우면 영영 안 돌아온다.**
         *
         * 파생 조건("보유 && 미장착")으로 짜면 여기서 걸린다 - 마지막에
         * 혈조를 빼는 순간 조언이 되살아난다. `introEquipDone`이 세이브에
         * 있어야 하는 이유 전부가 이 한 검사다.
         */
        [Test]
        public void TheIntroHighlight_NeverReturnsAfterEquipping()
        {
            var system = BuildSkills(15, SkillGachaCurve.UnlockStage);
            try
            {
                Assert.IsTrue(system.GrantGachaSkill(Intro), "혈조를 못 받았다");

                // 받자마자 빈 자리가 있으면 이미 끼워져 있다(FillEmptySlots).
                // 온보딩이 가리킬 것이 없는 상태 - 그것도 정상이다
                var equipped = SkillIntroGuide.Resolve(system, true, true);
                Assert.AreEqual(SkillIntroHint.None, equipped.Hint,
                    "장착까지 끝난 뒤에는 가리킬 것이 없어야 한다");

                // 이제 빼 본다. 파생 조건이면 여기서 강조가 되살아난다
                for (int s = 0; s < system.SlotCapacity; s++)
                    if (system.EquippedAt(s) == Intro) system.Equip(s, -1);

                Assert.IsFalse(system.IsEquipped(Intro), "테스트 전제가 틀렸다 - 안 빠졌다");

                var afterRemoval = SkillIntroGuide.Resolve(system, true, true);
                Assert.AreEqual(SkillIntroHint.None, afterRemoval.Hint,
                    "혈조를 빼자 끝난 온보딩이 되살아났다");

                // 대조군: 같은 상태인데 표식만 없으면 조언이 나온다.
                // 위 통과가 "언제나 None"이라서 나온 것이 아님을 보인다
                var withoutMark = SkillIntroGuide.Resolve(system, true, false);
                Assert.AreNotEqual(SkillIntroHint.None, withoutMark.Hint,
                    "표식이 없으면 조언이 있어야 한다 - 아니면 위 검사가 헛것이다");
            }
            finally { Destroy(system); }
        }

        /** 받지도 않았으면 아무 말도 안 한다 */
        [Test]
        public void TheIntroHighlight_StaysQuietBeforeTheFreePull()
        {
            var system = BuildSkills(15, SkillGachaCurve.UnlockStage);
            try
            {
                Assert.IsTrue(system.GrantGachaSkill(Intro));

                var advice = SkillIntroGuide.Resolve(system, false, false);
                Assert.AreEqual(SkillIntroHint.None, advice.Hint,
                    "무료 10연을 받기 전에는 온보딩 안내가 없어야 한다");
            }
            finally { Destroy(system); }
        }

        // ------------------------------------------------------------ 순서

        /**
         * @brief **강화가 교체보다 먼저다.** 그리고 넘어서는 순간 넘어간다.
         *
         * 문서의 실측 그대로다 - 혈조 Lv.1(0.45/2.5 = 0.180)은 연참
         * Lv.5(0.54 x 1.12^4 / 3 = 0.283)보다 낮고, `1.12^k >= 1.5735`이므로
         * **혈조도 Lv.5**여야 따라잡는다. 여기서는 곡선을 다시 적지 않고
         * 실제 값을 재서 경계를 찾는다 - 상수를 적으면 곡선이 바뀔 때
         * 이 검사가 조용히 거짓말을 한다.
         */
        [Test]
        public void TheIntroFlow_UpgradesBeforeSwapping()
        {
            var system = BuildSkills(15, SkillGachaCurve.UnlockStage);
            try
            {
                Assert.IsTrue(system.GrantGachaSkill(Intro));

                // 자리를 하나만 남기고 거기에 연참을 끼운다. 빈 칸이 없어야
                // 교체 이야기가 성립한다
                int capacity = system.SlotCapacity;
                Assert.GreaterOrEqual(capacity, 1, "자리가 하나도 없다");

                for (int s = 0; s < capacity; s++)
                    if (system.EquippedAt(s) >= 0) system.Equip(s, -1);

                // 연참을 자리마다 채워 빈 칸을 없앤다. 남는 자리가 있으면
                // 조언은 Equip이 되고 교체 단계가 안 온다
                Assert.IsTrue(system.Equip(0, Chain), "연참을 못 끼웠다");
                for (int s = 1; s < capacity; s++)
                {
                    int filler = FirstUnlockedOtherThan(system, Intro, Chain);
                    Assert.GreaterOrEqual(filler, 0, "채울 오의가 모자란다");
                    Assert.IsTrue(system.Equip(s, filler), "자리를 못 채웠다");
                }

                for (int s = 0; s < capacity; s++)
                    Assert.GreaterOrEqual(system.EquippedAt(s), 0, "빈 자리가 남았다");

                // 끼워진 것을 전부 올려 혈조 Lv.1보다 세게 만든다.
                //
                // **연참만 올리면 안 된다.** 비교 상대는 연참이 아니라 그
                // 구성에서 **가장 약한 자리**이고, 채움용으로 들어간 일섬이
                // 더 약하면 판정은 그쪽을 본다 - 처음 이 검사를 연참 기준으로
                // 짰다가 Swap이 먼저 떠서 걸렸다
                int guard = 0;
                while (WeakestRate(system) <= SkillIntroGuide.RateOf(system, Intro))
                {
                    for (int s = 0; s < capacity; s++)
                    {
                        int i = system.EquippedAt(s);
                        if (i >= 0 && !system.IsMaxed(i))
                            SetLevel(system, i, LevelOf(system, i) + 1);
                    }
                    Assert.Less(++guard, 500, "구성이 혈조를 못 넘어선다");
                }

                var before = SkillIntroGuide.Resolve(system, true, false);
                Assert.AreEqual(SkillIntroHint.Upgrade, before.Hint,
                    "약한 혈조를 끼우라고 하면 안 된다 - 강화가 먼저다");
                Assert.AreEqual(Intro, before.SkillIndex, "강화 대상이 혈조가 아니다");

                // 이제 혈조를 한 칸씩 올린다. 가장 약한 자리를 넘어서기
                // 전까지는 계속 Upgrade여야 한다
                guard = 0;
                double bar = WeakestRate(system);
                int weakest = WeakestIndex(system);

                while (SkillIntroGuide.RateOf(system, Intro) < bar)
                {
                    var mid = SkillIntroGuide.Resolve(system, true, false);
                    Assert.AreEqual(SkillIntroHint.Upgrade, mid.Hint,
                        "아직 약한데 교체로 넘어갔다");

                    SetLevel(system, Intro, LevelOf(system, Intro) + 1);

                    Assert.Less(++guard, 500, "경계를 못 넘었다 - 곡선이 닫혀 있다");
                }

                var after = SkillIntroGuide.Resolve(system, true, false);
                Assert.AreEqual(SkillIntroHint.Swap, after.Hint,
                    "따라잡았는데 아직 강화하라고 한다");
                Assert.AreEqual(weakest, after.WeakestIndex,
                    "밀어낼 자리가 가장 약한 것이 아니다");
                Assert.AreEqual(system.SlotOf(weakest), after.SlotIndex,
                    "가리키는 자리가 그 오의가 든 자리가 아니다");
            }
            finally { Destroy(system); }
        }

        /**
         * @brief 자리를 비우면 **교체가 아니라 장착으로 넘어간다.**
         *
         * 이 게임에 "밀어내며 끼우기"는 없다 - 칩을 눌러 비우고(Equip(slot, -1))
         * 줄의 장착을 누르는 두 동작이다. 비운 뒤에도 그 빈 칩이 계속
         * 깜빡이면 플레이어는 이미 한 일을 또 하라는 말로 읽는다.
         */
        [Test]
        public void TheIntroFlow_PointsAtTheRowOnceASlotIsFree()
        {
            var system = BuildSkills(15, SkillGachaCurve.UnlockStage);
            try
            {
                Assert.IsTrue(system.GrantGachaSkill(Intro));
                EquipOnly(system, 0, Chain);

                // 혈조가 안 끼워진 채 자리가 비어 있는 상태를 만든다
                Assert.IsFalse(system.IsEquipped(Intro));

                bool anyFree = false;
                for (int s = 0; s < system.SlotCapacity; s++)
                    if (!system.IsSlotLocked(s) && system.EquippedAt(s) < 0) anyFree = true;
                Assert.IsTrue(anyFree, "전제가 안 섰다 - 빈 자리가 있어야 한다");

                var advice = SkillIntroGuide.Resolve(system, true, false);
                Assert.AreEqual(SkillIntroHint.Equip, advice.Hint,
                    "빈 자리가 있는데 강화나 교체를 가리킨다");
                Assert.AreEqual(Intro, advice.SkillIndex, "가리키는 줄이 혈조가 아니다");
            }
            finally { Destroy(system); }
        }

        /** 안 받은 오의는 가리킬 수 없다 */
        [Test]
        public void TheIntroHighlight_NeedsTheSkillFirst()
        {
            var system = BuildSkills(15, SkillGachaCurve.UnlockStage);
            try
            {
                Assert.IsFalse(system.IsUnlocked(Intro), "전제가 틀렸다 - 아직 없어야 한다");

                var advice = SkillIntroGuide.Resolve(system, true, false);
                Assert.AreEqual(SkillIntroHint.None, advice.Hint,
                    "없는 오의를 끼우라고 한다");
            }
            finally { Destroy(system); }
        }

        /** 지금 구성에서 가장 약한 자리의 기여. 조언이 비교하는 값 그대로다 */
        private static double WeakestRate(SkillSystem system)
        {
            double worst = double.MaxValue;
            for (int s = 0; s < system.SlotCapacity; s++)
            {
                int i = system.EquippedAt(s);
                if (i < 0) continue;

                double rate = SkillIntroGuide.RateOf(system, i);
                if (rate < worst) worst = rate;
            }
            return worst;
        }

        private static int WeakestIndex(SkillSystem system)
        {
            double worst = double.MaxValue;
            int found = -1;
            for (int s = 0; s < system.SlotCapacity; s++)
            {
                int i = system.EquippedAt(s);
                if (i < 0) continue;

                double rate = SkillIntroGuide.RateOf(system, i);
                if (rate >= worst) continue;

                worst = rate;
                found = i;
            }
            return found;
        }

        // ------------------------------------------------------------ 화면 연결

        /**
         * @brief 조언이 **실제로 화면 부품에 닿는가.**
         *
         * 계산이 맞는 것과 화면이 그것을 읽는 것은 다르다 - 이번 실기
         * 확인에서 걸린 둘이 정확히 그 차이였다(`MarkIntroEquipDone` 호출자
         * 0개, `GuideQuestCard`가 1인수 판 사용). 값이 맞는데 아무도 안 읽는
         * 상태를 테스트가 못 잡으면 같은 일이 또 난다.
         *
         * 여기서 재는 것은 **한 번에 한 곳**이라는 계약이다. 강조 부품이
         * 붙은 오브젝트가 둘 이상이면 화면이 두 곳을 가리킨다.
         */
        [Test]
        public void TheIntroHighlight_LightsExactlyOnePlace()
        {
            var system = BuildSkills(15, SkillGachaCurve.UnlockStage);
            try
            {
                Assert.IsTrue(system.GrantGachaSkill(Intro));

                // 흐름의 세 걸음을 차례로 만들어 본다. 어느 걸음에서도
                // 힌트는 하나뿐이어야 한다
                var steps = new[]
                {
                    SkillIntroGuide.Resolve(system, true, false),
                    SkillIntroGuide.Resolve(system, true, true),
                    SkillIntroGuide.Resolve(system, false, false)
                };

                foreach (var advice in steps)
                {
                    if (advice.Hint == SkillIntroHint.None)
                    {
                        Assert.AreEqual(-1, advice.SkillIndex,
                            "가리킬 것이 없는데 줄 번호가 남아 있다");
                        continue;
                    }

                    // 줄을 가리키는 힌트는 자리를 안 켜고, 자리를 가리키는
                    // 힌트는 줄을 안 켠다 - 두 부품의 판정이 서로 배타적이다
                    bool row = advice.Hint == SkillIntroHint.Upgrade
                               || advice.Hint == SkillIntroHint.Equip;
                    bool chip = advice.Hint == SkillIntroHint.Swap;

                    Assert.AreNotEqual(row, chip, "줄과 자리가 동시에 켜진다");

                    if (row)
                        Assert.AreEqual(Intro, advice.SkillIndex,
                            "줄 강조가 혈조가 아닌 것을 가리킨다");
                    else
                        Assert.GreaterOrEqual(advice.SlotIndex, 0,
                            "자리 강조인데 가리킬 자리가 없다");
                }
            }
            finally { Destroy(system); }
        }

        /**
         * @brief 무인수 `Resolve`는 게이트 칸을 **감춘다.**
         *
         * 기본값이 반대면(`int.MaxValue, false`) 넘기는 것을 잊은 호출자에게
         * 온보딩 칸이 st1부터 뜨고 받은 뒤에도 안 사라진다. 실제로 그
         * 상태였고 화면이 못 깨는 칸을 영영 가리켰다 - 기본값을 못 박는다.
         */
        [Test]
        public void TheBareResolve_HidesTheOnboardingStep()
        {
            var go = new GameObject("~IntroGuideQuests");
            try
            {
                var quests = go.AddComponent<QuestSystem>();
                Invoke(quests, "Awake");

                var bare = GuideQuestLine.Resolve(quests);
                var steps = GuideQuestCatalog.Steps;

                if (bare.Step >= 0 && bare.Step < steps.Length)
                    Assert.AreNotEqual(GuideGate.SkillGachaIntro, steps[bare.Step].Gate,
                        "인자를 안 넘긴 호출에 온보딩 칸이 나왔다 - "
                        + "그 칸은 QuestSystem으로 완료할 수가 없어 카드가 멈춘다");

                // 대조군: 명시적으로 넘기면 그 칸이 나올 수 있어야 한다.
                // 아니면 위 통과가 "그 칸은 원래 안 나온다"라서 나온 것이다
                var explicitly = GuideQuestLine.Resolve(quests, SkillGachaCurve.UnlockStage, false);
                Assert.GreaterOrEqual(explicitly.Step, 0, "명시 호출이 아무 칸도 못 골랐다");
            }
            finally { Object.DestroyImmediate(go); }
        }

        private static void Invoke(MonoBehaviour target, string method, bool optional)
        {
            var m = target.GetType().GetMethod(method,
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (m == null && optional) return;
            Assert.IsNotNull(m, target.GetType().Name + "." + method + " 가 없다");
            m.Invoke(target, null);
        }

        private static int FirstUnlockedOtherThan(SkillSystem system, int a, int b)
        {
            for (int i = 0; i < SkillCatalog.Count; i++)
            {
                if (i == a || i == b) continue;
                if (!system.IsUnlocked(i) || system.IsEquipped(i)) continue;
                return i;
            }
            return -1;
        }
    }
}
