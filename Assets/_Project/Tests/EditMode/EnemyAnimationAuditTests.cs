using System.Collections.Generic;
using NUnit.Framework;
using Onikiri.Battle;
using UnityEditor;
using UnityEngine;

namespace Onikiri.Tests
{
    /**
     * @brief 생성된 요괴 정의가 **어느 태그를 어느 칸에 넣었는가**.
     *
     * ## 왜 애셋을 검사하는가
     *
     * `EnemyClipTests`는 규칙을 검사한다 - 걸어올 때 걷기를 걸고, 스윙 중에 피격이
     * 덮지 않는다. 그 규칙이 전부 맞아도 **빌더가 태그를 잘못 집으면** 화면은
     * 여전히 틀린다. 48단계 감사에서 실제로 확인한 것이 그 지점이었다: 규칙은
     * 멀쩡한데 여덟 파일에 태그가 열둘 남아 있었고, 아무도 그것을 몰랐다.
     *
     * 여기서 거는 것은 "빌더가 집어온 결과물"이다.
     *
     * ## 사망 클립은 그림으로 알아본다
     *
     * 이 팩의 사망 연출에는 공통 문법이 있다 - 흰 플래시 -> 조각남 -> 흩어짐.
     * 흩어지는 순간 **그려진 폭이 몇 배로 벌어진다.** 실측하면 대기는 1.0~1.18배
     * 안에서 놀고 공격도 1.32~1.50배인데, 사망은 1.67~4.47배다.
     *
     * 그래서 임계값을 박지 않고 **순서**로 건다: 사망은 그 요괴의 어느 클립보다도
     * 크게 흩어져야 한다. 임계값(1.6 같은 것)을 박으면 공격 1.50과 사망 1.67 사이
     * 0.17에 테스트가 매달리고, 아트가 한 프레임만 바뀌어도 뒤집힌다.
     *
     * **이 검사가 잡는 것이 바로 이 스텝이 찾으러 나섰던 오배치다** - 사망 프레임을
     * 공격 칸에 넣으면 공격 칸의 흩어짐이 사망보다 커져서 여기서 걸린다.
     */
    public class EnemyAnimationAuditTests
    {
        /** 빌더가 aseprite에서 만드는 잡몹 정의들이 사는 곳 */
        const string MobFolder = "Assets/_Project/Data";

        static List<EnemyDefinition> LoadDefinitions(string folder)
        {
            var definitions = new List<EnemyDefinition>();
            foreach (var guid in AssetDatabase.FindAssets("t:EnemyDefinition", new[] { folder }))
            {
                var definition = AssetDatabase.LoadAssetAtPath<EnemyDefinition>(
                    AssetDatabase.GUIDToAssetPath(guid));
                if (definition != null) definitions.Add(definition);
            }
            Assert.Greater(definitions.Count, 0, "요괴 정의가 하나도 없다: " + folder);
            return definitions;
        }

        static int Length(Sprite[] frames) { return frames != null ? frames.Length : 0; }

        /**
         * @brief 클립 안에서 그려진 폭이 가장 넓을 때와 가장 좁을 때의 비.
         *
         * aseprite 임포터가 프레임마다 여백을 잘라내므로(트리밍) `sprite.rect`가
         * 곧 그려진 크기다. 텍스처를 읽지 않아도 되는 것이 요점이다 - 임포트된
         * 아틀라스는 CPU에서 읽을 수 없다.
         */
        static float SpreadOf(Sprite[] frames)
        {
            if (frames == null || frames.Length == 0) return 0f;

            float widest = 0f, narrowest = float.MaxValue;
            foreach (var frame in frames)
            {
                if (frame == null) continue;
                float width = frame.rect.width;
                if (width <= 0f) continue;
                if (width > widest) widest = width;
                if (width < narrowest) narrowest = width;
            }
            return narrowest > 0f && narrowest < float.MaxValue ? widest / narrowest : 0f;
        }

