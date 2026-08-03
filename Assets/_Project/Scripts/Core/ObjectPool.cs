using System.Collections.Generic;
using UnityEngine;

namespace Onikiri.Core
{
    /// <summary>
    /// Reusable pool of pooled components.
    ///
    /// Idle games spawn and kill enemies, damage numbers and hit effects constantly, and
    /// per-spawn Instantiate/Destroy is the usual cause of GC hitches on mobile. Everything
    /// is created once during prewarm and then recycled by toggling activation.
    ///
    /// If demand ever exceeds the prewarm the pool still grows rather than failing, but it
    /// counts those growths so an undersized prewarm shows up instead of hiding.
    /// </summary>
    public sealed class ObjectPool<T> where T : Component
    {
        private readonly T prefab;
        private readonly Transform parent;
        private readonly Stack<T> idle = new Stack<T>();
        private readonly List<T> all = new List<T>();

        /// <summary>Instances created after prewarm because the pool ran dry.</summary>
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
            if (idle.Contains(instance)) return;   // double release would hand it out twice

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
