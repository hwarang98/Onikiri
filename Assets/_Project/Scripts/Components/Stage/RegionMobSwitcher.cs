using Onikiri.Progression;
using UnityEngine;

namespace Onikiri.Battle
{
    /**
     * @brief 지역이 바뀌면 잡몹 스폰 풀을 갈아끼운다. RegionBackgroundSwitcher의 잡몹판.
     *
     * ## 이미 서 있는 요괴는 건드리지 않는다
     *
     * 교체는 다음 스폰부터다. 지역은 보스를 잡아야 넘어가고, 보스전은 진입할 때
     * 필드를 비우므로(BossFight.Challenge -> ClearField) 정상 경로에서는 빈 필드에
     * 새 풀이 채워진다. 테스트 패널로 스테이지를 건너뛰면 옛 지역 몹이 잠깐
     * 남는데, 그것은 여기서 죽이지 않는다 - 보스전 도중에 스테이지가 바뀌는
     * 경로에서 ClearField가 보스까지 치워버리기 때문이다. 남은 몹은 곧 베인다.
     *
     * ## 배경과 달리 연출이 없다
     *
     * 배경은 화면 전체가 바뀌므로 암전으로 가리지만, 스폰 풀은 화면 밖에서
     * 일어나는 일이다. 가릴 것이 없다.
     */
    [DefaultExecutionOrder(-40)]
    public sealed class RegionMobSwitcher : MonoBehaviour
    {
        [SerializeField] private BossRoster roster;
        [SerializeField] private EnemySpawner spawner;

        [Tooltip("확대판 보스도 그 지역의 잡몹이어야 한다. 스포너와 같은 풀을 물린다")]
        [SerializeField] private BossFight bossFight;

        [SerializeField] private StageProgress progress;

        /** 마지막으로 적용한 지역. 같은 지역이면 아무것도 하지 않는다 */
        private RegionConfig applied;

        /** 지금 적용된 지역. 테스트 패널이 현재 풀을 보여줄 때 쓴다 */
        public RegionConfig Applied { get { return applied; } }

        private void Start()
        {
            if (progress == null) progress = StageProgress.Instance;
            if (progress != null) progress.Changed += Refresh;

            Refresh();
        }

        private void OnDestroy()
        {
            if (progress != null) progress.Changed -= Refresh;
        }

        private void Refresh()
        {
            if (roster == null || spawner == null) return;

            int stage = progress != null ? progress.Stage : 1;

            int stageInRegion;
            var region = roster.RegionForStage(stage, out stageInRegion);
            if (region == null || region == applied) return;

            applied = region;

            // 몹이 없는 지역은 앞 지역의 풀을 그대로 쓴다. 배경과 같은 규칙이다
            if (region.mobs == null || region.mobs.mobs == null || region.mobs.mobs.Length == 0)
                return;

            spawner.SetDefinitions(region.mobs.mobs);

            // 일반 스테이지 보스(잡몹 확대판)와 챕터 정예도 같은 풀에서 나와야
            // "방금까지 베던 놈의 우두머리"가 성립한다
            if (bossFight != null) bossFight.SetStageBossDefinitions(region.mobs.mobs);
        }
    }
}