        /**
         * @brief 사망 칸에 정말 사망 연출이 들어 있는가.
         *
         * 격자로 자른 시트형 보스는 프레임마다 칸이 똑같아서(SliceGrid) 이 신호가
         * 아예 없다. 그런 정의는 건너뛴다 - 검사할 수 없는 것과 통과한 것을
         * 구분하지 않으면 나중에 전부 격자 시트로 바뀌었을 때 이 검사가 조용히
         * 아무것도 안 하게 된다.
         */
        [Test]
        public void DeathClip_ScattersWiderThanEveryOtherClip()
        {
            int checkedCount = 0;

            foreach (var definition in LoadDefinitions(MobFolder))
            {
                float death = SpreadOf(definition.deathFrames);

                // 격자 시트(보스)는 폭이 고정이라 이 신호를 못 쓴다
                if (death <= 1.0001f) continue;

                checkedCount++;

                Assert.Greater(death, SpreadOf(definition.idleFrames),
                    definition.name + ": 대기 클립이 사망보다 크게 흩어진다 - " +
                    "두 칸이 서로 바뀐 것 아닌가");

                Assert.Greater(death, SpreadOf(definition.attackFrames),
                    definition.name + ": 공격 클립이 사망보다 크게 흩어진다 - " +
                    "사망 프레임을 공격 칸에 넣은 것 아닌가");

                Assert.Greater(death, SpreadOf(definition.walkFrames),
                    definition.name + ": 걷기 클립이 사망보다 크게 흩어진다");
            }

            Assert.Greater(checkedCount, 0,
                "폭 신호로 검사할 수 있는 요괴가 하나도 없다 - 검사가 조용히 꺼졌다");
        }

        /**
         * @brief 같은 프레임 묶음이 두 칸에 동시에 들어가 있지 않은가.
         *
         * 위의 폭 검사가 못 잡는 종류다. 사망을 공격에 **그대로 복사**하면 두
         * 칸의 흩어짐이 같아지므로 `Greater`가 아슬아슬하게 통과할 수 있다.
         */
        [Test]
        public void NoTwoSlots_ShareTheSameFrames()
        {
            foreach (var definition in LoadDefinitions(MobFolder))
            {
                var slots = new Dictionary<string, Sprite[]>
                {
                    { "대기", definition.idleFrames },
                    { "걷기", definition.walkFrames },
                    { "공격", definition.attackFrames },
                    { "피격", definition.hurtFrames },
                    { "사망", definition.deathFrames },
                };

                var names = new List<string>(slots.Keys);
                for (int a = 0; a < names.Count; a++)
                {
                    for (int b = a + 1; b < names.Count; b++)
                    {
                        var first = slots[names[a]];
                        var second = slots[names[b]];
                        if (Length(first) == 0 || Length(second) == 0) continue;
                        if (Length(first) != Length(second)) continue;

                        bool identical = true;
                        for (int i = 0; i < first.Length; i++)
                        {
                            if (first[i] != second[i]) { identical = false; break; }
                        }

                        Assert.IsFalse(identical, string.Format(
                            "{0}: {1} 칸과 {2} 칸이 같은 프레임을 쓴다 - 한쪽이 오배치다",
                            definition.name, names[a], names[b]));
                    }
                }
            }
        }

        /**
         * @brief 대기와 사망은 어느 요괴에게도 비어 있으면 안 된다.
         *
         * 나머지 셋(걷기·공격·피격)은 비어 있어도 되는 칸이다 - 팩에 그 태그가
         * 없는 요괴가 대부분이고, `Enemy`가 전부 대체 경로를 갖고 있다.
         */
        [Test]
        public void EveryEnemy_HasIdleAndDeath()
        {
            foreach (var definition in LoadDefinitions(MobFolder))
            {
                Assert.Greater(Length(definition.idleFrames), 0, definition.name + ": 대기가 비었다");
                Assert.Greater(Length(definition.deathFrames), 0, definition.name + ": 사망이 비었다");
            }
        }

