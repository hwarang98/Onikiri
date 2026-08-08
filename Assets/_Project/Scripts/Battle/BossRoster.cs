using UnityEngine;

namespace Onikiri.Battle
{
    /**
     * @brief 지역들을 순서대로 늘어놓은 것. 스테이지 번호를 보스로 바꾼다.
     *
     * 이 애셋이 "스테이지 37의 보스는 누구인가"에 답하는 유일한 곳이다. 12단계까지
     * 그 답은 `stage % 5 == 0`이라는 코드 한 줄이었고, 그래서 배치를 바꾸려면
     * 코드를 고쳐야 했다.
     */
    [CreateAssetMenu(fileName = "BossRoster", menuName = "Onikiri/Boss Roster")]
    public sealed class BossRoster : ScriptableObject
    {
        [Tooltip("앞에서부터 순서대로. 1스테이지는 첫 지역의 첫 스테이지다")]
        public RegionConfig[] regions;

        /**
         * @brief 정의된 지역을 모두 지난 뒤의 처리.
         *
         * 지금은 지역 1만 확정됐다(지역 2·3 피날레는 신규 보스 대기). 11스테이지
         * 이후에 보스가 아예 없으면 진행이 멈추므로 **마지막 지역의 배치를
         * 반복한다.**
         *
         * 이것이 최종 설계라서가 아니라, 지역이 추가되기 전까지 게임이 계속
         * 돌아야 하기 때문이다. 지역 2 애셋이 들어오면 반복 구간은 그만큼
         * 뒤로 밀린다.
         */
        public RegionConfig RegionForStage(int stage, out int stageInRegion)
        {
            stageInRegion = 1;
            if (regions == null || regions.Length == 0) return null;

            int remaining = Mathf.Max(1, stage);

            foreach (var region in regions)
            {
                if (region == null || region.stageCount <= 0) continue;
                if (remaining <= region.stageCount)
                {
                    stageInRegion = remaining;
                    return region;
                }
                remaining -= region.stageCount;
            }

            // 정의된 지역을 다 지났다. 마지막 지역을 반복한다
            var last = regions[regions.Length - 1];
            if (last == null || last.stageCount <= 0) return null;

            int into = (remaining - 1) % last.stageCount + 1;
            stageInRegion = into;
            return last;
        }

        /** 이 스테이지의 보스. null이면 그 스테이지 잡몹의 확대판 */
        public BossConfig BossForStage(int stage)
        {
            int stageInRegion;
            var region = RegionForStage(stage, out stageInRegion);
            return region != null ? region.BossForStageInRegion(stageInRegion) : null;
        }

        /** 이 스테이지가 지역 피날레인가 */
        public bool IsFinale(int stage)
        {
            int stageInRegion;
            var region = RegionForStage(stage, out stageInRegion);
            return region != null && region.finaleBoss != null && stageInRegion >= region.stageCount;
        }

        /** 이 스테이지가 챕터 관문인가. 피날레는 포함하지 않는다 */
        public bool IsChapter(int stage)
        {
            int stageInRegion;
            var region = RegionForStage(stage, out stageInRegion);
            if (region == null || region.chapterEvery <= 0) return false;
            if (stageInRegion >= region.stageCount) return false;
            return stageInRegion % region.chapterEvery == 0;
        }
    }
}
