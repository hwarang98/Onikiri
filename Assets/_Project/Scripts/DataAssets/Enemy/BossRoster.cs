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
         * @brief 정의된 지역을 모두 지난 뒤의 처리 - **처음부터 다시 돈다** (42단계).
         *
         * 41스테이지까지는 "마지막 지역 반복"이었다. 지역 2~4가 아직 없던
         * 시절의 임시 처방이었는데, 네 지역이 다 들어온 지금 그 규칙은 무한
         * 구간(st41+)을 요괴 소굴 한 판에 영원히 가둔다 - 배경도 잡몹도
         * 보스도 지역 전환 연출도 st40에서 정지한다.
         *
         * 순환이 나은 이유 셋:
         *   - 시각 다양성 4배가 신규 애셋 0장으로 나온다 (값싼 콘텐츠 원칙)
         *   - 밸런스 중립이다. 모든 지역의 잡몹 풀이 같은 가중 평균(128/9·49/9)을
         *     지키므로(RegionMobPoolTests의 36단계 불변식) 어느 지역이 나와도
         *     보스 체력·방치 보상이 같다. 난이도는 지역이 아니라 곡선이 낸다
         *   - 지역 전환 연출(참격)이 무한 구간에서도 10스테이지마다 살아난다 -
         *     스위처들의 "같은 지역이면 조기 반환"이 객체 동일성이라, 지역이
         *     실제로 바뀌어야 연출이 돈다
         *
         * 화면의 지역 번호(HUDStage의 "지역 7")는 여기와 무관하게 계속
         * 오른다 - 그쪽은 BossCurve.RegionOf(순수 산수)다. 이 함수는 그 번호에
         * **어느 세트를 입힐 것인가**만 답한다.
         */
        public RegionConfig RegionForStage(int stage, out int stageInRegion)
        {
            stageInRegion = 1;
            if (regions == null || regions.Length == 0) return null;

            int total = 0;
            foreach (var region in regions)
                if (region != null && region.stageCount > 0) total += region.stageCount;
            if (total <= 0) return null;

            // 전체 한 바퀴(지금은 40) 안의 위치로 접는다. 1바퀴째든 10바퀴째든
            // 같은 자리에는 같은 지역이 선다
            int remaining = (Mathf.Max(1, stage) - 1) % total + 1;

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

            // 접은 값은 total 이하이므로 위 루프가 반드시 답을 냈다. 여기는
            // regions가 전부 null/0인 경로뿐인데 total 검사가 이미 걸렀다
            return null;
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
