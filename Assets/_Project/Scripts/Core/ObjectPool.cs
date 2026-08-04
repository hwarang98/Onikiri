using System.Collections.Generic;
using UnityEngine;

namespace Onikiri.Core
{
    /**
     * @brief 컴포넌트를 재사용하는 범용 오브젝트 풀.
     *
     * 방치형은 적·데미지 숫자·타격 이펙트를 끊임없이 만들고 없앤다.
     * 스폰마다 Instantiate/Destroy를 하면 모바일에서 GC 히칭의 주범이 되므로,
     * prewarm 시점에 한 번만 생성하고 이후에는 활성/비활성 토글로 돌려쓴다.
     *
     * prewarm을 넘어서는 수요가 오면 실패하지 않고 늘어나되, 그 횟수를 세어
     * 드러낸다. 조용히 커지면 프레임 히칭의 원인을 찾을 수 없기 때문이다.
     */
    public sealed class ObjectPool<T> where T : Component
    {
        private readonly T prefab;
        private readonly Transform parent;
        private readonly Stack<T> idle = new Stack<T>();
        private readonly List<T> all = new List<T>();

        /** prewarm 이후 풀이 비어 추가 생성된 인스턴스 수 */
        public int GrowthCount { get; private set; }

        public int CountAll { get { return all.Count; } }
        public int CountIdle { get { return idle.Count; } }
        public int CountActive { get { return all.Count - idle.Count; } }

        public ObjectPool(T prefab, Transform parent, int prewarm)
        {
            if (prefab == null) throw new System.ArgumentNullException("prefab");

            this.prefab = prefab;
            this.parent = parent;

            for (int i = 0; i < prewarm; i++)
            {
                var instance = Create();
                instance.gameObject.SetActive(false);
                idle.Push(instance);
            }
        }

        private T Create()
        {
            var instance = Object.Instantiate(prefab, parent);
            instance.name = prefab.name + "_" + all.Count;
            all.Add(instance);
            return instance;
        }

        public T Get()
        {
            T instance;
            if (idle.Count > 0)
            {
                instance = idle.Pop();
            }
            else
            {
                instance = Create();
                GrowthCount++;
            }

            instance.gameObject.SetActive(true);
            return instance;
        }

        public void Release(T instance)
        {
            if (instance == null) return;
            // 이중 반환을 막지 않으면 같은 인스턴스가 두 번 배급된다
            if (idle.Contains(instance)) return;

            instance.gameObject.SetActive(false);
            idle.Push(instance);
        }

        public void ReleaseAll()
        {
            foreach (var instance in all)
            {
                if (instance == null || idle.Contains(instance)) continue;
                instance.gameObject.SetActive(false);
                idle.Push(instance);
            }
        }
    }
}