        /**
         * @brief 다크 사무라이는 공격을 **여러 벌** 갖는다.
         *
         * 그 팩의 오의 블록에는 서로 다른 공격이 넷 들어 있다(혈참·혈조·혈륜·혈파).
         * 한 벌만 쓰면 2초마다 같은 그림이 돌아 금방 벽지가 된다.
         *
         * 넷이 **서로 다른 프레임**이어야 하는 것이 요점이다. 같은 시트를 네 번
         * 물려도 개수 검사는 통과하지만 화면은 하나도 안 달라진다.
         */
        [Test]
        public void TheYokaiBoss_HasSeveralDistinctAttacks()
        {
            var definition = AssetDatabase.LoadAssetAtPath<EnemyDefinition>(
                "Assets/_Project/Data/Generated/Enemy_Boss_RedEyeYokai.asset");
            Assert.IsNotNull(definition, "요괴 보스 정의가 없다 - Build Combat Content 를 실행하세요");

            var clips = new List<Sprite[]>();
            if (Length(definition.attackFrames) > 0) clips.Add(definition.attackFrames);
            if (definition.attackVariants != null)
            {
                foreach (var variant in definition.attackVariants)
                    if (variant != null && Length(variant.frames) > 0) clips.Add(variant.frames);
            }

            Assert.GreaterOrEqual(clips.Count, 3, string.Format(
                "공격이 {0}벌뿐이다 - 오의 블록에서 잘라온 넷이 다 물리지 않았다", clips.Count));

            for (int a = 0; a < clips.Count; a++)
            {
                for (int b = a + 1; b < clips.Count; b++)
                {
                    Assert.AreNotSame(clips[a], clips[b], "같은 배열이 두 번 물렸다");

                    // 첫 프레임이 같으면 같은 시트다
                    Assert.AreNotEqual(clips[a][0], clips[b][0], string.Format(
                        "공격 {0}과 {1}이 같은 시트다 - 벌 수만 늘고 화면은 안 바뀐다", a, b));
                }
            }
        }

        /**
         * @brief 추가 공격은 **그 보스만** 갖는다.
         *
         * 잡몹이 변형을 들면 확대판 보스로 설 때 엉뚱한 그림이 섞인다. 지금은
         * 그 팩에만 여러 벌이 있으므로, 다른 정의에 생기면 배선이 샌 것이다.
         */
        [Test]
        public void OnlyTheYokaiBoss_HasAttackVariants()
        {
            foreach (var definition in LoadDefinitions(MobFolder))
            {
                if (definition.name == "Enemy_Boss_RedEyeYokai") continue;

                int variants = 0;
                if (definition.attackVariants != null)
                {
                    foreach (var variant in definition.attackVariants)
                        if (variant != null && Length(variant.frames) > 0) variants++;
                }

                Assert.AreEqual(0, variants,
                    definition.name + ": 추가 공격이 붙어 있다 - 요괴 보스 전용 배선이 샜다");
            }
        }

        /**
         * @brief 예고 시간이 **쓸 수 있는 값인가.**
         *
         * ## 이 검사가 말하지 않는 것
         *
         * 한때 이 자리가 "확대판 보스가 휘두르는 것을 보여줄 수 있는가"였다.
         * 참격 이펙트가 모든 보스의 공용 기본값이던 시절의 문장이고, 그 기본값은
         * **되돌렸다** - 한 요괴의 서명이 다섯 보스에 전부 새어 나갔기 때문이다
         * (BossFight.defaultAttackVfxId 주석).
         *
         * 그래서 지금 확대판 보스(외눈 등롱·정예)는 참격을 안 뿜는다. 예고
         * 시간은 그대로 있지만 띄울 클립이 없으므로 화면에는 아무 일도 안 생긴다.
         * 그 상태를 "통과"라고 적으면 이 검사가 거짓말을 한다.
         *
         * ## 그래서 무엇을 거는가
         *
         * 예고 시간 자체의 성질만 건다 - **음수가 아니고 주기보다 짧을 것.**
         * 언젠가 그 보스에게 자기 참격이 생기면 이 값이 그대로 쓰이고, 그때
         * 주기보다 길면 이전 참격이 다음 타격까지 이어져 "언제 맞는가"가
         * 화면에서 사라진다.
         *
         * 어느 쪽이든 **피해량과 주기는 이 값을 안 본다**(Enemy.UpdateAttack).
         */
        [Test]
        public void AttackTelegraph_IsShorterThanTheAttackInterval()
        {
            foreach (var definition in LoadDefinitions(MobFolder))
            {
                // 공격 주기가 없는 정의는 보스로 설 일이 없다
                if (definition.attackInterval <= 0f) continue;

                Assert.GreaterOrEqual(definition.attackTelegraphSeconds, 0f,
                    definition.name + ": 예고 시간이 음수다");

                Assert.Less(definition.attackTelegraphSeconds, definition.attackInterval,
                    definition.name + ": 예고 시간이 공격 주기보다 길다 - " +
                    "이펙트가 붙는 날 이전 참격이 다음 타격까지 이어진다");
            }
        }
    }
}
