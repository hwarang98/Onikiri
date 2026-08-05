using NUnit.Framework;
using Onikiri.Core;
using UnityEngine;

namespace Onikiri.Tests
{
    /**
     * @brief 사양서가 스폰마다의 Instantiate/Destroy를 금지하므로, 그 보장이 기대는
     *        동작을 못 박는 테스트들.
     *
     * prewarm이 수요를 감당하고, 반환된 인스턴스가 다시 나오며, prewarm을 넘는 증식은
     * 숨겨지지 않고 보고된다.
     */
    public class ObjectPoolTests
    {
        private GameObject prefabObject;
        private SpriteRenderer prefab;
        private Transform parent;

        [SetUp]
        public void SetUp()
        {
            prefabObject = new GameObject("PoolPrefab");
            prefab = prefabObject.AddComponent<SpriteRenderer>();
            parent = new GameObject("PoolParent").transform;
        }

        [TearDown]
        public void TearDown()
        {
            if (prefabObject != null) Object.DestroyImmediate(prefabObject);
            if (parent != null) Object.DestroyImmediate(parent.gameObject);
        }

        [Test]
        public void Prewarm_CreatesInstancesUpFrontAndLeavesThemInactive()
        {
            var pool = new ObjectPool<SpriteRenderer>(prefab, parent, 5);

            Assert.AreEqual(5, pool.CountAll);
            Assert.AreEqual(5, pool.CountIdle);
            Assert.AreEqual(0, pool.CountActive);
            Assert.AreEqual(0, pool.GrowthCount);
            Assert.AreEqual(5, parent.childCount);

            for (int i = 0; i < parent.childCount; i++)
                Assert.IsFalse(parent.GetChild(i).gameObject.activeSelf, "prewarmed instance should start inactive");
        }

        [Test]
        public void Get_ActivatesWithoutAllocatingWhilePrewarmLasts()
        {
            var pool = new ObjectPool<SpriteRenderer>(prefab, parent, 4);

            for (int i = 0; i < 4; i++)
            {
                var instance = pool.Get();
                Assert.IsTrue(instance.gameObject.activeSelf);
            }

            Assert.AreEqual(0, pool.GrowthCount, "prewarm should have covered every Get");
            Assert.AreEqual(4, pool.CountAll);
            Assert.AreEqual(4, pool.CountActive);
        }

        [Test]
        public void Get_BeyondPrewarm_GrowsAndReportsIt()
        {
            var pool = new ObjectPool<SpriteRenderer>(prefab, parent, 1);

            pool.Get();
            pool.Get();
            pool.Get();

            Assert.AreEqual(3, pool.CountAll);
            Assert.AreEqual(2, pool.GrowthCount, "growth past the prewarm must be visible, not silent");
        }

        [Test]
        public void Release_ReturnsTheSameInstanceOnNextGet()
        {
            var pool = new ObjectPool<SpriteRenderer>(prefab, parent, 1);

            var first = pool.Get();
            pool.Release(first);
            var second = pool.Get();

            Assert.AreSame(first, second);
            Assert.AreEqual(1, pool.CountAll);
            Assert.AreEqual(0, pool.GrowthCount);
        }

        [Test]
        public void Release_DeactivatesTheInstance()
        {
            var pool = new ObjectPool<SpriteRenderer>(prefab, parent, 1);

            var instance = pool.Get();
            Assert.IsTrue(instance.gameObject.activeSelf);

            pool.Release(instance);
            Assert.IsFalse(instance.gameObject.activeSelf);
        }

        [Test]
        public void DoubleRelease_DoesNotHandOutTheSameInstanceTwice()
        {
            var pool = new ObjectPool<SpriteRenderer>(prefab, parent, 2);

            var instance = pool.Get();
            pool.Release(instance);
            pool.Release(instance);

            var a = pool.Get();
            var b = pool.Get();
            Assert.AreNotSame(a, b, "a double release must not put one instance in the pool twice");
        }

        [Test]
        public void ReleaseAll_ReturnsEverythingToIdle()
        {
            var pool = new ObjectPool<SpriteRenderer>(prefab, parent, 3);
            pool.Get();
            pool.Get();

            pool.ReleaseAll();

            Assert.AreEqual(0, pool.CountActive);
            Assert.AreEqual(3, pool.CountIdle);
        }

        [Test]
        public void SpawnerSizedPool_NeverGrowsUnderItsIntendedLoad()
        {
            // 스포너는 8개 풀에서 요괴 4마리를 유지하며 죽을 때마다 재활용한다.
            // 여기서 재현해 두면 prewarm이 부족할 때 폰에서의 프레임 히칭이 아니라
            // 실패하는 테스트로 먼저 드러난다
            var pool = new ObjectPool<SpriteRenderer>(prefab, parent, 8);
            var live = new System.Collections.Generic.List<SpriteRenderer>();

            for (int wave = 0; wave < 200; wave++)
            {
                while (live.Count < 4) live.Add(pool.Get());
                pool.Release(live[0]);
                live.RemoveAt(0);
            }

            Assert.AreEqual(0, pool.GrowthCount, "pool of 8 should comfortably sustain 4 alive");
        }
    }
}
