using NUnit.Framework;
using Onikiri.Core;
using UnityEngine;

namespace Onikiri.Tests
{
    /**
     * @brief "동작 중이라 끊으면 안 된다"를 애니메이터가 정확히 말하는지.
     *
     * ## 왜 이 검사가 생겼는가
     *
     * 보스 도전을 누르면 사무라이가 **간헐적으로** idle 자세 그대로 보스에게
     * 달려갔다. 원인은 두 겹이었고 둘 다 이 구분에 걸려 있었다.
     *
     * PlayerCombat이 "스윙 중인가"를 `IsPlaying && swingStarted`로 판정했는데,
     * **루프 클립도 IsPlaying이 계속 true다.** 거기에 swingStarted가 낡은 값으로
     * 남으면(대상이 사라져도 리셋되지 않았다) 두 조건이 영원히 참이 되어,
     * 달리기로 갈아타라는 요청을 계속 거절했다.
     *
     * 그래서 "동작 중"의 정의를 플래그가 아니라 애니메이터가 갖게 했다 -
     * 한 번 재생 클립이 도는 중인가 하나로 끝난다.
     */
    public class SpriteAnimatorStateTests
    {
        static SpriteAnimator NewAnimator()
        {
            var go = new GameObject("AnimatorUnderTest");
            go.AddComponent<SpriteRenderer>();
            return go.AddComponent<SpriteAnimator>();
        }

        static Sprite[] TwoFrames()
        {
            var texture = new Texture2D(2, 2);
            var sprite = Sprite.Create(texture, new Rect(0f, 0f, 2f, 2f), new Vector2(0.5f, 0f));
            return new[] { sprite, sprite };
        }

        [Test]
        public void LoopingClip_IsPlayingButNotOneShot()
        {
            var animator = NewAnimator();
            animator.Play(TwoFrames(), 12f, true);

            Assert.IsTrue(animator.IsPlaying, "루프 클립이 재생 중이 아니다");

            // 이 한 줄이 버그의 핵심이다. 예전 코드는 IsPlaying만 보고 "동작 중"으로
            // 읽었고, idle이 도는 내내 클립 교체를 거절했다
            Assert.IsFalse(animator.IsOneShot,
                "루프 클립을 '끊으면 안 되는 동작'으로 보고한다 - "
                + "그러면 idle이 도는 동안 달리기로 영원히 못 바꾼다");

            Object.DestroyImmediate(animator.gameObject);
        }

        [Test]
        public void OneShotClip_ReportsOneShot()
        {
            var animator = NewAnimator();
            animator.Play(TwoFrames(), 12f, false);

            Assert.IsTrue(animator.IsPlaying);
            Assert.IsTrue(animator.IsOneShot, "스윙 도중인데 끊어도 되는 것으로 보고한다");

            Object.DestroyImmediate(animator.gameObject);
        }

        [Test]
        public void StoppedAnimator_IsNeitherPlayingNorOneShot()
        {
            var animator = NewAnimator();
            animator.Play(TwoFrames(), 12f, false);
            animator.Stop();

            Assert.IsFalse(animator.IsPlaying);
            Assert.IsFalse(animator.IsOneShot,
                "멈춘 애니메이터가 동작 중이라고 보고하면 그 뒤 클립 교체가 전부 막힌다");

            Object.DestroyImmediate(animator.gameObject);
        }

        /**
         * @brief 루프 클립으로 갈아타면 '동작 중'이 풀린다.
         *
         * 스윙이 끝나고 idle/달리기로 돌아온 뒤에는 언제든 다시 갈아탈 수 있어야
         * 한다. 여기가 막히면 한 번 스윙한 뒤로 달리기 전환이 영영 안 된다.
         */
        [Test]
        public void SwitchingFromOneShotToLoop_ClearsTheOneShotState()
        {
            var animator = NewAnimator();

            animator.Play(TwoFrames(), 12f, false);
            Assert.IsTrue(animator.IsOneShot);

            animator.Play(TwoFrames(), 12f, true);
            Assert.IsFalse(animator.IsOneShot,
                "루프 클립으로 갈아탔는데 아직 동작 중이라고 한다");

            Object.DestroyImmediate(animator.gameObject);
        }
    }
}
