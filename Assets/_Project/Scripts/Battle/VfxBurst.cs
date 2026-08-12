using Onikiri.Core;
using UnityEngine;

namespace Onikiri.Battle
{
    /**
     * @brief `VfxLibrary`의 클립을 월드 한 지점에 한 번 재생한다.
     *
     * 재생 자체는 `PackSlash`가 한다 - 스프라이트 한 장을 프레임마다 갈아 끼우고
     * 끝나면 꺼지는 일이라, 참격이든 요괴에게서 뜯은 조각이든 하는 일이 같다.
     * `SkillPerformer`가 팩 참격에 쓰는 것과 **같은 프리팹, 같은 풀 방식**이다.
     *
     * ## 왜 별도 컴포넌트인가
     *
     * `SkillPerformer`는 플레이어의 안무를 안다 - 돌진 거리, 타격 프레임, 잔상.
     * 요괴가 참격을 뿜는 데에는 그중 아무것도 필요 없고, 필요한 것은 "이 자리에
     * 이 클립"뿐이다. 안무 쪽에 얹으면 요괴가 플레이어의 안무 표에 줄을 하나
     * 차지하게 되고, 그 줄에는 쓰지 않는 칸이 스무 개 붙는다.
     *
     * ## 풀
     *
     * 요괴는 2초마다 휘두르고 클립은 0.3초 안에 끝나므로 동시에 떠 있는 것은
     * 보통 한 장이다. prewarm 2면 늘어날 일이 없고, 그래도 늘어나면
     * `PoolGrowthCount`가 세어 테스트 패널에 드러난다 - 조용히 커지는 풀은
     * 프레임 히칭의 원인을 찾을 수 없게 만든다(ObjectPool 주석).
     */
    public sealed class VfxBurst : MonoBehaviour
    {
        [SerializeField] private VfxLibrary library;
        [SerializeField] private PackSlash slashPrefab;
        [SerializeField] private Transform vfxParent;
        [SerializeField] private int prewarm = 2;

        private ObjectPool<PackSlash> pool;

        /** prewarm을 넘어 늘어난 횟수. 테스트 패널이 읽는다 */
        public int PoolGrowthCount { get { return pool != null ? pool.GrowthCount : 0; } }

        /** 지금 재생 중인 클립 수. 테스트 패널이 읽는다 */
        public int ActiveCount { get { return pool != null ? pool.CountActive : 0; } }

        public VfxLibrary Library { get { return library; } }

        private void Awake()
        {
            if (vfxParent == null) vfxParent = transform;
            if (slashPrefab != null)
                pool = new ObjectPool<PackSlash>(slashPrefab, vfxParent, prewarm);
        }

        /** 이 이름의 클립이 라이브러리에 있고 프레임이 들어 있는가 */
        public bool Has(string id)
        {
            var clip = library != null ? library.Find(id) : null;
            return clip != null && clip.frames != null && clip.frames.Length > 0;
        }

        /** 이 클립이 한 번 도는 데 걸리는 시간 (초). 없으면 0 */
        public float SecondsOf(string id)
        {
            var clip = library != null ? library.Find(id) : null;
            return clip != null ? clip.Seconds : 0f;
        }

        /**
         * @brief 클립 한 번.
         *
         * @param origin 시전자의 발밑. 클립의 forwardOffset/heightOffset이 여기서 잰다
         * @param flip   시전자가 왼쪽을 보고 있으면 true. 앞쪽 방향과 회전각이 함께 뒤집힌다
         * @return 재생했으면 true. 클립이 없거나 풀이 없으면 false
         */
        public bool Play(string id, Vector3 origin, bool flip)
        {
            if (pool == null || library == null) return false;

            var clip = library.Find(id);
            if (clip == null || clip.frames == null || clip.frames.Length == 0) return false;

            // 앞쪽은 바라보는 방향이다. 요괴는 왼쪽을 보고 서 있으므로 flip이
            // 참일 때 -x가 앞이 된다. 반전이 각도의 부호까지 뒤집는 것은
            // PackSlash가 처리한다(좌우 반전은 세로축 거울이라 각도도 뒤집힌다)
            var position = origin + new Vector3(
                flip ? -clip.forwardOffset : clip.forwardOffset,
                clip.heightOffset,
                0f);

            var slash = pool.Get();
            slash.Play(clip.frames, clip.frameRate, position, clip.angle, clip.scale, flip, Release);
            return true;
        }

        private void Release(PackSlash slash)
        {
            if (pool != null) pool.Release(slash);
        }
    }
}
