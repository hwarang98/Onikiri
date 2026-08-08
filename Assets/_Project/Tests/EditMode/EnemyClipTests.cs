using NUnit.Framework;
using Onikiri.Battle;
using Onikiri.Core;
using UnityEngine;

namespace Onikiri.Tests
{
    /**
     * @brief 요괴가 **지금 상태에 맞는 클립**을 화면에 띄우는가.
     *
     * ## 왜 이 검사가 생겼는가
     *
     * 처형인(지역 2 피날레)에서 두 가지가 한꺼번에 지적됐다.
     *
     *   1. 걷기 시트가 있는데도 **선 자세로 미끄러져** 왔다.
     *   2. 도끼를 드는 동작이 **한 번도 보이지 않았다.**
     *
     * 둘 다 "어느 클립을 거는가"의 문제이고, 둘 다 눈으로만 확인하면 다음 팩에서
     * 똑같이 재발한다. 특히 2번은 원인이 간접적이다 - 공격 클립이 없는 것이
     * 아니라 **피격 클립에 계속 덮어써져** 있었다. 애셋만 봐서는 정상으로 보인다.
     *
     * ## 왜 Update를 돌리지 않는가
     *
     * EditMode에서는 MonoBehaviour의 Update가 돌지 않는다. 그래서 이동으로 인한
     * 상태 전환은 여기서 재현할 수 없고, 대신 **클립을 고르는 규칙 자체**를 건다 -
     * Spawn이 무엇을 거는지, 피격이 무엇을 덮는지. 그 두 지점이 실제로 틀렸던
     * 자리다.
     */
    public class EnemyClipTests
    {
        static Sprite Frame()
        {
            var texture = new Texture2D(2, 2);
            return Sprite.Create(texture, new Rect(0f, 0f, 2f, 2f), new Vector2(0.5f, 0f));
        }

        /** 클립마다 다른 스프라이트를 넣어 화면에 뜬 것이 어느 클립인지 구분한다 */
        static Sprite[] Clip()
        {
            var frame = Frame();
            return new[] { frame, frame };
        }

        static EnemyDefinition Definition(bool withWalk, bool withAttack)
        {
            var definition = ScriptableObject.CreateInstance<EnemyDefinition>();
            definition.idleFrames = Clip();
            definition.walkFrames = withWalk ? Clip() : new Sprite[0];
            definition.hurtFrames = Clip();
            definition.deathFrames = Clip();
            definition.attackFrames = withAttack ? Clip() : new Sprite[0];
            definition.frameRate = 12f;
            definition.moveSpeed = 1f;
            definition.hoverHeight = 0f;
            definition.artBottomOffset = 0f;
            return definition;
        }

        /**
         * @brief 검사 대상 요괴 하나.
         *
         * 참조를 SerializedObject로 꽂는다. EditMode에서는 AddComponent가
         * **Awake를 부르지 않아서**, 인스펙터에서 물려두는 필드가 비어 있는 채로
         * 남는다 - 프리팹이 하는 일을 손으로 대신하는 것이다.
         */
        static Enemy NewEnemy(out SpriteAnimator animator)
        {
            var go = new GameObject("EnemyUnderTest");
            var renderer = go.AddComponent<SpriteRenderer>();
            animator = go.AddComponent<SpriteAnimator>();
            var enemy = go.AddComponent<Enemy>();

            Wire(animator, "target", renderer);
            Wire(enemy, "spriteRenderer", renderer);
            Wire(enemy, "animator", animator);

            return enemy;
        }

