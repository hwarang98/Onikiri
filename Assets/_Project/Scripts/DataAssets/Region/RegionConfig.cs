using UnityEngine;

namespace Onikiri.Battle
{
    /**
     * @brief 지역 하나의 길이와 그 안의 보스 배치.
     *
     * 지역 길이를 하드코딩하지 않는 이유는, 그것이 밸런스가 아니라 **연출의
     * 단위**이기 때문이다. 지역 1이 10스테이지인 것은 첫 지역에서 플레이어가
     * 배워야 할 것(파밍 -> 챕터 관문 -> 피날레)이 그 길이에 들어가기 때문이고,
     * 지역 2가 12스테이지가 될 수도 있다. 그때 코드를 고쳐야 한다면 그것은
     * 데이터가 코드에 갇혀 있다는 뜻이다.
     */
    [CreateAssetMenu(fileName = "Region_", menuName = "Onikiri/Region Config")]
    public sealed class RegionConfig : ScriptableObject
    {
        [Tooltip("화면에 뜨지는 않는다. 에디터에서 어느 지역인지 알아보기 위한 것")]
        public string displayName = "지역 1";

        [Tooltip("이 지역의 스테이지 수. 마지막 스테이지가 피날레다")]
        public int stageCount = 10;

        [Tooltip("챕터 보스 주기. 10스테이지 지역에서 5면 5스테이지가 챕터 관문")]
        public int chapterEvery = 5;

        /**
         * @brief 이 지역의 배경 한 벌. 비우면 앞 지역의 배경을 그대로 쓴다.
         *
         * 21단계에 생겼다. 지역이 넘어갈 때 바뀌는 것이 보스만이면 화면은
         * 계속 같은 곳이고, "새 지역에 왔다"가 숫자로만 남는다.
         *
         * 비워둘 수 있게 한 이유는 지역을 늘리는 것과 배경을 준비하는 것이
         * 같은 속도로 진행되지 않기 때문이다 - 배경이 아직 없는 지역도
         * 배치는 할 수 있어야 한다.
         */
        public RegionBackgroundSet background;

        /**
         * @brief 이 지역의 잡몹 한 벌. 비우면 앞 지역의 잡몹을 그대로 쓴다.
         *
         * 배경과 같은 규칙이다(36단계). 지역을 늘리는 것과 그 지역의 몹을
         * 고르는 것이 같은 속도로 진행되지 않으므로, 몹이 아직 없는 지역도
         * 배치는 할 수 있어야 한다.
         */
        public RegionMobSet mobs;

        [Tooltip("챕터 보스. 지역 중간의 관문")]
        public BossConfig chapterBoss;

        /**
         * @brief 지역 피날레.
         *
         * **여기서만 나오는 보스다.** 5스테이지와 10스테이지에 같은 놈이 두 번
         * 나오면 피날레의 무게가 사라진다. 12단계까지는 다크 사무라이가 5의
         * 배수마다 나왔고, 그래서 챕터 보스가 특별하지 않았다.
         */
        public BossConfig finaleBoss;

        /**
         * @brief 일반 스테이지 보스. 비우면 그 스테이지 잡몹의 확대판이 쓰인다.
         *
         * 비워두는 것이 기본이다. 일반 보스가 "이 스테이지의 우두머리"로 읽히려면
         * 방금까지 베던 잡몹이어야 하고, 그것은 지역 설정이 아니라 스테이지가
         * 정한다.
         */
        public BossConfig normalBossOverride;

        /**
         * @brief 이 지역 안에서 stageInRegion(1부터)의 보스는 무엇인가.
         *
         * @return 해당 BossConfig. null이면 그 스테이지 잡몹의 확대판
         */
        public BossConfig BossForStageInRegion(int stageInRegion)
        {
            if (stageInRegion >= stageCount && finaleBoss != null) return finaleBoss;

            if (chapterEvery > 0 && stageInRegion % chapterEvery == 0 && chapterBoss != null)
                return chapterBoss;

            return normalBossOverride;
        }
    }
}