        static void Wire(Object component, string field, Object value)
        {
            var serialized = new UnityEditor.SerializedObject(component);
            serialized.FindProperty(field).objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        static void Spawn(Enemy enemy, EnemyDefinition definition)
        {
            enemy.Spawn(definition, 5f, 0f, 0, BigDouble.FromDouble(100d),
                        BigDouble.One, BigDouble.One);
        }

        /**
         * @brief 걸어 들어오는 동안에는 걷기다.
         *
         * Spawn 직후의 상태가 Approaching이므로, 여기서 idle이 걸리면 화면 밖에서
         * 큐 앞줄까지 오는 내내 선 자세로 미끄러진다.
         */
        [Test]
        public void SpawnedEnemy_WalksInWhenThePackHasAWalkClip()
        {
            SpriteAnimator animator;
            var enemy = NewEnemy(out animator);
            var definition = Definition(true, false);

            Spawn(enemy, definition);

            Assert.AreEqual(Enemy.State.Approaching, enemy.CurrentState);
            Assert.AreSame(definition.walkFrames, animator.CurrentClip,
                "걷기 시트가 있는데 선 자세로 들어온다");

            Object.DestroyImmediate(enemy.gameObject);
        }

        /**
         * @brief 걷기가 없는 팩은 지금까지와 똑같이 idle로 걷는다.
         *
         * 잡몹 대부분이 여기에 해당한다 - 도깨비불은 떠다니므로 걷기 시트가 없고,
         * 있어야 할 이유도 없다. 새 칸이 생겼다고 이쪽이 깨지면 안 된다.
         */
        [Test]
        public void SpawnedEnemy_FallsBackToIdleWhenThereIsNoWalkClip()
        {
            SpriteAnimator animator;
            var enemy = NewEnemy(out animator);
            var definition = Definition(false, false);

            Spawn(enemy, definition);

            Assert.AreSame(definition.idleFrames, animator.CurrentClip,
                "걷기가 없는 요괴가 빈 클립을 걸었다");

            Object.DestroyImmediate(enemy.gameObject);
        }

        /**
         * @brief 피격은 공격 동작을 끊지 않는다.
         *
         * **이것이 처형인의 공격이 안 보이던 이유다.** 플레이어 공격속도가
         * 3.88/s면 0.26초마다 피격 클립이 다시 깔리는데 공격 클립은 0.92초짜리라,
         * 도끼를 드는 프레임 두세 장만 스치고 매번 지워졌다.
         */
        [Test]
        public void BeingHitMidSwing_DoesNotReplaceTheAttackClip()
        {
            SpriteAnimator animator;
            var enemy = NewEnemy(out animator);
            var definition = Definition(true, true);

            Spawn(enemy, definition);
            animator.Play(definition.attackFrames, 12f, false);

            enemy.TakeDamage(BigDouble.One);

            Assert.AreSame(definition.attackFrames, animator.CurrentClip,
                "피격 클립이 공격 동작을 덮어썼다 - 보스가 도끼를 끝까지 들지 못한다");

            Object.DestroyImmediate(enemy.gameObject);
        }

        /**
         * @brief 그렇다고 피격 연출이 사라지면 안 된다.
         *
         * 위 규칙을 "공격 중이 아닐 때"로 좁혀두지 않으면 반대쪽으로 넘어간다 -
         * 맞아도 아무 반응이 없는 보스가 된다.
         */
        [Test]
        public void BeingHitWhileResting_StillPlaysTheHurtClip()
        {
            SpriteAnimator animator;
            var enemy = NewEnemy(out animator);
            var definition = Definition(true, true);

            Spawn(enemy, definition);

            enemy.TakeDamage(BigDouble.One);

            Assert.AreSame(definition.hurtFrames, animator.CurrentClip,
                "맞았는데 피격 연출이 없다");

            Object.DestroyImmediate(enemy.gameObject);
        }

        /**
         * @brief 죽는 순간에는 공격 중이어도 사망 연출이 이긴다.
         *
         * 공격 보호가 사망까지 막으면 보스가 도끼를 든 자세로 굳은 채 사라진다.
         * 보호는 피격 클립에만 걸려 있어야 한다.
         */
        [Test]
        public void DyingMidSwing_PlaysTheDeathClip()
        {
            SpriteAnimator animator;
            var enemy = NewEnemy(out animator);
            var definition = Definition(true, true);

            Spawn(enemy, definition);
            animator.Play(definition.attackFrames, 12f, false);

            enemy.TakeDamage(BigDouble.FromDouble(1000d));

            Assert.AreEqual(Enemy.State.Dying, enemy.CurrentState);
            Assert.AreSame(definition.deathFrames, animator.CurrentClip,
                "스윙 도중에 죽으면 사망 연출이 안 나온다");

            Object.DestroyImmediate(enemy.gameObject);
        }
    }
}
